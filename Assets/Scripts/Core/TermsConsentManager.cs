using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Persists whether the custom terms popup was shown and the user's decision.
    /// Undecided state applies only until the first choice; later sessions skip the gate.
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

        public static event Action OnConsentResolved;

        public static bool HasUserDecided => GetConsentState() != ConsentState.Undecided;

        public static bool HasAccepted => GetConsentState() == ConsentState.Accepted;

        public static bool WasPopupShown => PlayerPrefs.GetInt(KeyPopupShown, 0) == 1;

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

        private static void SetConsentState(ConsentState state)
        {
            if (GetConsentState() == state)
                return;

            PlayerPrefs.SetInt(KeyConsentState, (int)state);
            PlayerPrefs.Save();
            Debug.Log($"[TermsConsentManager] Consent state set to {state}.");

            if (state != ConsentState.Undecided)
                OnConsentResolved?.Invoke();
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
