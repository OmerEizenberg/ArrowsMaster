using System;

namespace LiftEngine.Mediation
{
    internal sealed class MediationAdInfo
    {
        public LiftEngineAdFormat Format;
        public string AdUnitId;
        public string NetworkName;
        public double Revenue;
        public string AdFormat;
    }

    internal sealed class MediationAdError
    {
        public LiftEngineAdFormat Format;
        public string AdUnitId;
        public int Code;
        public string Message;
    }

    internal interface IMediationAdapter
    {
        LiftEngineMediationPlatform Platform { get; }
        bool IsInitialized { get; }

        void Initialize(LiftEngineSettings settings, Action<bool> onComplete);
        void AddPayload(LiftEngineAdFormat format, string adUnitId, string payloadKey, string payloadValue);
        void ClearPayload(LiftEngineAdFormat format, string adUnitId, string payloadKey);
        void RequestLoad(LiftEngineAdFormat format, string adUnitId, string maxPlacement = null);
        bool IsReady(LiftEngineAdFormat format, string adUnitId);
        bool HasLoadedWithRevenue(LiftEngineAdFormat format, string adUnitId);
        void Show(LiftEngineAdFormat format, string adUnitId, string maxPlacement = null);
        void HideBanner(string adUnitId);
        void ResetLoadState(LiftEngineAdFormat format, string adUnitId);
        void DestroyAd(LiftEngineAdFormat format, string adUnitId);

        event Action<MediationAdInfo> AdLoaded;
        event Action<MediationAdError> AdLoadFailed;
        event Action<MediationAdInfo> AdDisplayed;
        event Action<MediationAdError> AdDisplayFailed;
        event Action<MediationAdInfo> AdHidden;
        event Action<MediationAdInfo> AdClicked;
        event Action<MediationAdInfo> AdRevenuePaid;
        event Action<MediationAdInfo> AdRewarded;
    }
}
