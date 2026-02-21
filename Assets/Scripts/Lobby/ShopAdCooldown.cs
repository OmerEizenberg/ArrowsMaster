using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Assets.Scripts.Core;

namespace Assets.Scripts.Lobby
{
    /// <summary>
    /// Handles the cooldown logic for the rewarded ad in the shop.
    /// Manages UI states (button interactability, ad image visibility, timer display)
    /// and persists the cooldown state across app restarts.
    /// </summary>
    public class ShopAdCooldown : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The button used to trigger the rewarded ad.")]
        [SerializeField] private Button m_AdButton;
        
        [Tooltip("The icon image of the ad inside the button.")]
        [SerializeField] private GameObject m_AdImage;
        
        [Tooltip("The text mesh pro element used to show the countdown.")]
        [SerializeField] private TextMeshProUGUI m_TimerText;

        [Header("Settings")]
        [Tooltip("The duration of the cooldown in seconds (default 240 for 4 minutes).")]
        [SerializeField] private float m_CooldownDurationSeconds = 240f;
        
        [Tooltip("The PlayerPrefs key used to store the cooldown end time.")]
        [SerializeField] private string m_CooldownEndKey = "ShopAdCooldownEnd";

        private bool m_IsCooldownActive = false;
        private DateTime m_CooldownEndTime;

        private void OnEnable()
        {
            // Subscribe to the specific event for coins reward
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived += HandleCoinsRewardReceived;
            }
            
            // Initial check on enable (handles app start or returning to lobby)
            RefreshCooldownState();
        }

        private void OnDisable()
        {
            // Unsubscribe to avoid memory leaks or duplicate triggers
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived -= HandleCoinsRewardReceived;
            }
        }

        private void HandleCoinsRewardReceived()
        {
            StartCooldown();
        }

        private void StartCooldown()
        {
            float duration = m_CooldownDurationSeconds;
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
            {
                duration = RemoteConfigManager.Instance.RewardedAdCoinsCooldown;
            }

            m_CooldownEndTime = DateTime.Now.AddSeconds(duration);
            // Save as binary string for cross-session persistence
            PlayerPrefs.SetString(m_CooldownEndKey, m_CooldownEndTime.ToBinary().ToString());
            PlayerPrefs.Save();
            
            m_IsCooldownActive = true;
            UpdateUI();
        }

        private void RefreshCooldownState()
        {
            if (PlayerPrefs.HasKey(m_CooldownEndKey))
            {
                string storedValue = PlayerPrefs.GetString(m_CooldownEndKey);
                if (long.TryParse(storedValue, out long binaryTime))
                {
                    m_CooldownEndTime = DateTime.FromBinary(binaryTime);
                    
                    if (DateTime.Now < m_CooldownEndTime)
                    {
                        m_IsCooldownActive = true;
                    }
                    else
                    {
                        m_IsCooldownActive = false;
                        PlayerPrefs.DeleteKey(m_CooldownEndKey);
                    }
                }
                else
                {
                    m_IsCooldownActive = false;
                }
            }
            else
            {
                m_IsCooldownActive = false;
            }
            
            UpdateUI();
        }

        private void Update()
        {
            if (m_IsCooldownActive)
            {
                TimeSpan remaining = m_CooldownEndTime - DateTime.Now;
                
                if (remaining.TotalSeconds <= 0)
                {
                    m_IsCooldownActive = false;
                    PlayerPrefs.DeleteKey(m_CooldownEndKey);
                    UpdateUI();
                }
                else
                {
                    // Format as MM:SS
                    m_TimerText.text = string.Format("{0:D2}:{1:D2}", remaining.Minutes, remaining.Seconds);
                }
            }
        }

        private void UpdateUI()
        {
            // In case of switch bettwen cooldown to active or viseversa the change should happen immidetly
            if (m_AdButton != null)
            {
                m_AdButton.interactable = !m_IsCooldownActive;
            }

            if (m_AdImage != null)
            {
                m_AdImage.SetActive(!m_IsCooldownActive);
            }

            if (m_TimerText != null)
            {
                m_TimerText.gameObject.SetActive(m_IsCooldownActive);
            }
        }
    }
}
