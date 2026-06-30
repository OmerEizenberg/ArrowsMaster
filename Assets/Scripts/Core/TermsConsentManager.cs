using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Persists whether the cosmetic terms popup was shown and the user's acknowledgement.
    /// MAX SDK init runs independently in AdsManager and is not gated by terms acknowledgement.
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

        private const string SessionCountKey = "TotalSessionCount";
        private const string LevelKey = "CurrentLevel";
        private const string LevelProgressKey = "LevelProgress";
        private const string ArrowsCurrencyKey = "ArrowsCurrency";
        private const string BoostersInitializedKey = "BoostersInitialized";

        public static bool HasUserDecided => GetConsentState() != ConsentState.Undecided;

        public static bool HasAccepted => GetConsentState() == ConsentState.Accepted;

        public static bool WasPopupShown => PlayerPrefs.GetInt(KeyPopupShown, 0) == 1;

        static TermsConsentManager()
        {
            MigrateLegacyTermsAgreed();
            EnsureReturningPlayerGrandfathered();
            EnsureBugPeriodUpgradeRecovery();
        }

        public static void EnsureReturningPlayerGrandfathered()
        {
            if (HasUserDecided)
                return;

            if (!HasExistingPlayerProgress())
                return;

            AcceptGrandfatheredPlayer(
                "existing save progress detected (pre-June non-blocking terms flow)");
        }

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
