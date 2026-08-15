using System;
using System.Collections;
using System.Collections.Generic;
using LiftEngine.Ads;
using LiftEngine.Api;
using LiftEngine.Context;
using LiftEngine.Mediation;
using UnityEngine;

namespace LiftEngine
{
    internal sealed class LiftEngineController
    {
        private LiftEngineSettings _settings;
        private LiftEngineHost _host;
        private ReportContextService _context;
        private LiftEngineApiClient _api;
        private IMediationAdapter _mediation;
        private AdLoadOrchestrator _orchestrator;
        private AdPrewarmService _prewarm;

        private LiftEngineShowAdCallbacks _activeCallbacks;
        private LiftEngineAdFormat? _activeFormat;
        private readonly HashSet<LiftEngineAdFormat> _pendingReportFormats = new();
        private Coroutine _reportDebounceCoroutine;
        private Coroutine _firstReportRetryCoroutine;
        private bool _firstReportSucceeded;
        private int _reportInFlightCount;
        private bool _prewarmAllRequestedAfterFirstReport;
        /// <summary>
        /// Per-format: display already recorded revenue for the current impression.
        /// Must not be cleared on AdHidden — MAX often pays AdRevenuePaid after hide.
        /// </summary>
        private readonly HashSet<LiftEngineAdFormat> _displayRevenueRecordedFormats = new();

        /// <summary>
        /// Per-format FIFO of impressions waiting for MAX AdRevenuePaid → track/activeview.
        /// Isolated queues so banner/interstitial/rewarded never collide; late ILRD matches
        /// the oldest waiter for that format even after a refresh/prewarm.
        /// </summary>
        private readonly Queue<ActiveViewWaiter> _bannerActiveViewWaiters = new();
        private readonly Queue<ActiveViewWaiter> _interstitialActiveViewWaiters = new();
        private readonly Queue<ActiveViewWaiter> _rewardedActiveViewWaiters = new();

        public bool IsInitialized { get; private set; }

        /// <summary>
        /// One pending impression for a format, with auction/plc pinned at enqueue time.
        /// </summary>
        private sealed class ActiveViewWaiter
        {
            public LiftEngineAdFormat Format;
            public string Keyword;
            public string AuctionId;
            public int AttemptIndex;
            public float AppliedValue;
            public string Plc;
            public bool ViewSent;
        }

        public void Initialize(LiftEngineSettings settings, LiftEngineHost host)
        {
            if (IsInitialized)
                return;

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host;
            LiftEngineLogger.SetVerbose(settings.verboseLogging);

            _context = new ReportContextService();
            _context.Initialize();
            _context.BeginIpCountryLookup(_host);

            _api = new LiftEngineApiClient(_settings, _host);
            _mediation = MediationAdapterFactory.Create(_settings.mediationPlatform);
            _orchestrator = new AdLoadOrchestrator(_settings, _mediation, _host, _context);
            _prewarm = new AdPrewarmService(_settings, _api, _context, _mediation, _orchestrator, _host);

            SubscribeMediationEvents();

            _mediation.Initialize(_settings, success =>
            {
                if (!success)
                {
                    LiftEngineLogger.LogError("Mediation initialization failed.");
                    LiftEngineTrackReporter.ReportError(
                        _api, _settings, _context, LiftEngineAdFormat.Interstitial,
                        "mediation_init_failed", "AppLovin MAX mediation initialization failed.");
                    LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Failed);
                    return;
                }

                CompleteInit();
            });
        }

        /// <summary>
        /// Context-only report (init / attribution). No ad format — omits ecpm_history.
        /// </summary>
        public void SendReport(Action<bool> callback = null) =>
            SendReportInternal(null, callback);

        public void SendReport(LiftEngineAdFormat format, Action<bool> callback = null) =>
            SendReportInternal(format, callback);

        private void SendReportInternal(LiftEngineAdFormat? format, Action<bool> callback)
        {
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var payload = _context.BuildPayload(format);
            LiftEngineLogger.LogClient(
                format.HasValue
                    ? $"Report — sending context ({format.Value})"
                    : "Report — sending context (no format)");
            _reportInFlightCount++;
            _api.Report(deviceId, payload, success =>
            {
                _reportInFlightCount = Math.Max(0, _reportInFlightCount - 1);
                if (success)
                {
                    LiftEngineLogger.LogBackend("Report — OK");
                    OnFirstReportSucceeded();
                }
                else
                {
                    LiftEngineLogger.LogBackendWarning("Report — failed");
                    if (!_firstReportSucceeded && IsInitialized && _reportInFlightCount == 0)
                        ScheduleFirstReportRetry();
                }

                callback?.Invoke(success);
            });
        }

        private void CompleteInit()
        {
            IsInitialized = true;

            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var appVersion = Application.version;
            var platform = DeviceOsProvider.GetOs();
            LiftEngineLogger.LogClient(
                $"Init — device={deviceId}, app_version={appVersion}, platform={platform}");
            _api.TrackInit(deviceId, appVersion, platform);

            LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Success);

            _prewarm.StartBackgroundRefill();

            if (LiftEngineRuntimeTuning.PrewarmOnInit)
                _prewarmAllRequestedAfterFirstReport = true;

            EnsureFirstReportBeforePrewarm();
        }

        private void EnsureFirstReportBeforePrewarm()
        {
            if (_firstReportSucceeded)
            {
                UnlockPrewarm();
                return;
            }

            if (_reportInFlightCount > 0)
                return;

            SendReport();
        }

        private void ScheduleFirstReportRetry()
        {
            if (_firstReportSucceeded || _firstReportRetryCoroutine != null)
                return;

            _firstReportRetryCoroutine = _host.StartCoroutine(RetryFirstReportRoutine());
        }

        private IEnumerator RetryFirstReportRoutine()
        {
            yield return new WaitForSeconds(LiftEngineRuntimeTuning.FirstReportRetryIntervalSeconds);
            _firstReportRetryCoroutine = null;

            if (_firstReportSucceeded || !IsInitialized)
                yield break;

            LiftEngineLogger.LogClient("Report — retrying first report before prewarm");
            EnsureFirstReportBeforePrewarm();
        }

        private void OnFirstReportSucceeded()
        {
            if (_firstReportSucceeded)
                return;

            _firstReportSucceeded = true;
            if (_firstReportRetryCoroutine != null)
            {
                _host.StopCoroutine(_firstReportRetryCoroutine);
                _firstReportRetryCoroutine = null;
            }

            if (IsInitialized)
                UnlockPrewarm();
        }

        private void UnlockPrewarm()
        {
            _prewarm.AllowPrewarmAfterFirstReport();

            if (!_prewarmAllRequestedAfterFirstReport)
                return;

            _prewarmAllRequestedAfterFirstReport = false;
            _prewarm.PrewarmAll();
        }

        private void SubscribeMediationEvents()
        {
            _mediation.AdLoaded += info =>
            {
                // Banner auto-refresh has no AdDisplayed — enqueue a waiter per fill so late
                // ILRD still matches this impression's pinned auction/plc (do not clear prior waiters).
                if (info.Format == LiftEngineAdFormat.Banner)
                    EnqueueBannerActiveViewWaiter(info);

                LiftEngineSdkCallbacks.RaiseAdLoaded(info);
            };
            _mediation.AdDisplayed += info =>
            {
                _displayRevenueRecordedFormats.Remove(info.Format);

                _context.RecordAdImpression(info.Format);
                TryRecordImpressionRevenue(info.Format, info.Revenue, fromDisplay: true);
                OnAdDisplayedForActiveView(info);
                QueueReportAfterAdDisplay(info.Format);

                LiftEngineSdkCallbacks.RaiseAdDisplayed(info);
                _activeCallbacks?.OnAdDisplayed?.Invoke();
            };
            _mediation.AdHidden += info =>
            {
                // Do NOT clear activeview waiters or display-revenue dedupe here:
                // AdRevenuePaid often arrives after hide.

                LiftEngineSdkCallbacks.RaiseAdHidden(info);
                _activeCallbacks?.OnAdHidden?.Invoke();
                _activeCallbacks = null;
                _activeFormat = null;

                QueueReportAfterAdDisplay(info.Format);

                if (LiftEngineRuntimeTuning.PrewarmAfterShow)
                    _prewarm.Prewarm(info.Format);
            };
            _mediation.AdClicked += info => _activeCallbacks?.OnAdClicked?.Invoke();
            _mediation.AdRewarded += info =>
            {
                LiftEngineSdkCallbacks.RaiseAdRewarded(info);
                _activeCallbacks?.OnAdRewarded?.Invoke();
            };
            _mediation.AdRevenuePaid += info =>
            {
                TryRecordImpressionRevenue(info.Format, info.Revenue, fromDisplay: false);
                TrackActiveViewOnRevenue(info);
                QueueReportAfterAdDisplay(info.Format);
                LiftEngineSdkCallbacks.RaiseAdRevenue(info);
            };
            _mediation.AdDisplayFailed += err =>
            {
                TrackMediationError(err, "mediation_display_failed");
                _activeCallbacks?.OnAdDisplayFailed?.Invoke(err.Message);
            };
            _mediation.AdLoadFailed += err =>
            {
                TrackMediationError(err, "mediation_load_failed");
            };
        }

        public void SetAttribution(string installType, string mediaSource) =>
            _context.SetAttribution(installType, mediaSource);

        public void SetIdfaApproved(bool approved) => _context.SetIdfaApproved(approved);

        public void SetCountryCode(string countryCode) => _context.SetCountryCode(countryCode);

        public void NotifyPurchase(float amountUsd) => _context.NotifyPurchase(amountUsd);

        public void CheckHealth(Action<bool> callback) =>
            CheckHealth((ok, _) => callback?.Invoke(ok));

        public void CheckHealth(Action<bool, string> callback)
        {
            if (_api == null)
            {
                callback?.Invoke(false, "SDK not started");
                return;
            }

            _api.CheckHealth(callback);
        }

        public bool IsAdReady(LiftEngineAdFormat format) => _prewarm.IsReady(format);

        public AdPrewarmState GetPrewarmState(LiftEngineAdFormat format) => _prewarm.GetState(format);

        public void LoadAd(LiftEngineAdFormat format) => _prewarm.Prewarm(format);

        public void ShowAd(LiftEngineAdFormat format, LiftEngineShowAdParams parameters,
            LiftEngineShowAdCallbacks callbacks)
        {
            if (!IsInitialized)
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, format, "sdk_not_initialized",
                    "LiftEngine SDK not initialized.");
                callbacks?.OnAdDisplayFailed?.Invoke("LiftEngine SDK not initialized.");
                return;
            }

            _activeCallbacks = callbacks;
            _activeFormat = format;

            if (IsAdReady(format))
            {
                _mediation.Show(format, _settings.GetAdUnitId(format), _context.GetMaxPlacement(format));
                return;
            }

            _host.StartCoroutine(WaitAndShow(format, callbacks));
        }

        private IEnumerator WaitAndShow(LiftEngineAdFormat format, LiftEngineShowAdCallbacks callbacks)
        {
            if (_prewarm.GetState(format) == AdPrewarmState.Idle || _prewarm.GetState(format) == AdPrewarmState.Failed)
                _prewarm.Prewarm(format);

            var elapsed = 0f;
            while (!IsAdReady(format) && elapsed < LiftEngineRuntimeTuning.ShowWaitMaxSeconds)
            {
                yield return new WaitForSeconds(LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds);
                elapsed += LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds;

                if (_prewarm.GetState(format) == AdPrewarmState.Failed)
                    _prewarm.Prewarm(format);
            }

            if (!IsAdReady(format))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, format, "ad_not_ready",
                    $"Ad not ready after {elapsed:F0}s (prewarm state={_prewarm.GetState(format)}).");
                callbacks?.OnAdDisplayFailed?.Invoke($"Ad not ready after {elapsed:F0}s.");
                yield break;
            }

            _mediation.Show(format, _settings.GetAdUnitId(format), _context.GetMaxPlacement(format));
        }

        public void HideBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
                _mediation.HideBanner(adUnitId);
            // Keep banner activeview waiters — MAX may still pay revenue after hide.
        }

        public void DestroyBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
                _mediation.DestroyAd(LiftEngineAdFormat.Banner, adUnitId);
            // Keep waiters for late ILRD; they are removed only when activeview is sent.
        }

        public void ClearDebugContext() => _context.ClearContextData();

        public ReportContextService ContextService => _context;
        public LiftEngineApiClient ApiClient => _api;
        public LiftEngineSettings Settings => _settings;

        /// <summary>
        /// Records per-impression revenue into ecpm_history. AdRevenuePaid is the primary source;
        /// display revenue is used when MAX includes it on the display callback.
        /// Still skips revenue &lt;= 0 (unchanged) — activeview itself always sends rev as given.
        /// </summary>
        private void TryRecordImpressionRevenue(LiftEngineAdFormat format, double revenueUsd, bool fromDisplay)
        {
            if (revenueUsd <= 0d)
            {
                if (fromDisplay)
                    LiftEngineLogger.Log(
                        $"ecpm_history {format} — display had no revenue; waiting for AdRevenuePaid");
                return;
            }

            if (!fromDisplay && _displayRevenueRecordedFormats.Contains(format))
                return;

            _context.RecordAdRevenue(format, revenueUsd);
            if (fromDisplay)
                _displayRevenueRecordedFormats.Add(format);
        }

        private void OnAdDisplayedForActiveView(MediationAdInfo info)
        {
            switch (info.Format)
            {
                case LiftEngineAdFormat.Banner:
                    OnBannerDisplayedForActiveView(info);
                    break;
                case LiftEngineAdFormat.Interstitial:
                    OnInterstitialDisplayedForActiveView(info);
                    break;
                case LiftEngineAdFormat.Rewarded:
                    OnRewardedDisplayedForActiveView(info);
                    break;
            }
        }

        /// <summary>
        /// Banner fill already enqueued on AdLoaded; display only sends track/view for the
        /// oldest waiter that has not yet reported view (first show). If none, enqueue.
        /// </summary>
        private void OnBannerDisplayedForActiveView(MediationAdInfo info)
        {
            var queue = _bannerActiveViewWaiters;
            ActiveViewWaiter waiter = null;
            foreach (var pending in queue)
            {
                if (!pending.ViewSent)
                {
                    waiter = pending;
                    break;
                }
            }

            if (waiter == null)
            {
                waiter = CreateActiveViewWaiter(info);
                queue.Enqueue(waiter);
            }

            SendTrackViewIfNeeded(waiter, info);
        }

        private void OnInterstitialDisplayedForActiveView(MediationAdInfo info)
        {
            var waiter = CreateActiveViewWaiter(info);
            _interstitialActiveViewWaiters.Enqueue(waiter);
            SendTrackViewIfNeeded(waiter, info);
        }

        private void OnRewardedDisplayedForActiveView(MediationAdInfo info)
        {
            var waiter = CreateActiveViewWaiter(info);
            _rewardedActiveViewWaiters.Enqueue(waiter);
            SendTrackViewIfNeeded(waiter, info);
        }

        private void EnqueueBannerActiveViewWaiter(MediationAdInfo info)
        {
            var waiter = CreateActiveViewWaiter(info);
            _bannerActiveViewWaiters.Enqueue(waiter);
            LiftEngineLogger.Log(
                $"activeview waiter enqueued (banner) — auction_id={waiter.AuctionId}, " +
                $"plc={waiter.Plc}, queue={_bannerActiveViewWaiters.Count}");
        }

        private ActiveViewWaiter CreateActiveViewWaiter(MediationAdInfo info)
        {
            if (!_context.HasValidAuctionContext(info.Format))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, info.Format, "no_auction_context",
                    "Impression without auction_id; applying fallback auction context.",
                    info);
                _context.EnsureFallbackAuctionContext(info.Format);
            }

            var (keyword, auctionId) = _context.GetAuctionContext(info.Format);
            var attemptIndex = _context.GetAppliedAttemptIndex(info.Format);
            var appliedValue = _context.GetAppliedValue(info.Format);
            var plc = ResolveImpressionPlc(info);

            return new ActiveViewWaiter
            {
                Format = info.Format,
                Keyword = keyword,
                AuctionId = auctionId,
                AttemptIndex = attemptIndex,
                AppliedValue = appliedValue,
                Plc = plc,
                ViewSent = false
            };
        }

        private void TrackActiveViewOnRevenue(MediationAdInfo info)
        {
            switch (info.Format)
            {
                case LiftEngineAdFormat.Banner:
                    TrackBannerActiveViewOnRevenue(info);
                    break;
                case LiftEngineAdFormat.Interstitial:
                    TrackInterstitialActiveViewOnRevenue(info);
                    break;
                case LiftEngineAdFormat.Rewarded:
                    TrackRewardedActiveViewOnRevenue(info);
                    break;
            }
        }

        private void TrackBannerActiveViewOnRevenue(MediationAdInfo info) =>
            CompleteActiveViewWaiter(_bannerActiveViewWaiters, info);

        private void TrackInterstitialActiveViewOnRevenue(MediationAdInfo info) =>
            CompleteActiveViewWaiter(_interstitialActiveViewWaiters, info);

        private void TrackRewardedActiveViewOnRevenue(MediationAdInfo info) =>
            CompleteActiveViewWaiter(_rewardedActiveViewWaiters, info);

        /// <summary>
        /// Pops the oldest waiter for this format (one activeview per impression), sends
        /// track/activeview with whatever rev MAX provided (including 0), then removes the waiter.
        /// If the queue is empty (orphan ILRD), still sends once using freshly pinned context.
        /// </summary>
        private void CompleteActiveViewWaiter(Queue<ActiveViewWaiter> queue, MediationAdInfo info)
        {
            ActiveViewWaiter waiter;
            if (queue.Count > 0)
            {
                waiter = queue.Peek();
            }
            else
            {
                LiftEngineLogger.LogWarning(
                    $"activeview revenue with no waiter ({info.Format}) — sending orphan event");
                waiter = CreateActiveViewWaiter(info);
            }

            SendTrackViewIfNeeded(waiter, info);

            // Always send, including rev == 0. Only drop the waiter after the send attempt.
            SendTrackActiveView(waiter, (float)info.Revenue);

            if (queue.Count > 0 && ReferenceEquals(queue.Peek(), waiter))
                queue.Dequeue();
        }

        private void SendTrackViewIfNeeded(ActiveViewWaiter waiter, MediationAdInfo info)
        {
            if (waiter.ViewSent)
                return;

            // Prefer MAX placement from this callback when waiter was created without it.
            if (string.IsNullOrEmpty(waiter.Plc) && !string.IsNullOrEmpty(info?.MaxPlacement))
                waiter.Plc = info.MaxPlacement;

            SendTrackView(waiter);
            waiter.ViewSent = true;
        }

        private void SendTrackView(ActiveViewWaiter waiter)
        {
            var timestamp = ContextNormalizers.UnixTimestampSeconds();
            var bundleId = Application.identifier;
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var adType = _settings.GetModelName(waiter.Format);
            var plc = waiter.Plc ?? string.Empty;

            if (string.IsNullOrEmpty(plc))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, waiter.Format, "missing_plc",
                    "view event missing MAX placement (plc); cannot attribute impression route.");
            }

            LiftEngineLogger.LogClient(
                $"Track view — ad_type={adType}, bundle={bundleId}, device={deviceId}, " +
                $"app_version={Application.version}, plc={plc}, placement_id={plc}, " +
                $"keyword={waiter.Keyword}, auction_id={waiter.AuctionId}, attempt={waiter.AttemptIndex}, " +
                $"timestamp={timestamp}");
            _api.TrackView(bundleId, deviceId, adType, plc, waiter.Keyword, waiter.AuctionId, timestamp,
                waiter.AttemptIndex);
        }

        private void SendTrackActiveView(ActiveViewWaiter waiter, float rev)
        {
            var timestamp = ContextNormalizers.UnixTimestampSeconds();
            var bundleId = Application.identifier;
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var adType = _settings.GetModelName(waiter.Format);
            var plc = waiter.Plc ?? string.Empty;

            if (string.IsNullOrEmpty(plc))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, waiter.Format, "missing_plc",
                    "activeview event missing MAX placement (plc); cannot attribute impression route.");
            }

            // History for THIS format only (revenue already pushed in TryRecordImpressionRevenue when > 0).
            var ecpmHistory = EcpmHistoryBuffer.GetForFormat(waiter.Format);
            LiftEngineLogger.LogClient(
                $"Track activeview — ad_type={adType}, bundle={bundleId}, device={deviceId}, " +
                $"app_version={Application.version}, plc={plc}, placement_id={plc}, " +
                $"keyword={waiter.Keyword}, auction_id={waiter.AuctionId}, attempt={waiter.AttemptIndex}, " +
                $"timestamp={timestamp}, rev={rev}, v={waiter.AppliedValue}, ecpm_history_len={ecpmHistory.Length}");
            _api.TrackActiveView(bundleId, deviceId, adType, plc, waiter.Keyword, waiter.AuctionId, timestamp,
                rev, waiter.AttemptIndex, waiter.AppliedValue, ecpmHistory);
        }

        /// <summary>
        /// Resolves the MAX placement used for this impression (e.g. LiftEngine_a_rv).
        /// Priority: MAX AdInfo.Placement → context route for format.
        /// </summary>
        private string ResolveImpressionPlc(MediationAdInfo info)
        {
            if (!string.IsNullOrEmpty(info?.MaxPlacement))
                return info.MaxPlacement;

            return _context.GetMaxPlacement(info.Format);
        }

        private void TrackMediationError(MediationAdError err, string errorCode)
        {
            if (err == null)
                return;

            LiftEngineTrackReporter.ReportError(
                _api, _settings, _context, err.Format, errorCode,
                $"MAX code={err.Code}: {err.Message}", adUnitId: err.AdUnitId);
        }

        private void QueueReportAfterAdDisplay(LiftEngineAdFormat format)
        {
            // Accumulate formats — a single pending slot was last-writer-wins and could
            // replace a banner report with interstitial/rewarded (wrong ecpm_history).
            _pendingReportFormats.Add(format);
            if (_reportDebounceCoroutine != null)
                _host.StopCoroutine(_reportDebounceCoroutine);

            _reportDebounceCoroutine = _host.StartCoroutine(SendReportDebounced());
        }

        private IEnumerator SendReportDebounced()
        {
            yield return new WaitForSeconds(2f);

            if (_pendingReportFormats.Count > 0)
            {
                var formats = new List<LiftEngineAdFormat>(_pendingReportFormats);
                _pendingReportFormats.Clear();
                foreach (var format in formats)
                    SendReport(format);
            }

            _reportDebounceCoroutine = null;
        }
    }
}
