using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Singular;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Initializes Singular once the SDK-init gate opens (immediately on Android; after the ATT decision
    /// on iOS). Decoupled from the cosmetic terms popup so attribution coverage tracks SDK init.
    /// SingularSDKObject must have InitializeOnAwake disabled.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class IOSAttributionBootstrap : MonoBehaviour
    {
        public static bool IsAttResolved { get; private set; }
        public static bool IsAttAuthorized { get; private set; }

        private const int SingularAttWaitIntervalSeconds = 60;
        private const float SingularInstanceWaitTimeoutSeconds = 30f;

        private bool _singularInitStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<IOSAttributionBootstrap>() != null)
                return;

            var go = new GameObject("IOSAttributionBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<IOSAttributionBootstrap>();

            SceneManager.sceneLoaded += OnFirstSceneLoaded;
        }

        private static void OnFirstSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnFirstSceneLoaded;
            DisableSingularAutoInitOnAllInstances();
        }

        private void Awake()
        {
#if UNITY_EDITOR
            // SingularSDK.Awake returns early in Editor and never registers an instance.
            return;
#endif
            TermsConsentManager.OnSdkInitAllowed += HandleSdkInitAllowed;

            if (TermsConsentManager.IsSdkInitAllowed)
                StartCoroutine(InitializeSingularWhenReady());
        }

        private void OnDestroy()
        {
            TermsConsentManager.OnSdkInitAllowed -= HandleSdkInitAllowed;
        }

        private void HandleSdkInitAllowed()
        {
            if (_singularInitStarted)
                return;

            StartCoroutine(InitializeSingularWhenReady());
        }

#if UNITY_IOS && !UNITY_EDITOR
        public static void SetAttResolved(bool authorized)
        {
            if (IsAttResolved)
                return;

            IsAttAuthorized = authorized;
            IsAttResolved = true;

            if (authorized)
                SingularSDK.TrackingOptIn();

            Debug.Log($"[IOSAttributionBootstrap] ATT resolved: authorized={authorized}");
        }
#else
        public static void SetAttResolved(bool authorized)
        {
            IsAttResolved = true;
            IsAttAuthorized = authorized;
        }
#endif

        private IEnumerator InitializeSingularWhenReady()
        {
            if (_singularInitStarted)
                yield break;

            _singularInitStarted = true;

#if UNITY_IOS && !UNITY_EDITOR
            if (!IsAttResolved)
            {
                Debug.LogError("[IOSAttributionBootstrap] SDK init allowed before ATT resolved on iOS.");
                _singularInitStarted = false;
                yield break;
            }
#endif

            yield return WaitForSingularInstance();
            TryInitializeSingular();
        }

        private static IEnumerator WaitForSingularInstance()
        {
            float deadline = Time.realtimeSinceStartup + SingularInstanceWaitTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var singular = FindFirstObjectByType<SingularSDK>();
                if (singular != null)
                {
                    PrepareSingularForManualInit(singular);
                    yield break;
                }

                yield return null;
            }

            Debug.LogError(
                $"[IOSAttributionBootstrap] SingularSDKObject not found within {SingularInstanceWaitTimeoutSeconds}s.");
        }

        private static void DisableSingularAutoInitOnAllInstances()
        {
            var instances = FindObjectsByType<SingularSDK>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var singular in instances)
                PrepareSingularForManualInit(singular);
        }

        private static void PrepareSingularForManualInit(SingularSDK singular)
        {
            if (singular == null)
                return;

            singular.InitializeOnAwake = false;
        }

        private static void TryInitializeSingular()
        {
            var singular = FindFirstObjectByType<SingularSDK>();
            if (singular == null)
            {
                Debug.LogError("[IOSAttributionBootstrap] SingularSDKObject not found in scene; cannot initialize Singular.");
                return;
            }

            PrepareSingularForManualInit(singular);

            if (SingularSDK.Initialized)
            {
                Debug.LogWarning(
                    "[IOSAttributionBootstrap] Singular already initialized before bootstrap; " +
                    "ensure SingularSDKObject.InitializeOnAwake is disabled in the scene.");
                AdsManager.NotifySingularSdkInitialized();
                SingularAttributionBridge.NotifySingularInitialized();
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            singular.waitForTrackingAuthorizationWithTimeoutInterval = SingularAttWaitIntervalSeconds;
            if (!singular.SKANEnabled)
            {
                singular.SKANEnabled = true;
                Debug.LogWarning("[IOSAttributionBootstrap] Re-enabled Singular SKAN.");
            }
#endif

            SingularSDK.InitializeSingularSDK();
            if (SingularSDK.Initialized)
            {
                Debug.Log("[IOSAttributionBootstrap] Singular SDK initialized.");
                AdsManager.NotifySingularSdkInitialized();
                SingularAttributionBridge.NotifySingularInitialized();
            }
            else
            {
                Debug.LogError("[IOSAttributionBootstrap] Singular SDK failed to initialize.");
            }
        }
    }
}
