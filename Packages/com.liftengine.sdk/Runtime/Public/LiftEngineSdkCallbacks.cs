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
        public static event Action<Api.LiftEnginePredictResult> OnPredictSuccessEvent;
        public static event Action<Api.LiftEngineError> OnPredictFailedEvent;
        public static event Action<LiftEngineAdInfo> OnAdLoadedEvent;
        public static event Action<LiftEngineAdInfo> OnAdDisplayedEvent;
        public static event Action<LiftEngineAdInfo> OnAdHiddenEvent;
        public static event Action<LiftEngineAdInfo> OnAdRewardedEvent;
        public static event Action<LiftEngineAdInfo> OnAdRevenuePaidEvent;

        internal static void RaiseInitialized(LiftEngineInitializationStatus status) =>
            OnSdkInitializedEvent?.Invoke(status);

        internal static void RaisePredictSuccess(Api.LiftEnginePredictResult result) =>
            OnPredictSuccessEvent?.Invoke(result);

        internal static void RaisePredictFailed(Api.LiftEngineError error) =>
            OnPredictFailedEvent?.Invoke(error);

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
