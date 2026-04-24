using UnityEngine;
using TMPro;

/// <summary>
/// Updates the lobby icon with the seasonal timer and unclaimed reward notification (Red Dot + Count).
/// </summary>
public class LegendPassTimerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_TimerText;
    
    [Header("Notification Badge")]
    [SerializeField] private GameObject m_NotificationDot;
    [SerializeField] private TextMeshProUGUI m_NotificationCountText;
    
    [SerializeField] private float m_UpdateInterval = 30f;

    private float m_Timer;

    private void OnEnable()
    {
        RefreshLobbyState();
        m_Timer = m_UpdateInterval;
        
        if (LegendPassManager.Instance != null)
        {
            LegendPassManager.Instance.OnProgressChanged += RefreshLobbyState;
        }
    }

    private void OnDisable()
    {
        if (LegendPassManager.Instance != null)
        {
            LegendPassManager.Instance.OnProgressChanged -= RefreshLobbyState;
        }
    }

    private void Update()
    {
        m_Timer -= Time.deltaTime;
        if (m_Timer <= 0)
        {
            RefreshLobbyState();
            m_Timer = m_UpdateInterval;
        }
    }

    private void RefreshLobbyState()
    {
        if (LegendPassManager.Instance == null) return;

        // 1. Update Timer
        if (m_TimerText != null)
        {
            m_TimerText.text = LegendPassManager.Instance.GetTimerString();
        }

        // 2. Update Red Dot and Count
        int unclaimedCount = LegendPassManager.Instance.GetUnclaimedRewardsCount();
        bool hasUnclaimed = unclaimedCount > 0;

        if (m_NotificationDot != null)
        {
            m_NotificationDot.SetActive(hasUnclaimed);
        }

        if (m_NotificationCountText != null)
        {
            m_NotificationCountText.text = unclaimedCount.ToString();
        }
    }
}
