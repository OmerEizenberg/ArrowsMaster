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
                {
                    _api.CheckHealth((ok, body) =>
                    {
                    LiftEngineLogger.Log(ok ? $"Health OK: {body}" : $"Health failed: {body}");
                        CompleteInit();
                    });
                }
                else
                {
                    CompleteInit();
                }
            });
        }

        private void CompleteInit()
        {
            IsInitialized = true;
            LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Success);

            if (_settings.prewarmOnInit)
                _prewarm.PrewarmAll();
        }

        private void SubscribeMediationEvents()
        {
            _mediation.AdLoaded += info => LiftEngineSdkCallbacks.RaiseAdLoaded(info);
            _mediation.AdDisplayed += info =>
            {
                _context.RecordAdDisplayed(info.Format, info.Revenue);
                TrackDisplay(info);
                LiftEngineSdkCallbacks.RaiseAdDisplayed(info);
                _activeCallbacks?.OnAdDisplayed?.Invoke();
            };
            _mediation.AdHidden += info =>
            {
                LiftEngineSdkCallbacks.RaiseAdHidden(info);
                _activeCallbacks?.OnAdHidden?.Invoke();
                _activeCallbacks = null;
                _activeFormat = null;

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
                _context.RecordAdDisplayed(info.Format, info.Revenue);
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

            while (!IsAdReady(format))
            {
                yield return new WaitForSeconds(_settings.readinessCheckIntervalSeconds);

                if (_prewarm.GetState(format) == AdPrewarmState.Failed)
                {
                    callbacks?.OnAdDisplayFailed?.Invoke("Ad prewarm failed.");
                    yield break;
                }
            }

            _mediation.Show(format, _settings.GetAdUnitId(format));
        }

        public void HideBanner()
        {
            var adUnitId = _settings.GetAdUnitId(LiftEngineAdFormat.Banner);
            if (!string.IsNullOrEmpty(adUnitId))
                _mediation.HideBanner(adUnitId);
        }

        public void ClearDebugContext() => _context.ClearContextData();

        public ReportContextService ContextService => _context;
        public LiftEngineApiClient ApiClient => _api;
        public LiftEngineSettings Settings => _settings;

        private void TrackDisplay(MediationAdInfo info)
        {
            var (keyword, auctionId) = _context.GetAuctionContext(info.Format);
            var timestamp = DateTime.UtcNow.ToString("o");
            var bundleId = Application.identifier;
            var placementId = _settings.GetPlacementId(info.Format);
            var rev = info.Revenue > 0 ? (float?)info.Revenue : null;

            if (info.Format == LiftEngineAdFormat.Banner)
            {
                _api.TrackView(bundleId, placementId, keyword, auctionId, timestamp, rev);
            }
            else
            {
                _api.TrackActiveView(bundleId, _settings.GetModelName(info.Format), placementId, keyword,
                    auctionId, timestamp, rev);
            }
        }

        private void TrackError(LiftEngineAdFormat format, string code, string message)
        {
            var (_, auctionId) = _context.GetAuctionContext(format);
            _api.TrackError(Application.identifier, auctionId, code, message);
        }
    }
}
