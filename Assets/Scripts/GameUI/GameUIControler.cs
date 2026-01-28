using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Core;
using TMPro;

public class GameUIContoleer : MonoBehaviour
{
    [SerializeField] private GameObject m_LobbyUI;
    [SerializeField] private GameObject m_GameUI;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private Animator m_XIndicatAnim;
    [SerializeField] private Image[] m_Hearts;
    [SerializeField] private GameObject m_HintButton;
    [SerializeField] private GameObject m_TimerContainer; // Container for timer UI
    [SerializeField] private TextMeshProUGUI m_TimerText; // Timer display (MM:SS)
    [SerializeField] private TextMeshProUGUI m_FailureTitle; // "Out of Lives!" or "Time's Up!"
    [SerializeField] private TextMeshProUGUI m_FailureSubtitle; // Subtitle text
    
    [Header("Timer Colors")]
    [SerializeField] private Color m_TimerDefaultColor = Color.white; // Default timer color
    [SerializeField] private Color m_TimerWarningColor = Color.red; // Color when 30 seconds or less
    
    private readonly Color activeColor = Color.white; // #FFFFFF
    private readonly Color inactiveColor = new Color(0.616f, 0.616f, 0.616f, 0.5f); // #9D9D9D with 128 alpha (0.5f)

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged += UpdateLivesUI;
            GameManager.Instance.OnHintVisibilityChanged += ToggleHintButton;
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
            GameManager.Instance.OnGameOver += OnGameOver;
            UpdateLivesUI(GameManager.Instance.CurrentLives);
            ToggleHintButton(false); // Hide by default
            UpdateTimerVisibility();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged -= UpdateLivesUI;
            GameManager.Instance.OnHintVisibilityChanged -= ToggleHintButton;
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
            GameManager.Instance.OnGameOver -= OnGameOver;
        }
    }

    private void UpdateLivesUI(int currentLives)
    {
        if (m_Hearts == null) return;

        for (int i = 0; i < m_Hearts.Length; i++)
        {
            if (m_Hearts[i] != null)
            {
                m_Hearts[i].color = (i < currentLives) ? activeColor : inactiveColor;
            }
        }
    }
    
    public void PlayWrongAnimation()
    {
        m_XIndicatAnim.SetTrigger("Wrong");
    }

    public void UpdateTimerUI(string timeString)
    {
        if (m_TimerText != null)
        {
            m_TimerText.text = timeString;
            
            // Update color based on remaining time
            if (GameManager.Instance != null && (GameManager.Instance.IsTimedLevel || GameManager.Instance.LevelDuration>0))
            {
                float remainingTime = GameManager.Instance.CurrentTime;
                m_TimerText.color = remainingTime <= 30f ? m_TimerWarningColor : m_TimerDefaultColor;
            }
        }
    }
    
    private void UpdateTimerVisibility()
    {
        if (m_TimerContainer != null && GameManager.Instance != null)
        {
            m_TimerContainer.SetActive(GameManager.Instance.IsTimedLevel);
        }
    }
    
    private void OnLevelStarted()
    {
        UpdateTimerVisibility();
        int minutes = Mathf.FloorToInt(GameManager.Instance.CurrentTime / 60f);
        int seconds = Mathf.FloorToInt(GameManager.Instance.CurrentTime % 60f);
        string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);
        Debug.Log(">>>>Time String: " + timeString);
        UpdateTimerUI(timeString);

        // Reset timer color to default when level starts
        if (m_TimerText != null)
        {
            m_TimerText.color = m_TimerDefaultColor;
        }
    }
    
    private void OnGameOver()
    {
        UpdateFailureScreenText();
    }
    
    private void UpdateFailureScreenText()
    {
        if (GameManager.Instance != null)
        {
            if (m_FailureTitle != null)
            {
                m_FailureTitle.text = GameManager.Instance.GetFailureTitle();
            }
            
            if (m_FailureSubtitle != null)
            {
                m_FailureSubtitle.text = GameManager.Instance.GetFailureSubtitle();
            }
        }
    }

    public void restartLevel()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayClick();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartCurrentLevel();
        }
    }

    public void BackToLobby()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowInterstitial();
        }
        
        m_LobbyUI.SetActive(true);
        m_GameUI.SetActive(false);
    }

    private void ToggleHintButton(bool visible)
    {
        if (m_HintButton != null)
        {
            m_HintButton.SetActive(visible);
        }
    }

    public void OnHintButtonClicked()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowRewarded();
            GameManager.Instance.p_isHintRewarded = true;
            GameManager.Instance.p_isPlayOnRewarded = false;
        }
    }
}
