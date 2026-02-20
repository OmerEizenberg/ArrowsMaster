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
        public GameObject m_FunFact;
        [SerializeField] private TextMeshProUGUI m_FunFactText;
        [SerializeField] private string[] m_FunFactsDat;

        [Header("Shop UI")]
        [SerializeField] private TextMeshProUGUI m_PlayOnPriceText;
        [SerializeField] private TextMeshProUGUI m_UserBalanceText;
        [SerializeField] private GameObject m_ShopLayer;
        [SerializeField] private GameObject m_LevelCurrencyContainer;

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
        public event Action<int> OnLevelCurrencyChanged; 
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


        public bool p_isPlayOnRewarded = false;
        public bool p_isHintRewarded = false;

        private List<RectTransform> m_ActiveCombos = new List<RectTransform>();
        private bool wasTimerActiveBeforeAd = false;

        public bool CanInteract => isEntranceFinished && !isWinning && !isHintActive && !isTimeUp &&
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

            if (failureScreen != null) failureScreen.SetActive(false);
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
                };
            }
        }

        private void OnDestroy()
        {
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
                Debug.Log(">>> No ad AdsManager");
                HandleRewardReceived();
            }
        }

        private int playOnPurchaseCount = 0;

        public int GetPlayOnCost()
        {
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
            collectedLevelCurrency = 0; // Reset currency for new level attempt
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0;
            playOnPurchaseCount = 0;

            if (levelManager != null)
            {
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
            OnLevelCurrencyChanged?.Invoke(0);
            
            isEntranceFinished = false;
            isWinning = false;
            if (m_FunFact != null) m_FunFact.SetActive(false);
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.gameObject.SetActive(true);
            UpdateUIVisibility();
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
            collectedLevelCurrency = 0; // Reset currency for new level attempt
            
            // Reset timer state
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0;
            playOnPurchaseCount = 0;

            if (levelManager != null)
            {
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
            OnLevelCurrencyChanged?.Invoke(0);

            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
            ResetSelectionStates();

            m_GameUI.gameObject.SetActive(true);
            if (m_FunFact != null) m_FunFact.SetActive(false);
            UpdateUIVisibility();
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
            if (UserDataManager.Instance.CurrentLevel >= 5)
            {
                // Collect currency logic
                int coinsEarned = Mathf.Max(1, p_StreakCount);
                collectedLevelCurrency += coinsEarned;
                Debug.Log($"[GameManager] Arrow Success! Streak: {p_StreakCount}, Earned: {coinsEarned}, Total Collected: {collectedLevelCurrency}");
                
                // Notify UI
                OnLevelCurrencyChanged?.Invoke(collectedLevelCurrency);
            }

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
            if (UserDataManager.Instance.CurrentLevel < 6) return;
            p_StreakCount++;
            m_GameUI.PlayStreakAnimation();
        }

        public void ResetStreak()
        {
            p_StreakCount = 0;
            ClearActiveCombos();
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
            ClearActiveCombos();
            Debug.Log("Level Complete! Waiting for win screen...");
            
            // Award Collected Currency
            if (collectedLevelCurrency > 0)
            {
                UserDataManager.Instance.AddArrowsCurrency(collectedLevelCurrency);
                Debug.Log($"[GameManager] Level Won! Awarded {collectedLevelCurrency} ArrowsCurrency.");
            }

            if(p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementLevel();
            }
            else
            {
                UserDataManager.Instance.SaveMonthlyChallengeProgress(currentChallengeYear, currentChallengeMonth, currentChallengeDay);
            }

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
                AdsManager.Instance.ShowInterstitial(true);
                AdsManager.Instance.SpawnCoinsSmallExplosion();
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
            ClearActiveCombos();
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

            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
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

        private void UpdateUIVisibility()
        {
            if (m_LevelCurrencyContainer != null)
            {
                m_LevelCurrencyContainer.SetActive(UserDataManager.Instance.CurrentLevel >= 5);
            }
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
            ClearActiveCombos();
            Debug.Log("Time's up!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
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

        public Vector2 GetValidComboPosition(Vector2 idealScreenPos, float minDistancePercent)
        {
            // 1. Clean list (Remove nulls or inactive objects)
            m_ActiveCombos.RemoveAll(c => c == null || !c.gameObject.activeInHierarchy);

            float minDistancePx = Screen.width * minDistancePercent;
            float arrowAvoidanceDistPx = Screen.width * 0.12f;

            // Define bounds (UI Safety Zones)
            float minX = Screen.width * 0.12f;
            float maxX = Screen.width * 0.88f;
            float minY = Screen.height * 0.18f;
            float maxY = Screen.height * 0.82f;

            Camera cam = Camera.main;
            if (cam == null) return idealScreenPos;

            // 2. Identify the grid cell of the click (3x3 grid)
            float safeWidth = maxX - minX;
            float safeHeight = maxY - minY;
            int clickCellX = Mathf.FloorToInt((idealScreenPos.x - minX) / (safeWidth / 3f));
            int clickCellY = Mathf.FloorToInt((idealScreenPos.y - minY) / (safeHeight / 3f));
            clickCellX = Mathf.Clamp(clickCellX, 0, 2);
            clickCellY = Mathf.Clamp(clickCellY, 0, 2);

            // 3. Collect Obstacles
            List<Vector2> comboObstacles = new List<Vector2>();
            foreach (var combo in m_ActiveCombos) comboObstacles.Add(combo.position);

            List<Vector2> arrowObstacles = new List<Vector2>();
            if (GridManager.Instance != null)
            {
                foreach (var arrow in GridManager.Instance.GetAllArrows())
                {
                    if (arrow == null || arrow.segments.Count == 0) continue;
                    arrowObstacles.Add(cam.WorldToScreenPoint(arrow.segments[0].transform.position));
                    if (arrow.segments.Count > 1)
                        arrowObstacles.Add(cam.WorldToScreenPoint(arrow.segments[arrow.segments.Count - 1].transform.position));
                }
            }

            Vector2 bestPos = idealScreenPos;
            float bestScore = -100000f;

            // 4. Sample candidates across ALL cells, with a bonus for NOT being in the click cell
            int gridSamples = 6; // 6x6 samples across the whole safe area
            for (int ix = 0; ix < gridSamples; ix++)
            {
                for (int iy = 0; iy < gridSamples; iy++)
                {
                    // Calculate candidate position centrally in a 6x6 sampling grid
                    Vector2 candidate = new Vector2(
                        minX + (safeWidth / gridSamples) * (ix + 0.5f),
                        minY + (safeHeight / gridSamples) * (iy + 0.5f)
                    );

                    // Determine which of the 3x3 cells this candidate belongs to
                    int cellX = Mathf.FloorToInt((candidate.x - minX) / (safeWidth / 3f));
                    int cellY = Mathf.FloorToInt((candidate.y - minY) / (safeHeight / 3f));
                    
                    bool isOtherCell = (cellX != clickCellX || cellY != clickCellY);

                    // Score candidate
                    float score = CalculatePositionScore(candidate, idealScreenPos, comboObstacles, arrowObstacles, minDistancePx, arrowAvoidanceDistPx);
                    
                    // Variety Bonus: Prefer other cells strongly
                    if (isOtherCell) score += 500f; 

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPos = candidate;
                    }
                }
            }

            return bestPos;
        }

        private float CalculatePositionScore(Vector2 pos, Vector2 ideal, List<Vector2> combos, List<Vector2> arrows, float minDistPx, float arrowAvoidPx)
        {
            float score = 1000f;

            // Penalty for distance from ideal (prefer closer to click)
            score -= Vector2.Distance(pos, ideal) * 0.5f;

            // Hard penalty for combo overlaps
            foreach (var c in combos)
            {
                float dist = Vector2.Distance(pos, c);
                if (dist < minDistPx) score -= (minDistPx - dist) * 10f;
            }

            // Penalty for arrow proximity
            foreach (var a in arrows)
            {
                float dist = Vector2.Distance(pos, a);
                if (dist < arrowAvoidPx) score -= (arrowAvoidPx - dist) * 5f;
            }

            return score;
        }
    }
}
