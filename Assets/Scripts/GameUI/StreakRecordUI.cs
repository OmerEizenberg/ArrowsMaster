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





        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelStarted += UpdateDisplay;
            }
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelStarted -= UpdateDisplay;
            }
        }

        private void UpdateDisplay()
        {
            int currentStreak = UserDataManager.Instance.LevelStreak;

            if (m_RecordText != null)
            {
                if (currentStreak < 6)
                {
                    m_RecordText.text = $"{currentStreak}/6";
                }
                else
                {
                    m_RecordText.text = currentStreak.ToString();
                }
            }
            
            // Set sprite based on streak count
            if (m_FireIcon != null)
            {
                if (currentStreak >= 6)
                {
                    if (m_TargetSprite != null) m_FireIcon.sprite = m_TargetSprite;
                    
                    // Enable fire skew animation if present
                    var skew = m_FireIcon.GetComponent<UIFireSkew>();
                    if (skew != null) skew.enabled = true;
                }
                else
                {
                    if (m_BaseSprite != null) m_FireIcon.sprite = m_BaseSprite;
                    
                    // Disable fire skew animation if present
                    var skew = m_FireIcon.GetComponent<UIFireSkew>();
                    if (skew != null) skew.enabled = false;
                }

                Color c = m_FireIcon.color;
                c.a = 1f;
                m_FireIcon.color = c;
            }
        }


    }
}
