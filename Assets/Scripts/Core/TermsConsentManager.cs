using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Persists whether the custom terms popup was shown and the user's decision.
    /// SDK initialization (Singular + ads) is gated separately: Android after terms,
    /// iOS after terms and ATT.
    /// </summary>
    public static class TermsConsentManager
    {
        public enum ConsentState
        {
            Undecided = 0,
            Accepted = 1,
            Declined = 2
        }

        private const string KeyConsentState = "TermsConsentState";
        private const string KeyPopupShown = "TermsConsentPopupShown";
        private const string LegacyTermsAgreedKey = "TermsAgreed";

        // PlayerPrefs written by UserDataManager — used to detect veterans who played before the
        // June 2026 blocking terms gate (ads used to init without TermsConsentState being set).
        private const string SessionCountKey = "TotalSessionCount";
        private const string LevelKey = "CurrentLevel";
        private const string LevelProgressKey = "LevelProgress";
        private const string ArrowsCurrencyKey = "ArrowsCurrency";
        private const string BoostersInitializedKey = "BoostersInitialized";

        /// <summary>Fired once per session when Singular and ads are allowed to initialize.</summary>
        public static event Action OnSdkInitAllowed;

        public static bool HasUserDecided => GetConsentState() != ConsentState.Undecided;

        public static bool HasAccepted => GetConsentState() == ConsentState.Accepted;

        public static bool WasPopupShown => PlayerPrefs.GetInt(KeyPopupShown, 0) == 1;

        public static bool IsSdkInitAllowed { get; private set; }

        static TermsConsentManager()
        {
            MigrateLegacyTermsAgreed();
            EnsureReturningPlayerGrandfathered();
        }

        /// <summary>
        /// Re-run at bootstrap Awake (BeforeSceneLoad) so prefs are read after any early writers.
        /// Safe to call multiple times.
        /// </summary>
        public static void EnsureReturningPlayerGrandfathered()
        {
            if (HasUserDecided)
                return;

            if (!HasExistingPlayerProgress())
                return;

            AcceptGrandfatheredPlayer(
                "existing save progress detected (pre-June non-blocking terms flow)");
        }

        public static ConsentState GetConsentState()
        {
            return (ConsentState)PlayerPrefs.GetInt(KeyConsentState, (int)ConsentState.Undecided);
        }

        public static void MarkPopupShown()
        {
            if (WasPopupShown)
                return;

            PlayerPrefs.SetInt(KeyPopupShown, 1);
            PlayerPrefs.Save();
            Debug.Log("[TermsConsentManager] Terms popup marked as shown.");
        }

        public static void RecordAccepted()
        {
            SetConsentState(ConsentState.Accepted);
            PlayerPrefs.SetInt(LegacyTermsAgreedKey, 1);
            PlayerPrefs.Save();
        }

        public static void RecordDeclined()
        {
            SetConsentState(ConsentState.Declined);
        }

        /// <summary>
        /// Unblocks Singular and ads. On iOS this must only be called after ATT is resolved.
        /// </summary>
        public static void NotifySdkInitAllowed()
        {
            if (IsSdkInitAllowed || !HasAccepted)
                return;

            IsSdkInitAllowed = true;
            Debug.Log("[TermsConsentManager] SDK init allowed (terms accepted, ATT resolved on iOS).");

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(FirebaseManager.EVENT_PASSED_TERMS);

            OnSdkInitAllowed?.Invoke();
        }

        private static void SetConsentState(ConsentState state)
        {
            if (GetConsentState() == state)
                return;

            PlayerPrefs.SetInt(KeyConsentState, (int)state);
            PlayerPrefs.Save();
            Debug.Log($"[TermsConsentManager] Consent state set to {state}.");
        }

        private static void MigrateLegacyTermsAgreed()
        {
            if (HasUserDecided)
                return;

            if (PlayerPrefs.GetInt(LegacyTermsAgreedKey, 0) != 1)
                return;

            AcceptGrandfatheredPlayer("legacy TermsAgreed preference");
        }

        private static bool HasExistingPlayerProgress()
        {
            if (PlayerPrefs.GetInt(LegacyTermsAgreedKey, 0) == 1)
                return true;

            // TotalSessionCount is persisted at end of each session; >= 1 means this is not a first install.
            if (PlayerPrefs.GetInt(SessionCountKey, 0) >= 1)
                return true;

            if (PlayerPrefs.GetInt(LevelKey, 1) > 1)
                return true;

            if (PlayerPrefs.GetInt(ArrowsCurrencyKey, 0) > 0)
                return true;

            if (PlayerPrefs.GetInt(BoostersInitializedKey, 0) == 1)
                return true;

            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(LevelProgressKey, string.Empty)))
                return true;

            return false;
        }

        private static void AcceptGrandfatheredPlayer(string reason)
        {
            PlayerPrefs.SetInt(KeyConsentState, (int)ConsentState.Accepted);
            PlayerPrefs.SetInt(KeyPopupShown, 1);
            PlayerPrefs.SetInt(LegacyTermsAgreedKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[TermsConsentManager] Grandfathered returning player — consent auto-accepted ({reason}).");
        }
    }
}
