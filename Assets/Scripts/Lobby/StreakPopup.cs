using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Core;

namespace Assets.Scripts.Lobby
{
    public class StreakPopup : MonoBehaviour
    {
        [Header("Streak Steps (Assign from Left to Right)")]
        [SerializeField] private Image[] m_StepImages;

        [Header("Sprites")]
        [SerializeField] private Sprite m_DisabledSprite;
        [SerializeField] private Sprite m_ColoredSprite;

        [Header("Animation Settings")]
        [SerializeField] private float m_AnimDelayBetweenSteps = 0.15f;
        [SerializeField] private float m_AnimDuration = 0.3f;
        [SerializeField] private float m_PunchScale = 1.3f;

        private void OnEnable()
        {
            // Reset all to disabled initially
            for (int i = 0; i < m_StepImages.Length; i++)
            {
                if (m_StepImages[i] != null)
                {
                    m_StepImages[i].sprite = m_DisabledSprite;
                    m_StepImages[i].transform.localScale = Vector3.one;
                }
            }

            StartCoroutine(AnimateStreakSequence());
        }

        private IEnumerator AnimateStreakSequence()
        {
            // Give a tiny delay before animation starts so popup has time to open
            yield return new WaitForSeconds(0.2f);

            int currentStreak = 0;
            if (UserDataManager.Instance != null)
            {
                currentStreak = UserDataManager.Instance.LevelStreak;
            }

            // Cap to max length of steps available (should be 6)
            currentStreak = Mathf.Clamp(currentStreak, 0, m_StepImages.Length);

            // Animate each active step one by one
            for (int i = 0; i < currentStreak; i++)
            {
                if (m_StepImages[i] != null)
                {
                    StartCoroutine(AnimateStep(m_StepImages[i]));

                    // Optional: Play a nice tick/pop sound per step
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlayClick();
                    }

                    yield return new WaitForSeconds(m_AnimDelayBetweenSteps);
                }
            }
        }

        private IEnumerator AnimateStep(Image stepImage)
        {
            // Step 1: Switch to colored sprite
            stepImage.sprite = m_ColoredSprite;

            // Step 2: Pop out (Scale up)
            Vector3 originalScale = Vector3.one;
            Vector3 peakScale = originalScale * m_PunchScale;

            float halfDuration = m_AnimDuration * 0.5f;

            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                stepImage.transform.localScale = Vector3.Lerp(originalScale, peakScale, elapsed / halfDuration);
                yield return null;
            }

            // Step 3: Pop in (Scale down to normal)
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                stepImage.transform.localScale = Vector3.Lerp(peakScale, originalScale, elapsed / halfDuration);
                yield return null;
            }

            stepImage.transform.localScale = originalScale;
        }

        public void OnOkButtonClicked()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

            // Close the popup
            gameObject.SetActive(false);
        }
    }
}
