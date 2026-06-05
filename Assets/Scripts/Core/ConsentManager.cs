using UnityEngine;
using System;
using GoogleMobileAds.Ump.Api;

namespace Assets.Scripts.Core
{
    public class ConsentManager : MonoBehaviour
    {
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
                    onComplete?.Invoke();
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

                    onComplete?.Invoke();
                });
            });
        }

        /// <summary>
        /// Applies UMP consent results to AppLovin MAX before SDK initialization.
        /// Required because the built-in AppLovin consent flow is disabled.
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
    }
}
