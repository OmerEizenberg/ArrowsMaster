using System;
using UnityEngine;
#if GMA_PRESENT
using GoogleMobileAds.Ump.Api;
#endif

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Google UMP (User Messaging Platform) consent — same mechanism we shipped on June 2.
    /// Gathers GDPR/CMP consent (shown only where required, e.g. EEA) and persists the IAB TCF string
    /// that mediated networks read.
    ///
    /// NOTE: The Google Mobile Ads Unity SDK (which provides GoogleMobileAds.Ump.Api) was removed on
    /// June 19 ("Upgrade SDKs"). To activate this flow:
    ///   1. Re-import the Google Mobile Ads Unity plugin (includes the User Messaging Platform).
    ///   2. Add the scripting define symbol  GMA_PRESENT  (Player Settings > Scripting Define Symbols,
    ///      for Android + iOS).
    /// Until then RequestConsent is a no-op that immediately completes, so the build is never broken and
    /// MAX init still proceeds.
    /// </summary>
    public class ConsentManager
    {
        public static void RequestConsent(Action onComplete)
        {
#if GMA_PRESENT
            Debug.Log("[ConsentManager] Gathering consent information (Google UMP)...");

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
                        Debug.LogError($"[ConsentManager] Error showing consent form: {showError.Message}");
                    else
                        Debug.Log("[ConsentManager] Consent form shown or not required.");

                    onComplete?.Invoke();
                });
            });
#else
            Debug.LogWarning(
                "[ConsentManager] Google Mobile Ads UMP SDK not present; skipping consent gathering. " +
                "Re-import the GMA plugin and add the GMA_PRESENT define to enable the June-2 UMP flow.");
            onComplete?.Invoke();
#endif
        }
    }
}
