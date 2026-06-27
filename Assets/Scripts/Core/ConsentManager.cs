using System;
using UnityEngine;
#if GMA_PRESENT
using GoogleMobileAds.Ump.Api;
#endif

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Google UMP consent — gathers GDPR/CMP where required and applies privacy flags to MAX
    /// before InitializeSdk(). AppLovin's built-in consent flow stays disabled to avoid duplicate
    /// dialogs (Jun-19 regression that blocked Android tier-1 new-user registration).
    ///
    /// Consent for MAX is persisted after the first successful UMP completion or safeguard fallback
    /// so the next cold start skips UMP gathering and initializes MAX immediately.
    /// </summary>
    public static class ConsentManager
    {
        private const string KeyMaxConsentResolved = "MaxConsentResolvedForInit";
        private const string KeyMaxHasUserConsent = "MaxHasUserConsent";

        private static bool _consentAnalyticsLogged;
        private static bool _consentFlowCompleted;

        public static bool IsConsentFlowCompleted => _consentFlowCompleted;

        /// <summary>True when a prior session stored a MAX consent decision (UMP or safeguard).</summary>
        public static bool HasPersistedConsentForMax =>
            PlayerPrefs.GetInt(KeyMaxConsentResolved, 0) == 1;

        /// <summary>Skip UMP gathering when we already have a stored decision for MAX init.</summary>
        public static bool ShouldSkipUmpGathering => HasPersistedConsentForMax;

        public static void RequestConsent(Action onComplete)
        {
            if (ShouldSkipUmpGathering)
            {
                Debug.Log(
                    $"[ConsentManager] Skipping UMP gather — consent already resolved for MAX " +
                    $"(hasUserConsent={ReadPersistedHasUserConsent()}).");
                _consentFlowCompleted = true;
                onComplete?.Invoke();
                return;
            }

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
                    FinishConsentFlow(onComplete);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                        Debug.LogError($"[ConsentManager] Error showing consent form: {showError.Message}");
                    else
                        Debug.Log("[ConsentManager] Consent form shown or not required.");

                    FinishConsentFlow(onComplete);
                });
            });
#else
            Debug.Log("[ConsentManager] UMP C# layer unavailable; proceeding with default MAX privacy flags.");
            PersistConsentForMax(true);
            onComplete?.Invoke();
#endif
        }

        /// <summary>
        /// Safeguard / timeout path: UMP never completed and user will not see a form — store a
        /// non-personalized decision and unblock MAX init. Next session skips UMP and inits immediately.
        /// </summary>
        public static void ApplyFallbackConsentForMaxInit(string reason)
        {
            Debug.LogWarning(
                $"[ConsentManager] MAX consent fallback ({reason}): persisting hasUserConsent=false.");
            PersistConsentForMax(false);
        }

        /// <summary>
        /// UMP consent result + disable AppLovin duplicate GDPR UI. Call on main thread immediately
        /// before MaxSdk.InitializeSdk().
        /// </summary>
        public static void ConfigureMaxPrivacyBeforeInit()
        {
            ApplyMaxPrivacyFlags();
            MaxSdk.SetExtraParameter("consent_flow_enabled", "false");
        }

        /// <summary>
        /// Applies consent to AppLovin MAX. Uses persisted value when available; otherwise reads UMP.
        /// </summary>
        public static void ApplyMaxPrivacyFlags()
        {
            bool hasUserConsent = ResolveHasUserConsentForMax();
            MaxSdk.SetHasUserConsent(hasUserConsent);
            MaxSdk.SetDoNotSell(false);
            Debug.Log($"[ConsentManager] MAX privacy flags applied: hasUserConsent={hasUserConsent}");
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

        private static void FinishConsentFlow(Action onComplete)
        {
            _consentFlowCompleted = true;
            PersistConsentFromUmpOrDefault();
            LogConsentResultAnalytics();
            onComplete?.Invoke();
        }

        private static void PersistConsentFromUmpOrDefault()
        {
#if GMA_PRESENT
            PersistConsentForMax(ConsentInformation.CanRequestAds());
#else
            PersistConsentForMax(true);
#endif
        }

        private static void PersistConsentForMax(bool hasUserConsent)
        {
            PlayerPrefs.SetInt(KeyMaxConsentResolved, 1);
            PlayerPrefs.SetInt(KeyMaxHasUserConsent, hasUserConsent ? 1 : 0);
            PlayerPrefs.Save();
            _consentFlowCompleted = true;
            Debug.Log($"[ConsentManager] Persisted MAX consent: hasUserConsent={hasUserConsent}");
        }

        private static bool ReadPersistedHasUserConsent() =>
            PlayerPrefs.GetInt(KeyMaxHasUserConsent, 0) == 1;

        private static bool ResolveHasUserConsentForMax()
        {
            if (HasPersistedConsentForMax)
                return ReadPersistedHasUserConsent();

#if GMA_PRESENT
            return ConsentInformation.CanRequestAds();
#else
            return true;
#endif
        }
    }
}
