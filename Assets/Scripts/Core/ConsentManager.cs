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
    }
}
