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
            
            // Set privacy flags for LevelPlay based on user's choice
            // SDK 8.x/9.x uses LevelPlayPrivacySettings for centralized compliance
            var consents = new System.Collections.Generic.Dictionary<string, bool> 
            { 
                { "ironSource", isAuthorized },
                { "Facebook", isAuthorized },
                { "AdMob", isAuthorized },
                { "UnityAds", isAuthorized },
                { "AppLovin", isAuthorized },
                { "Pangle", isAuthorized },
                { "Vungle", isAuthorized }
            };
            LevelPlayPrivacySettings.SetGDPRConsents(consents);
            
            // Explicitly set CCPA and COPPA flags to maximize fill rate
            // SetCCPA(true) means the user has OPTED OUT. So we pass 'false' to indicate they are NOT opted out.
            LevelPlayPrivacySettings.SetCCPA(false); 
            LevelPlayPrivacySettings.SetCOPPA(false);   // App is not child-directed
            
            onComplete?.Invoke(isAuthorized);
#else
            // On other platforms (like Android/Editor), we send true to the callback directly.
            var consents = new System.Collections.Generic.Dictionary<string, bool> 
            { 
                { "ironSource", true },
                { "Facebook", true },
                { "AdMob", true },
                { "UnityAds", true },
                { "AppLovin", true },
                { "Pangle", true },
                { "Vungle", true }
            };
            LevelPlayPrivacySettings.SetGDPRConsents(consents);
            LevelPlayPrivacySettings.SetCCPA(false);
            LevelPlayPrivacySettings.SetCOPPA(false);

            Debug.Log("[IOSAdsHelper] Platform is not iOS. Defaulting LevelPlay privacy flags to 'true/false'.");
            onComplete?.Invoke(true);
            yield break;
#endif
        }
    }
}


