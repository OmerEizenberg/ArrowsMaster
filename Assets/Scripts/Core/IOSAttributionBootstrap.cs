using System.Collections;
using UnityEngine;
using Singular;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Lets Singular auto-initialize (InitializeOnAwake) with ATT wait configured in FirebaseManager,
    /// then notifies AdsManager when Singular is ready to flush queued ad revenue.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class IOSAttributionBootstrap : MonoBehaviour
    {
        private bool _singularReadyNotified;

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
#if UNITY_EDITOR
            return;
#endif
            StartCoroutine(WaitForSingularReadyAndNotify());
        }

        private IEnumerator WaitForSingularReadyAndNotify()
        {
            const float timeoutSeconds = 300f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!SingularSDK.Initialized && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (_singularReadyNotified)
                yield break;

            if (!SingularSDK.Initialized)
            {
                Debug.LogWarning(
                    $"[IOSAttributionBootstrap] Singular not initialized within {timeoutSeconds}s; " +
                    "queued ad revenue may be dropped.");
                yield break;
            }

            _singularReadyNotified = true;
            Debug.Log("[IOSAttributionBootstrap] Singular SDK ready.");
            AdsManager.NotifySingularSdkInitialized();
            SingularAttributionBridge.NotifySingularInitialized();
        }
    }
}
