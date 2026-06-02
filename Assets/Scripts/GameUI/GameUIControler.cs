using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Core;
using Assets.Scripts.GameUI;
using Assets.Scripts.Lobby;
using TMPro;

public class GameUIContoleer : MonoBehaviour
{
    public enum BoosterType { Magic, Hint, Refill, Shuffle }
    [SerializeField] private GameObject m_LobbyUI;
    [SerializeField] private GameObject m_GameUI;
    public GameManager gameManager;
    public Transform GameUIParent => m_GameUI != null ? m_GameUI.transform : transform;
    [SerializeField] private GameObject m_HeartsContainer; // For organization
    [SerializeField] private GameObject m_BoostersPanel;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private Animator m_XIndicatAnim;
    [SerializeField] private Image[] m_Hearts;
    [SerializeField] private TextMeshProUGUI m_HintBoosterText;
    [SerializeField] private GameObject m_HintBalance;
    [SerializeField] private GameObject m_HintIcon;
    [SerializeField] private GameObject m_HintAd;
    [SerializeField] private GameObject m_HintLockIcon;
    [SerializeField] private GameObject m_HintTooltip;
    private Coroutine m_HintTooltipCoroutine;
    [SerializeField] private GameObject m_TimerContainer; // Container for timer UI
    [SerializeField] private TextMeshProUGUI m_TimerText; // Timer display (MM:SS)
    [SerializeField] private TextMeshProUGUI m_FailureTitle; // "Out of Lives!" or "Time's Up!"
    [SerializeField] private TextMeshProUGUI m_FailureSubtitle; // Subtitle text
    [SerializeField] private TextMeshProUGUI m_FailureAdText; // Ad text
    [SerializeField] private TextMeshProUGUI m_FailureDescription; // Description text
    [Header("Failure Screen - One Life Play On")]
    [SerializeField] private GameObject m_LHeartObj;
    [SerializeField] private GameObject m_RHeartObj;
    [SerializeField] private GameObject m_PlusObj;
    [SerializeField] private GameObject m_NoAdsOfferImage; // Image for the special offer (coins + no ads)
    [SerializeField] private GameObject m_PlayOnAdButton; // Button to watch ad for PlayOn
    [SerializeField] private TextMeshProUGUI m_MagicBoosterText; // Display magic booster count
    [SerializeField] private GameObject m_MagicBalance;
    [SerializeField] private GameObject m_MagicIcon;
    [SerializeField] private GameObject m_MagicAd;
    [SerializeField] private GameObject m_MagicLockIcon;
    [SerializeField] private GameObject m_MagicTooltip;
    [SerializeField] private TextMeshProUGUI m_RefillBoosterText;
    [SerializeField] private GameObject m_RefillBalance;
    [SerializeField] private GameObject m_RefillIcon;
    [SerializeField] private GameObject m_RefillAd;
    [SerializeField] private GameObject m_RefillLockIcon;
    [SerializeField] private GameObject m_RefillTooltip;
    [SerializeField] private GameObject m_RefillFullLivesTooltip;
    [SerializeField] private TextMeshProUGUI m_ShuffleBoosterText;
    [SerializeField] private GameObject m_ShuffleBalance;
    [SerializeField] private GameObject m_ShuffleIcon;
    [SerializeField] private GameObject m_ShuffleAd;
    [SerializeField] private GameObject m_ShuffleLockIcon;
    [SerializeField] private GameObject m_ShuffleTooltip;
    private Coroutine m_ShuffleTooltipCoroutine;
    
    [Header("Booster Button Nudge")]
    [SerializeField] private RectTransform m_HintBoosterButton;
    [SerializeField] private RectTransform m_MagicBoosterButton;
    [SerializeField] private RectTransform m_RefillBoosterButton;
    [SerializeField] private RectTransform m_ShuffleBoosterButton;
    private const float RefillNudgeDelay = 0.7f;
    private const float IdleBoosterNudgeThreshold = 5f;
    private const int IdleBoosterMinLevel = 15;
    private float m_idleBoosterTimer;
    private int m_lastDisplayedLives = -1;
    private Coroutine m_refillNudgeDelayCoroutine;
    private Coroutine m_refillNudgeCoroutine;
    private Coroutine m_hintNudgeCoroutine;
    private Coroutine m_magicNudgeCoroutine;
    private Coroutine m_shuffleNudgeCoroutine;

    private enum BoosterNudgeStyle
    {
        SinglePulse,
        HeartbeatDouble
    }
    private bool m_boosterRestPosesCaptured;
    private BoosterButtonRestPose? m_hintRestPose;
    private BoosterButtonRestPose? m_magicRestPose;
    private BoosterButtonRestPose? m_refillRestPose;
    private BoosterButtonRestPose? m_shuffleRestPose;

    private struct BoosterButtonRestPose
    {
        public Vector2 AnchoredPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    [Header("Booster Visual Feedback")]
    [SerializeField] private RectTransform m_BoosterOverlayParent;
    [SerializeField] private GameObject m_BoosterImagePrefab; // Prefab with Image component
    [SerializeField] private Sprite m_MagicBoosterFeedbackSprite;
    [SerializeField] private Sprite m_HintBoosterFeedbackSprite;
    [SerializeField] private Sprite m_RefillBoosterFeedbackSprite;
    [SerializeField] private Sprite m_ShuffleBoosterFeedbackSprite;

    private Coroutine m_RefillTooltipCoroutine;
    private Coroutine m_RefillFullTooltipCoroutine;
    private Coroutine m_MagicTooltipCoroutine;
    
    [Header("Restart Button Fade")]
    [SerializeField] private GameObject m_RestartButton;
    [SerializeField] private Image m_RestartButtonImage;
    [SerializeField] private TextMeshProUGUI m_RestartButtonText;
    private Coroutine m_RestartButtonFadeCoroutine;
    private Coroutine m_AdButtonRefreshCoroutine;

    
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
        CaptureBoosterButtonRestPoses();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged += UpdateLivesUI;
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
            GameManager.Instance.OnGameOver += OnGameOver;
            UpdateLivesUI(GameManager.Instance.CurrentLives);
            ToggleHintButton(false); // Hide by default
            UpdateTimerVisibility();
            UpdateLevelHeaderText();
            UpdateBoostersPanelVisibility();
            UpdateMagicBoosterUI(UserDataManager.Instance.MagicBoosterCount);
            UserDataManager.Instance.OnMagicBoosterChanged += UpdateMagicBoosterUI;
            UpdateHintBoosterUI(UserDataManager.Instance.HintBoosterCount);
            UserDataManager.Instance.OnHintBoosterChanged += UpdateHintBoosterUI;
            UpdateRefillBoosterUI(UserDataManager.Instance.RefillBoosterCount);
            UserDataManager.Instance.OnRefillBoosterChanged += UpdateRefillBoosterUI;
            UpdateShuffleBoosterUI(UserDataManager.Instance.ShuffleBoosterCount);
            UserDataManager.Instance.OnShuffleBoosterChanged += UpdateShuffleBoosterUI;

            if (RemoteConfigManager.Instance != null)
            {
                RemoteConfigManager.Instance.OnConfigInitialized += OnRemoteConfigInitialized;
            }
        }
    }

    private void OnRemoteConfigInitialized()
    {
        UpdateShuffleBoosterUI(UserDataManager.Instance != null ? UserDataManager.Instance.ShuffleBoosterCount : 0);
    }

    private void Update()
    {
        #if UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (m_GameUI != null && m_GameUI.activeSelf)
            {
                BackToLobby();
            }
        }
        #endif

        UpdateIdleBoosterNudge();
    }

    private void OnEnable()
    {
        ResetBoosterButtonsToRestPose();

        UpdateMagicBoosterUI(UserDataManager.Instance.MagicBoosterCount);
        UpdateHintBoosterUI(UserDataManager.Instance.HintBoosterCount);
        UpdateRefillBoosterUI(UserDataManager.Instance.RefillBoosterCount);
        UpdateShuffleBoosterUI(UserDataManager.Instance.ShuffleBoosterCount);
    }

    private void OnDisable()
    {
        ResetBoosterButtonsToRestPose();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged -= UpdateLivesUI;
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
            GameManager.Instance.OnGameOver -= OnGameOver;
        }

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnMagicBoosterChanged -= UpdateMagicBoosterUI;
            UserDataManager.Instance.OnHintBoosterChanged -= UpdateHintBoosterUI;
            UserDataManager.Instance.OnRefillBoosterChanged -= UpdateRefillBoosterUI;
            UserDataManager.Instance.OnShuffleBoosterChanged -= UpdateShuffleBoosterUI;
        }

        if (RemoteConfigManager.Instance != null)
        {
            RemoteConfigManager.Instance.OnConfigInitialized -= OnRemoteConfigInitialized;
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

        if (m_lastDisplayedLives != 1 && currentLives == 1)
        {
            ScheduleRefillButtonNudge();
        }
        else if (currentLives != 1)
        {
            CancelRefillButtonNudge();
        }

        m_lastDisplayedLives = currentLives;
    }

    public void ResetIdleBoosterNudgeTimer()
    {
        m_idleBoosterTimer = 0f;
    }

    private void UpdateIdleBoosterNudge()
    {
        if (GameManager.Instance == null || !GameManager.Instance.CanInteract) return;
        if (UserDataManager.Instance == null || UserDataManager.Instance.CurrentLevel <= IdleBoosterMinLevel) return;

        m_idleBoosterTimer += Time.deltaTime;
        if (m_idleBoosterTimer < IdleBoosterNudgeThreshold) return;

        RectTransform target = PickIdleNudgeTarget();
        m_idleBoosterTimer = 0f;
        if (target != null)
        {
            PlayBoosterButtonNudge(target, BoosterNudgeStyle.SinglePulse);
        }
    }

    private RectTransform PickIdleNudgeTarget()
    {
        int level = UserDataManager.Instance.CurrentLevel;
        bool hintAvailable = level >= GameManager.HINT_BOOSTER_UNLOCK_LEVEL && m_HintBoosterButton != null;
        bool magicAvailable = level >= GameManager.MAGIC_BOOSTER_UNLOCK_LEVEL && m_MagicBoosterButton != null;

        if (hintAvailable && magicAvailable)
        {
            return Random.value < 0.5f ? m_HintBoosterButton : m_MagicBoosterButton;
        }

        if (hintAvailable) return m_HintBoosterButton;
        if (magicAvailable) return m_MagicBoosterButton;
        return null;
    }

    private void ScheduleRefillButtonNudge()
    {
        CancelRefillButtonNudge();
        if (m_RefillBoosterButton == null || GameManager.Instance == null || !GameManager.Instance.CanInteract) return;
        m_refillNudgeDelayCoroutine = StartCoroutine(RefillButtonNudgeDelayedRoutine());
    }

    private void CancelRefillButtonNudge()
    {
        if (m_refillNudgeDelayCoroutine != null)
        {
            StopCoroutine(m_refillNudgeDelayCoroutine);
            m_refillNudgeDelayCoroutine = null;
        }
    }

    private IEnumerator RefillButtonNudgeDelayedRoutine()
    {
        yield return new WaitForSeconds(RefillNudgeDelay);

        if (GameManager.Instance == null || !GameManager.Instance.CanInteract || GameManager.Instance.CurrentLives != 1)
        {
            m_refillNudgeDelayCoroutine = null;
            yield break;
        }

        PlayBoosterButtonNudge(m_RefillBoosterButton, BoosterNudgeStyle.HeartbeatDouble);
        m_refillNudgeDelayCoroutine = null;
    }

    private void PlayBoosterButtonNudge(RectTransform target, BoosterNudgeStyle style)
    {
        if (target == null) return;

        ref Coroutine nudgeCoroutine = ref GetNudgeCoroutineRef(target);
        if (nudgeCoroutine != null)
        {
            StopCoroutine(nudgeCoroutine);
        }

        nudgeCoroutine = StartCoroutine(BoosterButtonNudgeRoutine(target, style));
    }

    private ref Coroutine GetNudgeCoroutineRef(RectTransform target)
    {
        if (target == m_HintBoosterButton) return ref m_hintNudgeCoroutine;
        if (target == m_MagicBoosterButton) return ref m_magicNudgeCoroutine;
        return ref m_refillNudgeCoroutine;
    }

    private void CaptureBoosterButtonRestPoses()
    {
        if (m_HintBoosterButton != null)
        {
            m_hintRestPose = CreateRestPose(m_HintBoosterButton);
        }

        if (m_MagicBoosterButton != null)
        {
            m_magicRestPose = CreateRestPose(m_MagicBoosterButton);
        }

        if (m_RefillBoosterButton != null)
        {
            m_refillRestPose = CreateRestPose(m_RefillBoosterButton);
        }

        m_boosterRestPosesCaptured = true;
    }

    private static BoosterButtonRestPose CreateRestPose(RectTransform target)
    {
        return new BoosterButtonRestPose
        {
            AnchoredPosition = target.anchoredPosition,
            LocalRotation = target.localRotation,
            LocalScale = target.localScale
        };
    }

    private static void ApplyRestPose(RectTransform target, BoosterButtonRestPose restPose)
    {
        if (target == null) return;

        target.anchoredPosition = restPose.AnchoredPosition;
        target.localRotation = restPose.LocalRotation;
        target.localScale = restPose.LocalScale;
    }

    private bool TryGetRestPose(RectTransform target, out BoosterButtonRestPose restPose)
    {
        if (!m_boosterRestPosesCaptured)
        {
            CaptureBoosterButtonRestPoses();
        }

        if (target == m_HintBoosterButton && m_hintRestPose.HasValue)
        {
            restPose = m_hintRestPose.Value;
            return true;
        }

        if (target == m_MagicBoosterButton && m_magicRestPose.HasValue)
        {
            restPose = m_magicRestPose.Value;
            return true;
        }

        if (target == m_RefillBoosterButton && m_refillRestPose.HasValue)
        {
            restPose = m_refillRestPose.Value;
            return true;
        }

        restPose = default;
        return false;
    }

    public void ResetBoosterButtonsToRestPose()
    {
        CancelRefillButtonNudge();
        StopAllBoosterNudgeCoroutines();
        ResetIdleBoosterNudgeTimer();

        if (!m_boosterRestPosesCaptured)
        {
            CaptureBoosterButtonRestPoses();
        }

        if (m_hintRestPose.HasValue)
        {
            ApplyRestPose(m_HintBoosterButton, m_hintRestPose.Value);
        }

        if (m_magicRestPose.HasValue)
        {
            ApplyRestPose(m_MagicBoosterButton, m_magicRestPose.Value);
        }

        if (m_refillRestPose.HasValue)
        {
            ApplyRestPose(m_RefillBoosterButton, m_refillRestPose.Value);
        }
    }

    private void StopAllBoosterNudgeCoroutines()
    {
        if (m_hintNudgeCoroutine != null)
        {
            StopCoroutine(m_hintNudgeCoroutine);
            m_hintNudgeCoroutine = null;
        }

        if (m_magicNudgeCoroutine != null)
        {
            StopCoroutine(m_magicNudgeCoroutine);
            m_magicNudgeCoroutine = null;
        }

        if (m_refillNudgeCoroutine != null)
        {
            StopCoroutine(m_refillNudgeCoroutine);
            m_refillNudgeCoroutine = null;
        }
    }

    private IEnumerator BoosterButtonNudgeRoutine(RectTransform target, BoosterNudgeStyle style)
    {
        if (!TryGetRestPose(target, out BoosterButtonRestPose restPose))
        {
            GetNudgeCoroutineRef(target) = null;
            yield break;
        }

        Vector3 restScale = restPose.LocalScale;

        switch (style)
        {
            case BoosterNudgeStyle.SinglePulse:
                yield return BoosterScalePulseRoutine(target, restScale, 1.12f, 0.18f, 0.22f);
                break;
            case BoosterNudgeStyle.HeartbeatDouble:
                PlayRefillHeartNudgeSound();
                yield return BoosterScalePulseRoutine(target, restScale, 1.1f, 0.2f, 0.28f);
                yield return new WaitForSeconds(0.25f);
                PlayRefillHeartNudgeSound();
                yield return BoosterScalePulseRoutine(target, restScale, 1.1f, 0.2f, 0.28f);
                break;
        }

        ApplyRestPose(target, restPose);
        GetNudgeCoroutineRef(target) = null;
    }

    private static void PlayRefillHeartNudgeSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayHeartNudge();
        }
    }

    private static IEnumerator BoosterScalePulseRoutine(
        RectTransform target,
        Vector3 restScale,
        float peakMultiplier,
        float upDuration,
        float downDuration)
    {
        if (target == null) yield break;

        Vector3 peakScale = restScale * peakMultiplier;
        float elapsed = 0f;

        while (elapsed < upDuration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(restScale, peakScale, elapsed / upDuration);
            yield return null;
        }
        target.localScale = peakScale;

        elapsed = 0f;
        while (elapsed < downDuration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(peakScale, restScale, elapsed / downDuration);
            yield return null;
        }
        target.localScale = restScale;
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
            m_StreakText.text = "X" + GameManager.Instance.p_ComboMultiplier;
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
        StopFailureFadeCoroutine();
        StopAdButtonRefreshCoroutine();

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

        UpdateMagicBoosterUI(UserDataManager.Instance.MagicBoosterCount);
        UpdateHintBoosterUI(UserDataManager.Instance.HintBoosterCount);
        UpdateRefillBoosterUI(UserDataManager.Instance.RefillBoosterCount);
        UpdateShuffleBoosterUI(UserDataManager.Instance.ShuffleBoosterCount);
        UpdateBoostersPanelVisibility();
        UpdateLevelHeaderText();

        ResetComboIndication();
        ResetBoosterButtonsToRestPose();
        m_lastDisplayedLives = GameManager.Instance != null ? GameManager.Instance.CurrentLives : -1;
    }

    private void UpdateLevelHeaderText()
    {
        if (m_LevelHeaderText == null || GameManager.Instance == null) return;

        m_LevelHeaderText.isRightToLeftText = false;
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
                m_LevelHeaderText.text = $"{day}{suffix} {date.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)}";
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
        ResetBoosterButtonsToRestPose();

        UpdateFailureScreenText();
        
        if (m_RestartButtonFadeCoroutine != null) StopCoroutine(m_RestartButtonFadeCoroutine);
        m_RestartButtonFadeCoroutine = StartCoroutine(RestartButtonFadeRoutine());

        if (m_AdButtonRefreshCoroutine != null) StopCoroutine(m_AdButtonRefreshCoroutine);

        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.OnAdReadinessChanged -= UpdateAdButtonStatus;
            AdsManager.Instance.OnAdReadinessChanged += UpdateAdButtonStatus;
        }

        UpdateAdButtonStatus();
        m_AdButtonRefreshCoroutine = StartCoroutine(AdButtonRefreshRoutine());
    }



    public void StopFailureFadeCoroutine()
    {
        if (m_RestartButtonFadeCoroutine != null)
        {
            StopCoroutine(m_RestartButtonFadeCoroutine);
            m_RestartButtonFadeCoroutine = null;
        }

        StopAdButtonRefreshCoroutine();

        
        // Ensure button is reset to a visible state if needed, or hidden if we are starting a level
        // For now, let's just make sure it's not stuck in a half-faded state if we exit failure screen
        SetRestartButtonAlpha(1f);
    }

    private IEnumerator RestartButtonFadeRoutine()
    {
        if (m_RestartButton == null) yield break;

        // Hide immediately
        m_RestartButton.SetActive(false);
        SetRestartButtonAlpha(0f);

        // Wait 3 seconds
        yield return new WaitForSeconds(2f);

        // Fade in
        m_RestartButton.SetActive(true);
        float elapsed = 0f;
        float duration = 1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetRestartButtonAlpha(alpha);
            yield return null;
        }
        SetRestartButtonAlpha(1f);
        m_RestartButtonFadeCoroutine = null;
    }

    public void StopAdButtonRefreshCoroutine()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.OnAdReadinessChanged -= UpdateAdButtonStatus;
        }

        if (m_AdButtonRefreshCoroutine != null)
        {
            StopCoroutine(m_AdButtonRefreshCoroutine);
            m_AdButtonRefreshCoroutine = null;
        }
    }

    private IEnumerator AdButtonRefreshRoutine()
    {
        // Event-driven updates via OnAdReadinessChanged; infrequent fallback poll only.
        while (true)
        {
            yield return new WaitForSeconds(4f);

            if (AdsManager.Instance == null) yield break;

            UpdateAdButtonStatus();
        }
    }

    private void UpdateAdButtonStatus()
    {
        // Guard against destroyed button or missing instance
        if (m_PlayOnAdButton == null || AdsManager.Instance == null) return;
        
        // If not initialized yet, don't show but don't stop (might initialize soon)
        if (!AdsManager.Instance.IsInitialized)
        {
            if (m_PlayOnAdButton.activeSelf) m_PlayOnAdButton.SetActive(false);
            return;
        }

        bool isAdReady = AdsManager.Instance.IsRewardedReady || AdsManager.Instance.IsInterstitialReady;
        
        // Only call SetActive if state actually changed to avoid overhead
        if (m_PlayOnAdButton.activeSelf != isAdReady)
        {
            Debug.Log($"[GameUIContoleer] Ad availability updated. Play-On Reward Ad Ready: {isAdReady}");
            m_PlayOnAdButton.SetActive(isAdReady);
        }
    }


    private void SetRestartButtonAlpha(float alpha)

    {
        if (m_RestartButtonImage != null)
        {
            Color c = m_RestartButtonImage.color;
            c.a = alpha;
            m_RestartButtonImage.color = c;
        }
        if (m_RestartButtonText != null)
        {
            m_RestartButtonText.alpha = alpha;
        }
    }
    
    private void UpdateFailureScreenOneLifeVisuals()
    {
        bool oneLifeMode = GameManager.Instance != null && GameManager.Instance.IsOneLifePlayOnEnabled();

        if (m_LHeartObj != null)
        {
            m_LHeartObj.SetActive(!oneLifeMode);
        }

        if (m_RHeartObj != null)
        {
            m_RHeartObj.SetActive(!oneLifeMode);
        }

        if (m_PlusObj != null)
        {
            m_PlusObj.SetActive(oneLifeMode);
        }
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

            if (m_FailureAdText != null)
            {
                m_FailureAdText.text = GameManager.Instance.GetFailureAdText();
            }

            if (m_FailureDescription != null)
            {
                m_FailureDescription.text = GameManager.Instance.GetFailureDescription();
            }

            UpdateFailureScreenOneLifeVisuals();
        }

        if (m_NoAdsOfferImage != null)
        {
            bool hasNoAds = IAPManager.Instance != null && IAPManager.Instance.HasNoAds;
            m_NoAdsOfferImage.SetActive(!hasNoAds);
        }

        if (m_PlayOnAdButton != null && AdsManager.Instance != null)
        {
            bool isAdReady = AdsManager.Instance.IsRewardedReady || AdsManager.Instance.IsInterstitialReady;
            m_PlayOnAdButton.SetActive(isAdReady);
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
        if (AdsManager.Instance != null && GameManager.Instance != null && GameManager.Instance.PickedArrowsCount > 0)
        {
           AdsManager.Instance.ShowInterstitial(true);
        }
        
        if (GameManager.Instance == null || GameManager.Instance.p_isLevelProgression)
        {
            UserDataManager.Instance.ClearLevelProgress();
        }
        if (GameManager.Instance != null) GameManager.Instance.HideScreens();
        SetGameUIVisible(false);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ScheduleLobbyStreakRefresh();
        }
    }

    public void SetGameUIVisible(bool visible, bool notifyLobbyWhenHidden = true)
    {
        if (!visible)
        {
            ResetBoosterButtonsToRestPose();
        }

        if (m_LobbyUI != null) m_LobbyUI.SetActive(!visible);
        if (m_GameUI != null) m_GameUI.SetActive(visible);

        // GameUIController is on TopBar; lobby refresh used to disable this object directly.
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (visible)
        {
            ResetBoosterButtonsToRestPose();
        }

        if (!visible && notifyLobbyWhenHidden)
        {
            NotifyLobbyShown();
        }
    }

    private void NotifyLobbyShown()
    {
        HomeContoller home = FindFirstObjectByType<HomeContoller>();
        if (home != null)
        {
            home.RefreshLobbyUI();
        }
    }

    public MultiplyCoinsPopup ShowMultiplyCoinsPopup(int coinsWon)
    {
        GameObject prefab = Resources.Load<GameObject>("MultiplayerPopup");
        if (prefab == null)
        {
            Debug.LogWarning("[GameUIContoleer] MultiplyCoinsPopup prefab not found in Resources as 'MultiplayerPopup'.");
            return null;
        }
        
        GameObject popupGO = Instantiate(prefab, null);
        popupGO.SetActive(true);
        MultiplyCoinsPopup popup = popupGO.GetComponent<MultiplyCoinsPopup>();
        if (popup != null)
        {
            popup.Setup(coinsWon);
        }
        return popup;
    }

    private void ToggleHintButton(bool visible)
    {
    }

    private static void LogBoosterClicked(string eventName)
    {
        if (FirebaseManager.Instance == null) return;
        FirebaseManager.Instance.LogEvent(eventName);
    }

    public void OnHintButtonClicked()
    {
        LogBoosterClicked(FirebaseManager.EVENT_BOOSTER_HINT_CLICKED);
        ResetIdleBoosterNudgeTimer();

        if (UserDataManager.Instance.CurrentLevel < GameManager.HINT_BOOSTER_UNLOCK_LEVEL)
        {
            HideAllBoosterTooltips();
            m_HintTooltipCoroutine = StartCoroutine(ShowHintTooltipCoroutine());
            return;
        }

        if (UserDataManager.Instance.HintBoosterCount > 0)
        {
            if (UserDataManager.Instance.UseHintBooster(1))
            {
                StartCoroutine(BoosterSequence(m_HintBoosterFeedbackSprite, BoosterType.Hint, () => {
                    GameManager.Instance.ShowHint();
                }));
            }
        }
        else
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForHint();
            }
        }
    }

    public void HandleHintRewardReceived()
    {
        StartCoroutine(BoosterSequence(m_HintBoosterFeedbackSprite, BoosterType.Hint, () => {
            GameManager.Instance.ShowHint();
        }));
    }

    private void UpdateHintBoosterUI(int count)
    {
        bool isLocked = UserDataManager.Instance.CurrentLevel < GameManager.HINT_BOOSTER_UNLOCK_LEVEL;

        if (m_HintLockIcon != null)
        {
            m_HintLockIcon.SetActive(isLocked);
        }

        if (isLocked)
        {
            if (m_HintBoosterText != null) m_HintBoosterText.gameObject.SetActive(false);
            if (m_HintAd != null) m_HintAd.SetActive(true);
            if (m_HintBalance != null) m_HintBalance.SetActive(false);
            if (m_HintIcon != null) m_HintIcon.SetActive(false);
        }
        else
        {
            if (m_HintIcon != null) m_HintIcon.SetActive(true);
            if (m_HintLockIcon != null) m_HintLockIcon.SetActive(false);
            
            if (count > 0)
            {
                if (m_HintBoosterText != null)
                {
                    m_HintBoosterText.gameObject.SetActive(true);
                    m_HintBoosterText.text = count.ToString();
                }
                if (m_HintBalance != null) m_HintBalance.SetActive(true);
                if (m_HintAd != null) m_HintAd.SetActive(false);
            }
            else
            {
                if (m_HintBoosterText != null) m_HintBoosterText.gameObject.SetActive(false);
                if (m_HintBalance != null) m_HintBalance.SetActive(false);
                if (m_HintAd != null) m_HintAd.SetActive(true);
            }
        }
    }
    private void HideAllBoosterTooltips()
    {
        if (m_MagicTooltipCoroutine != null) StopCoroutine(m_MagicTooltipCoroutine);
        if (m_HintTooltipCoroutine != null) StopCoroutine(m_HintTooltipCoroutine);
        if (m_RefillTooltipCoroutine != null) StopCoroutine(m_RefillTooltipCoroutine);
        if (m_RefillFullTooltipCoroutine != null) StopCoroutine(m_RefillFullTooltipCoroutine);

        if (m_MagicTooltip != null) m_MagicTooltip.SetActive(false);
        if (m_HintTooltip != null) m_HintTooltip.SetActive(false);
        if (m_RefillTooltip != null) m_RefillTooltip.SetActive(false);
        if (m_RefillFullLivesTooltip != null) m_RefillFullLivesTooltip.SetActive(false);
        if (m_ShuffleTooltipCoroutine != null) StopCoroutine(m_ShuffleTooltipCoroutine);
        if (m_ShuffleTooltip != null) m_ShuffleTooltip.SetActive(false);

        m_MagicTooltipCoroutine = null;
        m_HintTooltipCoroutine = null;
        m_RefillTooltipCoroutine = null;
        m_RefillFullTooltipCoroutine = null;
        m_ShuffleTooltipCoroutine = null;
    }

    private System.Collections.IEnumerator ShowHintTooltipCoroutine()
    {
        if (m_HintTooltip != null)
        {
            m_HintTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_HintTooltip.SetActive(false);
        }
        m_HintTooltipCoroutine = null;
    }

    private void UpdateMagicBoosterUI(int count)
    {
        bool isLocked = UserDataManager.Instance.CurrentLevel < GameManager.MAGIC_BOOSTER_UNLOCK_LEVEL;

        if (m_MagicLockIcon != null)
        {
            m_MagicLockIcon.SetActive(isLocked);
        }

        if (isLocked)
        {
            m_MagicBoosterText.gameObject.SetActive(false);
            m_MagicAd.SetActive(true);
            m_MagicBalance.SetActive(false);
            m_MagicIcon.SetActive(false);
        }
        else
        {
            m_MagicIcon.SetActive(true);
            m_MagicLockIcon.SetActive(false);
            if (count > 0)
            {
                if (m_MagicBoosterText != null)
                {
                    m_MagicBoosterText.gameObject.SetActive(true);
                    m_MagicBoosterText.text = count.ToString();
                }
                 m_MagicBalance.SetActive(true);
                 m_MagicAd.SetActive(false);
            }
            else
            {
                if (m_MagicBoosterText != null) m_MagicBoosterText.gameObject.SetActive(false);
                 m_MagicBalance.SetActive(false);
                 m_MagicAd.SetActive(true);
            }
        }
    }

    public void OnMagicButtonClicked()
    {
        LogBoosterClicked(FirebaseManager.EVENT_BOOSTER_MAGIC_CLICKED);
        ResetIdleBoosterNudgeTimer();

        if (UserDataManager.Instance.CurrentLevel < GameManager.MAGIC_BOOSTER_UNLOCK_LEVEL)
        {
            HideAllBoosterTooltips();
            m_MagicTooltipCoroutine = StartCoroutine(ShowMagicTooltipCoroutine());
            return;
        }

        if (UserDataManager.Instance.MagicBoosterCount > 0)
        {
            if (UserDataManager.Instance.UseMagicBooster(1))
            {
                StartCoroutine(BoosterSequence(m_MagicBoosterFeedbackSprite, BoosterType.Magic, () => {
                    GameManager.Instance.ExecuteMagicBooster();
                }));
            }
        }
        else
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForMagic();
            }
        }
    }

    public void HandleMagicRewardReceived()
    {
        StartCoroutine(BoosterSequence(m_MagicBoosterFeedbackSprite, BoosterType.Magic, () => {
            GameManager.Instance.ExecuteMagicBooster();
        }));
    }

    private System.Collections.IEnumerator ShowMagicTooltipCoroutine()
    {
        if (m_MagicTooltip != null)
        {
            m_MagicTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_MagicTooltip.SetActive(false);
        }
        m_MagicTooltipCoroutine = null;
    }

    public void OnRefillButtonClicked()
    {
        LogBoosterClicked(FirebaseManager.EVENT_BOOSTER_REFILL_CLICKED);
        ResetIdleBoosterNudgeTimer();

        if (UserDataManager.Instance.CurrentLevel < GameManager.REFILL_BOOSTER_UNLOCK_LEVEL)
        {
            HideAllBoosterTooltips();
            m_RefillTooltipCoroutine = StartCoroutine(ShowRefillTooltipCoroutine());
            return;
        }

        if (GameManager.Instance.CurrentLives >= 3)
        {
            HideAllBoosterTooltips();
            m_RefillFullTooltipCoroutine = StartCoroutine(ShowRefillFullTooltipCoroutine());
            return;
        }

        if (UserDataManager.Instance.RefillBoosterCount > 0)
        {
            if (UserDataManager.Instance.UseRefillBooster(1))
            {
                StartCoroutine(BoosterSequence(m_RefillBoosterFeedbackSprite, BoosterType.Refill, () => {
                    GameManager.Instance.ExecuteRefillLife();
                }));
            }
        }
        else
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForLife();
            }
        }
    }

    public void HandleRefillRewardReceived()
    {
        StartCoroutine(BoosterSequence(m_RefillBoosterFeedbackSprite, BoosterType.Refill, () => {
                   GameManager.Instance.ExecuteRefillLife();
               }));
    }

    public void OnShuffleButtonClicked()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsShuffleOn) return;

        LogBoosterClicked(FirebaseManager.EVENT_BOOSTER_SHUFFLE_CLICKED);
        ResetIdleBoosterNudgeTimer();

        if (UserDataManager.Instance.CurrentLevel < GameManager.SHUFFLE_BOOSTER_UNLOCK_LEVEL)
        {
            HideAllBoosterTooltips();
            m_ShuffleTooltipCoroutine = StartCoroutine(ShowShuffleTooltipCoroutine());
            return;
        }

        if (UserDataManager.Instance.ShuffleBoosterCount > 0)
        {
            if (UserDataManager.Instance.UseShuffleBooster(1))
            {
                StartCoroutine(BoosterSequence(m_ShuffleBoosterFeedbackSprite, BoosterType.Shuffle, () => {
                    GameManager.Instance.ExecuteShuffleBooster();
                }));
            }
        }
        else
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForShuffle();
            }
        }
    }

    public void HandleShuffleRewardReceived()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsShuffleOn) return;

        StartCoroutine(BoosterSequence(m_ShuffleBoosterFeedbackSprite, BoosterType.Shuffle, () => {
            GameManager.Instance.ExecuteShuffleBooster();
        }));
    }

    private void UpdateShuffleBoosterUI(int count)
    {
        UpdateShuffleBoosterVisibility();

        if (GameManager.Instance == null || !GameManager.Instance.IsShuffleOn) return;

        bool isLocked = UserDataManager.Instance.CurrentLevel < GameManager.SHUFFLE_BOOSTER_UNLOCK_LEVEL;

        if (m_ShuffleLockIcon != null)
        {
            m_ShuffleLockIcon.SetActive(isLocked);
        }

        if (isLocked)
        {
            if (m_ShuffleBoosterText != null) m_ShuffleBoosterText.gameObject.SetActive(false);
            if (m_ShuffleAd != null) m_ShuffleAd.SetActive(true);
            if (m_ShuffleBalance != null) m_ShuffleBalance.SetActive(false);
            if (m_ShuffleIcon != null) m_ShuffleIcon.SetActive(false);
        }
        else
        {
            if (m_ShuffleIcon != null) m_ShuffleIcon.SetActive(true);
            if (m_ShuffleLockIcon != null) m_ShuffleLockIcon.SetActive(false);
            if (count > 0)
            {
                if (m_ShuffleBoosterText != null)
                {
                    m_ShuffleBoosterText.gameObject.SetActive(true);
                    m_ShuffleBoosterText.text = count.ToString();
                }
                if (m_ShuffleBalance != null) m_ShuffleBalance.SetActive(true);
                if (m_ShuffleAd != null) m_ShuffleAd.SetActive(false);
            }
            else
            {
                if (m_ShuffleBoosterText != null) m_ShuffleBoosterText.gameObject.SetActive(false);
                if (m_ShuffleBalance != null) m_ShuffleBalance.SetActive(false);
                if (m_ShuffleAd != null) m_ShuffleAd.SetActive(true);
            }
        }
    }

    private System.Collections.IEnumerator ShowShuffleTooltipCoroutine()
    {
        if (m_ShuffleTooltip != null)
        {
            m_ShuffleTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_ShuffleTooltip.SetActive(false);
        }
        m_ShuffleTooltipCoroutine = null;
    }

    private void UpdateShuffleBoosterVisibility()
    {
        bool show = GameManager.Instance != null && GameManager.Instance.IsShuffleOn;
        if (m_ShuffleBoosterButton != null)
        {
            m_ShuffleBoosterButton.gameObject.SetActive(show);
        }

        if (!show)
        {
            if (m_ShuffleTooltipCoroutine != null)
            {
                StopCoroutine(m_ShuffleTooltipCoroutine);
                m_ShuffleTooltipCoroutine = null;
            }
            if (m_ShuffleTooltip != null) m_ShuffleTooltip.SetActive(false);
        }
    }

    private void UpdateRefillBoosterUI(int count)
    {
        bool isLocked = UserDataManager.Instance.CurrentLevel < GameManager.REFILL_BOOSTER_UNLOCK_LEVEL;

        if (m_RefillLockIcon != null)
        {
            m_RefillLockIcon.SetActive(isLocked);
        }

        if (isLocked)
        {
            if (m_RefillBoosterText != null) m_RefillBoosterText.gameObject.SetActive(false);
            if (m_RefillAd != null) m_RefillAd.SetActive(true);
            if (m_RefillBalance != null) m_RefillBalance.SetActive(false);
            if (m_RefillIcon != null) m_RefillIcon.SetActive(false);
        }
        else
        {
            if (m_RefillIcon != null) m_RefillIcon.SetActive(true);
            if (m_RefillLockIcon != null) m_RefillLockIcon.SetActive(false);
            
            if (count > 0)
            {
                if (m_RefillBoosterText != null)
                {
                    m_RefillBoosterText.gameObject.SetActive(true);
                    m_RefillBoosterText.text = count.ToString();
                }
                if (m_RefillBalance != null) m_RefillBalance.SetActive(true);
                if (m_RefillAd != null) m_RefillAd.SetActive(false);
            }
            else
            {
                if (m_RefillBoosterText != null) m_RefillBoosterText.gameObject.SetActive(false);
                if (m_RefillBalance != null) m_RefillBalance.SetActive(false);
                if (m_RefillAd != null) m_RefillAd.SetActive(true);
            }
        }
    }

    private System.Collections.IEnumerator ShowRefillTooltipCoroutine()
    {
        if (m_RefillTooltip != null)
        {
            m_RefillTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_RefillTooltip.SetActive(false);
        }
        m_RefillTooltipCoroutine = null;
    }

    private void UpdateBoostersPanelVisibility()
    {
        if (m_BoostersPanel != null)
        {
            m_BoostersPanel.SetActive(UserDataManager.Instance.CurrentLevel >= 7);
        }
    }

    private System.Collections.IEnumerator ShowRefillFullTooltipCoroutine()
    {
        if (m_RefillFullLivesTooltip != null)
        {
            m_RefillFullLivesTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_RefillFullLivesTooltip.SetActive(false);
        }
        m_RefillFullTooltipCoroutine = null;
    }

    private IEnumerator BoosterSequence(Sprite boosterSprite, BoosterType type, System.Action onComplete)
    {
        if (m_BoosterOverlayParent == null || m_BoosterImagePrefab == null || boosterSprite == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (SoundManager.Instance != null)
        {
            switch (type)
            {
                case BoosterType.Magic: SoundManager.Instance.PlayMagicBooster(); break;
                case BoosterType.Hint: SoundManager.Instance.PlayHintBooster(); break;
                case BoosterType.Refill: SoundManager.Instance.PlayRefillBooster(); break;
                case BoosterType.Shuffle: SoundManager.Instance.PlayShuffleBooster(); break;
            }
        }

        GameObject boosterGO = Instantiate(m_BoosterImagePrefab, m_BoosterOverlayParent);
        Image boosterImage = boosterGO.GetComponent<Image>();
        if (boosterImage != null) boosterImage.sprite = boosterSprite;

        RectTransform rt = boosterGO.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero; // Center of overlay
        rt.localScale = Vector3.zero;

        CanvasGroup cg = boosterGO.GetComponent<CanvasGroup>();
        if (cg == null) cg = boosterGO.AddComponent<CanvasGroup>();

        // 1. Punch Scale Up
        float elapsed = 0f;
        float punchDuration = 0.25f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;
            // Ease out elastic-like punch
            float scale = Mathf.Lerp(0f, 1.2f, t);
            rt.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(1.2f, 1.2f, 1f);

        // Trigger action in parallel after initial punch
        onComplete?.Invoke();

        // 2. Settle Down
        elapsed = 0f;
        float settleDuration = 0.12f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / settleDuration;
            float scale = Mathf.Lerp(1.2f, 1.0f, t);
            rt.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;

        // 3. Short Pause
        yield return new WaitForSeconds(0.34f);

        // 4. Fade and Out
        elapsed = 0f;
        float fadeDuration = 0.21f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            cg.alpha = 1f - t;
            rt.localScale = Vector3.one * (1f + t * 0.2f); // Slight scale up while fading
            yield return null;
        }

        Destroy(boosterGO);
    }
}
