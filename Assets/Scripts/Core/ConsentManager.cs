using System;
using UnityEngine;
#if GMA_PRESENT
using GoogleMobileAds.Ump.Api;
#endif

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Google UMP consent — gathers GDPR/CMP where required, without taxing users who
    /// don't need it. Design goals: maximize MAX SDK init coverage (AppLovin DAU) and
    /// revenue, while honoring consent strictly where legally required.
    ///
    /// - Only a SUCCESSFUL UMP resolution is ever persisted. An error or timeout
    ///   (e.g. offline install day) is never stored as a decision, so the next session
    ///   re-evaluates instead of being poisoned forever (1.1.27–1.1.33 bug).
    /// - Exposes a State machine so AdsManager can initialize MAX the moment we know
    ///   consent is not required / already resolved / unobtainable, and only waits when
    ///   a GDPR user actually has the consent form in front of them.
    /// - MaxSdk.SetHasUserConsent(true) is applied only when consent is NOT required
    ///   (non-GDPR user) — this also heals installs where the legacy fallback wrote a
    ///   permanent hasUserConsent=false into the AppLovin SDK's own storage.
    /// - GDPR users are governed by the IAB TCF string that UMP writes; the AppLovin SDK
    ///   reads it automatically. We never call SetHasUserConsent(false).
    /// </summary>
    public static class ConsentManager
    {
        public enum ConsentGatherState
        {
            /// <summary>UMP status roundtrip still in flight (or not started).</summary>
            Pending = 0,
            /// <summary>User is outside GDPR scope — no form needed, safe to init/serve.</summary>
            NotRequired = 1,
            /// <summary>GDPR user, consent form is being loaded/shown — the only state worth waiting for.</summary>
            FormRequired = 2,
            /// <summary>UMP flow finished successfully (form completed or not needed).</summary>
            Completed = 3,
            /// <summary>UMP failed this session (offline etc.) — consent unobtainable now; retry next session.</summary>
            Failed = 4
        }

        // Legacy keys from 1.1.27–1.1.33. A stored "false" could mean nothing more than a
        // failed first UMP request, so the stored value is untrustworthy.
        private const string LegacyKeyResolved = "MaxConsentResolvedForInit";
        private const string LegacyKeyHasUserConsent = "MaxHasUserConsent";

        private const string KeyUmpResolved = "UmpConsentResolved_v2";
        private const string KeyUmpConsentNotRequired = "UmpConsentNotRequired_v2";

        private static bool _consentAnalyticsLogged;
        private static bool _umpFlowStartedThisSession;

        public static ConsentGatherState State { get; private set; } = ConsentGatherState.Pending;

        /// <summary>True when a previous session completed the UMP flow successfully.</summary>
        public static bool HasDefinitiveConsentResolution =>
            PlayerPrefs.GetInt(KeyUmpResolved, 0) == 1;

        private static bool PersistedConsentNotRequired =>
            PlayerPrefs.GetInt(KeyUmpConsentNotRequired, 0) == 1;

        /// <summary>
        /// Re-runs UMP when the prior attempt failed this session (offline, DNS, etc.).
        /// Safe to call from resume/health-check; does nothing if consent is already resolved.
        /// </summary>
        public static void RetryIfFailed(Action onComplete = null)
        {
#if GMA_PRESENT
            if (State != ConsentGatherState.Failed)
                return;

            Debug.Log("[ConsentManager] Retrying UMP consent gather after transient failure.");
            _consentAnalyticsLogged = false;
            _umpFlowStartedThisSession = false;
            State = ConsentGatherState.Pending;
            RunUmpFlow(onComplete);
#else
            onComplete?.Invoke();
#endif
        }

        public static void RequestConsent(Action onComplete)
        {
            DiscardLegacyPoisonedConsent();

#if GMA_PRESENT
            if (HasDefinitiveConsentResolution)
            {
                // Already resolved in a prior session — never block MAX init on a UMP
                // network roundtrip again. Still refresh in the background so the stored
                // status stays current (and a GDPR form can appear if it becomes required).
                State = ConsentGatherState.Completed;
                Debug.Log(
                    "[ConsentManager] Consent already resolved " +
                    $"(consentNotRequired={PersistedConsentNotRequired}); refreshing UMP in background.");
                RunUmpFlow(onComplete: null);
                onComplete?.Invoke();
                return;
            }

            RunUmpFlow(onComplete);
#else
            Debug.Log("[ConsentManager] UMP C# layer unavailable; proceeding without UMP gather.");
            State = ConsentGatherState.Completed;
            onComplete?.Invoke();
#endif
        }

        /// <summary>
        /// Applies the MAX privacy flags we can assert safely. Call on the main thread
        /// immediately before MaxSdk.InitializeSdk().
        /// </summary>
        public static void ApplyConsentToMax()
        {
            // CCPA: false = user has NOT opted out of sale of personal info.
            MaxSdk.SetDoNotSell(false);

            if (IsConsentNotRequired())
            {
                // Explicit true overwrites any hasUserConsent=false the legacy fallback wrote
                // into the AppLovin SDK's own storage for users who never needed consent.
                MaxSdk.SetHasUserConsent(true);
                Debug.Log("[ConsentManager] Consent not required for this user: MAX hasUserConsent=true applied.");
            }
            // GDPR users: governed by the UMP-written TCF string; never set false explicitly.
        }

        private static bool IsConsentNotRequired()
        {
            if (PersistedConsentNotRequired)
                return true;

#if GMA_PRESENT
            try
            {
                return ConsentInformation.ConsentStatus == ConsentStatus.NotRequired;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsentManager] Could not read UMP consent status: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if GMA_PRESENT
        private static void RunUmpFlow(Action onComplete)
        {
            if (_umpFlowStartedThisSession && onComplete == null)
                return;
            _umpFlowStartedThisSession = true;

            Debug.Log("[ConsentManager] Gathering consent information (Google UMP)...");

            var requestParameters = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = false
            };

            ConsentInformation.Update(requestParameters, (FormError updateError) =>
            {
                if (updateError != null)
                {
                    // Transient failure (offline, DNS, etc.) — deliberately NOT persisted,
                    // so the next session retries instead of being poisoned forever.
                    Debug.LogError($"[ConsentManager] Consent info update failed: {updateError.Message}");
                    AdvanceState(ConsentGatherState.Failed);
                    LogConsentResultAnalytics();
                    onComplete?.Invoke();
                    return;
                }

                // Status is known the moment Update returns: unblock init immediately for
                // users outside GDPR scope; only GDPR users with a pending form keep waiting.
                bool formRequired =
                    ConsentInformation.ConsentStatus == ConsentStatus.Required;
                AdvanceState(formRequired
                    ? ConsentGatherState.FormRequired
                    : ConsentGatherState.NotRequired);

                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                    {
                        // Form failed to show — the user's choice is unknown; do not persist.
                        Debug.LogError($"[ConsentManager] Error showing consent form: {showError.Message}");
                        AdvanceState(ConsentGatherState.Failed);
                    }
                    else
                    {
                        Debug.Log("[ConsentManager] Consent form shown or not required.");
                        PersistDefinitiveResolution();
                        AdvanceState(ConsentGatherState.Completed);
                    }

                    LogConsentResultAnalytics();
                    onComplete?.Invoke();
                });
            });
        }

        private static void PersistDefinitiveResolution()
        {
            bool notRequired = ConsentInformation.ConsentStatus == ConsentStatus.NotRequired;
            PlayerPrefs.SetInt(KeyUmpResolved, 1);
            PlayerPrefs.SetInt(KeyUmpConsentNotRequired, notRequired ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[ConsentManager] Persisted definitive UMP resolution (consentNotRequired={notRequired}).");
        }
#endif

        /// <summary>Never downgrade a session that already reached Completed (background refresh).</summary>
        private static void AdvanceState(ConsentGatherState next)
        {
            if (State == ConsentGatherState.Completed && next != ConsentGatherState.Completed)
                return;
            State = next;
        }

        private static void DiscardLegacyPoisonedConsent()
        {
            if (PlayerPrefs.GetInt(LegacyKeyResolved, 0) != 1)
                return;

            PlayerPrefs.DeleteKey(LegacyKeyResolved);
            PlayerPrefs.DeleteKey(LegacyKeyHasUserConsent);
            PlayerPrefs.Save();
            Debug.Log(
                "[ConsentManager] Discarded legacy persisted MAX consent " +
                "(1.1.27–1.1.33 could record a network failure as a permanent denial).");
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
