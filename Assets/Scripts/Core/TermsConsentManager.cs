using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Persists whether the (now cosmetic) terms popup was shown and the user's acknowledgement.
    /// SDK initialization is intentionally DECOUPLED from this acknowledgement to maximize MAX init:
    /// init is allowed immediately on Android and after the ATT decision on iOS (so customized/IDFA
    /// ads are preserved), regardless of whether the terms popup has been tapped.
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

        /// <summary>
        /// Session-only runtime gate — intentionally NOT persisted to PlayerPrefs. Each cold start
        /// resets to false; TermsConsentBootstrap re-opens it after ATT (iOS) or immediately (Android).
        /// Persisting would incorrectly skip ATT on a fresh install/reinstall.
        /// </summary>
        public static bool WasSdkInitAllowedThisSession => IsSdkInitAllowed;

        static TermsConsentManager()
        {
            MigrateLegacyTermsAgreed();
            EnsureReturningPlayerGrandfathered();
            EnsureBugPeriodUpgradeRecovery();
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

        /// <summary>
        /// Users who installed during the June 3–22 blocking-terms bug often have PopupShown=1 but
        /// TermsConsentState=Undecided (they closed before tapping Continue). Init is no longer gated,
        /// but this clears the stale Undecided state so they are not stuck re-showing the popup forever.
        /// </summary>
        public static void EnsureBugPeriodUpgradeRecovery()
        {
            if (HasUserDecided)
                return;

            if (!WasPopupShown)
                return;

            AcceptGrandfatheredPlayer(
                "terms popup was shown in a prior session (bug-period upgrade recovery)");
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

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(FirebaseManager.EVENT_TERMS_ACCEPTED_TAP);
        }

        public static void RecordDeclined()
        {
            SetConsentState(ConsentState.Declined);
        }

        /// <summary>
        /// Unblocks Singular and ads. Decoupled from terms acknowledgement so init is maximized.
        /// On iOS this must only be called after ATT is resolved (preserves IDFA / customized ads).
        /// </summary>
        public static void NotifySdkInitAllowed()
        {
            if (IsSdkInitAllowed)
                return;

            IsSdkInitAllowed = true;
            Debug.Log("[TermsConsentManager] SDK init allowed (ATT resolved on iOS; terms popup is non-blocking).");

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(FirebaseManager.EVENT_PASSED_TERMS);

            var handlers = OnSdkInitAllowed;
            if (handlers == null)
            {
                Debug.LogWarning(
                    "[TermsConsentManager] SDK init gate opened but no subscribers yet; " +
                    "consumers must check IsSdkInitAllowed on subscribe.");
                return;
            }

            handlers.Invoke();
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
