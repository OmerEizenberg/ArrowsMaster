using System;
using UnityEngine;
using TMPro;
using System.Collections.Generic;


namespace Assets.Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public LevelManager levelManager;
        public GameObject failureScreen;
        public GameUIContoleer m_GameUI;
        public GameObject m_LobbyUI;

        [Header("Settings")]
        public int maxLives = 3;

        public int CurrentLives { get; private set; }
        private int activeArrowsCount = 0;

        // Timer-related fields
        private float currentTime = 0f;
        private int levelDuration = 0; // 0 means no time limit
        private bool isTimerActive = false;
        public bool IsTimedLevel => levelDuration > 0;
        public float CurrentTime => currentTime;
        public int LevelDuration => levelDuration;

        // Events
        public event Action<int> OnLivesChanged;
        public event Action OnLevelStarted;
        public event Action OnGameOver;
        public event Action OnLevelWon;
        public event Action<bool> OnHintVisibilityChanged;
        public event Action<string> OnTimerUpdated; // Passes formatted time string MM:SS
        public bool p_isLevelProgression = true;

        public int currentChallengeYear;
        public int currentChallengeMonth;
        public int currentChallengeDay;

        private float hintTimer = 0f;
        private bool isEntranceFinished = false;
        private bool isHintVisible = false;
        private bool isWinning = false;
        private bool isHintActive = false;
        private bool isTimeUp = false;
        public float LastArrowSelectionTime { get; private set; } = -10f;
        public int p_StreakCount { get; private set; } = 0;


        public bool p_isPlayOnRewarded = false;
        public bool p_isHintRewarded = false;

        public bool CanInteract => isEntranceFinished && !isWinning && !isHintActive && !isTimeUp &&
                                (failureScreen == null || !failureScreen.activeInHierarchy) &&
                                (m_LobbyUI == null || !m_LobbyUI.activeInHierarchy);

        [SerializeField] private GameObject m_WinParticles;
        [SerializeField] private TextMeshProUGUI m_WinLevelText;
        private string[] m_LevelWinFeedbacks = new string[] { "Perfect !", "Well Done !", "Excellent !", "Amazing !", "Incredible !", "Masterpiece !", "Legendary !" , "You're a Legend !" , "Fantastic!" , "Awesome !" , "Phenomenal!"};

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (failureScreen != null) failureScreen.SetActive(false);
        }

        private void Start()
        {
            CurrentLives = maxLives;
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnRewardReceived += HandleRewardReceived;
            }

            if (levelManager != null)
            {
                levelManager.OnEntranceAnimationFinished += () => {
                    isEntranceFinished = true;
                    ResetHintTimer();
                };
            }
        }

        private void OnDestroy()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnRewardReceived -= HandleRewardReceived;
            }
        }

        private void HandleRewardReceived()
        {
            if (p_isHintRewarded)
            {
                Debug.Log("[GameManager] Hint Reward Received! Triggering show hint...");
                ShowHint();
            }
            else
            {
                if(p_isPlayOnRewarded)
                {
                    Debug.Log("[GameManager] PlayOn Reward Received! Refilling lives and hiding failure screen.");
                    ResetLives();
                    
                    // For time-based levels, add 60 seconds
                    if (IsTimedLevel)
                    {
                        currentTime += 60f;
                        isTimeUp = false;
                        isTimerActive = true;
                        UpdateTimerUI();
                    }
                    
                    HideFailureScreen();
                    ResetHintTimer();
                }
            }
        }

        private void ShowHint()
        {
            // Optimization: Use cached arrow list from GridManager instead of FindObjectsOfType
            if (GridManager.Instance == null) return;
            
            List<ArrowController> arrows = GridManager.Instance.GetAllArrows();
            ArrowController bestArrow = null;

            foreach (var arrow in arrows)
            {
                if (arrow != null && arrow.gameObject.activeInHierarchy && arrow.CanMoveForward())
                {
                    bestArrow = arrow;
                    break;
                }
            }

            // Fallback to any arrow if none are "clear"
            if (bestArrow == null && arrows.Count > 0) bestArrow = arrows[0];

            if (bestArrow != null)
            {
                // 1. Pan Camera to the head of the pickable arrow
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.FocusOn(bestArrow.GetHeadPosition(), 0.5f);
                }

                // 2. Flash trajectory preview
                isHintActive = true;
                bestArrow.ShowPreview();
                // We'll reset hint active state via a delayed call or coroutine
                StartCoroutine(ClearHintActive(3.0f, bestArrow));
            }

            ResetHintTimer();
        }

        public void PlayOn()
        {
            p_isPlayOnRewarded = true;
            p_isHintRewarded = false;
            Debug.Log("[GameManager] PlayOn method called.");
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewarded();
            }
            else
            {
                // Fallback if no AdsManager
                HandleRewardReceived();
            }
        }

        public void RestartCurrentLevel()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial();
            }

            if (levelManager != null && !string.IsNullOrEmpty(levelManager.CurrentLevelId))
            {
                if (p_isLevelProgression)
                {
                    StartLevel(levelManager.CurrentLevelId);
                }
                else
                {
                    StartChallengeLevel(levelManager.CurrentLevelId, currentChallengeYear, currentChallengeMonth, currentChallengeDay);
                }
            }
        }

        public void StartLevel(string levelId)
        {
            ResetLives();
            HideScreens();
            
            p_isLevelProgression = true;
            // Reset arrow count before loading new level
            activeArrowsCount = 0;
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0;

            if (levelManager != null)
            {
                levelManager.LoadLevelFromResources(levelId);
            }
            OnLevelStarted?.Invoke();
            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
            ResetSelectionStates();

m_GameUI.gameObject.SetActive(true);
        }

        public void StartChallengeLevel(string levelId, int year, int month, int day)
        {
            ResetLives();
            HideScreens();
            
            p_isLevelProgression = false;
            currentChallengeYear = year;
            currentChallengeMonth = month;
            currentChallengeDay = day;

            // Reset arrow count before loading new level
            activeArrowsCount = 0;
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0;

            if (levelManager != null)
            {
                levelManager.LoadChallengeLevelFromResources(levelId);
            }

            OnLevelStarted?.Invoke();
            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.gameObject.SetActive(true);
        }

        private void ResetLives()
        {
            CurrentLives = maxLives;
            OnLivesChanged?.Invoke(CurrentLives);
        }

        public void RegisterArrow()
        {
            activeArrowsCount++;
        }

        public void NotifyArrowSuccess()
        {
            if (activeArrowsCount > 0)
            {
                activeArrowsCount--;
                if (activeArrowsCount == 0)
                {
                    isWinning = true;
                    SetHintVisibility(false);
                    StartCoroutine(WinSequence());
                }
            }
        }

        public void NotifyArrowSelection()
        {
            LastArrowSelectionTime = Time.time;
        }

        public void IncrementStreak()
        {
            p_StreakCount++;
            m_GameUI.PlayStreakAnimation();
        }

        public void ResetStreak()
        {
            p_StreakCount = 0;
        }

        public void ResetSelectionStates()
        {
            LastArrowSelectionTime = -10f;
            p_StreakCount = 0;
            if (CameraController.Instance != null)
                CameraController.Instance.ResetPanState();
        }


        private System.Collections.IEnumerator WinSequence()
        {
            Debug.Log("Level Complete! Waiting for win screen...");
            if(p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementLevel();
            }
            else
            {
                UserDataManager.Instance.SaveMonthlyChallengeProgress(currentChallengeYear, currentChallengeMonth, currentChallengeDay);
            }
            yield return new WaitForSeconds(0.2f);
            levelManager.HideArrows();

            if (levelManager != null)
            {
                levelManager.PlayWinAnimation();
                
                // Play cheer sound based on difficulty
                if (SoundManager.Instance != null)
                {
                    int points = levelManager.TotalPointsInLevel;
                    if (points < 400) // Easy or Hard
                    {
                        SoundManager.Instance.PlaySmallCheer();
                    }
                    else if (points < 900) // Super Hard
                    {
                        SoundManager.Instance.PlayMediumCheer();
                    }
                    else // Nightmare
                    {
                        SoundManager.Instance.PlayBigCheer();
                    }
                }

                m_WinParticles.SetActive(true);
                m_WinLevelText.text = m_LevelWinFeedbacks[UnityEngine.Random.Range(0, m_LevelWinFeedbacks.Length)];
            }

            yield return new WaitForSeconds(2.5f);
            
            if (m_GameUI != null)
            {
                m_GameUI.gameObject.SetActive(false);
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial();
            }

            m_LobbyUI.SetActive(true);
            m_WinParticles.SetActive(false);
            CameraController.Instance.ResetZoom();
            OnLevelWon?.Invoke();
        }

        public void LoseLife()
        {
            if (CurrentLives > 0)
            {
                CurrentLives--;
                OnLivesChanged?.Invoke(CurrentLives);

                if (CurrentLives <= 0)
                {
                    HandleGameOver();
                }
            }
        }

        public void GainLife()
        {
            if (CurrentLives < maxLives) 
            {
                CurrentLives++;
                OnLivesChanged?.Invoke(CurrentLives);
            }
        }

        private void HandleGameOver()
        {
            Debug.Log("Game Over!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
            }
            OnGameOver?.Invoke();
        }

        public void HideScreens()
        {
            if (failureScreen != null) failureScreen.SetActive(false);
        }

        public void HideFailureScreen()
        {
            if (failureScreen != null)
            {
                failureScreen.SetActive(false);
            }
        }

        private void Update()
        {
            if (isEntranceFinished && !isWinning && !isHintVisible)
            {
                hintTimer += Time.deltaTime;
                if (hintTimer >= 5.0f)
                {
                    SetHintVisibility(true);
                    SoundManager.Instance.PlayHint();
                }
            }
            
            // Update countdown timer
            if (isTimerActive && isEntranceFinished && !isWinning)
            {
                currentTime -= Time.deltaTime;
                
                if (currentTime <= 0f)
                {
                    currentTime = 0f;
                    isTimerActive = false;
                    isTimeUp = true;
                    HandleTimeUp();
                }
                
                UpdateTimerUI();
            }
        }

        public void ResetHintTimer()
        {
            hintTimer = 0f;
            SetHintVisibility(false);
        }

        private void SetHintVisibility(bool visible)
        {
            isHintVisible = visible;
            OnHintVisibilityChanged?.Invoke(visible);
        }

        private System.Collections.IEnumerator ClearHintActive(float delay, ArrowController arrow)
        {
            yield return new WaitForSeconds(delay);
            if (arrow != null) arrow.HidePreview();
            isHintActive = false;
        }
        
        // Timer-related methods
        public void InitializeTimer(int durationInSeconds)
        {
            levelDuration = durationInSeconds;
            if (levelDuration > 0)
            {
                currentTime = levelDuration;
                lastDisplayedSecond = -1;
                // Timer will start when first touch happens (when isEntranceFinished is true)
                UpdateTimerUI();
            }
        }

        public void PlayWrongAnimation()
        {
            m_GameUI.PlayWrongAnimation();
        }

        public void StartTimer()
        {
            if (IsTimedLevel && !isTimerActive)
            {
                isTimerActive = true;
                isTimeUp = false;
            }
        }
        
        private int lastDisplayedSecond = -1;
        private void UpdateTimerUI()
        {
            if (IsTimedLevel || levelDuration > 0)
            {
                int currentSecond = Mathf.Max(0, Mathf.FloorToInt(currentTime));
                if (currentSecond != lastDisplayedSecond)
                {
                    lastDisplayedSecond = currentSecond;
                    int minutes = currentSecond / 60;
                    int seconds = currentSecond % 60;
                    string timeString = string.Format("{0:00}:{1:00}", minutes, seconds);
                    m_GameUI.UpdateTimerUI(timeString);
                }
            }
        }
        
        private void HandleTimeUp()
        {
            Debug.Log("Time's up!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
            }
            OnGameOver?.Invoke();
        }
        
        public string GetFailureTitle()
        {
            return IsTimedLevel ? "Time's Up!" : "Out of Lives!";
        }
        
        public string GetFailureSubtitle()
        {
            return IsTimedLevel ? "Watch an ad to get 60 seconds, refill livees\nand Keep Playing!" : "Watch an ad to refill livees\nand Keep Playing!";
        }
    }
}
