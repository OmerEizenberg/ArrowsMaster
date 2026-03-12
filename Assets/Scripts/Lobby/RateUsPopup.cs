using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Assets.Scripts.Core;

namespace Assets.Scripts.Lobby
{
    public class RateUsPopup : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_SubtitleText;
        [SerializeField] private Image[] m_Stars; // 5 stars ordered from left to right
        [SerializeField] private Button m_NotNowButton;

        [Header("Sprites")]
        [SerializeField] private Sprite m_EmptyStarSprite;
        [SerializeField] private Sprite m_FilledStarSprite;

        private bool m_IsInteracting = false;

        private void Start()
        {
            // Setup initial content
            m_TitleText.text = "Enjoying Arrows Legend?";
            
            #if UNITY_ANDROID
            m_SubtitleText.text = "Rate us in the play store!";
            #else
            m_SubtitleText.text = "Rate us in the app store!";
            #endif

            if (m_NotNowButton != null)
            {
                m_NotNowButton.onClick.AddListener(OnNotNowClicked);
            }

            // Setup star buttons
            for (int i = 0; i < m_Stars.Length; i++)
            {
                int rating = i + 1;
                Button btn = m_Stars[i].GetComponent<Button>();
                if (btn == null)
                {
                    btn = m_Stars[i].gameObject.AddComponent<Button>();
                }
                btn.onClick.AddListener(() => OnStarClicked(rating));
            }

            // Ensure all stars are initially empty
            UpdateStars(0);
        }

        private void OnStarClicked(int rating)
        {
            if (m_IsInteracting) return;
            
            UpdateStars(rating);

            if (rating >= 4)
            {
                // Send to store then close
                StartCoroutine(RedirectToStoreFlow());
            }
            else
            {
                // Just close after a short delay
                StartCoroutine(CloseAfterDelay(0.5f));
            }
        }

        private void UpdateStars(int rating)
        {
            for (int i = 0; i < m_Stars.Length; i++)
            {
                if (m_Stars[i] != null)
                {
                    m_Stars[i].sprite = (i < rating) ? m_FilledStarSprite : m_EmptyStarSprite;
                }
            }
        }

        private void OnNotNowClicked()
        {
            if (m_IsInteracting) return;
            Destroy(gameObject);
        }

        private IEnumerator RedirectToStoreFlow()
        {
            m_IsInteracting = true;
            
            // App store links from SoftForceUpdateView
            #if UNITY_ANDROID
            string url = "https://play.google.com/store/apps/details?id=com.everybodygames.arrowsmaster";
            #elif UNITY_IOS
            string url = "https://apps.apple.com/us/app/arrows-legend-puzzle-escape/id6758734966";
            #else
            string url = "";
            #endif

            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }

            // Give a tiny bit of time for the OS to handle the URL before destroying the popup
            yield return new WaitForSeconds(0.1f);
            Destroy(gameObject);
        }

        private IEnumerator CloseAfterDelay(float delay)
        {
            m_IsInteracting = true;
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}
