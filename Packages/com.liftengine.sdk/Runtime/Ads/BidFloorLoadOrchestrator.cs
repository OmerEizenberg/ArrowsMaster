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

            if (prediction == null || prediction.multipliers == null || prediction.multipliers.Length == 0)
            {
                LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                    $"{format} — no predict multipliers, entering bid-0 fill loop.");
                LiftEngineSignalBus.Publish(new BidFloorPredictionFailedSignal(format));
                yield return BidZeroUntilFill(format, adUnitId, onComplete);
                yield break;
            }

            for (var i = 0; i < prediction.multipliers.Length; i++)
            {
                var bidFloor = prediction.prediction * prediction.multipliers[i];
                var floorStr = PredictDataNormalizers.FormatBidFloor(bidFloor);
                LiftEngineLogger.LogAttempt(i,
                    $"{format} — loading with multiplier[{i}]={prediction.multipliers[i]}, " +
                    $"prediction={prediction.prediction}, jC7Fp={floorStr}");

                _mediation.SetBidFloorExtra(format, adUnitId, floorStr);
                _mediation.RequestLoad(format, adUnitId);

                var success = false;
                yield return WaitForLoad(format, adUnitId, i, requireRevenue: true, result => success = result);

                if (success)
                {
                    LiftEngineLogger.LogAttempt(i, $"{format} — fill success with revenue.");
                    onComplete?.Invoke(true);
                    yield break;
                }

                LiftEngineLogger.LogAttemptWarning(i,
                    $"{format} — no fill with revenue, trying next multiplier.");
                _mediation.DestroyAd(format, adUnitId);
            }

            LiftEngineLogger.LogAttemptWarning(BidZeroAttempt,
                $"{format} — multiplier waterfall exhausted, entering bid-0 fill loop.");
            yield return BidZeroUntilFill(format, adUnitId, onComplete);
        }

        private IEnumerator BidZeroUntilFill(LiftEngineAdFormat format, string adUnitId, Action<bool> onComplete)
        {
            var bidZeroRound = 0;
            while (true)
            {
                LiftEngineLogger.LogAttempt(BidZeroAttempt,
                    $"{format} — bid-0 load round {bidZeroRound}, jC7Fp=0");

                _mediation.SetBidFloorExtra(format, adUnitId, "0");
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
                    $"{format} — bid-0 round {bidZeroRound} failed, retrying in {_settings.readinessCheckIntervalSeconds}s.");
                bidZeroRound++;
                yield return new WaitForSeconds(_settings.readinessCheckIntervalSeconds);
            }
        }

        private IEnumerator WaitForLoad(LiftEngineAdFormat format, string adUnitId, int attempt,
            bool requireRevenue, Action<bool> callback)
        {
            var elapsed = 0f;
            var timeout = _settings.loadAttemptTimeoutSeconds;

            while (elapsed < timeout)
            {
                if (requireRevenue)
                {
                    if (_mediation.HasLoadedWithRevenue(format, adUnitId))
                    {
                        callback?.Invoke(true);
                        yield break;
                    }
                }
                else if (_mediation.IsReady(format, adUnitId))
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
    }
}
