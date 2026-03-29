using UnityEngine;
using System;
using System.Collections;

using Unity.Services.LevelPlay;
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
                // We'll need to poll for the status change or use a callback if available.
                // Since this is a static helper, we'll let the caller handle the wait if they need to,
                // or we can provide a coroutine.
            }
            else
            {
                bool authorized = status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
                onComplete?.Invoke(authorized);
            }
#else
            Debug.Log("[IOSAdsHelper] ATT Request skipped (Not on iOS device).");
            onComplete?.Invoke(true); // Default to true on other platforms for ad flow
#endif
        }

        public static IEnumerator PollATTStatus(Action<bool> onComplete)
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Wait for user to make a choice
            while (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                yield return null;
            }
            
            bool isAuthorized = ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
            Debug.Log($"[IOSAdsHelper] ATT Status changed: {isAuthorized}");
            
            // Set consent for LevelPlay based on user's choice
            // Note: ironSource LevelPlay uses SetConsent(true/false) for GDPR/CCPA.
            // Using ATT choice as a proxy for consent is common when not using a separate GDPR popup.
            LevelPlay.SetConsent(isAuthorized);
            
            onComplete?.Invoke(isAuthorized);
#else
            // On other platforms (like Android/Editor), we send true to the callback directly.
            // We should still manually tell LevelPlay/ironSource that we have consent
            // to maximize revenue if we don't have a specific GDPR manager.
            LevelPlay.SetConsent(true);
            Debug.Log("[IOSAdsHelper] Platform is not iOS. Defaulting LevelPlay consent to 'true'.");
            onComplete?.Invoke(true);
            yield break;
#endif
        }
    }
}


