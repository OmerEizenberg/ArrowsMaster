using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Assets.Scripts.Core;

namespace Assets.Scripts.GameUI
{
    public class StreakRecordUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform m_TextHolder;
        [SerializeField] private TextMeshProUGUI m_RecordText;
        [SerializeField] private Image m_FireIcon;

        [Header("Sprites")]
        [SerializeField] private Sprite m_BaseSprite;
        [SerializeField] private Sprite m_TargetSprite;

        [Header("Animation Settings")]
        [SerializeField] private float m_PunchScale = 1.2f;
        [SerializeField] private float m_PunchDuration = 0.2f;
        [SerializeField] private float m_FadeDuration = 0.3f;

        private Coroutine m_AnimationCoroutine;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMaxStreakBroken += HandleMaxStreakBroken;
                GameManager.Instance.OnLevelStarted += UpdateDisplay;
            }
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMaxStreakBroken -= HandleMaxStreakBroken;
                GameManager.Instance.OnLevelStarted -= UpdateDisplay;
            }
        }

        private void UpdateDisplay()
        {
            if (m_RecordText != null)
            {
                m_RecordText.text = UserDataManager.Instance.MaxStreak.ToString();
            }
            
            // Ensure base state
            if (m_FireIcon != null && m_BaseSprite != null)
            {
                m_FireIcon.sprite = m_BaseSprite;
                Color c = m_FireIcon.color;
                c.a = 1f;
                m_FireIcon.color = c;
            }
        }

        private void UpdateDisplay(int newRecord)
        {
            if (m_RecordText != null)
            {
                m_RecordText.text = newRecord.ToString();
            }
        }

        private void HandleMaxStreakBroken(int newRecord)
        {
            UpdateDisplay(newRecord);

            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
            }
            m_AnimationCoroutine = StartCoroutine(RecordBrokenAnimation());
        }

        private IEnumerator RecordBrokenAnimation()
        {
            // 1. Punch Animation on Text Holder
            if (m_TextHolder != null)
            {
                StartCoroutine(PunchScaleRoutine(m_TextHolder, m_PunchScale, m_PunchDuration * 0.4f, m_PunchDuration * 0.6f));
            }

            // 2. Fade Icon between Sprites
            if (m_FireIcon != null && m_BaseSprite != null && m_TargetSprite != null)
            {
                // Fade to Target
                yield return StartCoroutine(FadeSpriteRoutine(m_FireIcon, m_BaseSprite, m_TargetSprite, m_FadeDuration));
                // Fade back to Base
                yield return StartCoroutine(FadeSpriteRoutine(m_FireIcon, m_TargetSprite, m_BaseSprite, m_FadeDuration));
            }
        }

        private IEnumerator PunchScaleRoutine(RectTransform target, float punchScale, float upDuration, float downDuration)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 peakScale = originalScale * punchScale;

            // Scale Up
            float elapsed = 0f;
            while (elapsed < upDuration)
            {
                target.localScale = Vector3.Lerp(originalScale, peakScale, elapsed / upDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            target.localScale = peakScale;

            // Scale Down
            elapsed = 0f;
            while (elapsed < downDuration)
            {
                target.localScale = Vector3.Lerp(peakScale, originalScale, elapsed / downDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            target.localScale = originalScale;
        }

        private IEnumerator FadeSpriteRoutine(Image image, Sprite from, Sprite to, float duration)
        {
            float elapsed = 0f;
            Color color = image.color;

            // Fade Out
            while (elapsed < duration * 0.5f)
            {
                color.a = Mathf.Lerp(1f, 0f, elapsed / (duration * 0.5f));
                image.color = color;
                elapsed += Time.deltaTime;
                yield return null;
            }

            image.sprite = to;
            elapsed = 0f;

            // Fade In
            while (elapsed < duration * 0.5f)
            {
                color.a = Mathf.Lerp(0f, 1f, elapsed / (duration * 0.5f));
                image.color = color;
                elapsed += Time.deltaTime;
                yield return null;
            }
            color.a = 1f;
            image.color = color;
        }
    }
}
