using System.Collections;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.GameUI
{
    public class CoinsRewardFloatingView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_Text;

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

            m_Text.text = "+" + amount.ToString("N0");
            m_Text.color = new Color(m_Text.color.r, m_Text.color.g, m_Text.color.b, 1f);

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
            Color startColor = m_Text.color;
            Vector2 startPos = m_RectTransform.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, distance);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                m_RectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                m_Text.color = new Color(
                    startColor.r,
                    startColor.g,
                    startColor.b,
                    Mathf.Lerp(startColor.a, 0f, t));

                yield return null;
            }

            m_AnimateCoroutine = null;
            Hide();
        }
    }
}
