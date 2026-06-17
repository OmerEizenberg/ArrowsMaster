using UnityEngine;
using System;
using GoogleMobileAds.Ump.Api;

namespace Assets.Scripts.Core
{
    public class ConsentManager : MonoBehaviour
    {
        private static bool _consentAnalyticsLogged;

        public static void RequestConsent(Action onComplete)
        {
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
                    FinishConsentFlow(onComplete);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                    {
                        Debug.LogError($"[ConsentManager] Error showing consent form: {showError.Message}");
                    }
                    else
                    {
                        Debug.Log("[ConsentManager] Consent form shown or not required.");
                    }

                    FinishConsentFlow(onComplete);
                });
            });
        }

        /// <summary>
        /// Logs passed_consent_approve or passed_consent_deny once per session, then continues.
        /// Safe to call again after a UMP timeout — duplicate events are suppressed.
        /// </summary>
        public static void LogConsentResultAnalytics()
        {
            if (_consentAnalyticsLogged)
                return;

            _consentAnalyticsLogged = true;

            bool canRequestAds = ConsentInformation.CanRequestAds();
            string eventName = canRequestAds
                ? FirebaseManager.EVENT_PASSED_CONSENT_APPROVE
                : FirebaseManager.EVENT_PASSED_CONSENT_DENY;

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(eventName);

            Debug.Log(
                $"[ConsentManager] Consent analytics: {eventName} " +
                $"(canRequestAds={canRequestAds}, status={ConsentInformation.ConsentStatus})");
        }

        /// <summary>
        /// Applies UMP consent results to AppLovin MAX before SDK initialization.
        /// MAX is always initialized afterward — denied consent uses non-personalized ads when available.
        /// </summary>
        public static void ApplyMaxPrivacyFlags()
        {
            bool canRequestAds = ConsentInformation.CanRequestAds();
            MaxSdk.SetHasUserConsent(canRequestAds);
            // false = user has NOT opted out of sale of personal info (CCPA)
            MaxSdk.SetDoNotSell(false);
            Debug.Log(
                $"[ConsentManager] MAX privacy flags applied: hasUserConsent={canRequestAds}, " +
                $"consentStatus={ConsentInformation.ConsentStatus}");
        }

        private static void FinishConsentFlow(Action onComplete)
        {
            LogConsentResultAnalytics();
            onComplete?.Invoke();
        }
    }
}
