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
        private LiftEngineAdFormat? _pendingReportFormat;
        private Coroutine _reportDebounceCoroutine;
        private Coroutine _firstReportRetryCoroutine;
        private bool _firstReportSucceeded;
        private int _reportInFlightCount;
        private bool _prewarmAllRequestedAfterFirstReport;
        private bool _displayRevenueRecordedThisImpression;
        private bool _bannerViewTrackedForCurrentFill;
        private bool _bannerActiveViewTrackedForCurrentFill;
        private bool _viewTrackedForCurrentImpression;
        private bool _activeViewTrackedForCurrentImpression;
        private readonly Dictionary<LiftEngineAdFormat, string> _impressionPlcByFormat = new();
        // Pinned at first view for this fill so activeview survives a post-hide prewarm override.
        private readonly Dictionary<LiftEngineAdFormat, (string keyword, string auctionId, int mulIndex)> _impressionAuctionByFormat = new();

        public bool IsInitialized { get; private set; }

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

        public void SendReport(Action<bool> callback = null) =>
            SendReport(LiftEngineAdFormat.Interstitial, callback);

        public void SendReport(LiftEngineAdFormat format, Action<bool> callback = null)
        {
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var payload = _context.BuildPayload(format);
            LiftEngineLogger.LogClient($"Report — sending context ({format})");
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

            EnsureFirstReportBeforePredict();
        }

        /// <summary>
        /// Backend requires report before predict. Blocks all prewarm/predict until first report succeeds.
        /// </summary>
        private void EnsureFirstReportBeforePredict()
        {
            if (_firstReportSucceeded)
            {
                UnlockPredictAndFlushPrewarm();
                return;
            }

            if (_reportInFlightCount > 0)
                return;

            SendReport(LiftEngineAdFormat.Interstitial);
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

            LiftEngineLogger.LogClient("Report — retrying first report before predict");
            EnsureFirstReportBeforePredict();
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
                UnlockPredictAndFlushPrewarm();
        }

        private void UnlockPredictAndFlushPrewarm()
        {
            _prewarm.AllowPredictAfterFirstReport();

            if (!_prewarmAllRequestedAfterFirstReport)
                return;

            _prewarmAllRequestedAfterFirstReport = false;
            _prewarm.PrewarmAll();
        }

        private void SubscribeMediationEvents()
        {
            _mediation.AdLoaded += info =>
            {
                if (info.Format == LiftEngineAdFormat.Banner)
                {
                    _bannerViewTrackedForCurrentFill = false;
                    _bannerActiveViewTrackedForCurrentFill = false;
                    _impressionPlcByFormat.Remove(LiftEngineAdFormat.Banner);
                    _impressionAuctionByFormat.Remove(LiftEngineAdFormat.Banner);
                }

                LiftEngineSdkCallbacks.RaiseAdLoaded(info);
            };
            _mediation.AdDisplayed += info =>
            {
                _displayRevenueRecordedThisImpression = false;
                _viewTrackedForCurrentImpression = false;
                _activeViewTrackedForCurrentImpression = false;
                // New impression: drop prior pin so view/activeview capture this fill's auction + plc.
                // Do not clear these on AdHidden — MAX often pays revenue after hide.
                _impressionPlcByFormat.Remove(info.Format);
                _impressionAuctionByFormat.Remove(info.Format);

                _context.RecordAdImpression(info.Format);
                TryRecordImpressionRevenue(info.Format, info.Revenue, fromDisplay: true);
                TrackViewOnDisplay(info);
                QueueReportAfterAdDisplay(info.Format);

                LiftEngineSdkCallbacks.RaiseAdDisplayed(info);
                _activeCallbacks?.OnAdDisplayed?.Invoke();
            };
            _mediation.AdHidden += info =>
            {
                _displayRevenueRecordedThisImpression = false;

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
            {
                _mediation.HideBanner(adUnitId);
                _bannerViewTrackedForCurrentFill = false;
                _bannerActiveViewTrackedForCurrentFill = false;
            }
        }

        public void DestroyBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
            {
                _mediation.DestroyAd(LiftEngineAdFormat.Banner, adUnitId);
                _bannerViewTrackedForCurrentFill = false;
                _bannerActiveViewTrackedForCurrentFill = false;
            }
        }

        public void ClearDebugContext() => _context.ClearContextData();

        public ReportContextService ContextService => _context;
        public LiftEngineApiClient ApiClient => _api;
        public LiftEngineSettings Settings => _settings;

        /// <summary>
        /// Records per-impression revenue into ecpm_history. AdRevenuePaid is the primary source;
        /// display revenue is used when MAX includes it on the display callback.
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

            if (!fromDisplay && _displayRevenueRecordedThisImpression)
                return;

            _context.RecordAdRevenue(format, revenueUsd);
            if (fromDisplay)
                _displayRevenueRecordedThisImpression = true;
        }

        private void TrackViewOnDisplay(MediationAdInfo info)
        {
            if (info.Format == LiftEngineAdFormat.Banner)
            {
                if (_bannerViewTrackedForCurrentFill)
                    return;

                _bannerViewTrackedForCurrentFill = true;
            }
            else if (_viewTrackedForCurrentImpression)
            {
                return;
            }
            else
            {
                _viewTrackedForCurrentImpression = true;
            }

            EnsureAuctionContext(info);
            SendTrackView(info);
        }

        private void TrackActiveViewOnRevenue(MediationAdInfo info)
        {
            if (info.Format == LiftEngineAdFormat.Banner)
            {
                if (!_bannerViewTrackedForCurrentFill)
                    TrackViewOnDisplay(info);

                if (_bannerActiveViewTrackedForCurrentFill)
                    return;

                _bannerActiveViewTrackedForCurrentFill = true;
            }
            else
            {
                if (!_viewTrackedForCurrentImpression)
                    TrackViewOnDisplay(info);

                if (_activeViewTrackedForCurrentImpression)
                    return;

                _activeViewTrackedForCurrentImpression = true;
            }

            if (info.Revenue <= 0d)
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, info.Format, "revenue_missing",
                    "MAX AdRevenuePaid callback received without revenue; activeview requires rev.",
                    info);
                return;
            }

            EnsureAuctionContext(info);
            SendTrackActiveView(info, (float)info.Revenue);
        }

        /// <summary>
        /// Resolves auction context for this impression and pins it so view + activeview stay aligned
        /// even if a post-hide prewarm overrides the store with a new predict/fallback id.
        /// </summary>
        private void EnsureAuctionContext(MediationAdInfo info)
        {
            if (_impressionAuctionByFormat.TryGetValue(info.Format, out var pinned) &&
                !string.IsNullOrEmpty(pinned.auctionId))
                return;

            if (!_context.HasValidAuctionContext(info.Format))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, info.Format, "no_auction_context",
                    "Impression without predict auction_id; applying fallback auction context.",
                    info);
                _context.EnsureFallbackAuctionContext(info.Format);
            }

            var (keyword, auctionId) = _context.GetAuctionContext(info.Format);
            var mulIndex = _context.GetWinningMultiplierIndex(info.Format);
            if (!string.IsNullOrEmpty(auctionId))
                _impressionAuctionByFormat[info.Format] = (keyword, auctionId, mulIndex);
        }

        private (string keyword, string auctionId, int mulIndex) GetImpressionAuction(LiftEngineAdFormat format)
        {
            if (_impressionAuctionByFormat.TryGetValue(format, out var pinned) &&
                !string.IsNullOrEmpty(pinned.auctionId))
                return pinned;

            var (keyword, auctionId) = _context.GetAuctionContext(format);
            return (keyword, auctionId, _context.GetWinningMultiplierIndex(format));
        }

        private void SendTrackView(MediationAdInfo info)
        {
            var (keyword, auctionId, mulIndex) = GetImpressionAuction(info.Format);
            var timestamp = PredictDataNormalizers.UnixTimestampSeconds();
            var bundleId = Application.identifier;
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var plc = ResolveAndCacheImpressionPlc(info);
            var adType = _settings.GetModelName(info.Format);

            if (string.IsNullOrEmpty(plc))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, info.Format, "missing_plc",
                    "view event missing MAX placement (plc); cannot attribute impression route.",
                    info);
            }

            LiftEngineLogger.LogClient(
                $"Track view — ad_type={adType}, bundle={bundleId}, device={deviceId}, " +
                $"app_version={Application.version}, plc={plc}, placement_id={plc}, " +
                $"keyword={keyword}, auction_id={auctionId}, Mulindex={mulIndex}, timestamp={timestamp}");
            _api.TrackView(bundleId, deviceId, adType, plc, keyword, auctionId, timestamp, mulIndex);
        }

        private void SendTrackActiveView(MediationAdInfo info, float rev)
        {
            var (keyword, auctionId, mulIndex) = GetImpressionAuction(info.Format);
            var timestamp = PredictDataNormalizers.UnixTimestampSeconds();
            var bundleId = Application.identifier;
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            // Prefer placement captured at display so reload after show cannot change plc mid-impression.
            var plc = GetCachedOrResolveImpressionPlc(info);
            var adType = _settings.GetModelName(info.Format);

            if (string.IsNullOrEmpty(plc))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, info.Format, "missing_plc",
                    "activeview event missing MAX placement (plc); cannot attribute impression route.",
                    info);
            }

            LiftEngineLogger.LogClient(
                $"Track activeview — ad_type={adType}, bundle={bundleId}, device={deviceId}, " +
                $"app_version={Application.version}, plc={plc}, placement_id={plc}, " +
                $"keyword={keyword}, auction_id={auctionId}, Mulindex={mulIndex}, timestamp={timestamp}, rev={rev}");
            _api.TrackActiveView(bundleId, deviceId, adType, plc, keyword, auctionId, timestamp, rev, mulIndex);
        }

        /// <summary>
        /// Resolves the MAX placement used for this impression (e.g. LiftEngine_a_rv) and caches it
        /// so view + activeview report the same plc even if a reload changes route afterwards.
        /// Priority: MAX AdInfo.Placement → mediation state → context route for format.
        /// </summary>
        private string ResolveAndCacheImpressionPlc(MediationAdInfo info)
        {
            var plc = ResolveImpressionPlc(info);
            if (!string.IsNullOrEmpty(plc))
                _impressionPlcByFormat[info.Format] = plc;
            return plc;
        }

        private string GetCachedOrResolveImpressionPlc(MediationAdInfo info)
        {
            if (_impressionPlcByFormat.TryGetValue(info.Format, out var cached) &&
                !string.IsNullOrEmpty(cached))
                return cached;

            return ResolveAndCacheImpressionPlc(info);
        }

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
            _pendingReportFormat = format;
            if (_reportDebounceCoroutine != null)
                _host.StopCoroutine(_reportDebounceCoroutine);

            _reportDebounceCoroutine = _host.StartCoroutine(SendReportDebounced());
        }

        private IEnumerator SendReportDebounced()
        {
            yield return new WaitForSeconds(2f);

            if (_pendingReportFormat.HasValue)
            {
                var format = _pendingReportFormat.Value;
                _pendingReportFormat = null;
                SendReport(format);
            }

            _reportDebounceCoroutine = null;
        }
    }
}
