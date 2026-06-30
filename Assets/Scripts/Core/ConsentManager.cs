using System;
using UnityEngine;
#if GMA_PRESENT
using GoogleMobileAds.Ump.Api;
#endif

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Google UMP consent — gathers GDPR/CMP where required before MAX init.
    /// AppLovin built-in consent flow (AppLovinInternalSettings.json) handles MAX-side GDPR UI.
    /// </summary>
    public static class ConsentManager
    {
        private static bool _consentAnalyticsLogged;

        public static void RequestConsent(Action onComplete)
        {
#if GMA_PRESENT
            Debug.Log("[ConsentManager] Gathering consent information...");

            var requestParameters = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = false
            };

            ConsentInformation.Update(requestParameters, (FormError updateError) =>
            {
                if (updateError != null)
                {
                    Debug.LogError($"[ConsentManager] Consent info update failed: {updateError.Message}");
                    LogConsentResultAnalytics();
                    onComplete?.Invoke();
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                        Debug.LogError($"[ConsentManager] Error showing consent form: {showError.Message}");
                    else
                        Debug.Log("[ConsentManager] Consent form shown or not required.");

                    LogConsentResultAnalytics();
                    onComplete?.Invoke();
                });
            });
#else
            Debug.Log("[ConsentManager] UMP C# layer unavailable; proceeding without UMP gather.");
            onComplete?.Invoke();
#endif
        }

        public static void LogConsentResultAnalytics()
        {
            if (_consentAnalyticsLogged)
                return;

            _consentAnalyticsLogged = true;

#if GMA_PRESENT
            bool canRequestAds = ConsentInformation.CanRequestAds();
            string eventName = canRequestAds
                ? FirebaseManager.EVENT_PASSED_CONSENT_APPROVE
                : FirebaseManager.EVENT_PASSED_CONSENT_DENY;

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(eventName);

            Debug.Log(
                $"[ConsentManager] Consent analytics: {eventName} " +
                $"(canRequestAds={canRequestAds}, status={ConsentInformation.ConsentStatus})");
#else
            Debug.Log("[ConsentManager] Consent analytics skipped (UMP unavailable).");
#endif
        }
    }
}
