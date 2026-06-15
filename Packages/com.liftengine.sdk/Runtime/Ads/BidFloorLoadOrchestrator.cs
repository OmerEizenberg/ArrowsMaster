using System;
using System.Collections;
using LiftEngine.Api;
using LiftEngine.Context;
using LiftEngine.Mediation;
using UnityEngine;

namespace LiftEngine.Ads
{
    internal sealed class BidFloorLoadOrchestrator
    {
        private const int BidZeroAttempt = -1;

        private readonly LiftEngineSettings _settings;
        private readonly IMediationAdapter _mediation;
        private readonly MonoBehaviour _host;

        public BidFloorLoadOrchestrator(LiftEngineSettings settings, IMediationAdapter mediation, MonoBehaviour host)
        {
            _settings = settings;
            _mediation = mediation;
            _host = host;
        }

        public void TryLoadWithPrediction(LiftEngineAdFormat format, LiftEnginePredictResult prediction,
            Action<bool> onComplete)
        {
            _host.StartCoroutine(LoadRoutine(format, prediction, onComplete));
        }

        private IEnumerator LoadRoutine(LiftEngineAdFormat format, LiftEnginePredictResult prediction,
            Action<bool> onComplete)
        {
            var adUnitId = _settings.GetAdUnitId(format);
            if (string.IsNullOrEmpty(adUnitId))
            {
                LiftEngineLogger.LogError($"Missing ad unit id for {format}");
                onComplete?.Invoke(false);
                yield break;
            }

            var payloadKey = prediction?.param;

            if (prediction == null || prediction.multipliers == null || prediction.multipliers.Length == 0)
            {
                LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                    $"{format} — no predict multipliers available → skipping [Attempt 0..N] → [Attempt -1]");
                LiftEngineSignalBus.Publish(new BidFloorPredictionFailedSignal(format));
                yield return BidZeroUntilFill(format, adUnitId, payloadKey, onComplete);
                yield break;
            }

            var requireRevenue = RequiresRevenueForMultiplierPhase(format);

            for (var i = 0; i < prediction.multipliers.Length; i++)
            {
                var scaledValue = prediction.prediction * prediction.multipliers[i];
                var payloadValue = PredictDataNormalizers.FormatPayloadValue(scaledValue);
                LiftEngineLogger.LogAttempt(i,
                    $"{format} — loading with multiplier[{i}]={prediction.multipliers[i]}, " +
                    $"prediction={prediction.prediction}, payload={payloadValue}, requireRevenue={requireRevenue}");

                _mediation.AddPayload(format, adUnitId, payloadKey, payloadValue);
                _mediation.RequestLoad(format, adUnitId);

                var success = false;
                yield return WaitForLoad(format, adUnitId, i, requireRevenue, result => success = result);

                if (success)
                {
                    LiftEngineLogger.LogAttempt(i, $"{format} — fill success at multiplier[{i}].");
                    onComplete?.Invoke(true);
                    yield break;
                }

                LiftEngineLogger.LogAttemptWarning(i,
                    $"{format} — no fill at multiplier[{i}], trying next.");
                _mediation.DestroyAd(format, adUnitId);
            }

            LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                $"{format} — all multipliers exhausted → [Attempt -1]");
            yield return BidZeroUntilFill(format, adUnitId, payloadKey, onComplete);
        }

        private bool RequiresRevenueForMultiplierPhase(LiftEngineAdFormat format)
        {
            // Banners rarely expose ILRD at load time; full-screen formats do on device.
            if (format == LiftEngineAdFormat.Banner)
                return false;

            return true;
        }

        private IEnumerator BidZeroUntilFill(LiftEngineAdFormat format, string adUnitId, string payloadKey,
            Action<bool> onComplete)
        {
            var maxRounds = Mathf.Max(1, _settings.maxBidZeroRounds);
            for (var bidZeroRound = 0; bidZeroRound < maxRounds; bidZeroRound++)
            {
                LiftEngineLogger.LogAttempt(BidZeroAttempt,
                    $"{format} — fallback load round {bidZeroRound + 1}/{maxRounds}, payload=0");

                _mediation.AddPayload(format, adUnitId, payloadKey, "0");
                _mediation.RequestLoad(format, adUnitId);

                var success = false;
                yield return WaitForLoad(format, adUnitId, BidZeroAttempt, requireRevenue: false, result => success = result);

                if (success)
                {
                    LiftEngineLogger.LogAttempt(BidZeroAttempt, $"{format} — fill success at bid 0.");
                    onComplete?.Invoke(true);
                    yield break;
                }

                LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                    $"{format} — bid-0 round {bidZeroRound + 1} failed, retrying in {_settings.readinessCheckIntervalSeconds}s.");
                yield return new WaitForSeconds(_settings.readinessCheckIntervalSeconds);
            }

            LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                $"{format} — bid-0 exhausted after {maxRounds} round(s); no fill.");
            onComplete?.Invoke(false);
        }

        private IEnumerator WaitForLoad(LiftEngineAdFormat format, string adUnitId, int attempt,
            bool requireRevenue, Action<bool> callback)
        {
            var elapsed = 0f;
            var timeout = _settings.loadAttemptTimeoutSeconds;

            while (elapsed < timeout)
            {
                if (IsLoadSuccessful(format, adUnitId, requireRevenue))
                {
                    callback?.Invoke(true);
                    yield break;
                }

                yield return new WaitForSeconds(_settings.readinessCheckIntervalSeconds);
                elapsed += _settings.readinessCheckIntervalSeconds;
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

            if (_settings.treatEditorLoadAsFilledForMultiplierPhase &&
                Application.isEditor &&
                _mediation.IsReady(format, adUnitId))
            {
                return true;
            }

            return false;
        }
    }
}
