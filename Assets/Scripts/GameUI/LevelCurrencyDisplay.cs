using UnityEngine;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.GameUI
{
    public class LevelCurrencyDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI m_CurrencyText;
        [SerializeField] private GameObject m_FloatingTextPrefab;
        
        [Header("Animation Settings")]
        [SerializeField] private float m_TextPunchScale = 1.2f;
        [SerializeField] private float m_PunchDuration = 0.2f;
        [SerializeField] private float m_FloatDuration = 1.0f;
        [SerializeField] private float m_FloatDistance = 50f;
        [SerializeField] private Vector2 m_SpawnOffsetRange = new Vector2(-20f, 20f);
        [SerializeField] private float m_SpawnOffsetYRange = 200f;

        private int m_CurrentAmount = 0;
        private Coroutine m_PunchCoroutine;
        private Vector3 m_OriginalScale;
        private Vector2 m_FloatingDistance = new Vector2(0f, 0f);
        private void Start()
        {
             m_FloatingDistance = new Vector2(-Screen.width/23f, Screen.height/20f);

            if (m_CurrencyText != null)
            {
                m_OriginalScale = m_CurrencyText.transform.localScale;
                m_CurrencyText.text = "0";
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCurrencyChanged += UpdateCurrencyDisplay;
                m_CurrentAmount = GameManager.Instance.CollectedLevelCurrency;
                if (m_CurrencyText != null) m_CurrencyText.text = m_CurrentAmount.ToString("N0");
            }
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null && m_CurrencyText != null)
            {
                m_CurrentAmount = GameManager.Instance.CollectedLevelCurrency;
                m_CurrencyText.text = m_CurrentAmount.ToString("N0");
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCurrencyChanged -= UpdateCurrencyDisplay;
            }
        }

        private void UpdateCurrencyDisplay(int newAmount, Vector2 clickPos)
        {
            int difference = newAmount - m_CurrentAmount;
            
            if (difference > 0)
            {
                // Instantiate floating text
                if (m_FloatingTextPrefab != null)
                {
                    ShowFloatingText(difference, clickPos);
                }

                // Punch animation for main text
                if (m_CurrencyText != null)
                {
                    if (m_PunchCoroutine != null) StopCoroutine(m_PunchCoroutine);
                    m_PunchCoroutine = StartCoroutine(AnimatePunch());
                }
            }

            m_CurrentAmount = newAmount;
            
            if (m_CurrencyText != null)
            {
                m_CurrencyText.text = newAmount.ToString("N0");
            }
            m_CurrentAmount = newAmount;
        }

        private void ShowFloatingText(int amount, Vector2 clickPos)
        {
            if (m_CurrencyText == null) return;

            // Instantiate the prefab
            // Use m_CurrencyText.transform.parent so they are siblings and share the same coordinate space
            GameObject floatingObj = Instantiate(m_FloatingTextPrefab, m_CurrencyText.transform.parent);
            TextMeshProUGUI floatingText = floatingObj.GetComponent<TextMeshProUGUI>();
            
            if (floatingText != null)
            {
                floatingText.text = "+" + amount.ToString("N0");
                
                RectTransform rect = floatingObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (clickPos != Vector2.zero)
                    {
                        RectTransform parentRect = rect.parent as RectTransform;
                        Canvas canvas = rect.GetComponentInParent<Canvas>();
                        
                        // Use the correct camera for coordinate conversion
                        Camera cam = null;
                        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                        }

                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, (clickPos+m_FloatingDistance), cam, out Vector2 localPos))
                        {
                            // Important: ensure anchors are center-middle so anchoredPosition matches the calculated localPos
                            rect.anchorMin = new Vector2(0.5f, 0.5f);
                            rect.anchorMax = new Vector2(0.5f, 0.5f);
                            rect.pivot = new Vector2(0.5f, 0.5f);
                            rect.anchoredPosition = localPos;
                        }
                    }
                    else
                    {
                        // Randomize x position slightly
                        float xOffset = Random.Range(m_SpawnOffsetRange.x, m_SpawnOffsetRange.y);
                        
                        // Position relative to the main currency text:
                        // Start at CurrencyText's position + upward offset + random X
                        Vector2 basePos = m_CurrencyText.rectTransform.anchoredPosition;
                        
                        // We can assume a standard vertical offset (e.g., 50 units above the center of the text)
                        rect.anchoredPosition = basePos + new Vector2(xOffset, m_CurrencyText.rectTransform.rect.height + m_SpawnOffsetYRange); 
                    }
                }

                StartCoroutine(AnimateFloatingText(floatingText, rect));
            }
            else
            {
                Destroy(floatingObj);
            }
        }

        private System.Collections.IEnumerator AnimateFloatingText(TextMeshProUGUI textComp, RectTransform rectTransform)
        {
            float elapsed = 0f;
            Color startColor = textComp.color;
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0, m_FloatDistance);

            while (elapsed < m_FloatDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / m_FloatDuration;

                // Move up
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                // Fade out (alpha from 1 to 0)
                textComp.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));

                yield return null;
            }

            Destroy(textComp.gameObject);
        }

        private System.Collections.IEnumerator AnimatePunch()
        {
            float elapsed = 0f;
            Vector3 targetScale = m_OriginalScale * m_TextPunchScale;

            // Scale up
            while (elapsed < m_PunchDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (m_PunchDuration / 2);
                m_CurrencyText.transform.localScale = Vector3.Lerp(m_OriginalScale, targetScale, t);
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < m_PunchDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (m_PunchDuration / 2);
                m_CurrencyText.transform.localScale = Vector3.Lerp(targetScale, m_OriginalScale, t);
                yield return null;
            }

            m_CurrencyText.transform.localScale = m_OriginalScale;
            m_PunchCoroutine = null;
        }
    }
}
