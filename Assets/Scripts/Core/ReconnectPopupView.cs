using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Core
{
    public class ReconnectPopupView : MonoBehaviour
    {
        private const string DefaultTitle = "No Internet Connection";
        private const string DefaultRetryLabel = "Retry";

        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private TextMeshProUGUI m_RetryButtonText;

        public event Action OnRetryClicked;

        public void Initialize(string title = null, string retryLabel = null)
        {
            EnsureUiReferences();

            if (m_TitleText != null)
                m_TitleText.text = string.IsNullOrEmpty(title) ? DefaultTitle : title;

            if (m_RetryButtonText != null)
                m_RetryButtonText.text = string.IsNullOrEmpty(retryLabel) ? DefaultRetryLabel : retryLabel;

            if (m_RetryButton != null)
            {
                m_RetryButton.onClick.RemoveListener(HandleRetryClicked);
                m_RetryButton.onClick.AddListener(HandleRetryClicked);
            }
        }

        private void HandleRetryClicked()
        {
            OnRetryClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (m_RetryButton != null)
                m_RetryButton.onClick.RemoveListener(HandleRetryClicked);
        }

        private void EnsureUiReferences()
        {
            if (m_TitleText != null && m_RetryButton != null)
                return;

            BuildFallbackUi();
        }

        private void BuildFallbackUi()
        {
            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
                rootRect = gameObject.AddComponent<RectTransform>();

            StretchToParent(rootRect);

            var dimmer = CreateUiObject("Dimmer", rootRect);
            var dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);
            StretchToParent(dimmer.GetComponent<RectTransform>());

            var panel = CreateUiObject("Panel", rootRect);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 420f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);

            var titleGo = CreateUiObject("Title", panelRect);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.08f, 0.45f);
            titleRect.anchorMax = new Vector2(0.92f, 0.9f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            m_TitleText = titleGo.AddComponent<TextMeshProUGUI>();
            m_TitleText.alignment = TextAlignmentOptions.Center;
            m_TitleText.fontSize = 44f;
            m_TitleText.color = Color.white;
            m_TitleText.text = DefaultTitle;

            var buttonGo = CreateUiObject("RetryButton", panelRect);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.2f, 0.12f);
            buttonRect.anchorMax = new Vector2(0.8f, 0.32f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.72f, 0.35f, 1f);

            m_RetryButton = buttonGo.AddComponent<Button>();
            m_RetryButton.targetGraphic = buttonImage;

            var labelGo = CreateUiObject("Label", buttonRect);
            StretchToParent(labelGo.GetComponent<RectTransform>());

            m_RetryButtonText = labelGo.AddComponent<TextMeshProUGUI>();
            m_RetryButtonText.alignment = TextAlignmentOptions.Center;
            m_RetryButtonText.fontSize = 38f;
            m_RetryButtonText.color = Color.white;
            m_RetryButtonText.text = DefaultRetryLabel;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localPosition = Vector3.zero;
        }
    }
}
