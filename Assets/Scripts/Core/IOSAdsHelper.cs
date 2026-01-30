using UnityEngine;
#if UNITY_IOS
using Unity.Services.LevelPlay;
// Note: You may need to install the "iOS 14 Advertising Support" package 
// from the Unity Package Manager to use ATTrackingStatusBinding
// using Unity.Advertisement.IosSupport;
#endif

namespace Assets.Scripts.Core
{
    public class IOSAdsHelper : MonoBehaviour
    {
        public static void RequestATT()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log("[IOSAdsHelper] Requesting App Tracking Transparency permission...");
            
            /* 
            // If you have the iOS Support package, uncomment this:
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }
            */
            
            // LevelPlay also handles some attribution, but ATT is a system-level prompt.
            // Ensure you have added the 'NSUserTrackingUsageDescription' key to your Info.plist
#else
            Debug.Log("[IOSAdsHelper] ATT Request skipped (Not on iOS device).");
#endif
        }
    }
}
