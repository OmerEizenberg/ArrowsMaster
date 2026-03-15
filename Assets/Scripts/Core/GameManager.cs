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
        public const int ADS_START_LEVEL = 12;
        public const int COINS_START_LEVEL = 5;


        public GameObject failureScreen;
        public GameUIContoleer m_GameUI;
        public GameObject m_LobbyUI;
        public GameObject m_FunFact;
        [SerializeField] private TextMeshProUGUI m_FunFactText;
        [SerializeField] private string[] m_FunFactsDat;
        [SerializeField] private GameObject[] m_LevelUIElements;
        private GameObject m_currentLevelUIElement;

        [Header("Shop UI")]
        [SerializeField] private TextMeshProUGUI m_PlayOnPriceText;
        [SerializeField] private TextMeshProUGUI m_UserBalanceText;
        [SerializeField] private TextMeshProUGUI m_RewardedAdAmountText;
        [SerializeField] private GameObject m_ShopLayer;
        [SerializeField] private GameObject m_LevelCurrencyContainer;
        [SerializeField] private GameObject m_StreakRecordContainer;
        [SerializeField] private TextMeshProUGUI m_ArrowsLeftText;
        [SerializeField] private RectTransform m_ArrowsLeftHolder;

        [Header("Settings")]
        public int maxLives = 3;

        public int CurrentLives { get; private set; }
        private int activeArrowsCount = 0;

        // Timer-related fields
        private float currentTime = 0f;
        private float levelDuration = 0f; // 0 means no time limit
        private bool isTimerActive = false;
        public bool IsTimedLevel => levelDuration > 0;
        public float CurrentTime => currentTime;
        public float LevelDuration => levelDuration;

        // Events
        public event Action<int> OnLivesChanged;
        public event Action OnLevelStarted;
        public event Action OnGameOver;
        public event Action OnLevelWon;
        public event Action<bool> OnHintVisibilityChanged;
        public event Action<string> OnTimerUpdated; // Passes formatted time string MM:SS
        public event Action<int, Vector2> OnLevelCurrencyChanged; 
        public event Action<int> OnMaxStreakBroken;
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
        private int collectedLevelCurrency = 0; // Currency collected during the current level attempt
        public int CollectedLevelCurrency => collectedLevelCurrency;

        public int PickedArrowsCount => p_pickedArrowIds.Count;

        public bool p_isPlayOnRewarded = false;
        public bool p_isHintRewarded = false;

        private List<RectTransform> m_ActiveCombos = new List<RectTransform>();
        private List<GameObject> m_ActiveVoices = new List<GameObject>();
        private bool wasTimerActiveBeforeAd = false;
        private List<int> p_pickedArrowIds = new List<int>();
        private Coroutine m_PeriodicSaveCoroutine;
        private LevelProgress m_LastSavedProgress;

        private Vector2 m_ScreenCenter;
        private Vector2[] m_QuarterCenters = new Vector2[4];

        public bool CanInteract => isEntranceFinished && !isWinning && !isTimeUp &&
                                (failureScreen == null || !failureScreen.activeInHierarchy) &&
                                (m_LobbyUI == null || !m_LobbyUI.activeInHierarchy) &&
                                (m_FunFact == null || !m_FunFact.activeInHierarchy);

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

            HideScreens();

            InitializeScreenPositions();
        }

        private void InitializeScreenPositions()
        {
            float w = Screen.width;
            float h = Screen.height;
            m_ScreenCenter = new Vector2(w * 0.5f, h * 0.4f);
            
            // 4 quarters (Pizza style - center of each quadrant)
            m_QuarterCenters[0] = new Vector2(w * 0.3f, h * 0.7f); // Top Left
            m_QuarterCenters[1] = new Vector2(w * 0.7f, h * 0.7f); // Top Right
            m_QuarterCenters[2] = new Vector2(w * 0.3f, h * 0.2f); // Bottom Left
            m_QuarterCenters[3] = new Vector2(w * 0.7f, h * 0.2f); // Bottom Right
        }

        private void Start()
        {
            CurrentLives = maxLives;
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnRewardReceived += HandleRewardReceived;
                AdsManager.Instance.OnAdOpened += HandleAdOpened;
                AdsManager.Instance.OnAdClosed += HandleAdClosed;
            }

            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCurrencyChanged += UpdateUserBalanceUI;
            }

            if (m_LevelCurrencyContainer == null && m_GameUI != null)
            {
                var display = m_GameUI.GetComponentInChildren<Assets.Scripts.GameUI.LevelCurrencyDisplay>(true);
                if (display != null) m_LevelCurrencyContainer = display.gameObject;
            }

            if (levelManager != null)
            {
                levelManager.OnEntranceAnimationFinished += () => {
                    isEntranceFinished = true;
                    ResetHintTimer();
                };

                // Move Fun Fact trigger to happen earlier (1 second before entrance finished)
                levelManager.OnEntranceAnimationStarted += () => {
                    if (m_FunFact != null && UserDataManager.Instance.CurrentLevel < m_FunFactsDat.Length && m_FunFactsDat[UserDataManager.Instance.CurrentLevel].Length > 2) {
                        m_FunFact.SetActive(true);
                        m_FunFactText.text = m_FunFactsDat[UserDataManager.Instance.CurrentLevel];
                    }

                    int currentLevel = UserDataManager.Instance.CurrentLevel;
                    if (m_LevelUIElements != null && currentLevel < m_LevelUIElements.Length)
                    {
                        GameObject prefab = m_LevelUIElements[currentLevel];
                        if (prefab != null)
                        {
                            if (m_currentLevelUIElement != null) Destroy(m_currentLevelUIElement);
                            m_currentLevelUIElement = Instantiate(prefab, m_GameUI.GameUIParent);
                            Destroy(m_currentLevelUIElement , 5.0f);
                        }
                    }
                };
            }
            
            // Check and restore progress if exists
            CheckAndRestoreProgress();
        }

        private void OnDestroy()
        {
            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnRewardReceived -= HandleRewardReceived;
                AdsManager.Instance.OnAdOpened -= HandleAdOpened;
                AdsManager.Instance.OnAdClosed -= HandleAdClosed;
            }

            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCurrencyChanged -= UpdateUserBalanceUI;
            }
        }

        private void UpdateUserBalanceUI(int amount)
        {
            if (m_UserBalanceText != null)
            {
                m_UserBalanceText.text = amount.ToString("N0");
            }
        }

        private void HandleRewardReceived()
        {
            if (p_isHintRewarded)
            {
                Debug.Log("[GameManager] Hint Reward Received! Triggering show hint...");
                ShowHint();
                p_isHintRewarded = false;
            }
            else if (p_isPlayOnRewarded)
            {
                Debug.Log("[GameManager] PlayOn Reward Received (Ad)!");
                ExecutePlayOn();
                p_isPlayOnRewarded = false;
            }
        }

        private void HandleAdOpened()
        {
            if (isTimerActive)
            {
                Debug.Log("[GameManager] Ad Opened. Pausing timer.");
                wasTimerActiveBeforeAd = true;
                isTimerActive = false;
            }
            else
            {
                wasTimerActiveBeforeAd = false;
            }
        }

        private void HandleAdClosed()
        {
            if (wasTimerActiveBeforeAd)
            {
                Debug.Log("[GameManager] Ad Closed. Resuming timer.");
                isTimerActive = true;
                wasTimerActiveBeforeAd = false;
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
                StartCoroutine(ClearHintActive(45.0f, bestArrow));
            }

            ResetHintTimer(false);
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
                Debug.Log(">>> No ad AdsManager");
                HandleRewardReceived();
            }
        }

        private int playOnPurchaseCount = 0;

        public int GetPlayOnCost()
        {
            if (RemoteConfigManager.Instance != null)
            {
                if (playOnPurchaseCount == 0) return RemoteConfigManager.Instance.FirstPlayOn;
                if (playOnPurchaseCount == 1) return RemoteConfigManager.Instance.SecPlayOn;
                return RemoteConfigManager.Instance.ThirdPlayOn;
            }

            if (playOnPurchaseCount == 0) return 1600;
            if (playOnPurchaseCount == 1) return 3200;
            return 4200;
        }

        public void BuyPlayOn()
        {
            int cost = GetPlayOnCost();
            if (UserDataManager.Instance.ReduceArrowsCurrency(cost))
            {
                Debug.Log($"[GameManager] Bought PlayOn for {cost}.");
                playOnPurchaseCount++;
                UpdateUserBalanceUI(UserDataManager.Instance.ArrowsCurrency);
                ExecutePlayOn();
            }
            else
            {
                Debug.Log("Open Shop");
                if (m_ShopLayer != null)
                {
                    m_ShopLayer.SetActive(true);
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlayShop();
                    }
                }
            }
        }

        private void ExecutePlayOn()
        {
             Debug.Log("[GameManager] PlayOn Executing! Refilling lives or adding time.");
                
            if (isTimeUp)
            {
               Debug.Log("[GameManager] Time's up! Adding 60 seconds.");
               currentTime += 60f;
               isTimeUp = false;
               isTimerActive = true;
               wasTimerActiveBeforeAd = false; 
               UpdateTimerUI();
            }
            else
            {
               Debug.Log("[GameManager] Out of lives! Refilling lives.");
               ResetLives();
               if (IsTimedLevel)
               {
                   isTimerActive = true;
                   wasTimerActiveBeforeAd = false;
               }
            }
            
            HideFailureScreen();
            ResetHintTimer();
        }

        public void RestartCurrentLevel()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial(true);
            }

            if (p_isLevelProgression && (failureScreen == null || !failureScreen.activeInHierarchy))
            {
                UserDataManager.Instance.IncrementCurrentLevelAttempts();
            }

            if (levelManager != null && !string.IsNullOrEmpty(levelManager.CurrentLevelId))
            {
                if (p_isLevelProgression)
                {
                    UserDataManager.Instance.ClearLevelProgress(); // Clear when manually restarting level progression
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
            if (UserDataManager.Instance.LastAttemptLevelId != levelId || UserDataManager.Instance.CurrentLevelAttempts == 0)
            {
                UserDataManager.Instance.ResetCurrentLevelAttempts(levelId);
            }

            ResetLives();
            HideScreens();
            if (m_currentLevelUIElement != null) Destroy(m_currentLevelUIElement);
            
            p_isLevelProgression = true;
            // Reset arrow count before loading new level
            activeArrowsCount = 0;
            UpdateArrowsLeftUI(false);
            collectedLevelCurrency = 0; // Reset currency for new level attempt
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0f;
            playOnPurchaseCount = 0;
            m_LastSavedProgress = null;

            if (levelManager != null)
            {
                p_pickedArrowIds.Clear();
                levelManager.LoadLevelFromResources(levelId);
            }

            // --- Analytics: level_start ---
            int attemptCount = PlayerPrefs.GetInt("AttemptCount_" + levelId, 0) + 1;
            PlayerPrefs.SetInt("AttemptCount_" + levelId, attemptCount);
            PlayerPrefs.Save();

            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_LEVEL_START, 
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_LEVEL_ID, levelId),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_ATTEMPT_COUNT, attemptCount));

                // FTUE: tutorial_begin
                if (levelId == "Level1" && attemptCount == 1)
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_TUTORIAL_BEGIN);
                }
            }
            // -----------------------------

            OnLevelStarted?.Invoke();
            // Reset UI for level currency
            OnLevelCurrencyChanged?.Invoke(0, Vector2.zero);
            
            isEntranceFinished = false;
            isWinning = false;
            if (m_FunFact != null) m_FunFact.SetActive(false);
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.SetGameUIVisible(true);
            UpdateUIVisibility();

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            m_PeriodicSaveCoroutine = StartCoroutine(PeriodicSaveCoroutine());
        }

        public void StartChallengeLevel(string levelId, int year, int month, int day)
        {
            ResetLives();
            HideScreens();
            if (m_currentLevelUIElement != null) Destroy(m_currentLevelUIElement);
            
            p_isLevelProgression = false;
            currentChallengeYear = year;
            currentChallengeMonth = month;
            currentChallengeDay = day;

            // Reset arrow count before loading new level
            activeArrowsCount = 0;
            UpdateArrowsLeftUI(false);
            collectedLevelCurrency = 0; // Reset currency for new level attempt
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0f;
            playOnPurchaseCount = 0;
            m_LastSavedProgress = null;

            if (levelManager != null)
            {
                p_pickedArrowIds.Clear();
                levelManager.LoadChallengeLevelFromResources(levelId);
            }

            // --- Analytics: level_start (Challenge) ---
            int attemptCount = PlayerPrefs.GetInt("AttemptCount_Challenge_" + levelId, 0) + 1;
            PlayerPrefs.SetInt("AttemptCount_Challenge_" + levelId, attemptCount);
            PlayerPrefs.Save();

            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_LEVEL_START, 
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_LEVEL_ID, "Challenge_" + levelId),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_ATTEMPT_COUNT, attemptCount));
            }
            // ----------------------------------------

            OnLevelStarted?.Invoke();
            // Reset UI for level currency
            OnLevelCurrencyChanged?.Invoke(0, Vector2.zero);

            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.SetGameUIVisible(true);
            if (m_FunFact != null) m_FunFact.SetActive(false);
            UpdateUIVisibility();

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            m_PeriodicSaveCoroutine = StartCoroutine(PeriodicSaveCoroutine());
        }

        private void ResetLives()
        {
            CurrentLives = maxLives;
            OnLivesChanged?.Invoke(CurrentLives);
        }

        public void RegisterArrow()
        {
            activeArrowsCount++;
            UpdateArrowsLeftUI(false);
        }

        public void NotifyArrowSuccess(Vector2 clickPosition, int arrowId)
        {
            if (UserDataManager.Instance.CurrentLevel >= COINS_START_LEVEL)
            {
                // Collect currency logic

                int coinsEarned = Mathf.Max(1, p_StreakCount);
                collectedLevelCurrency += coinsEarned;
                Debug.Log($"[GameManager] Arrow Success! Streak: {p_StreakCount}, Earned: {coinsEarned}, Total Collected: {collectedLevelCurrency}");
                
                // Notify UI
                OnLevelCurrencyChanged?.Invoke(collectedLevelCurrency, clickPosition);
            }

            if (activeArrowsCount > 0)
            {
                activeArrowsCount--;
                p_pickedArrowIds.Add(arrowId);
                UpdateArrowsLeftUI(true);
                if (activeArrowsCount == 0)
                {
                    isWinning = true;
                    SetHintVisibility(false);
                    StartCoroutine(WinSequence());
                }
                SaveCurrentProgress();
            }
        }

        private void UpdateArrowsLeftUI(bool animate)
        {
            if (m_ArrowsLeftText != null)
            {
                m_ArrowsLeftText.text = activeArrowsCount.ToString();
            }

            if (animate && m_ArrowsLeftHolder != null)
            {
                if (m_ArrowsLeftPunchCoroutine != null) StopCoroutine(m_ArrowsLeftPunchCoroutine);
                m_ArrowsLeftPunchCoroutine = StartCoroutine(ArrowsLeftPunchAnimation());
            }
        }

        private Coroutine m_ArrowsLeftPunchCoroutine;
        private System.Collections.IEnumerator ArrowsLeftPunchAnimation()
        {
            if (m_ArrowsLeftHolder == null) yield break;

            float upDuration = 0.08f;
            float downDuration = 0.05f;
            float punchScale = 1.3f;
            
            Vector3 originalScale = Vector3.one;
            Vector3 peakScale = originalScale * punchScale;

            // Scale Up
            float elapsed = 0f;
            while (elapsed < upDuration)
            {
                m_ArrowsLeftHolder.localScale = Vector3.Lerp(originalScale, peakScale, elapsed / upDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            m_ArrowsLeftHolder.localScale = peakScale;

            // Scale Down
            elapsed = 0f;
            while (elapsed < downDuration)
            {
                m_ArrowsLeftHolder.localScale = Vector3.Lerp(peakScale, originalScale, elapsed / downDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            m_ArrowsLeftHolder.localScale = originalScale;
            m_ArrowsLeftPunchCoroutine = null;
        }

        public void NotifyArrowSelection()
        {
            LastArrowSelectionTime = Time.time;
        }

        public void IncrementStreak()
        {
            if (UserDataManager.Instance.CurrentLevel < 6) return;
            p_StreakCount++;
            m_GameUI.PlayStreakAnimation();

            if (p_StreakCount > UserDataManager.Instance.MaxStreak)
            {
                UserDataManager.Instance.UpdateMaxStreak(p_StreakCount);
                OnMaxStreakBroken?.Invoke(p_StreakCount);
            }
        }

        public void ResetStreak()
        {
            p_StreakCount = 0;
            ClearActiveCombos();
            if (m_GameUI != null) m_GameUI.ResetComboIndication();
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
            if (m_GameUI != null) m_GameUI.ResetComboIndication();
            ClearActiveCombos();
            ClearActiveVoices();
            Debug.Log("Level Complete! Waiting for win screen...");
            
            // Award Collected Currency
            if (collectedLevelCurrency > 0)
            {
                UserDataManager.Instance.AddArrowsCurrency(collectedLevelCurrency);
                Debug.Log($"[GameManager] Level Won! Awarded {collectedLevelCurrency} ArrowsCurrency.");
            }

            if(p_isLevelProgression)
            {
                int completedLevel = UserDataManager.Instance.CurrentLevel;
                UserDataManager.Instance.IncrementLevel();
                UserDataManager.Instance.ClearCurrentLevelAttempts();
                UserDataManager.Instance.IsRateUsCheckPending = true;

                // --- Google Play Games Achievements ---
                CheckAchievements(completedLevel);
                // --------------------------------------
            }
            else
            {
                UserDataManager.Instance.SaveMonthlyChallengeProgress(currentChallengeYear, currentChallengeMonth, currentChallengeDay);
            }
            
            UserDataManager.Instance.ClearLevelProgress(); // Clear on win
            if (m_PeriodicSaveCoroutine != null) { StopCoroutine(m_PeriodicSaveCoroutine); m_PeriodicSaveCoroutine = null; }

            // --- Analytics: level_end (Success) ---
            if (FirebaseManager.Instance != null && levelManager != null)
            {
                string id = p_isLevelProgression ? levelManager.CurrentLevelId : "Challenge_" + levelManager.CurrentLevelId;
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_LEVEL_END,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_LEVEL_ID, id),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SUCCESS, 1),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SCORE, levelManager.TotalPointsInLevel));

                // FTUE: tutorial_complete
                if (levelManager.CurrentLevelId == "Level1")
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_TUTORIAL_COMPLETE);
                }
            }
            // --------------------------------------

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
                VibrationManager.VibrateSuccess();

                if (m_WinParticles != null) m_WinParticles.SetActive(true);
                if (m_WinLevelText != null)
                {
                    m_WinLevelText.gameObject.SetActive(true);
                    m_WinLevelText.text = m_LevelWinFeedbacks[UnityEngine.Random.Range(0, m_LevelWinFeedbacks.Length)];
                }
            }


            yield return new WaitForSeconds(2.5f);
            
            if (p_isLevelProgression && UserDataManager.Instance.CurrentLevel <= ADS_START_LEVEL)

            {
                Debug.Log($"[GameManager] Below Ads level ({ADS_START_LEVEL}). Transitioning to next level directly.");
                StartLevel($"level{UserDataManager.Instance.CurrentLevel}");
            }
            else
            {
                m_GameUI.SetGameUIVisible(false);
                HideScreens();
                
                if (AdsManager.Instance != null)
            {
                // Show ad only if we are past the first entry level to the lobby
                if(UserDataManager.Instance.CurrentLevel > ADS_START_LEVEL )
                {
                    AdsManager.Instance.ShowInterstitial(true);
                }
                AdsManager.Instance.SpawnCoinsSmallExplosion();
            }
                
                CameraController.Instance.ResetZoom();
                OnLevelWon?.Invoke();
            }


        }

        public void LoseLife()
        {
            if (CurrentLives > 0)
            {
                CurrentLives--;
                OnLivesChanged?.Invoke(CurrentLives);
                SaveCurrentProgress(); // Save after losing a life

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
            if (m_GameUI != null) m_GameUI.ResetComboIndication();
            ClearActiveCombos();
            ClearActiveVoices();
            Debug.Log("Game Over!");

            if (m_PlayOnPriceText != null)
            {
                m_PlayOnPriceText.text = GetPlayOnCost().ToString("N0");
            }

            if (m_UserBalanceText != null)
            {
                Debug.Log("User Balance: " + UserDataManager.Instance.ArrowsCurrency);
                m_UserBalanceText.text = UserDataManager.Instance.ArrowsCurrency.ToString("N0");
            }

            if (m_RewardedAdAmountText != null)
            {
                int amount = 2000;
                if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
                {
                    amount = RemoteConfigManager.Instance.CoinsRewardedAd;
                }
                m_RewardedAdAmountText.text = amount.ToString("N0");
            }

            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
            }

            if (p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementCurrentLevelAttempts();
            }

            // --- Analytics: level_end (Fail - Lives) ---
            if (FirebaseManager.Instance != null && levelManager != null)
            {
                string id = p_isLevelProgression ? levelManager.CurrentLevelId : "Challenge_" + levelManager.CurrentLevelId;
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_LEVEL_END,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_LEVEL_ID, id),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SUCCESS, 0),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SCORE, 0));
            }
            // -------------------------------------------

            UserDataManager.Instance.ClearLevelProgress(); // Clear on game over
            if (m_PeriodicSaveCoroutine != null) { StopCoroutine(m_PeriodicSaveCoroutine); m_PeriodicSaveCoroutine = null; }
            OnGameOver?.Invoke();
        }

        public void HideScreens()
        {
            if (failureScreen != null) failureScreen.SetActive(false);
            if (m_WinParticles != null) m_WinParticles.SetActive(false);
            if (m_WinLevelText != null) m_WinLevelText.gameObject.SetActive(false);
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
            bool isFailureVisible = failureScreen != null && failureScreen.activeInHierarchy;
            bool isLobbyVisible = m_LobbyUI != null && m_LobbyUI.activeInHierarchy;

            if (isEntranceFinished && !isWinning && !isHintVisible && !isFailureVisible && !isLobbyVisible)
            {
                hintTimer += Time.deltaTime;
                if (hintTimer >= 5.0f)
                {
                    SetHintVisibility(true);
                    SoundManager.Instance.PlayHint();
                }
            }
            
            // Update countdown timer
            if (isTimerActive && isEntranceFinished && !isWinning && !isFailureVisible && !isLobbyVisible)
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

        public void ResetHintTimer(bool clearActiveHint = true)
        {
            hintTimer = 0f;
            SetHintVisibility(false);
            
            // If there's an active hint, clear it immediately when user starts interacting
            if (clearActiveHint && isHintActive)
            {
                isHintActive = false;
                List<ArrowController> arrows = GridManager.Instance.GetAllArrows();
                foreach (var arrow in arrows)
                {
                    if (arrow != null) arrow.HidePreview();
                }
            }
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

        private void UpdateUIVisibility()
        {
            if (m_LevelCurrencyContainer != null)
            {
                m_LevelCurrencyContainer.SetActive(UserDataManager.Instance.CurrentLevel >= COINS_START_LEVEL);


            }

            if (m_StreakRecordContainer != null)
            {
                m_StreakRecordContainer.SetActive(UserDataManager.Instance.CurrentLevel >= 6);
            }
        }
        
        // Timer-related methods
        public void InitializeTimer(float durationInSeconds)
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
            if (IsTimedLevel)
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
            if (m_GameUI != null) m_GameUI.ResetComboIndication();
            ClearActiveCombos();
            Debug.Log("Time's up!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
            }

            if (p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementCurrentLevelAttempts();
            }

            // --- Analytics: level_end (Fail - Time) ---
            if (FirebaseManager.Instance != null && levelManager != null)
            {
                string id = p_isLevelProgression ? levelManager.CurrentLevelId : "Challenge_" + levelManager.CurrentLevelId;
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_LEVEL_END,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_LEVEL_ID, id),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SUCCESS, 0),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_SCORE, 0));
            }
            // ------------------------------------------

            if (m_PlayOnPriceText != null)
            {
                m_PlayOnPriceText.text = GetPlayOnCost().ToString("N0");
            }

            if (m_UserBalanceText != null)
            {
                m_UserBalanceText.text = UserDataManager.Instance.ArrowsCurrency.ToString("N0");
            }

            OnGameOver?.Invoke();
        }
        
        public string GetFailureTitle()
        {
            return isTimeUp ? "Time's Up!" : "Out of Lives!";
        }
        
        public string GetFailureSubtitle()
        {
            if (isTimeUp)
            {
                return "Watch an ad to get 60 seconds\nand Keep Playing!";
            }
            else
            {
                return "Watch an ad to refill lives\nand Keep Playing!";
            }
        }

        public string GetFailureDescription()
        {
            if (isTimeUp)
            {
                return "Time's up! get more 60 seconds with coins or by watching a short ad.";
            }
            else
            {
                return "Refill lives with coins or by watching a short ad.";
            }
        }

        public void RegisterCombo(RectTransform rect)
        {
            if (rect != null) m_ActiveCombos.Add(rect);
        }

        public void ClearActiveCombos()
        {
            foreach (var combo in m_ActiveCombos)
            {
                if (combo != null && combo.gameObject != null)
                {
                    Destroy(combo.gameObject);
                }
            }
            m_ActiveCombos.Clear();
        }

        public void RegisterVoice(GameObject voice)
        {
            if (voice != null)
            {
                m_ActiveVoices.Add(voice);
            }
        }

        public void ClearActiveVoices()
        {
            foreach (var voice in m_ActiveVoices)
            {
                if (voice != null)
                {
                    Destroy(voice);
                }
            }
            m_ActiveVoices.Clear();
        }

        public Vector2 GetValidComboPosition(bool isVoice)
        {
            if (isVoice)
            {
                return m_ScreenCenter;
            }

            // Return center of a random quarter for combo feedback
            return m_QuarterCenters[UnityEngine.Random.Range(0, 4)];
        }

        private void CheckAndRestoreProgress()
        {
            LevelProgress progress = UserDataManager.Instance.LoadLevelProgress();
            if (progress != null && progress.hasProgress && !string.IsNullOrEmpty(progress.levelId))
            {
                Debug.Log($"[GameManager] Found saved progress for level {progress.levelId}. Restoring...");
                RestoreLevelProgress(progress);
            }
        }

        private void RestoreLevelProgress(LevelProgress progress)
        {
            CurrentLives = progress.remainingLives > 0 ? progress.remainingLives : maxLives;
            OnLivesChanged?.Invoke(CurrentLives);
            HideScreens();
            if (m_currentLevelUIElement != null) Destroy(m_currentLevelUIElement);

            p_isLevelProgression = progress.isChallenge ? false : true;
            currentChallengeYear = progress.challengeYear;
            currentChallengeMonth = progress.challengeMonth;
            currentChallengeDay = progress.challengeDay;
            p_pickedArrowIds = new List<int>(progress.pickedArrowIds);

            // Reset arrow count before loading
            activeArrowsCount = 0;
            UpdateArrowsLeftUI(false);
            collectedLevelCurrency = progress.collectedCoins;

            isTimerActive = false;
            isTimeUp = false;
            lastDisplayedSecond = -1;
            levelDuration = 0f; // Will be set by LoadLevel
            playOnPurchaseCount = 0;

            if (levelManager != null)
            {
                if (progress.isChallenge)
                {
                    levelManager.LoadChallengeLevelFromResources(progress.levelId, p_pickedArrowIds);
                }
                else
                {
                    levelManager.LoadLevelFromResources(progress.levelId, p_pickedArrowIds);
                }
            }

            // Restore time AFTER level loading to avoid it being overwritten by InitializeTimer
            currentTime = progress.remainingTime;
            levelDuration = progress.levelDuration; // Restore levelDuration as well
            if (IsTimedLevel)
            {
                if (p_pickedArrowIds != null && p_pickedArrowIds.Count > 0)
                {
                    isTimerActive = true; // Auto-resume if progress was made
                }
                else
                {
                    isTimerActive = progress.isTimerActive;
                }
            }
            else
            {
                isTimerActive = false;
            }

            m_LastSavedProgress = progress;
            Debug.Log($"[GameManager] Progress Restored: Level={progress.levelId}, Time={currentTime}/{levelDuration}, Lives={CurrentLives}, Active={isTimerActive}");

            OnLevelStarted?.Invoke();
            OnLevelCurrencyChanged?.Invoke(collectedLevelCurrency, Vector2.zero);
            
            isEntranceFinished = false;
            isWinning = false;
            if (m_FunFact != null) m_FunFact.SetActive(false);
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.SetGameUIVisible(true);
            UpdateUIVisibility();

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            m_PeriodicSaveCoroutine = StartCoroutine(PeriodicSaveCoroutine());
            
            // Special case: if timer was already active, it will resumed in Update once isEntranceFinished is true
        }

        private void SaveCurrentProgress()
        {
            if (isWinning || (failureScreen != null && failureScreen.activeInHierarchy) || (m_LobbyUI != null && m_LobbyUI.activeInHierarchy))
            {
                return;
            }

            if (levelManager == null || string.IsNullOrEmpty(levelManager.CurrentLevelId)) return;

            // Optimization: Only save if something has changed
            if (m_LastSavedProgress != null)
            {
                bool changed = false;
                if (m_LastSavedProgress.levelId != levelManager.CurrentLevelId) changed = true;
                else if (m_LastSavedProgress.isChallenge != (!p_isLevelProgression)) changed = true;
                else if (m_LastSavedProgress.remainingLives != CurrentLives) changed = true;
                else if (m_LastSavedProgress.isTimerActive != isTimerActive) changed = true;
                else if (m_LastSavedProgress.pickedArrowIds.Count != p_pickedArrowIds.Count) changed = true;
                else if (m_LastSavedProgress.collectedCoins != collectedLevelCurrency) changed = true;
                else if (isTimerActive && Mathf.Abs(m_LastSavedProgress.remainingTime - currentTime) >= 1.0f) changed = true;
                else if (!p_isLevelProgression) // Challenge-specific checks
                {
                    if (m_LastSavedProgress.challengeYear != currentChallengeYear ||
                        m_LastSavedProgress.challengeMonth != currentChallengeMonth ||
                        m_LastSavedProgress.challengeDay != currentChallengeDay)
                        changed = true;
                }

                if (!changed) return;
            }

            LevelProgress progress = new LevelProgress();
            progress.levelId = levelManager.CurrentLevelId;
            progress.isChallenge = !p_isLevelProgression;
            progress.pickedArrowIds = new List<int>(p_pickedArrowIds);
            progress.remainingTime = currentTime;
            progress.remainingLives = CurrentLives;
            progress.levelDuration = levelDuration;
            progress.isTimerActive = isTimerActive;
            progress.challengeYear = currentChallengeYear;
            progress.challengeMonth = currentChallengeMonth;
            progress.challengeDay = currentChallengeDay;
            progress.collectedCoins = collectedLevelCurrency;
            progress.hasProgress = true;

            m_LastSavedProgress = progress;
            UserDataManager.Instance.SaveLevelProgress(progress);
            Debug.Log($"[GameManager] Progress saved for level {progress.levelId}. Picked arrows: {progress.pickedArrowIds.Count}, Time={currentTime}/{levelDuration}, Active={isTimerActive}");
        }

        private void CheckAchievements(int completedLevel)
        {
            if (PlayGamesManager.Instance == null) return;

            string achievementId = string.Empty;

            switch (completedLevel)
            {
                case 1: achievementId = PlayGamesManager.ACHIEVEMENT_FINISH_TUTORIAL; break;
                case 25: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_25_LEVELS; break;
                case 50: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_50_LEVELS; break;
                case 75: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_75_LEVELS; break;
                case 100: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_100_LEVELS; break;
                case 150: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_150_LEVELS; break;
                case 200: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_200_LEVELS; break;
                case 250: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_250_LEVELS; break;
                case 300: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_300_LEVELS; break;
                case 400: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_400_LEVELS; break;
                case 500: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_500_LEVELS; break;
                case 600: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_600_LEVELS; break;
                case 750: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_750_LEVELS; break;
                case 1000: achievementId = PlayGamesManager.ACHIEVEMENT_COMPLETED_1000_LEVELS; break;
            }

            if (!string.IsNullOrEmpty(achievementId))
            {
                PlayGamesManager.Instance.UnlockAchievement(achievementId);
            }
        }

        private System.Collections.IEnumerator PeriodicSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(5.0f);
                if (isEntranceFinished && !isWinning && !isTimeUp)
                {
                    SaveCurrentProgress();
                }
            }
        }
    }
}
