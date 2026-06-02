using System;
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Assets.Scripts.GameUI;
using Assets.Scripts.Lobby;


namespace Assets.Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        private static bool s_ApplicationIsQuitting;

        public static GameManager Instance 
        { 
            get 
            {
                if (s_ApplicationIsQuitting)
                {
                    return null;
                }

                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("References")]
        public LevelManager levelManager;
        public const int ADS_START_LEVEL = 12;
        public const int HINT_BOOSTER_UNLOCK_LEVEL = 9;
        public const int COINS_START_LEVEL = 5;
        public const int MAGIC_BOOSTER_UNLOCK_LEVEL = 18;
        public const int REFILL_BOOSTER_UNLOCK_LEVEL = 9;
        public const int SHUFFLE_BOOSTER_UNLOCK_LEVEL = 14;

        public static string GetChallengeLevelId(int month, int day, int year)
        {
            return $"Level{165 - (month + day + (year % 10))}";
        }

        public static int GetChallengeLevelNumber(int month, int day, int year)
        {
            return 165 - (month + day + (year % 10));
        }

        public GameObject failureScreen;
        public GameUIContoleer m_GameUI;
        public GameObject m_LobbyUI;
        public GameObject m_FunFact;
        [SerializeField] private TextMeshProUGUI m_FunFactText;
        [SerializeField] private string[] m_FunFactsDat;
        [SerializeField] private GameObject[] m_LevelUIElements;
        private GameObject m_currentLevelUIElement;

        [Header("FTUE Arrow Nudge")]
        [SerializeField] private GameObject m_ArrowNudgePrefab;
        private GameObject m_currentArrowNudge;
        private float m_ArrowNudgeTimer = 0f;
        private const float ARROW_NUDGE_DELAY = 7f;

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
        private bool m_IsChallengeLevelActive;
        public bool IsChallengeLevelActive => m_IsChallengeLevelActive;
        public static bool g_IsFromGame = false;

        public int currentChallengeYear;
        public int currentChallengeMonth;
        public int currentChallengeDay;

        private float hintTimer = 0f;
        private float hintAdPollTimer = 0f;
        private bool cachedHintAdReady = false;
        private const float HintAdPollInterval = 2f;
        private bool isEntranceFinished = false;
        private bool isHintVisible = false;
        private bool isWinning = false;
        private bool isHintActive = false;
        private bool isShuffleInProgress = false;
        private Coroutine m_ShuffleBoosterCoroutine;
        private bool isTimeUp = false;
        public float LastArrowSelectionTime { get; private set; } = -10f;
        public int p_StreakCount { get; private set; } = 0;
        public int p_ComboMultiplier
        {
            get
            {
                if (p_StreakCount >= 10) return 10;
                if (p_StreakCount >= 5) return 5;
                if (p_StreakCount >= 2) return 2;
                return 1;
            }
        }

        private int collectedLevelCurrency = 0; // Currency collected during the current level attempt
        public int CollectedLevelCurrency => collectedLevelCurrency;

        public int PickedArrowsCount => p_pickedArrowIds.Count;

        public bool p_isPlayOnRewarded = false;
        public bool p_isHintRewarded = false;

        private List<RectTransform> m_ActiveCombos = new List<RectTransform>();
        private List<GameObject> m_ActiveVoices = new List<GameObject>();
        private int m_SuccessSaveCounter = 0;
        private float m_LastSaveTime = 0f;
        private Dictionary<string, Queue<GameObject>> m_EffectPools = new Dictionary<string, Queue<GameObject>>();
        private bool wasTimerActiveBeforeAd = false;
        private List<int> p_pickedArrowIds = new List<int>();
        private Coroutine m_PeriodicSaveCoroutine;
        private LevelProgress m_LastSavedProgress;
        private int m_SavedStreakBeforeGameOver = 0;

        private Vector2 m_ScreenCenter;
        private Vector2[] m_QuarterCenters = new Vector2[4];

        public bool CanInteract => isEntranceFinished && !isWinning && !isTimeUp && !isShuffleInProgress &&
                                (failureScreen == null || !failureScreen.activeInHierarchy) &&
                                (m_LobbyUI == null || !m_LobbyUI.activeInHierarchy) &&
                                (m_FunFact == null || !m_FunFact.activeInHierarchy);

        [SerializeField] private GameObject m_WinParticles;
        [SerializeField] private TextMeshProUGUI m_WinLevelText;

        [Header("Post-Win Level Choice")]
        [SerializeField] private PostWinLevelChoiceView m_PostWinLevelChoiceView;
        private string[] m_LevelWinFeedbacks = new string[] { "Perfect !", "Well Done !", "Excellent !", "Amazing !", "Incredible !", "Masterpiece !", "Legendary !" , "You're a Legend !" , "Fantastic!" , "Awesome !" , "Phenomenal!"};
        public int p_lastWinAmount;
        private float m_LastMultiplyPopupTime = -180f; // Initialize so it can show on first win
        private bool m_DeferredLobbyStreakSync;

        public bool IsDeferredLobbyStreakSyncPending => m_DeferredLobbyStreakSync;

        private void OnApplicationQuit()
        {
            s_ApplicationIsQuitting = true;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this); // Destroy the component, not the gameObject
                return;
            }
            Instance = this;
            s_ApplicationIsQuitting = false;
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
                AdsManager.Instance.OnHintRewardReceived += HandleHintRewardReceived;
                AdsManager.Instance.OnPlayOnRewardReceived += ExecutePlayOn;
                AdsManager.Instance.OnMagicRewardReceived += HandleMagicRewardReceived;
                AdsManager.Instance.OnLifeRewardReceived += HandleLifeRewardReceived;
                AdsManager.Instance.OnShuffleRewardReceived += HandleShuffleRewardReceived;
                AdsManager.Instance.OnAdOpened += HandleAdOpened;
                AdsManager.Instance.OnAdClosed += HandleAdClosed;
                AdsManager.Instance.OnAdReadinessChanged += RefreshHintAdReadyCache;
                RefreshHintAdReadyCache();
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
                    if (m_FunFact != null) m_FunFact.SetActive(false);
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
                            if (currentLevel != 1)
                            {
                                Destroy(m_currentLevelUIElement , 5.0f);
                            }
                        }
                    }
                };
            }
            
            // Check and restore progress if exists
            CheckAndRestoreProgress();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnRewardReceived -= HandleRewardReceived;
                AdsManager.Instance.OnHintRewardReceived -= HandleHintRewardReceived;
                AdsManager.Instance.OnPlayOnRewardReceived -= ExecutePlayOn;
                AdsManager.Instance.OnMagicRewardReceived -= HandleMagicRewardReceived;
                AdsManager.Instance.OnLifeRewardReceived -= HandleLifeRewardReceived;
                AdsManager.Instance.OnShuffleRewardReceived -= HandleShuffleRewardReceived;
                AdsManager.Instance.OnAdOpened -= HandleAdOpened;
                AdsManager.Instance.OnAdClosed -= HandleAdClosed;
                AdsManager.Instance.OnAdReadinessChanged -= RefreshHintAdReadyCache;
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
            // Legacy/Fallback for generic GameReward if still used
            if (p_isHintRewarded)
            {
                Debug.Log("[GameManager] Hint Reward Received via legacy path! Triggering show hint...");
                ShowHint();
                p_isHintRewarded = false;
            }
            else if (p_isPlayOnRewarded)
            {
                Debug.Log("[GameManager] PlayOn Reward Received via legacy path (Ad)!");
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
            RefreshHintAdReadyCache();
        }

        private void RefreshHintAdReadyCache()
        {
            cachedHintAdReady = AdsManager.Instance != null &&
                (AdsManager.Instance.IsRewardedReady || AdsManager.Instance.IsInterstitialReady);
        }

        public void ShowHint()
        {
            // Optimization: Use cached arrow list from GridManager instead of FindObjectsOfType
            if (GridManager.Instance == null) return;
            
            List<ArrowController> nonBlocked = GridManager.Instance.GetNonBlockedArrows(1);
            ArrowController bestArrow = (nonBlocked.Count > 0) ? nonBlocked[0] : null;

            // Fallback to any arrow if none are "clear" (e.g. if the level is actually stuck)
            if (bestArrow == null)
            {
                List<ArrowController> allArrows = GridManager.Instance.GetAllArrows();
                if (allArrows.Count > 0) bestArrow = allArrows[0];
            }

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

                // Save level state (including timer) when hint is used
                SaveCurrentProgress();
            }

            ResetHintTimer(false);
        }

        private void HandleHintRewardReceived()
        {
            UserDataManager.Instance.AddHintBooster(1);
            UserDataManager.Instance.UseHintBooster(1);
            if (m_GameUI != null) m_GameUI.HandleHintRewardReceived();
            else ShowHint();
        }

        private void HandleMagicRewardReceived()
        {
            UserDataManager.Instance.AddMagicBooster(1);
            UserDataManager.Instance.UseMagicBooster(1);
            if (m_GameUI != null) m_GameUI.HandleMagicRewardReceived();
            else ExecuteMagicBooster();
        }

        private void HandleLifeRewardReceived()
        {
            UserDataManager.Instance.AddRefillBooster(1);
            UserDataManager.Instance.UseRefillBooster(1);
            if (m_GameUI != null) m_GameUI.HandleRefillRewardReceived();
            else ExecuteRefillLife();
        }

        private void HandleShuffleRewardReceived()
        {
            UserDataManager.Instance.AddShuffleBooster(1);
            UserDataManager.Instance.UseShuffleBooster(1);
            if (m_GameUI != null) m_GameUI.HandleShuffleRewardReceived();
            else ExecuteShuffleBooster();
        }

        public void ExecuteRefillLife()
        {
            ResetLives();
        }

        public void ExecuteMagicBooster()
        {
            StartCoroutine(ExecuteMagicBoosterRoutine());
        }

        private System.Collections.IEnumerator ExecuteMagicBoosterRoutine()
        {
            if (GridManager.Instance == null) yield break;

            List<ArrowController> nonBlocked = GridManager.Instance.GetNonBlockedArrows(1);
            for (int i = 0; i < nonBlocked.Count; i++)
            {
                ArrowController arrow = nonBlocked[i];
                if (arrow != null && arrow.segments.Count > 0)
                {
                    // Focus camera on the arrow being activated
                    if (CameraController.Instance != null)
                    {
                        CameraController.Instance.FocusOn(arrow.GetHeadPosition(), 0.5f);
                    }

                    // Select from the tail (segments[0]) to mimic a user click
                    arrow.OnArrowClicked(arrow.segments[0], Vector2.zero);
                    
                    if (i < nonBlocked.Count - 1)
                    {
                        yield return new WaitForSeconds(0.15f);
                    }
                }
            }
            
            // Save progress after Magic Wand removes arrows
            SaveCurrentProgress();
        }

        public void ExecuteShuffleBooster()
        {
            if (!IsShuffleOn) return;

            if (m_ShuffleBoosterCoroutine != null)
            {
                StopCoroutine(m_ShuffleBoosterCoroutine);
                m_ShuffleBoosterCoroutine = null;
            }
            m_ShuffleBoosterCoroutine = StartCoroutine(ExecuteShuffleBoosterRoutine());
        }

        private void CancelShuffleBoosterInteractionLock()
        {
            isShuffleInProgress = false;
            if (m_ShuffleBoosterCoroutine != null)
            {
                StopCoroutine(m_ShuffleBoosterCoroutine);
                m_ShuffleBoosterCoroutine = null;
            }

            if (GridManager.Instance == null) return;
            List<ArrowController> allArrows = GridManager.Instance.GetAllArrows();
            for (int i = 0; i < allArrows.Count; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow != null)
                {
                    arrow.ResetShuffleInteractionState();
                }
            }
        }

        private System.Collections.IEnumerator ExecuteShuffleBoosterRoutine()
        {
            isShuffleInProgress = true;

            if (GridManager.Instance == null)
            {
                isShuffleInProgress = false;
                m_ShuffleBoosterCoroutine = null;
                yield break;
            }

            List<ShuffleMovePlan> plans = ShuffleBoosterPlanner.BuildShufflePlans();
            if (plans.Count == 0)
            {
                isShuffleInProgress = false;
                m_ShuffleBoosterCoroutine = null;
                yield break;
            }

            List<List<ShuffleMovePlan>> parallelGroups = ShuffleBoosterPlanner.PartitionIntoParallelGroups(plans);
            int movedCount = 0;

            for (int g = 0; g < parallelGroups.Count; g++)
            {
                List<ShuffleMovePlan> group = parallelGroups[g];
                if (group == null || group.Count == 0) continue;

                if (group.Count == 1)
                {
                    yield return group[0].Arrow.ShuffleRelocateRoutine(group[0].HeadSteps);
                    movedCount++;
                }
                else
                {
                    yield return RunShufflePlansInParallel(group);
                    movedCount += group.Count;
                }

                if (g < parallelGroups.Count - 1)
                {
                    yield return new WaitForSeconds(0.08f);
                }
            }

            if (movedCount > 0)
            {
                FinalizeShuffleBoardState();
                RefreshActiveHintPreview();
                SaveCurrentProgress();
            }
            else
            {
                Debug.LogWarning("[GameManager] Shuffle booster: no arrows could be relocated.");
            }

            isShuffleInProgress = false;
            m_ShuffleBoosterCoroutine = null;
        }

        private void FinalizeShuffleBoardState()
        {
            if (GridManager.Instance == null) return;

            List<ArrowController> allArrows = GridManager.Instance.GetAllArrows();
            for (int i = 0; i < allArrows.Count; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow != null)
                {
                    arrow.ResetShuffleInteractionState();
                }
            }

            GridManager.Instance.RebuildOccupancyFromSegments();
            GridManager.Instance.RebuildDependencyTree();
        }

        private System.Collections.IEnumerator RunShufflePlansInParallel(List<ShuffleMovePlan> group)
        {
            int remaining = 0;
            for (int i = 0; i < group.Count; i++)
            {
                ShuffleMovePlan plan = group[i];
                if (plan?.Arrow == null || plan.HeadSteps == null || plan.HeadSteps.Count == 0)
                {
                    continue;
                }
                remaining++;
                StartCoroutine(RunSingleShufflePlan(plan, () => remaining--));
            }

            const float maxWaitSeconds = 12f;
            float elapsed = 0f;
            while (remaining > 0 && elapsed < maxWaitSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (remaining > 0)
            {
                Debug.LogWarning("[GameManager] Shuffle parallel group timed out; forcing arrow reset.");
                for (int i = 0; i < group.Count; i++)
                {
                    if (group[i]?.Arrow != null)
                    {
                        group[i].Arrow.ResetShuffleInteractionState();
                    }
                }
            }
        }

        private System.Collections.IEnumerator RunSingleShufflePlan(ShuffleMovePlan plan, System.Action onComplete)
        {
            yield return plan.Arrow.ShuffleRelocateRoutine(plan.HeadSteps);
            onComplete?.Invoke();
        }

        /// <summary>Re-evaluates hint preview after shuffle without clearing the hint-active state.</summary>
        public void RefreshActiveHintPreview()
        {
            if (!isHintActive || GridManager.Instance == null) return;

            List<ArrowController> allArrows = GridManager.Instance.GetAllArrows();
            for (int i = 0; i < allArrows.Count; i++)
            {
                if (allArrows[i] != null) allArrows[i].HidePreview();
            }

            List<ArrowController> nonBlocked = GridManager.Instance.GetNonBlockedArrows(1);
            ArrowController bestArrow = nonBlocked.Count > 0 ? nonBlocked[0] : null;
            if (bestArrow == null && allArrows.Count > 0)
            {
                bestArrow = allArrows[0];
            }

            if (bestArrow != null && !bestArrow.IsMoving)
            {
                bestArrow.ShowPreview();
            }
        }

        public void PlayOn()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

            Debug.Log("[GameManager] PlayOn method called.");
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForPlayOn();
            }
            else
            {
                // Fallback if no AdsManager
                Debug.Log(">>> No ad AdsManager");
                ExecutePlayOn();
            }
        }

        private int playOnPurchaseCount = 0;

        public int GetPlayOnCost()
        {
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
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
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

            int cost = GetPlayOnCost();
            if (UserDataManager.Instance.ReduceArrowsCurrency(cost))
            {
                Debug.Log($"[GameManager] Bought PlayOn for {cost}.");
                UpdateUserBalanceUI(UserDataManager.Instance.ArrowsCurrency);
                ExecutePlayOn();
            }
            else
            {
                Debug.Log("Open Shop");
                if (m_ShopLayer != null)
                {
                    if (IAPManager.Instance != null) IAPManager.Instance.WarmUp();
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
             playOnPurchaseCount++;
                
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
               RefillLivesForPlayOn();
               if (IsTimedLevel)
               {
                   isTimerActive = true;
                   wasTimerActiveBeforeAd = false;
               }
            }
            
            if (m_SavedStreakBeforeGameOver > 0)
            {
                UserDataManager.Instance.RestoreLevelStreak(m_SavedStreakBeforeGameOver);
                m_SavedStreakBeforeGameOver = 0;
            }

            HideFailureScreen();
            ResetHintTimer();
            SaveCurrentProgress();
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
                UserDataManager.Instance.ResetLevelStreak();
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyMainLevelFailed();
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
            if (m_LobbyUI != null) m_LobbyUI.SetActive(false);
            g_IsFromGame = true;
            p_isLevelProgression = true;
            m_IsChallengeLevelActive = false;

            p_pickedArrowIds.Clear();
            UserDataManager.Instance.ClearLevelProgress(); 
            ResetLevelState();
            
            HideScreens();
            
            m_GameUI.SetGameUIVisible(true);
            UpdateUIVisibility();

            if (levelManager != null)
            {
                levelManager.LoadLevelFromResources(levelId);
            }
            
            ResetLives(); // ResetLives calls SaveCurrentProgress, do it after level is loaded/cleared
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
            CancelShuffleBoosterInteractionLock();
            if (m_FunFact != null) m_FunFact.SetActive(false);
            ResetHintTimer();
            ResetSelectionStates();

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            m_PeriodicSaveCoroutine = StartCoroutine(PeriodicSaveCoroutine());
        }

        public void StartChallengeLevel(string levelId, int year, int month, int day)
        {
            g_IsFromGame = true;
            p_pickedArrowIds.Clear();
            UserDataManager.Instance.ClearLevelProgress();
            
            HideScreens();
            if (m_currentLevelUIElement != null) Destroy(m_currentLevelUIElement);
            
            p_isLevelProgression = false;
            m_IsChallengeLevelActive = true;
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
            m_SavedStreakBeforeGameOver = 0;

            m_GameUI.SetGameUIVisible(true);
            UpdateUIVisibility();

            if (levelManager != null)
            {
                levelManager.LoadChallengeLevelFromResources(levelId);
            }

            ResetLives(); // ResetLives calls SaveCurrentProgress, do it after level state is set

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
            CancelShuffleBoosterInteractionLock();
            if (m_FunFact != null) m_FunFact.SetActive(false);
            ResetHintTimer();
            ResetSelectionStates();

            if (m_PeriodicSaveCoroutine != null) StopCoroutine(m_PeriodicSaveCoroutine);
            m_PeriodicSaveCoroutine = StartCoroutine(PeriodicSaveCoroutine());
        }

        private void ResetLives()
        {
            CurrentLives = maxLives;
            OnLivesChanged?.Invoke(CurrentLives);
            SaveCurrentProgress(); // Save restored lives to level state
        }

        public bool IsOneLifePlayOnEnabled()
        {
            return !isTimeUp
                && RemoteConfigManager.Instance != null
                && RemoteConfigManager.Instance.IsConfigReady
                && RemoteConfigManager.Instance.OneLifePlayOn;
        }

        private void RefillLivesForPlayOn()
        {
            if (IsOneLifePlayOnEnabled())
            {
                CurrentLives = 1;
                OnLivesChanged?.Invoke(CurrentLives);
                SaveCurrentProgress();
            }
            else
            {
                ResetLives();
            }
        }

        public void ResetLevelState()
        {
            activeArrowsCount = 0;
            p_StreakCount = 0;
            collectedLevelCurrency = 0;
            playOnPurchaseCount = 0;
            isWinning = false;
            isEntranceFinished = false;
            CancelShuffleBoosterInteractionLock();
            
            // Clear timer state so it doesn't carry over from previous levels
            isTimerActive = false;
            isTimeUp = false;
            currentTime = 0f;
            lastDisplayedSecond = -1;
            levelDuration = 0f;

            if (m_currentArrowNudge != null) { Destroy(m_currentArrowNudge); m_currentArrowNudge = null; }
            m_ArrowNudgeTimer = 0f;
            
            UpdateArrowsLeftUI(false);
        }

        public void RegisterArrow()
        {
            activeArrowsCount++;
            UpdateArrowsLeftUI(false);
        }

        public void NotifyArrowSuccess(Vector2 clickPosition, int arrowId)
        {
            if (m_currentArrowNudge != null)
            {
                Destroy(m_currentArrowNudge);
                m_currentArrowNudge = null;
            }
            m_ArrowNudgeTimer = 0f;

            if (UserDataManager.Instance.CurrentLevel >= COINS_START_LEVEL)
            {
                // Collect currency logic

                int coinsEarned = p_ComboMultiplier;
                if (p_ComboMultiplier == 1 && UserDataManager.Instance.CurrentLevel >= 25 && UserDataManager.Instance.LevelStreak >= 6)
                {
                    coinsEarned = 2;
                }
                collectedLevelCurrency += coinsEarned;
                
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
                
                // OPTIMIZATION: Only save every 10th arrow success to reduce disk I/O hits
                m_SuccessSaveCounter++;
                if (m_SuccessSaveCounter >= 10)
                {
                    m_SuccessSaveCounter = 0;
                    SaveCurrentProgress();
                }
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
            if (m_currentLevelUIElement != null)
            {
                Destroy(m_currentLevelUIElement);
                m_currentLevelUIElement = null;
            }
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
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayNewRecord();
                }
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
            UserDataManager.Instance.ClearLevelProgress(); // Clear backup immediately on win
            if (m_PeriodicSaveCoroutine != null) { StopCoroutine(m_PeriodicSaveCoroutine); m_PeriodicSaveCoroutine = null; }
            
            if (m_GameUI != null) m_GameUI.ResetComboIndication();
            ClearActiveCombos();
            ClearActiveVoices();
            
            // Award Collected Currency
            if (collectedLevelCurrency > 0)
            {
                UserDataManager.Instance.AddArrowsCurrency(collectedLevelCurrency);
            }

            if(p_isLevelProgression)
            {
                int completedLevel = UserDataManager.Instance.CurrentLevel;
                UserDataManager.Instance.IncrementLevelStreak();
                UserDataManager.Instance.IncrementLevel();
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyMainLevelWon();
                UserDataManager.Instance.ClearCurrentLevelAttempts();
                UserDataManager.Instance.IsRateUsCheckPending = true;

                // --- Legend Pass Progression ---
                if (LegendPassManager.Instance != null)
                {
                    LegendPassManager.Instance.OnLevelComplete();
                }
                // -----------------------------

                // --- Social Platform Achievements (Android/iOS) ---
                CheckAchievements(completedLevel);
                // --------------------------------------------------
            }
            else
            {
                UserDataManager.Instance.SaveMonthlyChallengeProgress(currentChallengeYear, currentChallengeMonth, currentChallengeDay);
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyChallengeLevelWon();
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
                if (levelManager.CurrentLevelId == "Level15")
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_TUTORIAL_COMPLETE);
                }
            }
            // --------------------------------------

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

            // Multiply popup is deferred until lobby when post-win level choice is enabled.
            if (!ShouldShowPostWinLevelChoice())
            {
                yield return TryShowMultiplyCoinsPopupAndWait();
            }

            if (p_isLevelProgression && UserDataManager.Instance.CurrentLevel <= ADS_START_LEVEL)
            {
                Debug.Log($"[GameManager] Below Ads level ({ADS_START_LEVEL}). Transitioning to next level directly.");
                StartLevel($"level{UserDataManager.Instance.CurrentLevel}");
            }
            else if (ShouldShowPostWinLevelChoice())
            {
                yield return PostWinLevelChoiceFlow();
            }
            else
            {
                TransitionToLobbyAfterWin(showAd: true);
            }
        }

        private bool ShouldShowPostWinLevelChoice()
        {
            // Main campaign levels only (timed or not). Challenge levels always use the classic lobby flow.
            if (!p_isLevelProgression || m_IsChallengeLevelActive) return false;
            // Levels 1–12 keep legacy flow (auto-advance / lobby); remote toggle applies from level 13+.
            if (UserDataManager.Instance.CurrentLevel <= ADS_START_LEVEL) return false;
            if (m_PostWinLevelChoiceView == null) return false;
            if (RemoteConfigManager.Instance == null) return false;
            return RemoteConfigManager.Instance.IsPostWinLevelChoiceEnabled;
        }

        private void HideWinSequencePresentation()
        {
            if (m_WinParticles != null) m_WinParticles.SetActive(false);
            if (m_WinLevelText != null) m_WinLevelText.gameObject.SetActive(false);
        }

        private System.Collections.IEnumerator PostWinLevelChoiceFlow()
        {
            HideWinSequencePresentation();

            int completedLevel = UserDataManager.Instance.CurrentLevel - 1;
            m_PostWinLevelChoiceView.Show(completedLevel);

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial(true);
                AdsManager.Instance.SpawnCoinsSmallExplosion();
            }

            while (m_PostWinLevelChoiceView.SelectedChoice == PostWinLevelChoiceView.Choice.None)
            {
                yield return null;
            }

            PostWinLevelChoiceView.Choice choice = m_PostWinLevelChoiceView.SelectedChoice;
            m_PostWinLevelChoiceView.Hide();

            if (choice == PostWinLevelChoiceView.Choice.NextLevel)
            {
                HideScreens();
                if (CameraController.Instance != null) CameraController.Instance.ResetZoom();
                StartLevel($"level{UserDataManager.Instance.CurrentLevel}");
            }
            else
            {
                yield return TryShowMultiplyCoinsPopupAndWait();
                m_DeferredLobbyStreakSync = true;
                TransitionToLobbyAfterWin(showAd: false, deferStreakRefresh: true);
                yield return SyncLobbyStreakUIAfterTransition();
            }
        }

        private bool ShouldOfferMultiplyCoinsPopup()
        {
            bool isCooldownUp = (Time.time - m_LastMultiplyPopupTime) >= 180f;
            bool isAdReady = AdsManager.Instance != null &&
                (AdsManager.Instance.IsMultiplyRewardedReady || AdsManager.Instance.IsInterstitialReady);
            return collectedLevelCurrency > 0
                && m_GameUI != null
                && isCooldownUp
                && UserDataManager.Instance.CurrentLevel > 16
                && UserDataManager.Instance.CurrentLevel % 2 == 1
                && isAdReady;
        }

        private System.Collections.IEnumerator TryShowMultiplyCoinsPopupAndWait()
        {
            if (!ShouldOfferMultiplyCoinsPopup()) yield break;

            m_LastMultiplyPopupTime = Time.time;
            p_lastWinAmount = collectedLevelCurrency;
            MultiplyCoinsPopup popup = m_GameUI.ShowMultiplyCoinsPopup(collectedLevelCurrency);
            if (popup != null)
            {
                while (popup != null) yield return null;
            }
        }

        private void TransitionToLobbyAfterWin(bool showAd, bool deferStreakRefresh = false)
        {
            m_GameUI.SetGameUIVisible(false);
            HideScreens();

            if (showAd && AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial(true);
                AdsManager.Instance.SpawnCoinsSmallExplosion();
            }

            if (CameraController.Instance != null) CameraController.Instance.ResetZoom();

            if (!deferStreakRefresh)
            {
                ScheduleLobbyStreakRefresh();
            }

            OnLevelWon?.Invoke();
        }

        public void ScheduleLobbyStreakRefresh()
        {
            m_DeferredLobbyStreakSync = true;
            StartCoroutine(SyncLobbyStreakUIAfterTransition());
        }

        private System.Collections.IEnumerator SyncLobbyStreakUIAfterTransition()
        {
            // Let lobby hierarchy, ads, and multiply popup fully settle before streak UI + animation.
            yield return null;
            yield return null;

            m_DeferredLobbyStreakSync = false;

            HomeContoller home = FindFirstObjectByType<HomeContoller>();
            if (home != null)
            {
                home.RefreshLevelStreakUI();
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
                m_PlayOnPriceText.text = "Play on - " + GetPlayOnCost().ToString("N0");
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
                if (AdsManager.Instance != null) AdsManager.Instance.ShowSettingsBanner();
            }

            if (p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementCurrentLevelAttempts();
                m_SavedStreakBeforeGameOver = UserDataManager.Instance.LevelStreak;
                UserDataManager.Instance.ResetLevelStreak();
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyMainLevelFailed();
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
            if (AdsManager.Instance != null) AdsManager.Instance.HideSettingsBanner();
            if (m_WinParticles != null) m_WinParticles.SetActive(false);
            if (m_WinLevelText != null) m_WinLevelText.gameObject.SetActive(false);
            if (m_PostWinLevelChoiceView != null) m_PostWinLevelChoiceView.Hide();
            if (m_GameUI != null) m_GameUI.StopFailureFadeCoroutine();
        }


        public void HideFailureScreen()
        {
            if (failureScreen != null)
            {
                failureScreen.SetActive(false);
                if (AdsManager.Instance != null) AdsManager.Instance.HideSettingsBanner();
            }
            if (m_GameUI != null) m_GameUI.StopFailureFadeCoroutine();
        }

        private void Update()
        {
            bool isFailureVisible = failureScreen != null && failureScreen.activeInHierarchy;
            bool isLobbyVisible = m_LobbyUI != null && m_LobbyUI.activeInHierarchy;

            if (isEntranceFinished && !isWinning && !isHintVisible && !isFailureVisible && !isLobbyVisible)
            {
                hintTimer += Time.deltaTime;
                hintAdPollTimer += Time.deltaTime;
                if (hintAdPollTimer >= HintAdPollInterval)
                {
                    hintAdPollTimer = 0f;
                    RefreshHintAdReadyCache();
                }
                if (hintTimer >= 5.0f && cachedHintAdReady)
                {
                    SetHintVisibility(true);
                    SoundManager.Instance.PlayHint();
                }
            }

            // FTUE arrow nudge for levels 2-6
            int nudgeLevel = UserDataManager.Instance.CurrentLevel;
            if (isEntranceFinished && !isWinning && !isFailureVisible && !isLobbyVisible
                && nudgeLevel > 1 && nudgeLevel < 7
                && m_ArrowNudgePrefab != null && m_currentArrowNudge == null)
            {
                m_ArrowNudgeTimer += Time.deltaTime;
                if (m_ArrowNudgeTimer >= ARROW_NUDGE_DELAY && GridManager.Instance != null)
                {
                    List<ArrowController> free = GridManager.Instance.GetNonBlockedArrows(1);
                    if (free.Count > 0 && free[0] != null)
                    {
                        ArrowController targetArrow = free[0];
                        m_currentArrowNudge = Instantiate(m_ArrowNudgePrefab, targetArrow.transform);

                        Vector3 center = Vector3.zero;
                        int segCount = targetArrow.segments.Count;
                        for (int i = 0; i < segCount; i++)
                        {
                            center += targetArrow.segments[i].CachedTransform.position;
                        }
                        if (segCount > 0) center /= segCount;

                        Vector3 local = targetArrow.transform.InverseTransformPoint(center);
                        m_currentArrowNudge.transform.localPosition = new Vector3(local.x, local.y, 0f);
                    }
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
            hintAdPollTimer = 0f;
            SetHintVisibility(false);
            if (m_GameUI != null) m_GameUI.ResetIdleBoosterNudgeTimer();
            
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
                m_StreakRecordContainer.SetActive(UserDataManager.Instance.CurrentLevel >= 25);
            }
        }
        
        // Timer-related methods
        public float PointsToSecondsMultiplier =>
            RemoteConfigManager.Instance != null
                ? RemoteConfigManager.Instance.PtsMul
                : 0.28f;

        public const int ALL_LEVELS_TIMER_START_LEVEL = 15;

        public bool IsAllLevelsTimerEnabled =>
            RemoteConfigManager.Instance != null &&
            RemoteConfigManager.Instance.AllLevelsTimer &&
            UserDataManager.Instance != null &&
            UserDataManager.Instance.CurrentLevel >= ALL_LEVELS_TIMER_START_LEVEL;

        public bool IsShuffleOn =>
            RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.IsShuffleOn;

        public void InitializeTimer(float durationInSeconds)
        {
            Debug.Log($"[GameManager] InitializeTimer called. duration={durationInSeconds}, isLevelProgression={p_isLevelProgression}, AllLevelsTimer={IsAllLevelsTimerEnabled}, ConfigReady={RemoteConfigManager.Instance?.IsConfigReady}, FirebaseNative={RemoteConfigManager.Instance?.IsFirebaseNativeReady}");

            if (p_isLevelProgression)
            {
                if (IsAllLevelsTimerEnabled)
                {
                    levelDuration = durationInSeconds;
                }
                else
                {
                    levelDuration = 0f;
                }
            }
            else
            {
                levelDuration = durationInSeconds;
            }

            if (levelDuration > 0)
            {
                currentTime = levelDuration;
                lastDisplayedSecond = -1;
                Debug.Log($"[GameManager] Timer initialized: {levelDuration}s");
                UpdateTimerUI();
            }
            else
            {
                Debug.Log("[GameManager] Timer NOT set (levelDuration=0).");
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
            ClearActiveVoices();
            Debug.Log("Time's up!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
                if (AdsManager.Instance != null) AdsManager.Instance.ShowSettingsBanner();
            }

            if (p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementCurrentLevelAttempts();
                m_SavedStreakBeforeGameOver = UserDataManager.Instance.LevelStreak;
                UserDataManager.Instance.ResetLevelStreak();
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyMainLevelFailed();
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
                m_PlayOnPriceText.text = "Play on - " + GetPlayOnCost().ToString("N0");
            }

            if (m_UserBalanceText != null)
            {
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

            UserDataManager.Instance.ClearLevelProgress();
            if (m_PeriodicSaveCoroutine != null) { StopCoroutine(m_PeriodicSaveCoroutine); m_PeriodicSaveCoroutine = null; }
            OnGameOver?.Invoke();
        }
        
        public string GetFailureTitle()
        {
            return isTimeUp ? "Time's Up!" : "Continue?";
        }
        
        public string GetFailureSubtitle()
        {
            if (isTimeUp)
            {
                return "Watch an ad to get 60 seconds\nand Keep Playing!";
            }

            if (IsOneLifePlayOnEnabled())
            {
                return "Watch an ad to add 1 live\nand Keep Playing!";
            }

            return "Watch an ad to refill lives\nand Keep Playing!";
        }

         public string GetFailureAdText()
        {
            if (isTimeUp)
            {
                return "+60 Seconds";
            }

            if (IsOneLifePlayOnEnabled())
            {
                return "Add Life";
            }

            return "Add More Lives";
        }

        public string GetFailureDescription()
        {
            if (isTimeUp)
            {
                return "Time's up! get more 60 seconds with coins or by watching a short ad.";
            }

            if (IsOneLifePlayOnEnabled())
            {
                return "Add 1 live with coins or by watching a short ad.";
            }

            return "Refill lives with coins or by watching a short ad.";
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
                    // OPTIMIZATION #6: Return to pool
                    ReturnEffect(combo.gameObject);
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
                    // OPTIMIZATION #6: Return to pool
                    ReturnEffect(voice);
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

        #region Object Pooling
        public GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null) return null;
            string poolKey = prefab.name;
            if (!m_EffectPools.ContainsKey(poolKey)) m_EffectPools[poolKey] = new Queue<GameObject>();

            GameObject obj;
            if (m_EffectPools[poolKey].Count > 0)
            {
                obj = m_EffectPools[poolKey].Dequeue();
                if (obj == null) return SpawnEffect(prefab, position, rotation, parent); // Handle destroyed objects
                
                obj.transform.SetParent(parent);
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
            }
            else
            {
                obj = Instantiate(prefab, position, rotation, parent);
                // Tag the object with its pool key so we know where to return it
                EffectPoolTag tag = obj.AddComponent<EffectPoolTag>();
                tag.PoolKey = poolKey;
            }
            return obj;
        }

        public void ReturnEffect(GameObject obj)
        {
            if (obj == null) return;
            EffectPoolTag tag = obj.GetComponent<EffectPoolTag>();
            if (tag != null)
            {
                obj.SetActive(false);
                if (!m_EffectPools.ContainsKey(tag.PoolKey)) m_EffectPools[tag.PoolKey] = new Queue<GameObject>();
                m_EffectPools[tag.PoolKey].Enqueue(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        private class EffectPoolTag : MonoBehaviour
        {
            public string PoolKey;
        }
        #endregion

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
            m_IsChallengeLevelActive = progress.isChallenge;
            currentChallengeYear = progress.challengeYear;
            currentChallengeMonth = progress.challengeMonth;
            currentChallengeDay = progress.challengeDay;
            p_pickedArrowIds = new List<int>(progress.pickedArrowIds);

            // Reset arrow count before loading
            activeArrowsCount = 0;
            UpdateArrowsLeftUI(false);

            isTimerActive = false;
            isTimeUp = false;
            lastDisplayedSecond = -1;
            levelDuration = 0f; // Will be set by LoadLevel

            if (levelManager != null)
            {
                m_GameUI.SetGameUIVisible(true);
                UpdateUIVisibility();
                if (progress.isChallenge)
                {
                    levelManager.LoadChallengeLevelFromResources(progress.levelId, p_pickedArrowIds);
                }
                else
                {
                    levelManager.LoadLevelFromResources(progress.levelId, p_pickedArrowIds);
                }
            }

            // Restore state AFTER level loading to avoid it being overwritten by ClearLevel/ResetLevelState
            collectedLevelCurrency = progress.collectedCoins;
            playOnPurchaseCount = progress.playOnPurchaseCount;

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

            // Enforce that normal levels never have a timer (unless AllLevelsTimer is on)
            if (p_isLevelProgression && !IsAllLevelsTimerEnabled)
            {
                levelDuration = 0f;
                currentTime = 0f;
                isTimerActive = false;
            }

            m_LastSavedProgress = progress;
            Debug.Log($"[GameManager] Progress Restored: Level={progress.levelId}, Time={currentTime}/{levelDuration}, Lives={CurrentLives}, Active={isTimerActive}");

            OnLevelStarted?.Invoke();
            OnLevelCurrencyChanged?.Invoke(collectedLevelCurrency, Vector2.zero);
            
            isEntranceFinished = false;
            isWinning = false;
            CancelShuffleBoosterInteractionLock();
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
                else if (m_LastSavedProgress.playOnPurchaseCount != playOnPurchaseCount) changed = true;
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
            progress.playOnPurchaseCount = playOnPurchaseCount;
            progress.hasProgress = true;

            m_LastSavedProgress = progress;
            m_LastSaveTime = Time.time;
            m_SuccessSaveCounter = 0; // Reset counter whenever a real disk save occurs
            UserDataManager.Instance.SaveLevelProgress(progress);
            Debug.Log($"[GameManager] Progress saved for level {progress.levelId}. Picked arrows: {progress.pickedArrowIds.Count}, Time={currentTime}/{levelDuration}, Active={isTimerActive}");
        }

        private void CheckAchievements(int completedLevel)
        {
            if (SocialManager.Instance == null) return;

            string milestone = string.Empty;

            switch (completedLevel)
            {
                case 15: milestone = "tutorial"; break;
                case 25: milestone = "lvl25"; break;
                case 50: milestone = "lvl50"; break;
                case 75: milestone = "lvl75"; break;
                case 100: milestone = "lvl100"; break;
                case 150: milestone = "lvl150"; break;
                case 200: milestone = "lvl200"; break;
                case 250: milestone = "lvl250"; break;
                case 300: milestone = "lvl300"; break;
                case 400: milestone = "lvl400"; break;
                case 500: milestone = "lvl500"; break;
                case 600: milestone = "lvl600"; break;
                case 750: milestone = "lvl750"; break;
                case 1000: milestone = "lvl1000"; break;
            }

            if (!string.IsNullOrEmpty(milestone))
            {
                SocialManager.Instance.UnlockAchievement(milestone);
            }
        }

        private System.Collections.IEnumerator PeriodicSaveCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(5.0f);
                if (isEntranceFinished && !isWinning && !isTimeUp)
                {
                    // OPTIMIZATION: Skip periodic save if a save was recently triggered (e.g. by 10th arrow or life loss)
                    if (Time.time - m_LastSaveTime >= 4.5f)
                    {
                        SaveCurrentProgress();
                    }
                }
            }
        }
    }
}
