using UnityEngine;
using System;
using System.Collections;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace Assets.Scripts.Core
{
    public class IOSAdsHelper
    {
        public static void RequestATT(Action<bool> onComplete = null)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log("[IOSAdsHelper] Requesting App Tracking Transparency permission...");
            
            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
            }
            else
            {
                bool authorized = status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
                onComplete?.Invoke(authorized);
            }
#else
            Debug.Log("[IOSAdsHelper] ATT Request skipped (Not on iOS device).");
            onComplete?.Invoke(true);
#endif
        }

        public static IEnumerator PollATTStatus(Action<bool> onComplete)
        {
#if UNITY_IOS && !UNITY_EDITOR
            while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return null;
            }

            bool isAuthorized = ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                                ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
            Debug.Log($"[IOSAdsHelper] ATT Status changed: {isAuthorized}");
            onComplete?.Invoke(isAuthorized);
#else
            Debug.Log("[IOSAdsHelper] Platform is not iOS. ATT does not apply.");
            onComplete?.Invoke(true);
            yield break;
#endif
        }

        /// <summary>
        /// Blocks until the user responds to the ATT dialog (or status is already decided).
        /// Must complete before Singular or ads initialize on iOS.
        /// </summary>
        public static IEnumerator ResolveAttBlocking()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (IOSAttributionBootstrap.IsAttResolved)
                yield break;

            Debug.Log("[IOSAdsHelper] Waiting for ATT decision before SDK init...");

            var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                ATTrackingStatusBinding.RequestAuthorizationTracking();

            while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                   ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return null;
            }

            bool isAuthorized = ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                                ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
            IOSAttributionBootstrap.SetAttResolved(isAuthorized);
            Debug.Log($"[IOSAdsHelper] ATT decision received: authorized={isAuthorized}");
#else
            yield break;
#endif
        }
    }
}
