using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Lobby;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Shows the custom terms popup on the first undecided session and blocks consent-dependent SDK flows
    /// until the user accepts or declines.
    /// </summary>
    [DefaultExecutionOrder(-10001)]
    public class TermsConsentBootstrap : MonoBehaviour
    {
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

            if (TermsConsentManager.HasUserDecided)
            {
                Debug.Log($"[TermsConsentBootstrap] Consent already decided ({TermsConsentManager.GetConsentState()}).");
                return;
            }

            StartCoroutine(ShowPopupWhenReady());
        }

        private IEnumerator ShowPopupWhenReady()
        {
            yield return null;

            if (TermsConsentManager.HasUserDecided || _popupInstance != null)
                yield break;

            EnsureOverlayCanvas();
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

            var view = _popupInstance.GetComponent<TermsAndConditionsPopup>();
            if (view == null)
            {
                Debug.LogError("[TermsConsentBootstrap] TermsAndConditionsPopup component missing on prefab.");
                yield break;
            }

            view.OnAgreed += HandleConsentAccepted;
            Debug.Log("[TermsConsentBootstrap] Showing terms popup; consent-dependent flows are paused.");
        }

        private void HandleConsentAccepted()
        {
            if (_popupInstance != null)
            {
                var view = _popupInstance.GetComponent<TermsAndConditionsPopup>();
                if (view != null)
                    view.OnAgreed -= HandleConsentAccepted;
                _popupInstance = null;
            }

            TermsConsentManager.RecordAccepted();
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
    }
}
