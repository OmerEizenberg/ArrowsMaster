using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Assets.Scripts.Lobby;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Drives SDK-init enablement and shows the (now cosmetic) terms popup. SDK init is intentionally
    /// NOT blocked by the popup: at launch we resolve ATT on iOS — to keep IDFA/customized ads — then
    /// immediately allow MAX/Singular to initialize. The terms popup is shown in parallel purely as a
    /// legal acknowledgement and never gates initialization. This maximizes MAX init vs DAU.
    /// </summary>
    [DefaultExecutionOrder(-10001)]
    public class TermsConsentBootstrap : MonoBehaviour
    {
        // Safety net: if AllowSdkInitWhenReady never completes (coroutine stopped, unexpected hang),
        // force-open the gate so AdsManager/Singular are never permanently blocked this session.
        // Must exceed IOSAdsHelper.AttResolutionTimeoutSeconds (30s) so iOS ATT always resolves first.
        private const float SdkInitGateFallbackSeconds = 35f;

        private static TermsConsentBootstrap _instance;
        private static Transform _overlayRoot;
        private GameObject _popupInstance;

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

            // Decoupled from the popup: resolve ATT on iOS (for customized ads), then allow init —
            // always, regardless of whether the user has acknowledged the terms. Runs every launch.
            StartCoroutine(AllowSdkInitWhenReady());
            StartCoroutine(EnsureSdkInitGateFallback());

            // The terms acknowledgement popup is cosmetic and non-blocking; only show it to users who
            // have not acknowledged yet.
            if (!TermsConsentManager.HasUserDecided)
                StartCoroutine(ShowPopupWhenReady());
            else
                Debug.Log($"[TermsConsentBootstrap] Terms already acknowledged ({TermsConsentManager.GetConsentState()}).");
        }

        /// <summary>
        /// Opens the SDK-init gate as early as possible. On iOS we first await the ATT decision (with the
        /// helper's own 30s timeout) so IDFA-based customized ads are preserved; on Android init is allowed
        /// immediately. Never waits on the cosmetic terms popup.
        /// </summary>
        private IEnumerator AllowSdkInitWhenReady()
        {
            // One frame so AdsManager has subscribed to OnSdkInitAllowed before we raise it.
            yield return null;

            // Defer native SDK init until the first scene has finished loading. Starting MAX/Singular
            // during BeforeSceneLoad/scene activation was causing native crashes right as the lobby appeared.
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded)
            {
                bool sceneReady = false;
                void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneReady = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
                while (!sceneReady && !activeScene.isLoaded)
                    yield return null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            yield return ResolveAttIfNeeded();
            TermsConsentManager.NotifySdkInitAllowed();
        }

        private static IEnumerator EnsureSdkInitGateFallback()
        {
            float deadline = Time.realtimeSinceStartup + SdkInitGateFallbackSeconds;
            while (!TermsConsentManager.IsSdkInitAllowed && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (TermsConsentManager.IsSdkInitAllowed)
                yield break;

            Debug.LogWarning(
                $"[TermsConsentBootstrap] SDK init gate still closed after {SdkInitGateFallbackSeconds}s; force-opening.");
            TermsConsentManager.NotifySdkInitAllowed();
        }

        private IEnumerator ShowPopupWhenReady()
        {
            yield return null;

            if (TermsConsentManager.HasUserDecided || _popupInstance != null)
                yield break;

            // Never compete with Google UMP on first launch — wait for the init gate, then for MAX
            // to finish (or time out) before overlaying the cosmetic terms acknowledgement.
            while (!TermsConsentManager.IsSdkInitAllowed)
                yield return null;

            const float maxSdkWaitSeconds = 20f;
            float waited = 0f;
            while (AdsManager.Instance != null &&
                   !AdsManager.Instance.IsInitialized &&
                   waited < maxSdkWaitSeconds)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }

            yield return new WaitForSeconds(0.5f);

            if (TermsConsentManager.HasUserDecided || _popupInstance != null)
                yield break;

            EnsureOverlayCanvas();
            EnsureEventSystem();
            var prefab = Resources.Load<GameObject>("TermsAndConditionsPopup");
            if (prefab == null)
            {
                Debug.LogError("[TermsConsentBootstrap] TermsAndConditionsPopup prefab missing from Resources.");
                yield break;
            }

            TermsConsentManager.MarkPopupShown();
            _popupInstance = Instantiate(prefab, _overlayRoot, false);
            _popupInstance.SetActive(true);

            var rect = _popupInstance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero;
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var view = _popupInstance.GetComponentInChildren<TermsAndConditionsPopup>(true);
            if (view == null)
            {
                Debug.LogError("[TermsConsentBootstrap] TermsAndConditionsPopup component missing on prefab.");
                yield break;
            }

            view.OnAgreed += HandleConsentAcknowledged;

            if (FirebaseManager.Instance != null)
                FirebaseManager.Instance.LogFunnelEvent(FirebaseManager.EVENT_TERMS_POPUP_SHOWN);

            Debug.Log("[TermsConsentBootstrap] Showing cosmetic terms popup (non-blocking; init already proceeding).");
        }

        /// <summary>
        /// Cosmetic acknowledgement: records the tap for analytics and dismisses the popup. Init is
        /// already running independently, so this does not start or gate any SDK flow.
        /// </summary>
        private void HandleConsentAcknowledged()
        {
            if (_popupInstance != null)
            {
                var view = _popupInstance.GetComponentInChildren<TermsAndConditionsPopup>(true);
                if (view != null)
                    view.OnAgreed -= HandleConsentAcknowledged;
                _popupInstance = null;
            }
        }

        private static IEnumerator ResolveAttIfNeeded()
        {
#if UNITY_IOS && !UNITY_EDITOR
            yield return IOSAdsHelper.ResolveAttBlocking();
#else
            yield break;
#endif
        }

        private static void EnsureOverlayCanvas()
        {
            if (_overlayRoot != null)
                return;

            var canvasGo = new GameObject("TermsConsentOverlay");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGo);
            _overlayRoot = canvasGo.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("TermsConsentEventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemGo);
            Debug.Log("[TermsConsentBootstrap] Created EventSystem for terms popup input.");
        }
    }
}
