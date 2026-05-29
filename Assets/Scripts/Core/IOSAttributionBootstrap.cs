using System.Collections;
using UnityEngine;
using Singular;

#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Ensures ATT is resolved before Singular starts so IDFA/SKAN data reaches the MMP and ad networks.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class IOSAttributionBootstrap : MonoBehaviour
    {
        public static bool IsAttResolved { get; private set; }
        public static bool IsAttAuthorized { get; private set; }

        private const float AttWaitTimeoutSeconds = 60f;
        private const int SingularAttWaitIntervalSeconds = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<IOSAttributionBootstrap>() != null)
                return;

            var go = new GameObject("IOSAttributionBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<IOSAttributionBootstrap>();
        }

        private void Awake()
        {
#if UNITY_IOS && !UNITY_EDITOR
            StartCoroutine(BootstrapIOS());
#else
            StartCoroutine(InitializeSingularAfterSceneLoad());
#endif
        }

        private IEnumerator InitializeSingularAfterSceneLoad()
        {
            yield return null;
            TryInitializeSingular();
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator BootstrapIOS()
        {
            IOSAdsHelper.RequestATT();

            float deadline = Time.realtimeSinceStartup + AttWaitTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
                if (status != ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                {
                    IsAttAuthorized = status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
                    IsAttResolved = true;

                    if (IsAttAuthorized)
                        SingularSDK.TrackingOptIn();

                    Debug.Log($"[IOSAttributionBootstrap] ATT resolved: authorized={IsAttAuthorized}");
                    break;
                }

                yield return null;
            }

            if (!IsAttResolved)
            {
                IsAttResolved = true;
                Debug.LogWarning("[IOSAttributionBootstrap] ATT timed out; initializing Singular without IDFA.");
            }

            // Allow the scene (and SingularSDKObject) to load before init.
            yield return null;

            TryInitializeSingular();
        }
#endif

        private static void TryInitializeSingular()
        {
            if (SingularSDK.Initialized)
                return;

            var singular = FindFirstObjectByType<SingularSDK>();
            if (singular != null)
            {
                singular.InitializeOnAwake = false;
#if UNITY_IOS && !UNITY_EDITOR
                singular.waitForTrackingAuthorizationWithTimeoutInterval = SingularAttWaitIntervalSeconds;
                if (!singular.SKANEnabled)
                {
                    singular.SKANEnabled = true;
                    Debug.LogWarning("[IOSAttributionBootstrap] Re-enabled Singular SKAN.");
                }
#endif
            }

            SingularSDK.InitializeSingularSDK();
            Debug.Log("[IOSAttributionBootstrap] Singular SDK initialized.");
        }
    }
}
