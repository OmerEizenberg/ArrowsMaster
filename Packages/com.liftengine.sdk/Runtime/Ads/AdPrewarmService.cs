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
        private readonly BidFloorLoadOrchestrator _orchestrator;
        private readonly MonoBehaviour _host;

        private readonly AdPrewarmState[] _states = new AdPrewarmState[3];
        private readonly bool[] _prewarmInFlight = new bool[3];
        private bool _refillLoopStarted;

        public AdPrewarmService(
            LiftEngineSettings settings,
            LiftEngineApiClient api,
            ReportContextService context,
            IMediationAdapter mediation,
            BidFloorLoadOrchestrator orchestrator,
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
            _states[index] = AdPrewarmState.Predicting;
            LiftEngineLogger.LogClient($"Prewarm {format} — starting predict");
            _host.StartCoroutine(PrewarmRoutine(format));
        }

        private IEnumerator BackgroundRefillLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(_settings.prewarmRetryIntervalSeconds);

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
            LiftEnginePredictResult prediction = null;
            LiftEngineError predictError = null;
            var deviceId = DeviceIdProvider.GetDeviceId();

            _context.ClearAuctionContext(format);
            LiftEngineLogger.LogClient($"Prewarm {format} — cleared auction context, starting predict");

            var payload = _context.BuildPayload(format);
            var predictDone = false;

            _api.Predict(deviceId, format, payload,
                result =>
                {
                    prediction = result;
                    predictDone = true;
                },
                error =>
                {
                    predictError = error;
                    predictDone = true;
                });

            var predictElapsed = 0f;
            while (!predictDone && predictElapsed < _settings.predictTimeoutSeconds)
            {
                yield return new WaitForSeconds(0.05f);
                predictElapsed += 0.05f;
            }

            if (prediction != null)
            {
                if (prediction.prediction <= 0f)
                    prediction.prediction = _settings.defaultPredictionFallback;

                _context.SetAuctionContext(format, prediction.keyword, prediction.auction_id, prediction.param,
                    prediction.message);
                LiftEngineLogger.LogBackend(
                    $"{format} predict OK — auction_id={prediction.auction_id}, keyword={prediction.keyword}, " +
                    $"param={prediction.param}, message={prediction.message}, " +
                    $"prediction={prediction.prediction}, multipliers={prediction.multipliers?.Length ?? 0}, " +
                    $"maxPlacement={_context.GetMaxPlacement(format)}");
                LiftEngineSdkCallbacks.RaisePredictSuccess(prediction);
            }
            else
            {
                var reason = predictError != null
                    ? $"{predictError.StatusCode}: {predictError.Message}"
                    : $"timeout after {_settings.predictTimeoutSeconds:F0}s";

                LiftEngineLogger.LogBackendWarning(
                    $"{format} predict failed ({reason}) — auction context cleared, falling back to bid-0");
                LiftEngineLogger.LogAttemptWarning(-1,
                    $"{format} — no multipliers available → [Attempt -1] bid-0 only");

                LiftEngineSdkCallbacks.RaisePredictFailed(predictError ?? new LiftEngineError(0, reason));
                LiftEngineSignalBus.Publish(new BidFloorPredictionFailedSignal(format));
            }

            _states[index] = AdPrewarmState.Loading;

            var loadDone = false;
            var loadSuccess = false;
            var maxPlacement = _context.GetMaxPlacement(format);
            _orchestrator.TryLoadWithPrediction(format, prediction, maxPlacement, success =>
            {
                loadSuccess = success;
                loadDone = true;
            });

            var loadElapsed = 0f;
            while (!loadDone && loadElapsed < _settings.prewarmMaxDurationSeconds)
            {
                yield return null;
                loadElapsed += Time.deltaTime;
            }

            if (!loadDone)
            {
                LiftEngineLogger.LogBackendWarning(
                    $"{format} prewarm timed out after {_settings.prewarmMaxDurationSeconds}s.");
                loadSuccess = false;
            }

            _states[index] = loadSuccess ? AdPrewarmState.Ready : AdPrewarmState.Failed;
            _prewarmInFlight[index] = false;

            if (!loadSuccess)
            {
                LiftEngineLogger.LogBackendWarning(
                    $"{format} prewarm finished without fill (state=Failed). Will retry in {_settings.prewarmRetryIntervalSeconds}s.");
                _host.StartCoroutine(SchedulePrewarmRetry(format));
            }

            LiftEngineSignalBus.Publish(new AdPrewarmCompletedSignal(format, loadSuccess));
            LiftEngineSignalBus.Publish(new AdReadyStateChangedSignal(format, loadSuccess));
        }

        private IEnumerator SchedulePrewarmRetry(LiftEngineAdFormat format)
        {
            yield return new WaitForSeconds(_settings.prewarmRetryIntervalSeconds);

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
