using System;
using System.Collections.Generic;
using LiftEngine.Context;

namespace LiftEngine.Mediation
{
    internal sealed class MediationAdapterFactory
    {
        public static IMediationAdapter Create(LiftEngineMediationPlatform platform)
        {
            return platform switch
            {
                LiftEngineMediationPlatform.AppLovinMax => new MaxMediationAdapter(),
                LiftEngineMediationPlatform.LevelPlay => new LevelPlayMediationAdapter(),
                _ => new NullMediationAdapter()
            };
        }
    }

    internal class NullMediationAdapter : IMediationAdapter
    {
        public virtual LiftEngineMediationPlatform Platform => LiftEngineMediationPlatform.None;
        public bool IsInitialized => false;

        public event Action<MediationAdInfo> AdLoaded;
        public event Action<MediationAdError> AdLoadFailed;
        public event Action<MediationAdInfo> AdDisplayed;
        public event Action<MediationAdError> AdDisplayFailed;
        public event Action<MediationAdInfo> AdHidden;
        public event Action<MediationAdInfo> AdClicked;
        public event Action<MediationAdInfo> AdRevenuePaid;
        public event Action<MediationAdInfo> AdRewarded;

        public virtual void Initialize(LiftEngineSettings settings, Action<bool> onComplete) =>
            onComplete?.Invoke(false);

        public void SetBidFloorExtra(LiftEngineAdFormat format, string adUnitId, string floorValue) { }
        public void ClearBidFloorExtra(LiftEngineAdFormat format, string adUnitId) { }
        public void RequestLoad(LiftEngineAdFormat format, string adUnitId) { }
        public bool IsReady(LiftEngineAdFormat format, string adUnitId) => false;
        public bool HasLoadedWithRevenue(LiftEngineAdFormat format, string adUnitId) => false;
        public void Show(LiftEngineAdFormat format, string adUnitId) { }
        public void HideBanner(string adUnitId) { }
        public void ResetLoadState(LiftEngineAdFormat format, string adUnitId) { }
        public void DestroyAd(LiftEngineAdFormat format, string adUnitId) { }
    }

    internal sealed class LevelPlayMediationAdapter : NullMediationAdapter
    {
        public override LiftEngineMediationPlatform Platform => LiftEngineMediationPlatform.LevelPlay;

        public override void Initialize(LiftEngineSettings settings, Action<bool> onComplete)
        {
            LiftEngineLogger.LogWarning("LevelPlay adapter is not implemented yet.");
            onComplete?.Invoke(false);
        }
    }
}
