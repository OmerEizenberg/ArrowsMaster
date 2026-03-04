using UnityEngine;

#if UNITY_IOS
using Unity.Services.LevelPlay;
using Unity.Advertisement.IosSupport;
#endif

namespace Assets.Scripts.Core
{
    public class IOSAdsHelper
    {
        public static void RequestATT()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log("[IOSAdsHelper] Requesting App Tracking Transparency permission...");
            
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }
#else
            Debug.Log("[IOSAdsHelper] ATT Request skipped (Not on iOS device).");
#endif
        }
    }
}
