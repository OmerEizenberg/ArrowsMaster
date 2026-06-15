using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.GAE;

namespace Assets.Scripts.GameUI
{
    public class CoinsRewardFloatingView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_Text;
        [SerializeField] private Image m_Icon;
        [SerializeField] private Sprite m_CoinSprite;
        [SerializeField] private Sprite m_GaeSprite;

        private RectTransform m_RectTransform;
        private Coroutine m_AnimateCoroutine;

        private void Awake()
        {
            if (m_Text == null)
            {
                m_Text = GetComponent<TextMeshProUGUI>();
            }

            m_RectTransform = transform as RectTransform;
        }

        public void Show(
            int amount,
            Vector2 clickPos,
            RectTransform parentRect,
            RectTransform currencyTextRect,
            Camera uiCamera,
            Vector2 floatingDistance,
            Vector2 spawnOffsetRange,
            float spawnOffsetYRange,
            float floatDuration,
            float floatDistance)
        {
            if (m_Text == null || m_RectTransform == null || parentRect == null)
            {
                return;
            }

            if (m_AnimateCoroutine != null)
            {
                StopCoroutine(m_AnimateCoroutine);
                m_AnimateCoroutine = null;
            }

            SetupRewardVisuals(amount);

            PositionAtClickOrFallback(
                clickPos,
                parentRect,
                currencyTextRect,
                uiCamera,
                floatingDistance,
                spawnOffsetRange,
                spawnOffsetYRange);

            gameObject.SetActive(true);
            m_AnimateCoroutine = StartCoroutine(AnimateFloat(floatDuration, floatDistance));
        }

        public void Hide()
        {
            if (m_AnimateCoroutine != null)
            {
                StopCoroutine(m_AnimateCoroutine);
                m_AnimateCoroutine = null;
            }

            gameObject.SetActive(false);
        }

        private void SetupRewardVisuals(int amount)
        {
            m_Text.gameObject.SetActive(true);
            m_Text.text = "+" + amount.ToString("N0");
            m_Text.color = new Color(m_Text.color.r, m_Text.color.g, m_Text.color.b, 1f);

            if (m_Icon == null)
            {
                return;
            }

            bool useGae = GAEManager.Instance != null && GAEManager.Instance.IsGameplayGaeCurrencyActive;
            Sprite sprite = useGae ? m_GaeSprite : m_CoinSprite;
            if (sprite == null && !useGae)
            {
                sprite = m_Icon.sprite;
            }

            m_Icon.sprite = sprite;
            m_Icon.enabled = sprite != null;
            if (sprite != null)
            {
                Color iconColor = m_Icon.color;
                m_Icon.color = new Color(iconColor.r, iconColor.g, iconColor.b, 1f);
            }
        }

        private void PositionAtClickOrFallback(
            Vector2 clickPos,
            RectTransform parentRect,
            RectTransform currencyTextRect,
            Camera uiCamera,
            Vector2 floatingDistance,
            Vector2 spawnOffsetRange,
            float spawnOffsetYRange)
        {
            if (clickPos != Vector2.zero)
            {
                m_RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                m_RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                m_RectTransform.pivot = new Vector2(0.5f, 0.5f);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        clickPos + floatingDistance,
                        uiCamera,
                        out Vector2 localPos))
                {
                    m_RectTransform.anchoredPosition = localPos;
                }

                return;
            }

            if (currencyTextRect == null)
            {
                return;
            }

            float xOffset = Random.Range(spawnOffsetRange.x, spawnOffsetRange.y);
            Vector2 basePos = currencyTextRect.anchoredPosition;
            m_RectTransform.anchoredPosition = basePos + new Vector2(
                xOffset,
                currencyTextRect.rect.height + spawnOffsetYRange);
        }

        private IEnumerator AnimateFloat(float duration, float distance)
        {
            float elapsed = 0f;
            Color startTextColor = m_Text.color;
            Color startIconColor = m_Icon != null ? m_Icon.color : Color.white;
            Vector2 startPos = m_RectTransform.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, distance);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                m_RectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                m_Text.color = new Color(
                    startTextColor.r,
                    startTextColor.g,
                    startTextColor.b,
                    Mathf.Lerp(startTextColor.a, 0f, t));

                if (m_Icon != null && m_Icon.enabled)
                {
                    m_Icon.color = new Color(
                        startIconColor.r,
                        startIconColor.g,
                        startIconColor.b,
                        Mathf.Lerp(startIconColor.a, 0f, t));
                }

                yield return null;
            }

            m_AnimateCoroutine = null;
            Hide();
        }
    }
}
