using UnityEngine;
using TMPro;

/// <summary>
/// Updates the lobby icon with the seasonal timer and unclaimed reward notification (Red Dot + Count).
/// The notification state is explicitly driven by events from LegendPassManager.
/// </summary>
public class LegendPassTimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_TimerText;
    
    [Header("Notification Badge")]
    [SerializeField] private GameObject m_NotificationDot;
    [SerializeField] private TextMeshProUGUI m_NotificationCountText;
    
    [SerializeField] private float m_TimerUpdateInterval = 60f;

    private float m_UpdateTimer;

    private void OnEnable()
    {
        // Initial state sync
        UpdateTimerUI();
        if (LegendPassManager.Instance != null)
        {
            UpdateNotificationUI(LegendPassManager.Instance.GetUnclaimedRewardsCount());
            
            // Subscribe to explicit manager events
            LegendPassManager.Instance.OnUnclaimedCountChanged += UpdateNotificationUI;
            LegendPassManager.Instance.OnProgressChanged += UpdateTimerUI; // For reset/rotation cases
        }
        
        m_UpdateTimer = m_TimerUpdateInterval;
    }

    private void OnDisable()
    {
        if (LegendPassManager.Instance != null)
        {
            LegendPassManager.Instance.OnUnclaimedCountChanged -= UpdateNotificationUI;
            LegendPassManager.Instance.OnProgressChanged -= UpdateTimerUI;
        }
    }

    private void Update()
    {
        // Only the seasonal timer uses a periodic update
        m_UpdateTimer -= Time.deltaTime;
        if (m_UpdateTimer <= 0)
        {
            UpdateTimerUI();
            m_UpdateTimer = m_TimerUpdateInterval;
        }
    }

    private void UpdateTimerUI()
    {
        if (m_TimerText != null && LegendPassManager.Instance != null)
        {
            m_TimerText.text = LegendPassManager.Instance.GetTimerString();
        }
    }

    /// <summary>
    /// This is now explicitly "controlled" by LegendPassManager via event.
    /// </summary>
    private void UpdateNotificationUI(int unclaimedCount)
    {
        bool hasUnclaimed = unclaimedCount > 0;

        if (m_NotificationDot != null)
        {
            m_NotificationDot.SetActive(hasUnclaimed);
        }

        if (m_NotificationCountText != null)
        {
            m_NotificationCountText.text = unclaimedCount.ToString();
        }
        
        Debug.Log($"[LegendPassLobbyUI] Notification Updated via Manager: {unclaimedCount} unclaimed.");
    }
}
