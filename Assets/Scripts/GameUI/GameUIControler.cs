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
    public Transform GameUIParent => m_GameUI != null ? m_GameUI.transform : transform;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private Animator m_XIndicatAnim;
    [SerializeField] private Image[] m_Hearts;
    [SerializeField] private GameObject m_HintButton;
    [SerializeField] private GameObject m_TimerContainer; // Container for timer UI
    [SerializeField] private TextMeshProUGUI m_TimerText; // Timer display (MM:SS)
    [SerializeField] private TextMeshProUGUI m_FailureTitle; // "Out of Lives!" or "Time's Up!"
    [SerializeField] private TextMeshProUGUI m_FailureSubtitle; // Subtitle text
    [SerializeField] private TextMeshProUGUI m_FailureDescription; // Description text
    
    [Header("Timer Colors")]
    [SerializeField] private Color m_TimerDefaultColor = Color.white; // Default timer color
    [SerializeField] private Color m_TimerWarningColor = Color.red; // Color when 30 seconds or less
    
    private readonly Color activeColor = Color.white; // #FFFFFF
    private readonly Color inactiveColor = new Color(0.616f, 0.616f, 0.616f, 0.5f); // #9D9D9D with 128 alpha (0.5f)

    [SerializeField] private Color[] m_streakColors;
    [SerializeField] private Color m_wrongColor;
    [SerializeField] private Image m_ColorIndication;

    [Header("Combo Timer Indication")]
    [SerializeField] private GameObject m_ComboTimerContainer;
    [SerializeField] private Image m_ComboTimerImage;
    [SerializeField] private TextMeshProUGUI m_StreakText;
    [SerializeField] private TextMeshProUGUI m_LevelHeaderText;

    // Must match the time condition used in ArrowController.OnArrowClicked (0.9f)
    public const float StreakTimeThreshold = 1.0f;
    private Coroutine m_ComboTimerCoroutine;

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
            UpdateLevelHeaderText();
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
        m_ColorIndication.color = m_wrongColor;
        m_XIndicatAnim.SetTrigger("Wrong");
        ResetComboIndication();
    }

    public void PlayStreakAnimation()
    {
        int streakIndex = GameManager.Instance.p_StreakCount % m_streakColors.Length;
        Color streakColor = m_streakColors[streakIndex];
        
        Debug.Log(">>>>STREAK INDEX:" + streakIndex);
        m_ColorIndication.color = streakColor;
        m_XIndicatAnim.SetTrigger("Wrong");

        // Combo Timer Indication
        if (m_ComboTimerContainer != null && m_ComboTimerImage != null)
        {
            m_ComboTimerImage.color = streakColor;
            m_ComboTimerImage.fillAmount = 1f;
            
            if (m_ComboTimerCoroutine != null) StopCoroutine(m_ComboTimerCoroutine);
            m_ComboTimerCoroutine = StartCoroutine(ComboTimerRoutine());
        }

        if (m_StreakText != null)
        {
            m_StreakText.text = "X" + GameManager.Instance.p_StreakCount;
            StartCoroutine(PunchScaleRoutine(m_StreakText.rectTransform, 1.4f, 0.08f, 0.05f));
        }
    }

    private IEnumerator PunchScaleRoutine(RectTransform target, float punchScale, float upDuration, float downDuration)
    {
        if (target == null) yield break;

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

    private IEnumerator ComboTimerRoutine()
    {
        if (m_ComboTimerContainer != null)
        {
            Debug.Log("[GameUIContoleer] Setting ComboTimerContainer ACTIVE");
            m_ComboTimerContainer.SetActive(true);
        }
        
        float elapsed = 0f;
        while (elapsed < StreakTimeThreshold)
        {
            elapsed += Time.deltaTime;
            if (m_ComboTimerImage != null)
            {
                m_ComboTimerImage.fillAmount = 1f - (elapsed / StreakTimeThreshold);
            }
            yield return new WaitForEndOfFrame();
        }

        if (m_ComboTimerImage != null) m_ComboTimerImage.fillAmount = 0f;
        if (m_ComboTimerContainer != null)
        {
            Debug.Log("[GameUIContoleer] Setting ComboTimerContainer INACTIVE (Routine finished)");
            m_ComboTimerContainer.SetActive(false);
        }
        m_ComboTimerCoroutine = null;
    }

    public void ResetComboIndication()
    {
        if (m_ComboTimerCoroutine != null)
        {
            StopCoroutine(m_ComboTimerCoroutine);
            m_ComboTimerCoroutine = null;
        }

        if (m_ComboTimerContainer != null)
        {
            Debug.Log("[GameUIContoleer] Setting ComboTimerContainer INACTIVE (Reset called)");
            m_ComboTimerContainer.SetActive(false);
        }
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
    
        UpdateTimerUI(timeString);

        // Reset timer color to default when level starts
        if (m_TimerText != null)
        {
            m_TimerText.color = m_TimerDefaultColor;
        }

        UpdateLevelHeaderText();

        ResetComboIndication();
    }

    private void UpdateLevelHeaderText()
    {
        if (m_LevelHeaderText == null || GameManager.Instance == null) return;

        if (GameManager.Instance.p_isLevelProgression)
        {
            m_LevelHeaderText.text = $"Level {UserDataManager.Instance.CurrentLevel}";
        }
        else
        {
            int day = GameManager.Instance.currentChallengeDay;
            int month = GameManager.Instance.currentChallengeMonth;
            int year = GameManager.Instance.currentChallengeYear;

            try
            {
                System.DateTime date = new System.DateTime(year, month, day);
                string suffix = GetDaySuffix(day);
                m_LevelHeaderText.text = $"{day}{suffix} {date:MMM}";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameUIContoleer] Error formatting challenge date: {e.Message}");
                m_LevelHeaderText.text = "Challenge Level";
            }
        }
    }

    private string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return "th";

        switch (day % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
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

            if (m_FailureDescription != null)
            {
                m_FailureDescription.text = GameManager.Instance.GetFailureDescription();
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
            AdsManager.Instance.ShowInterstitial(true);
        }
        
        UserDataManager.Instance.ClearLevelProgress();
        SetGameUIVisible(false);
    }

    public void SetGameUIVisible(bool visible)
    {
        if (m_LobbyUI != null) m_LobbyUI.SetActive(!visible);
        if (m_GameUI != null) m_GameUI.SetActive(visible);
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
            GameManager.Instance.p_isHintRewarded = true;
            GameManager.Instance.p_isPlayOnRewarded = false;
            AdsManager.Instance.ShowRewarded();
        }
    }
}
