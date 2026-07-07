using System;

namespace LiftEngine
{
    public sealed class LiftEngineShowAdParams
    {
        public string PlacementName { get; set; }
        public string PlacementId { get; set; }
        public bool SkipPredict { get; set; }
    }

    public sealed class LiftEngineShowAdCallbacks
    {
        public Action OnAdDisplayed;
        public Action OnAdHidden;
        public Action OnAdClicked;
        public Action OnAdRewarded;
        public Action<string> OnAdDisplayFailed;
    }

    public static class LiftEngineSdkCallbacks
    {
        public static event Action<LiftEngineInitializationStatus> OnSdkInitializedEvent;
        public static event Action<LiftEnginePredictEventArgs> OnPredictSuccessEvent;
        public static event Action<LiftEngineOperationError> OnPredictFailedEvent;
        public static event Action<LiftEngineAdInfo> OnAdLoadedEvent;
        public static event Action<LiftEngineAdInfo> OnAdDisplayedEvent;
        public static event Action<LiftEngineAdInfo> OnAdHiddenEvent;
        public static event Action<LiftEngineAdInfo> OnAdRewardedEvent;
        public static event Action<LiftEngineAdInfo> OnAdRevenuePaidEvent;

        internal static void RaiseInitialized(LiftEngineInitializationStatus status) =>
            OnSdkInitializedEvent?.Invoke(status);

        internal static void RaisePredictSuccess(LiftEngineAdFormat format)
        {
            OnPredictSuccessEvent?.Invoke(new LiftEnginePredictEventArgs
            {
                Format = format,
                Succeeded = true
            });
        }

        internal static void RaisePredictFailed(LiftEngineAdFormat format, Api.LiftEngineError error)
        {
            OnPredictFailedEvent?.Invoke(new LiftEngineOperationError
            {
                StatusCode = error?.StatusCode ?? 0,
                Message = error?.Message ?? "Unknown error"
            });
        }

        internal static void RaiseAdLoaded(Mediation.MediationAdInfo info) =>
            OnAdLoadedEvent?.Invoke(LiftEngineAdInfo.FromMediation(info));

        internal static void RaiseAdDisplayed(Mediation.MediationAdInfo info) =>
            OnAdDisplayedEvent?.Invoke(LiftEngineAdInfo.FromMediation(info));

        internal static void RaiseAdHidden(Mediation.MediationAdInfo info) =>
            OnAdHiddenEvent?.Invoke(LiftEngineAdInfo.FromMediation(info));

        internal static void RaiseAdRewarded(Mediation.MediationAdInfo info) =>
            OnAdRewardedEvent?.Invoke(LiftEngineAdInfo.FromMediation(info));

        internal static void RaiseAdRevenue(Mediation.MediationAdInfo info) =>
            OnAdRevenuePaidEvent?.Invoke(LiftEngineAdInfo.FromMediation(info));
    }
}
