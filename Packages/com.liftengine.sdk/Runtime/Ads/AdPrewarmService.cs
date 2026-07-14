using System;
using System.Collections;
using LiftEngine.Api;
using LiftEngine.Context;
using LiftEngine.Mediation;
using UnityEngine;

namespace LiftEngine.Ads
{
    internal sealed class AdPrewarmService
    {
        private readonly LiftEngineSettings _settings;
        private readonly LiftEngineApiClient _api;
        private readonly ReportContextService _context;
        private readonly IMediationAdapter _mediation;
        private readonly AdLoadOrchestrator _orchestrator;
        private readonly MonoBehaviour _host;

        private readonly AdPrewarmState[] _states = new AdPrewarmState[3];
        private readonly bool[] _prewarmInFlight = new bool[3];
        private bool _refillLoopStarted;

        public AdPrewarmService(
            LiftEngineSettings settings,
            LiftEngineApiClient api,
            ReportContextService context,
            IMediationAdapter mediation,
            AdLoadOrchestrator orchestrator,
            MonoBehaviour host)
        {
            _settings = settings;
            _api = api;
            _context = context;
            _mediation = mediation;
            _orchestrator = orchestrator;
            _host = host;
        }

        public AdPrewarmState GetState(LiftEngineAdFormat format) => _states[(int)format];

        public bool IsReady(LiftEngineAdFormat format)
        {
            var adUnitId = _settings.GetAdUnitId(format);
            return _states[(int)format] == AdPrewarmState.Ready && _mediation.IsReady(format, adUnitId);
        }

        public void StartBackgroundRefill()
        {
            if (_refillLoopStarted)
                return;

            _refillLoopStarted = true;
            _host.StartCoroutine(BackgroundRefillLoop());
        }

        public void PrewarmAll()
        {
            Prewarm(LiftEngineAdFormat.Rewarded);
            Prewarm(LiftEngineAdFormat.Interstitial);
            Prewarm(LiftEngineAdFormat.Banner);
        }

        public void Prewarm(LiftEngineAdFormat format)
        {
            var index = (int)format;
            if (_prewarmInFlight[index])
                return;

            _prewarmInFlight[index] = true;
            _states[index] = AdPrewarmState.Optimizing;
            LiftEngineLogger.LogClient($"Prewarm {format} — starting optimization");
            _host.StartCoroutine(PrewarmRoutine(format));
        }

        private IEnumerator BackgroundRefillLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(LiftEngineRuntimeTuning.PrewarmRetryIntervalSeconds);

                for (var i = 0; i < _states.Length; i++)
                {
                    var format = (LiftEngineAdFormat)i;
                    if (IsReady(format) || _prewarmInFlight[i])
                        continue;

                    LiftEngineLogger.LogClient(
                        $"Refill — {format} not ready (state={_states[i]}), restarting prewarm.");
                    Prewarm(format);
                }
            }
        }

        private IEnumerator PrewarmRoutine(LiftEngineAdFormat format)
        {
            var index = (int)format;
            LiftEngineOptimizationResult optimization = null;
            LiftEngineError optimizationError = null;
            var deviceId = DeviceIdProvider.GetDeviceId();
            var adUnitId = _settings.GetAdUnitId(format);

            if (string.IsNullOrEmpty(adUnitId))
            {
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, format, "missing_ad_unit",
                    $"No MAX ad unit configured for {format}.", adUnitId: adUnitId);
                _states[index] = AdPrewarmState.Failed;
                _prewarmInFlight[index] = false;
                LiftEngineSignalBus.Publish(new AdPrewarmCompletedSignal(format, false));
                LiftEngineSignalBus.Publish(new AdReadyStateChangedSignal(format, false));
                yield break;
            }

            _context.ClearAuctionContext(format);
            LiftEngineLogger.LogClient($"Prewarm {format} — cleared context, requesting optimization");

            var payload = _context.BuildPayload(format);
            var optimizationDone = false;

            _api.RequestOptimization(deviceId, format, payload,
                result =>
                {
                    optimization = result;
                    optimizationDone = true;
                },
                error =>
                {
                    optimizationError = error;
                    optimizationDone = true;
                });

            var optimizationElapsed = 0f;
            while (!optimizationDone && optimizationElapsed < LiftEngineRuntimeTuning.OptimizationTimeoutSeconds)
            {
                yield return new WaitForSeconds(0.05f);
                optimizationElapsed += 0.05f;
            }

            if (optimization != null)
            {
                optimization.ResolveOptimizationValue(LiftEngineRuntimeTuning.DefaultOptimizationFallback);

                _context.SetAuctionContext(format, optimization.keyword, optimization.auction_id, optimization.param,
                    optimization.treatment, optimization.group_ratios);
                if (!_context.HasValidAuctionContext(format))
                {
                    LiftEngineTrackReporter.ReportError(
                        _api, _settings, _context, format, "optimization_missing_auction",
                        "Predict response returned without auction_id.", adUnitId: adUnitId);
                    _context.EnsureFallbackAuctionContext(format);
                }

                LiftEngineLogger.LogBackend(
                    $"{format} optimization OK — placement={_context.GetMaxPlacement(format)}, " +
                    $"multipliers={optimization.multipliers?.Length ?? 0}");
                LiftEngineSdkCallbacks.RaiseOptimizationSuccess(format);
            }
            else
            {
                var reason = optimizationError != null
                    ? $"{optimizationError.StatusCode}: {optimizationError.Message}"
                    : $"timeout after {LiftEngineRuntimeTuning.OptimizationTimeoutSeconds:F0}s";

                LiftEngineLogger.LogBackendWarning(
                    $"{format} optimization failed ({reason}) — using fallback load");
                LiftEngineLogger.LogAttemptWarning(-1,
                    $"{format} — optimization unavailable → fallback load only");

                LiftEngineSdkCallbacks.RaiseOptimizationFailed(format,
                    optimizationError ?? new LiftEngineError(0, reason));
                LiftEngineSignalBus.Publish(new OptimizationUnavailableSignal(format));
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, format, "optimization_failed", reason, adUnitId: adUnitId);
                _context.EnsureFallbackAuctionContext(format);
            }

            _states[index] = AdPrewarmState.Loading;

            var loadDone = false;
            var loadSuccess = false;
            var maxPlacement = _context.GetMaxPlacement(format);
            _orchestrator.TryLoadWithOptimization(format, optimization, maxPlacement, success =>
            {
                loadSuccess = success;
                loadDone = true;
            });

            var loadElapsed = 0f;
            while (!loadDone && loadElapsed < LiftEngineRuntimeTuning.PrewarmMaxDurationSeconds)
            {
                yield return null;
                loadElapsed += Time.deltaTime;
            }

            var loadTimedOut = !loadDone;
            if (loadTimedOut)
            {
                LiftEngineLogger.LogBackendWarning(
                    $"{format} prewarm timed out after {LiftEngineRuntimeTuning.PrewarmMaxDurationSeconds}s.");
                LiftEngineTrackReporter.ReportError(
                    _api, _settings, _context, format, "prewarm_load_timeout",
                    $"Prewarm load did not complete within {LiftEngineRuntimeTuning.PrewarmMaxDurationSeconds}s.",
                    adUnitId: adUnitId);
                loadSuccess = false;
            }

            _states[index] = loadSuccess ? AdPrewarmState.Ready : AdPrewarmState.Failed;
            _prewarmInFlight[index] = false;

            if (!loadSuccess)
            {
                LiftEngineLogger.LogBackendWarning(
                    $"{format} prewarm finished without fill (state=Failed). Will retry in {LiftEngineRuntimeTuning.PrewarmRetryIntervalSeconds}s.");
                if (!loadTimedOut)
                {
                    LiftEngineTrackReporter.ReportError(
                        _api, _settings, _context, format, "prewarm_no_fill",
                        "Prewarm finished without a fill after optimization and fallback load attempts.",
                        adUnitId: adUnitId);
                }

                _host.StartCoroutine(SchedulePrewarmRetry(format));
            }

            LiftEngineSignalBus.Publish(new AdPrewarmCompletedSignal(format, loadSuccess));
            LiftEngineSignalBus.Publish(new AdReadyStateChangedSignal(format, loadSuccess));
        }

        private IEnumerator SchedulePrewarmRetry(LiftEngineAdFormat format)
        {
            yield return new WaitForSeconds(LiftEngineRuntimeTuning.PrewarmRetryIntervalSeconds);

            var index = (int)format;
            if (IsReady(format) || _prewarmInFlight[index])
                yield break;

            LiftEngineLogger.LogClient($"Prewarm {format} — retrying after no fill.");
            Prewarm(format);
        }
    }

    internal static class DeviceIdProvider
    {
        public static string GetDeviceId() =>
            SystemInfo.deviceUniqueIdentifier;
    }
}
