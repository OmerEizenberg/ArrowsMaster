using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Runs terms-related PlayerPrefs migration/grandfathering at launch. The cosmetic terms popup is
    /// disabled; MAX/ATT/UMP init is handled entirely by AdsManager.
    /// </summary>
    [DefaultExecutionOrder(-10001)]
    public class TermsConsentBootstrap : MonoBehaviour
    {
        private static TermsConsentBootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null)
                return;

            var go = new GameObject("TermsConsentBootstrap");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TermsConsentBootstrap>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            TermsConsentManager.EnsureReturningPlayerGrandfathered();
            TermsConsentManager.EnsureBugPeriodUpgradeRecovery();
        }
    }
}
