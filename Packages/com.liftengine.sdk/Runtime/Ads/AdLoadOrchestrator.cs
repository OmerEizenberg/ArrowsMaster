using System;
using System.Collections;
using LiftEngine.Api;
using LiftEngine.Context;
using LiftEngine.Mediation;
using UnityEngine;

namespace LiftEngine.Ads
{
    internal sealed class AdLoadOrchestrator
    {
        private const int FallbackAttemptIndex = -1;

        private readonly LiftEngineSettings _settings;
        private readonly IMediationAdapter _mediation;
        private readonly MonoBehaviour _host;

        public AdLoadOrchestrator(LiftEngineSettings settings, IMediationAdapter mediation, MonoBehaviour host)
        {
            _settings = settings;
            _mediation = mediation;
            _host = host;
        }

        public void TryLoadWithOptimization(LiftEngineAdFormat format, LiftEngineOptimizationResult optimization,
            string maxPlacement, Action<bool> onComplete)
        {
            _host.StartCoroutine(LoadRoutine(format, optimization, maxPlacement, onComplete));
        }

        private IEnumerator LoadRoutine(LiftEngineAdFormat format, LiftEngineOptimizationResult optimization,
            string maxPlacement, Action<bool> onComplete)
        {
            var adUnitId = _settings.GetAdUnitId(format);
            if (string.IsNullOrEmpty(adUnitId))
            {
                LiftEngineLogger.LogError($"Missing ad unit id for {format}");
                onComplete?.Invoke(false);
                yield break;
            }

            var payloadKey = optimization?.param;

            if (optimization == null || optimization.multipliers == null || optimization.multipliers.Length == 0)
            {
                LiftEngineLogger.LogAttemptWarning(FallbackAttemptIndex,
                    $"{format} — no optimization multipliers available → using fallback load");
                LiftEngineSignalBus.Publish(new OptimizationUnavailableSignal(format));
                yield return FallbackLoadUntilFill(format, adUnitId, payloadKey, maxPlacement, onComplete);
                yield break;
            }

            var requireRevenue = RequiresRevenueForMultiplierPhase(format);

            for (var i = 0; i < optimization.multipliers.Length; i++)
            {
                var scaledValue = optimization.prediction * optimization.multipliers[i];
                var payloadValue = PredictDataNormalizers.FormatPayloadValue(scaledValue);
                LiftEngineLogger.LogAttempt(i,
                    $"{format} — load attempt {i} with multiplier[{i}]={optimization.multipliers[i]}, " +
                    $"value={payloadValue}, requireRevenue={requireRevenue}");

                _mediation.AddPayload(format, adUnitId, payloadKey, payloadValue);
                _mediation.RequestLoad(format, adUnitId, maxPlacement);

                var success = false;
                yield return WaitForLoad(format, adUnitId, i, requireRevenue, result => success = result);

                if (success)
                {
                    LiftEngineLogger.LogAttempt(i, $"{format} — fill success at attempt {i}.");
                    onComplete?.Invoke(true);
                    yield break;
                }

                LiftEngineLogger.LogAttemptWarning(i,
                    $"{format} — no fill at attempt {i}, trying next.");
                _mediation.DestroyAd(format, adUnitId);
            }

            LiftEngineLogger.LogAttemptWarning(FallbackAttemptIndex,
                $"{format} — optimization attempts exhausted → fallback load");
            yield return FallbackLoadUntilFill(format, adUnitId, payloadKey, maxPlacement, onComplete);
        }

        private bool RequiresRevenueForMultiplierPhase(LiftEngineAdFormat format)
        {
            if (format == LiftEngineAdFormat.Banner)
                return false;

            return true;
        }

        private IEnumerator FallbackLoadUntilFill(LiftEngineAdFormat format, string adUnitId, string payloadKey,
            string maxPlacement, Action<bool> onComplete)
        {
            var maxRounds = Mathf.Max(1, LiftEngineRuntimeTuning.MaxFallbackLoadRounds);
            for (var round = 0; round < maxRounds; round++)
            {
                LiftEngineLogger.LogAttempt(FallbackAttemptIndex,
                    $"{format} — fallback load round {round + 1}/{maxRounds}");

                _mediation.AddPayload(format, adUnitId, payloadKey, "0");
                _mediation.RequestLoad(format, adUnitId, maxPlacement);

                var success = false;
                yield return WaitForLoad(format, adUnitId, FallbackAttemptIndex, requireRevenue: false,
                    result => success = result);

                if (success)
                {
                    LiftEngineLogger.LogAttempt(FallbackAttemptIndex, $"{format} — fill success on fallback load.");
                    onComplete?.Invoke(true);
                    yield break;
                }

                LiftEngineLogger.LogAttemptWarning(FallbackAttemptIndex,
                    $"{format} — fallback round {round + 1} failed, retrying in {LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds}s.");
                yield return new WaitForSeconds(LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds);
            }

            LiftEngineLogger.LogAttemptWarning(FallbackAttemptIndex,
                $"{format} — fallback load exhausted after {maxRounds} round(s); no fill.");
            onComplete?.Invoke(false);
        }

        private IEnumerator WaitForLoad(LiftEngineAdFormat format, string adUnitId, int attempt,
            bool requireRevenue, Action<bool> callback)
        {
            var elapsed = 0f;
            var timeout = LiftEngineRuntimeTuning.LoadAttemptTimeoutSeconds;

            while (elapsed < timeout)
            {
                if (IsLoadSuccessful(format, adUnitId, requireRevenue))
                {
                    callback?.Invoke(true);
                    yield break;
                }

                yield return new WaitForSeconds(LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds);
                elapsed += LiftEngineRuntimeTuning.ReadinessCheckIntervalSeconds;
            }

            LiftEngineLogger.LogAttemptWarning(attempt,
                $"{format} — load timed out after {timeout}s (requireRevenue={requireRevenue}).");
            callback?.Invoke(false);
        }

        private bool IsLoadSuccessful(LiftEngineAdFormat format, string adUnitId, bool requireRevenue)
        {
            if (!requireRevenue)
                return _mediation.IsReady(format, adUnitId);

            if (_mediation.HasLoadedWithRevenue(format, adUnitId))
                return true;

            if (LiftEngineRuntimeTuning.TreatEditorLoadAsFilled &&
                Application.isEditor &&
                _mediation.IsReady(format, adUnitId))
            {
                return true;
            }

            return false;
        }
    }
}
