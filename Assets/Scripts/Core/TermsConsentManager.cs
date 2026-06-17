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

        /// <summary>Fired once per session when Singular and ads are allowed to initialize.</summary>
        public static event Action OnSdkInitAllowed;

        public static bool HasUserDecided => GetConsentState() != ConsentState.Undecided;

        public static bool HasAccepted => GetConsentState() == ConsentState.Accepted;

        public static bool WasPopupShown => PlayerPrefs.GetInt(KeyPopupShown, 0) == 1;

        public static bool IsSdkInitAllowed { get; private set; }

        static TermsConsentManager()
        {
            MigrateLegacyTermsAgreed();
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
            if (PlayerPrefs.HasKey(KeyConsentState))
                return;

            if (PlayerPrefs.GetInt(LegacyTermsAgreedKey, 0) != 1)
                return;

            PlayerPrefs.SetInt(KeyConsentState, (int)ConsentState.Accepted);
            PlayerPrefs.SetInt(KeyPopupShown, 1);
            PlayerPrefs.Save();
            Debug.Log("[TermsConsentManager] Migrated legacy TermsAgreed preference.");
        }
    }
}
