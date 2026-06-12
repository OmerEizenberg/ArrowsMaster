using System;
using System.Collections;
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
        private BidFloorLoadOrchestrator _orchestrator;
        private AdPrewarmService _prewarm;

        private LiftEngineShowAdCallbacks _activeCallbacks;
        private LiftEngineAdFormat? _activeFormat;
        private LiftEngineAdFormat? _pendingReportFormat;
        private Coroutine _reportDebounceCoroutine;

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

            _api = new LiftEngineApiClient(_settings, _host);
            _mediation = MediationAdapterFactory.Create(_settings.mediationPlatform);
            _orchestrator = new BidFloorLoadOrchestrator(_settings, _mediation, _host);
            _prewarm = new AdPrewarmService(_settings, _api, _context, _mediation, _orchestrator, _host);

            SubscribeMediationEvents();

            _mediation.Initialize(_settings, success =>
            {
                if (!success)
                {
                    LiftEngineLogger.LogError("Mediation initialization failed.");
                    LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Failed);
                    return;
                }

                if (_settings.runHealthCheckOnInit)
                    _api.CheckHealth((_, _) => CompleteInit());
                else
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
            _api.Report(deviceId, payload, success =>
            {
                if (success)
                    LiftEngineLogger.LogBackend("Report — OK");
                else
                    LiftEngineLogger.LogBackendWarning("Report — failed");

                callback?.Invoke(success);
            });
        }

        private void CompleteInit()
        {
            IsInitialized = true;
            LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Success);

            _prewarm.StartBackgroundRefill();

            if (_settings.prewarmOnInit)
                _prewarm.PrewarmAll();
        }

        private void SubscribeMediationEvents()
        {
            _mediation.AdLoaded += info => LiftEngineSdkCallbacks.RaiseAdLoaded(info);
            _mediation.AdDisplayed += info =>
            {
                _context.RecordAdImpression(info.Format);
                TrackDisplay(info);
                if (info.Format == LiftEngineAdFormat.Banner)
                    QueueReportAfterAdDisplay(info.Format);

                LiftEngineSdkCallbacks.RaiseAdDisplayed(info);
                _activeCallbacks?.OnAdDisplayed?.Invoke();
            };
            _mediation.AdHidden += info =>
            {
                LiftEngineSdkCallbacks.RaiseAdHidden(info);
                _activeCallbacks?.OnAdHidden?.Invoke();
                _activeCallbacks = null;
                _activeFormat = null;

                QueueReportAfterAdDisplay(info.Format);

                if (_settings.prewarmAfterShow)
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
                _context.RecordAdRevenue(info.Format, info.Revenue);
                QueueReportAfterAdDisplay(info.Format);
                LiftEngineSdkCallbacks.RaiseAdRevenue(info);
            };
            _mediation.AdDisplayFailed += err =>
            {
                TrackError(err.Format, err.Code.ToString(), err.Message);
                _activeCallbacks?.OnAdDisplayFailed?.Invoke(err.Message);
            };
            _mediation.AdLoadFailed += err =>
            {
                TrackError(err.Format, err.Code.ToString(), err.Message);
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
                callbacks?.OnAdDisplayFailed?.Invoke("LiftEngine SDK not initialized.");
                return;
            }

            _activeCallbacks = callbacks;
            _activeFormat = format;

            if (IsAdReady(format))
            {
                _mediation.Show(format, _settings.GetAdUnitId(format));
                return;
            }

            _host.StartCoroutine(WaitAndShow(format, callbacks));
        }

        private IEnumerator WaitAndShow(LiftEngineAdFormat format, LiftEngineShowAdCallbacks callbacks)
        {
            if (_prewarm.GetState(format) == AdPrewarmState.Idle || _prewarm.GetState(format) == AdPrewarmState.Failed)
                _prewarm.Prewarm(format);

            var elapsed = 0f;
            while (!IsAdReady(format) && elapsed < _settings.showWaitMaxSeconds)
            {
                yield return new WaitForSeconds(_settings.readinessCheckIntervalSeconds);
                elapsed += _settings.readinessCheckIntervalSeconds;

                if (_prewarm.GetState(format) == AdPrewarmState.Failed)
                    _prewarm.Prewarm(format);
            }

            if (!IsAdReady(format))
            {
                callbacks?.OnAdDisplayFailed?.Invoke($"Ad not ready after {elapsed:F0}s.");
                yield break;
            }

            _mediation.Show(format, _settings.GetAdUnitId(format));
        }

        public void HideBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
                _mediation.HideBanner(adUnitId);
        }

        public void DestroyBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
                _mediation.DestroyAd(LiftEngineAdFormat.Banner, adUnitId);
        }

        public void ClearDebugContext() => _context.ClearContextData();

        public ReportContextService ContextService => _context;
        public LiftEngineApiClient ApiClient => _api;
        public LiftEngineSettings Settings => _settings;

        private void TrackDisplay(MediationAdInfo info)
        {
            var (keyword, auctionId) = _context.GetAuctionContext(info.Format);
            var timestamp = PredictDataNormalizers.UnixTimestampSeconds();
            var bundleId = Application.identifier;
            var deviceId = Ads.DeviceIdProvider.GetDeviceId();
            var placementId = _settings.GetPlacementId(info.Format);
            var rev = info.Revenue > 0 ? (float?)info.Revenue : null;

            var adType = _settings.GetModelName(info.Format);

            if (string.IsNullOrEmpty(auctionId))
                LiftEngineLogger.LogBackendWarning(
                    $"Track {info.Format} — missing auction_id for placement={placementId}");

            LiftEngineLogger.LogClient(
                $"Track activeview — ad_type={adType}, bundle={bundleId}, device={deviceId}, " +
                $"placement={placementId}, keyword={keyword}, auction_id={auctionId}, " +
                $"timestamp={timestamp}, rev={rev}");
            _api.TrackActiveView(bundleId, deviceId, adType, placementId, keyword, auctionId, timestamp, rev);
        }

        private void TrackError(LiftEngineAdFormat format, string code, string message)
        {
            var (_, auctionId) = _context.GetAuctionContext(format);
            _api.TrackError(Application.identifier, auctionId, code, message);
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
