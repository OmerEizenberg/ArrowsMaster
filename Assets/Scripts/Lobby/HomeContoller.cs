using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro; 

using Assets.Scripts.Core;
using Assets.Scripts.Data;

using Assets.Scripts.Utils;
using Assets.Scripts.LiveOps;
using Assets.Scripts.GAE;

namespace Assets.Scripts.Lobby
{
    public class HomeContoller : MonoBehaviour
    {
        [SerializeField] private GameObject m_LobbyUI;
        [SerializeField] private GameObject m_GameUI;


        [SerializeField] private GameObject m_CalanderLayer;
        [SerializeField] private GameObject m_SettingsLayer;
        [SerializeField] private GameObject m_DonateLayer;
        [SerializeField] private GameObject m_NoAdsLayer;
        [SerializeField] private GameObject m_ShopLayer;
        [SerializeField] private GameObject m_NoAdsCoinsBundleButton;
        [SerializeField] private GameObject m_NoAdsBadge;
        [SerializeField] private GameObject m_ShareBadge;
        [SerializeField] private GameObject m_LobbyAdReadyImage;
        [SerializeField] private GameObject m_ChallengeNotificationImage;
        [SerializeField] private Button m_ChallengeButton;
        [SerializeField] private GameObject m_LockedButton;
        [SerializeField] private GameObject m_LockedChallengeTooltip;
        [SerializeField] private Image m_ChallengeIcon;
        [SerializeField] private TextMeshProUGUI m_ChallengeText;

        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_DifficultyText;
        [SerializeField] private TextMeshProUGUI m_LobbyCurrencyText;
        [SerializeField] private TextMeshProUGUI m_ShopCurrencyText;
        [SerializeField] private TextMeshProUGUI m_RewardedAdAmountText;
        [SerializeField] private GameObject m_WatchAdForCoinsButton;

        [Header("Manager References")]
        public GameManager gameManager;
        
        [Header("UI Panels")]
        [SerializeField] private GameObject m_LevelStreakIcon;
        [SerializeField] private TextMeshProUGUI m_LevelStreakText;
        [SerializeField] private TextMeshProUGUI m_LevelStreakTextShade;
        [SerializeField] private GameObject m_StreakPopup;
        [SerializeField] private Sprite m_LevelStreakActiveSprite;
        [SerializeField] private Sprite m_LevelStreakInactiveSprite;
        
        [Header("Streak Fire Jump Settings")]
        [SerializeField] private float m_FireJumpDuration = 0.85f;
        [SerializeField] private float m_FireJumpArcHeightPercent = 0.15f; // % of screen height
        [SerializeField] private float m_FireJumpStartOffsetXPercent = 0.3f; // % of screen width
        [SerializeField] private float m_FireJumpStartOffsetYPercent = -0.4f; // % of screen height
        [SerializeField] private float m_FireJumpStartScale = 0.5f;
        [SerializeField] private float m_FireJumpEndScale = 2.0f;
        [SerializeField] private float m_FireJumpRotation = 720f;
        [SerializeField] private float m_FireJumpStartDelay = 0.75f;


        [SerializeField] private Color m_CircleColor;
        [SerializeField] private Color m_SuperHardColor;
        [SerializeField] private Color m_NightmareColor;
        [SerializeField] private Color m_HardColor;
        [SerializeField] private Color m_EasyColor;
        
        [SerializeField] private Color m_LevelColor;
        [SerializeField] private MonthlyChallengeController m_MonthlyChallengeController;
        [SerializeField] private LegendPassUI m_LegendPassUI;
        [SerializeField] private Transform m_LiveOpIconsContainer;
        public Transform LiveOpIconsContainer => m_LiveOpIconsContainer;

        [Header("Bottom Bar Tabs")]
        [SerializeField] private RectTransform m_SelectedTabBg;
        [SerializeField] private RectTransform m_HomeTab;
        [SerializeField] private RectTransform m_CalendarTab;
        [SerializeField] private RectTransform m_ShopTab;
        [SerializeField] private RectTransform m_HomeIcon;
        [SerializeField] private RectTransform m_CalendarIcon;
        [SerializeField] private RectTransform m_ShopIcon;
        [SerializeField] private RectTransform m_HomeText;
        [SerializeField] private RectTransform m_CalendarText;
        [SerializeField] private RectTransform m_ShopText;
        private Coroutine m_TabSlideCoroutine;

        [Header("Swipe Navigation")]
        [SerializeField] private float m_SwipeThreshold = 100f;
        private Vector2 m_SwipeStartPos;
        private bool m_IsSwiping;

        // Currency animation
        [SerializeField] private float m_CoinAnimDuration = 1.5f;
        [SerializeField] private float m_CoinScalePunch = 2.25f;
        // Static so the last displayed value is remembered across OnDisable/OnEnable cycles
        private static int s_LastDisplayedCurrencyValue = -1;
        private static int s_LastDisplayedLevelValue = -1;
        [SerializeField] private RollingLevelEffect m_LevelRoller;
        private Coroutine m_FireAnimationCoroutine;
        private GameObject m_ActiveFireSprite;
        private Coroutine m_CoinCountCoroutine;
        private Coroutine m_LobbyScaleCoroutine;
        private Coroutine m_ShopScaleCoroutine;
        private Coroutine m_TooltipCoroutine;
        private int m_LastToggleFrame = -1;

        public bool IsNoAdsLayerVisible => m_NoAdsLayer != null && m_NoAdsLayer.activeInHierarchy;

        public static bool IsNoAdsOfferVisible
        {
            get
            {
                HomeContoller home = FindFirstObjectByType<HomeContoller>();
                return home != null && home.IsNoAdsLayerVisible;
            }
        }

        private static void NotifyGaePendingCommit()
        {
            if (GAEManager.Instance != null)
            {
                GAEManager.Instance.TryCommitPendingArrows();
            }
        }

        private void HideNoAdsLayer()
        {
            if (m_NoAdsLayer == null || !m_NoAdsLayer.activeInHierarchy)
            {
                return;
            }

            m_NoAdsLayer.SetActive(false);
            NotifyGaePendingCommit();
        }


        private void Awake()
        {
            if (m_CalanderLayer != null) m_CalanderLayer.SetActive(false);
            if (m_SettingsLayer != null) m_SettingsLayer.SetActive(false);
            if (m_DonateLayer != null) m_DonateLayer.SetActive(false);
            if (m_NoAdsLayer != null) HideNoAdsLayer();
            if (m_ShopLayer != null) m_ShopLayer.SetActive(false);
        }

        private void Start()
        {
            UserDataManager.Instance.OnLevelChanged += OnLevelChangedWhileInLobby;
            UserDataManager.Instance.OnCurrencyChanged += UpdateCurrencyUI;
            UserDataManager.Instance.OnMonthlyProgressChanged += UpdateChallengeNotification;
            StartCoroutine(PrewarmBannerAfterLobbySettled());
        }

        private IEnumerator PrewarmBannerAfterLobbySettled()
        {
            // Create the native banner view during lobby idle, not on game-over/settings transitions.
            yield return new WaitForSeconds(2f);

            const float sdkWaitTimeoutSeconds = 30f;
            float waited = 0f;
            while (AdsManager.Instance != null &&
                   !AdsManager.Instance.IsInitialized &&
                   waited < sdkWaitTimeoutSeconds)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }

            if (AdsManager.Instance != null)
                AdsManager.Instance.PrewarmSettingsBanner();
        }

        private void OnEnable()
        {
            if (UserDataManager.Instance.CurrentLevel < GameManager.COINS_START_LEVEL)
            {
                m_LobbyCurrencyText.transform.parent.gameObject.SetActive(false);
                m_NoAdsBadge.SetActive(false);
                m_ShareBadge.SetActive(false);
            }
            else
            {
                m_LobbyCurrencyText.transform.parent.gameObject.SetActive(true);
                m_NoAdsBadge.SetActive(true);
                m_ShareBadge.SetActive(true);
            }

    //   PlayerPrefs.DeleteAll();
            RefreshLobbyUI();

            int currentCoins = UserDataManager.Instance.ArrowsCurrency;

            if (s_LastDisplayedCurrencyValue < 0)
            {
                // Very first time ever — set immediately, no animation
                s_LastDisplayedCurrencyValue = currentCoins;
                SetCurrencyTextImmediate(currentCoins);
            }
            else if (s_LastDisplayedCurrencyValue != currentCoins)
            {
                // Coins changed while we were away — animate from last known to current
                int startValue = s_LastDisplayedCurrencyValue;
                SetCurrencyTextImmediate(startValue);
                UpdateCurrencyUI(currentCoins);
            }
            else
            {
                // No change — just display current value instantly
                SetCurrencyTextImmediate(currentCoins);
            }

            WarmUpIAPStore();

            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnNoAdsStatusChanged += HandleNoAdsStatusChanged;
                IAPManager.Instance.OnPurchaseSuccess += HandlePurchaseSuccess;
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived += HandleCoinsRewardReceived;
                AdsManager.Instance.OnAdReadinessChanged += UpdateLobbyAdReadyImage;
                UpdateLobbyAdReadyImage();
            }

            if(GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                if (m_CalanderLayer != null && !m_CalanderLayer.activeSelf)
                {
                    OnCalanderButtonClicked();
                }
            }

            if (RemoteConfigManager.Instance != null)
            {
                if (RemoteConfigManager.Instance.IsConfigReady)
                {
                    CheckForUpdates();
                    UpdateRewardedAdAmount();
                }
                else
                {
                    RemoteConfigManager.Instance.OnConfigInitialized += CheckForUpdates;
                    RemoteConfigManager.Instance.OnConfigInitialized += UpdateRewardedAdAmount;
                }
            }

            CheckForRateUsPopup();
            UpdateChallengeNotification();
            CheckForNoAdsOffer();

            SnapTabBackground();
            
            if (LiveOpManager.Instance != null)
            {
                LiveOpManager.Instance.CheckLiveOps();
                LiveOpManager.Instance.SyncLobbyIcons();
            }

            StartCoroutine(DeferredGaeCommitAfterLobbyPopups());
        }

        private IEnumerator DeferredGaeCommitAfterLobbyPopups()
        {
            yield return null;
            yield return null;
            NotifyGaePendingCommit();
        }

        private void OnDisable()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
                IAPManager.Instance.OnPurchaseSuccess -= HandlePurchaseSuccess;
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived -= HandleCoinsRewardReceived;
                AdsManager.Instance.OnAdReadinessChanged -= UpdateLobbyAdReadyImage;
            }

            if (RemoteConfigManager.Instance != null)
            {
                RemoteConfigManager.Instance.OnConfigInitialized -= CheckForUpdates;
                RemoteConfigManager.Instance.OnConfigInitialized -= UpdateRewardedAdAmount;
            }

            // Stop any running animation coroutines
            if (m_CoinCountCoroutine != null) StopCoroutine(m_CoinCountCoroutine);
            if (m_LobbyScaleCoroutine != null) StopCoroutine(m_LobbyScaleCoroutine);
            if (m_ShopScaleCoroutine != null) StopCoroutine(m_ShopScaleCoroutine);
            if (m_TabSlideCoroutine != null) StopCoroutine(m_TabSlideCoroutine);
            
            CleanupFireAnimation();
        }

        private void OnDestroy()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnLevelChanged -= OnLevelChangedWhileInLobby;
                UserDataManager.Instance.OnCurrencyChanged -= UpdateCurrencyUI;
                UserDataManager.Instance.OnMonthlyProgressChanged -= UpdateChallengeNotification;
            }
        }

        private void Update()
        {
            HandleSwipeNavigation();
        }

        private void HandleSwipeNavigation()
        {
            // Reset swiping state if mouse is up
            if (Input.GetMouseButtonUp(0))
            {
                m_IsSwiping = false;
            }

            // If sub-overlays are open (Settings, Donate, NoAds), block swiping to avoid accidental transitions
            if ((m_SettingsLayer != null && m_SettingsLayer.activeInHierarchy) ||
                (m_DonateLayer != null && m_DonateLayer.activeInHierarchy) ||
                (m_NoAdsLayer != null && m_NoAdsLayer.activeInHierarchy))
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                m_SwipeStartPos = Input.mousePosition;
                m_IsSwiping = true;
            }
            else if (Input.GetMouseButtonUp(0) && m_IsSwiping)
            {
                m_IsSwiping = false;

                Vector2 swipeEndPos = Input.mousePosition;
                Vector2 delta = swipeEndPos - m_SwipeStartPos;

                if (Mathf.Abs(delta.x) > m_SwipeThreshold && Mathf.Abs(delta.y) < Mathf.Abs(delta.x))
                {
                    if (delta.x > 0)
                    {
                        // Finger moves towards Right
                        OnSwipeLeft(swipeEndPos);
                    }
                    else
                    {
                        // Finger moves towards Left
                        OnSwipeRight(swipeEndPos);
                    }
                }
            }
        }

        private void OnSwipeRight(Vector2 endPos)
        {
            // Calendar (left) -> Swipe Right (finger moves left) -> Home
            // Home (middle) -> Swipe Right (finger moves left) -> Shop
            
            if (m_CalanderLayer != null && m_CalanderLayer.activeInHierarchy)
            {
                 if (endPos.y > Screen.height * 0.33f)
                {
                    if (m_MonthlyChallengeController != null)
                    {
                        m_MonthlyChallengeController.NextMonth();
                    }
                }else{
                    // We are in Calendar, go to Home
                    OnCalanderButtonClicked(); // Toggles Calendar off, showing Home
                }
            }
            else if (m_ShopLayer != null && !m_ShopLayer.activeInHierarchy)
            {
                // We are in Home (since Shop is off and Calendar was checked above), go to Shop
                ShowShop();
            }
        }

        private void OnSwipeLeft(Vector2 endPos)
        {
            // Home (middle) -> Swipe Left (finger moves right) -> Calendar
            // Calendar (left) -> Swipe Left (finger moves right) -> Prev Month (if top 2/3 of screen)
            


            if (m_CalanderLayer != null && m_CalanderLayer.activeInHierarchy)
            {
                // We are in Calendar and swiping finger to the right.
                // User wants to move to previous month if finger is above 0.33 of screen height.
                if (endPos.y > Screen.height * 0.33f)
                {
                    if (m_MonthlyChallengeController != null)
                    {
                        m_MonthlyChallengeController.PrevMonth();
                    }
                }
            }
            else if (m_CalanderLayer != null && !m_CalanderLayer.activeInHierarchy)
            {
                // We are in Home, go to Calendar
                OnCalanderButtonClicked(); // Toggles Calendar on
            }
        }

        private void UpdateRewardedAdAmount()
        {
            if (m_RewardedAdAmountText == null) return;

            int amount = 2000;
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
            {
                amount = RemoteConfigManager.Instance.CoinsRewardedAd;
            }

            m_RewardedAdAmountText.text = amount.ToString("N0");
        }

        private void SetCurrencyTextImmediate(int amount)
        {
            string formatted = amount.ToString("N0");
            if (m_LobbyCurrencyText != null) m_LobbyCurrencyText.text = formatted;
            if (m_ShopCurrencyText != null) m_ShopCurrencyText.text = formatted;
        }

        private void UpdateCurrencyUI(int newAmount)
        {
            if (!gameObject.activeInHierarchy)
            {
                // Update the text immediately so it's correct if shown via another layer (e.g. Shop),
                // but DON'T update s_LastDisplayedCurrencyValue so OnEnable can detect and animate the change later.
                SetCurrencyTextImmediate(newAmount);
                return;
            }

            if (m_CoinCountCoroutine != null) StopCoroutine(m_CoinCountCoroutine);
            m_CoinCountCoroutine = StartCoroutine(AnimateCurrencyText(s_LastDisplayedCurrencyValue, newAmount));

            // Scale punch on both texts
            if (m_LobbyCurrencyText != null)
            {
                if (m_LobbyScaleCoroutine != null) StopCoroutine(m_LobbyScaleCoroutine);
                m_LobbyScaleCoroutine = StartCoroutine(ScalePunch(m_LobbyCurrencyText.transform));
            }
            if (m_ShopCurrencyText != null)
            {
                if (m_ShopScaleCoroutine != null) StopCoroutine(m_ShopScaleCoroutine);
                m_ShopScaleCoroutine = StartCoroutine(ScalePunch(m_ShopCurrencyText.transform));
            }
        }

        private IEnumerator AnimateCurrencyText(int fromValue, int toValue)
        {
            float elapsed = 0f;
            float duration = m_CoinAnimDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease out for a satisfying deceleration
                float easedT = 1f - (1f - t) * (1f - t);
                int current = (int)Mathf.Lerp(fromValue, toValue, easedT);
                s_LastDisplayedCurrencyValue = current;
                SetCurrencyTextImmediate(current);
                yield return null;
            }

            // Ensure final value is exact
            s_LastDisplayedCurrencyValue = toValue;
            SetCurrencyTextImmediate(toValue);
            m_CoinCountCoroutine = null;
        }

        private IEnumerator ScalePunch(Transform target)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 punchScale = originalScale * m_CoinScalePunch;
            float halfDuration = m_CoinAnimDuration * 0.35f;

            // Scale up
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(originalScale, punchScale, t);
                yield return null;
            }

            // Scale back down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(punchScale, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }

        private void HandlePurchaseSuccess(string productId)
        {
            HideShop();
        }

        private void HandleCoinsRewardReceived()
        {
            HideShop();
        }

        private void HandleNoAdsStatusChanged(bool hasNoAds)
        {
            if (hasNoAds)
            {
                if (m_NoAdsLayer != null) HideNoAdsLayer();
            }
            RefreshLobbyUI();
        }


        private void OnLevelChangedWhileInLobby()
        {
            if (!IsLobbyVisible()) return;
            RefreshLobbyUI();
        }

        private bool IsLobbyVisible()
        {
            GameObject lobby = m_LobbyUI;
            if (lobby == null && GameManager.Instance != null)
                lobby = GameManager.Instance.m_LobbyUI;
            return lobby != null && lobby.activeInHierarchy;
        }

        public void RefreshLobbyUI()
        {
            if (!IsLobbyVisible()) return;

            bool deferStreak = GameManager.Instance != null && GameManager.Instance.IsDeferredLobbyStreakSyncPending;
            if (!deferStreak)
            {
                RefreshLevelStreakUI();
            }

            // Hide gameplay canvas via GameUIController — do not deactivate m_GameUI.gameObject
            // (that component lives on TopBar; disabling it breaks the bar on the next level).
            if (GameManager.Instance != null && GameManager.Instance.m_GameUI != null)
            {
                GameManager.Instance.m_GameUI.SetGameUIVisible(false, notifyLobbyWhenHidden: false);
            }
            else if (m_GameUI != null && m_GameUI.activeSelf)
            {
                m_GameUI.SetActive(false);
                if (m_LobbyUI != null) m_LobbyUI.SetActive(true);
            }

            m_TitleText.text = "Arrows Master";
            m_LevelText.isRightToLeftText = false;
            
            string levelId;
            string folder;
            
            if (GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                m_LevelText.text = $"Challenge {m_MonthlyChallengeController.p_CurrentMonth}/{m_MonthlyChallengeController.p_CurrentDay}/{m_MonthlyChallengeController.p_CurrentYear}";
                int month = m_MonthlyChallengeController.p_CurrentMonth;
                int day = m_MonthlyChallengeController.p_CurrentDay;
                int year = m_MonthlyChallengeController.p_CurrentYear;
                levelId = GameManager.GetChallengeLevelId(month, day, year);
                folder = "ChallengeLevels";
            }
            else
            {
                int currentLevel = UserDataManager.Instance.CurrentLevel;
                bool isVisible = gameObject.activeInHierarchy;

                // Handle Rolling Animation logic
                if (s_LastDisplayedLevelValue < 0)
                {
                    s_LastDisplayedLevelValue = currentLevel;
                    m_LevelText.text = $"Level {currentLevel}";
                }
                else if (isVisible && s_LastDisplayedLevelValue < currentLevel)
                {
                    if (m_LevelRoller == null) m_LevelRoller = m_LevelText.GetComponent<RollingLevelEffect>();
                    if (m_LevelRoller == null) m_LevelRoller = m_LevelText.gameObject.AddComponent<RollingLevelEffect>();

                    m_LevelRoller.AnimateLevel(s_LastDisplayedLevelValue, currentLevel);
                    s_LastDisplayedLevelValue = currentLevel;
                }
                else if (isVisible || s_LastDisplayedLevelValue != currentLevel)
                {
                    // If visible, update text and sync value. 
                    // If not visible, we only update if it has changed, but we keep the text at the LAST DISPLAYED value 
                    // so OnEnable (when we become visible) can trigger the animation from s_LastDisplayedLevelValue to currentLevel.
                    if (isVisible)
                    {
                        m_LevelText.text = $"Level {currentLevel}";
                        s_LastDisplayedLevelValue = currentLevel;
                    }
                    else
                    {
                        m_LevelText.text = $"Level {s_LastDisplayedLevelValue}";
                    }
                }

                levelId = $"Level{currentLevel}";
                folder = "Levels";
            }

            TextAsset jsonFile = null;

            if (folder == "Levels" && GameManager.Instance != null && GameManager.Instance.levelManager != null)
            {
                jsonFile = GameManager.Instance.levelManager.GetLevelTextAsset(levelId);
            }
            else
            {
                jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId}");
            
                // Case-insensitive fallback
                if (jsonFile == null)
                {
                    if (levelId.StartsWith("level")) 
                        jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId.Replace("level", "Level")}");
                    else if (levelId.StartsWith("Level"))
                        jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId.Replace("Level", "level")}");
                }
            }
            

                int levelForDifficulty = UserDataManager.Instance.CurrentLevel;
                if (GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
                {
                    levelForDifficulty = GameManager.GetChallengeLevelNumber(
                        m_MonthlyChallengeController.p_CurrentMonth,
                        m_MonthlyChallengeController.p_CurrentDay,
                        m_MonthlyChallengeController.p_CurrentYear);
                }

                int lastDifit = levelForDifficulty % 10;
                
                if (lastDifit == 4 || lastDifit == 9 || levelForDifficulty < 7)
                {
                    m_DifficultyText.text = "Easy Level";
                    Color c = m_EasyColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (lastDifit == 1 || lastDifit == 2 || lastDifit == 5 || lastDifit == 7 || lastDifit == 0)
                {
                    m_DifficultyText.text = "Hard Level";
                    Color c = m_HardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (lastDifit == 3 || lastDifit == 6)
                {
                    m_DifficultyText.text = "Super Hard Level";
                    Color c = m_SuperHardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else
                {
                    m_DifficultyText.text = "Nightmare Level";
                    Color c = m_NightmareColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }

            m_LevelText.color = m_LevelColor;

            // Hide the No Ads + Coins bundle button if the user already has No Ads
            if (m_NoAdsCoinsBundleButton != null && IAPManager.Instance != null)
            {
                m_NoAdsCoinsBundleButton.SetActive(!IAPManager.Instance.HasNoAds);
                if (UserDataManager.Instance.CurrentLevel >= GameManager.COINS_START_LEVEL)


                {
                    m_NoAdsBadge.SetActive(!IAPManager.Instance.HasNoAds);
                }
            }

            UpdateLobbyAdReadyImage();
            UpdateChallengeLock();
        }

        /// <summary>
        /// Syncs lobby streak labels from saved data. Call whenever the lobby becomes visible
        /// (HomeController may stay enabled while only m_LobbyUI is toggled).
        /// </summary>
        public void RefreshLevelStreakUI()
        {
            if (UserDataManager.Instance == null || m_LevelStreakIcon == null) return;

            m_LevelStreakIcon.SetActive(true);

            int levelStreak = UserDataManager.Instance.LevelStreak;
            bool showAnimation = UserDataManager.Instance.NeedsLevelStreakAnimation && levelStreak > 0;

            if (UserDataManager.Instance.NeedsLevelStreakAnimation && levelStreak <= 0)
            {
                UserDataManager.Instance.NeedsLevelStreakAnimation = false;
            }

            bool isStreakActive = levelStreak >= 6;

            var iconImage = m_LevelStreakIcon.GetComponent<UnityEngine.UI.Image>();
            if (iconImage != null && m_LevelStreakActiveSprite != null && m_LevelStreakInactiveSprite != null)
            {
                iconImage.sprite = isStreakActive ? m_LevelStreakActiveSprite : m_LevelStreakInactiveSprite;
            }

            var fireSkew = m_LevelStreakIcon.GetComponent<Assets.Scripts.GameUI.UIFireSkew>();
            if (fireSkew != null)
            {
                if (fireSkew.enabled != isStreakActive)
                {
                    fireSkew.enabled = isStreakActive;
                    m_LevelStreakIcon.GetComponent<UnityEngine.UI.Graphic>()?.SetVerticesDirty();
                }
            }

            int displayStreak = showAnimation ? levelStreak - 1 : levelStreak;
            SetLevelStreakText(displayStreak);

            if (showAnimation)
            {
                CleanupFireAnimation();
                MonoBehaviour runner = isActiveAndEnabled
                    ? this
                    : (GameManager.Instance != null ? GameManager.Instance : this);
                m_FireAnimationCoroutine = runner.StartCoroutine(AnimateStreakFire(levelStreak));
            }
        }

        private void SetLevelStreakText(int streakValue)
        {
            string text = Mathf.Max(0, streakValue).ToString();
            if (m_LevelStreakText != null) m_LevelStreakText.text = text;
            if (m_LevelStreakTextShade != null) m_LevelStreakTextShade.text = text;
        }

        private void UpdateChallengeLock()
        {
            if (UserDataManager.Instance == null) return;

            bool isLocked = UserDataManager.Instance.CurrentLevel < 20;

            if (m_ChallengeButton != null)
            {
                m_ChallengeButton.interactable = !isLocked;
            }

            if (m_LockedButton != null)
            {
                m_LockedButton.SetActive(isLocked);
            }

            float alpha = isLocked ? 0.5f : 1.0f;

            if (m_ChallengeIcon != null)
            {
                Color color = m_ChallengeIcon.color;
                color.a = alpha;
                m_ChallengeIcon.color = color;
            }

            if (m_ChallengeText != null)
            {
                Color color = m_ChallengeText.color;
                color.a = alpha;
                m_ChallengeText.color = color;
            }

            UpdateChallengeNotification();
        }

        public void OnLegendPassClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            if (UserDataManager.Instance.CurrentLevel < 30) return;

            WarmUpIAPStore();

            if (m_LegendPassUI != null)
            {
                m_LegendPassUI.gameObject.SetActive(true);
            }
        }

        private void UpdateChallengeNotification()
        {
            if (m_ChallengeNotificationImage == null || UserDataManager.Instance == null) return;

            if (UserDataManager.Instance.CurrentLevel < 20)
            {
                m_ChallengeNotificationImage.SetActive(false);
                return;
            }

            // Get current date (only date portion)
            System.DateTime today = System.DateTime.Today;
            string lastSeenStr = PlayerPrefs.GetString("LastSeenChallengeDate", string.Empty);
            
            bool showNotification = true;
            if (!string.IsNullOrEmpty(lastSeenStr))
            {
                if (long.TryParse(lastSeenStr, out long binaryTime))
                {
                    System.DateTime lastSeenDate = System.DateTime.FromBinary(binaryTime);
                    // If we have seen it today or later, don't show the notification
                    if (today <= lastSeenDate)
                    {
                        showNotification = false;
                    }
                }
            }

            m_ChallengeNotificationImage.SetActive(showNotification);
            
            Debug.Log($"[HomeContoller] UpdateChallengeNotification: Today={today}, ShowNotification={showNotification}");
        }

        private void UpdateLobbyAdReadyImage()
        {
            if (m_LobbyAdReadyImage == null) return;

            bool isCooldownActive = false;
            string cooldownEndKey = "ShopAdCooldownEnd";

            if (PlayerPrefs.HasKey(cooldownEndKey))
            {
                string storedValue = PlayerPrefs.GetString(cooldownEndKey);
                if (long.TryParse(storedValue, out long binaryTime))
                {
                    System.DateTime cooldownEndTime = System.DateTime.FromBinary(binaryTime);
                    if (System.DateTime.Now < cooldownEndTime)
                    {
                        isCooldownActive = true;
                    }
                }
            }

            bool isAdReady = AdsManager.Instance != null && (AdsManager.Instance.IsCoinsRewardedReady || AdsManager.Instance.IsInterstitialReady);
            
            m_LobbyAdReadyImage.SetActive(!isCooldownActive && isAdReady);
            
            if (m_WatchAdForCoinsButton != null)
            {
                m_WatchAdForCoinsButton.SetActive(isAdReady);
            }
        }
        
        public void OnSettingsButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_SettingsLayer.activeInHierarchy)
            {
                m_SettingsLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(true);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
            }
        }
        
        public void OnDonateButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            WarmUpIAPStore();

            if(m_DonateLayer.activeInHierarchy)
            {
                m_DonateLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
                m_DonateLayer.SetActive(true);
            }
        }

        public void OnNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            WarmUpIAPStore();

            if(m_NoAdsLayer.activeInHierarchy)
            {
                HideNoAdsLayer();
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
                m_NoAdsLayer.SetActive(true);
            }
        }

        public void OnBuyDonationButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.Donate199);
            m_DonateLayer.SetActive(false);
        }

        public void OnBuyNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.NoAds999);
            HideNoAdsLayer();
        }

        public void OnCalanderButtonClicked()
        {
            if (Time.frameCount == m_LastToggleFrame) return;
            m_LastToggleFrame = Time.frameCount;

            SoundManager.Instance.PlayClick();
            CleanupFireAnimation();
            
            // Prevent interaction confusion by clearing any pending swipe state
            m_IsSwiping = false;


            // If calendar is already the active view, hide it
            if(m_CalanderLayer != null && m_CalanderLayer.activeInHierarchy)
            {
                m_CalanderLayer.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
                SlideTabBackground(m_HomeTab);
            }
            else
            {
                // Deactivate ALL other layers to ensure a clean state
                if (m_SettingsLayer != null) m_SettingsLayer.SetActive(false);
                if (m_DonateLayer != null) m_DonateLayer.SetActive(false);
                if (m_NoAdsLayer != null) HideNoAdsLayer();
                if (m_ShopLayer != null) m_ShopLayer.SetActive(false);

                m_CalanderLayer.SetActive(true);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = false;

                PlayerPrefs.SetString("LastSeenChallengeDate", System.DateTime.Today.ToBinary().ToString());
                PlayerPrefs.Save();
                UpdateChallengeNotification();
                SlideTabBackground(m_CalendarTab);
            }
            RefreshLobbyUI();
        }


        public void OnHomeButtonClicked()
        {
            if (Time.frameCount == m_LastToggleFrame) return;
            m_LastToggleFrame = Time.frameCount;

            SoundManager.Instance.PlayClick();
            CleanupFireAnimation();

            // Clear ALL layers to return to raw Home state
            if (m_SettingsLayer != null) m_SettingsLayer.SetActive(false);
            if (m_CalanderLayer != null) m_CalanderLayer.SetActive(false);
            if (m_ShopLayer != null) m_ShopLayer.SetActive(false);
            if (m_DonateLayer != null) m_DonateLayer.SetActive(false);
            if (m_NoAdsLayer != null) HideNoAdsLayer();

            if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
            SlideTabBackground(m_HomeTab);
            RefreshLobbyUI();
        }


        public void OnShopButtonClicked()
        {
            if (Time.frameCount == m_LastToggleFrame) return;
            m_LastToggleFrame = Time.frameCount;

            // Logic: Is the shop currently the primary active view?
            if (m_ShopLayer != null && m_ShopLayer.activeInHierarchy)
            {
                HideShop();
            }
            else
            {
                ShowShop();
            }
        }

        
        public void OnCloseShopButtonClicked()
        {
             if (Time.frameCount == m_LastToggleFrame) return;
             m_LastToggleFrame = Time.frameCount;
             HideShop();
        }

        public void ShowShop()
        {
            WarmUpIAPStore();
            SoundManager.Instance.PlayShop();
            CleanupFireAnimation();
            
            // To prevent interaction confusion, explicitly clear any pending swipe state
            m_IsSwiping = false;

            // Atomically manage layer states to prevent overlap glitches
            if (m_SettingsLayer != null) m_SettingsLayer.SetActive(false);
            if (m_CalanderLayer != null) m_CalanderLayer.SetActive(false);
            if (m_DonateLayer != null) m_DonateLayer.SetActive(false);
            if (m_NoAdsLayer != null) HideNoAdsLayer();
            
            if (m_ShopLayer != null) m_ShopLayer.SetActive(true);

            if (m_LobbyUI != null && m_LobbyUI.activeInHierarchy)
            {
                SlideTabBackground(m_ShopTab);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
                RefreshLobbyUI();
            }
        }

        public void HideShop()
        {
            SoundManager.Instance.PlayClick();
            m_IsSwiping = false;
            
            if (m_ShopLayer != null) m_ShopLayer.SetActive(false);

            if (m_LobbyUI != null && m_LobbyUI.activeInHierarchy)
            {
                SlideTabBackground(m_HomeTab);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
                RefreshLobbyUI();
            }
        }

        public void OnBuyProductButtonClicked(string productId)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            WarmUpIAPStore();
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.BuyProduct(productId);
            }
            else
            {
                Debug.LogError("[HomeContoller] IAPManager.Instance is null!");
            }
        }

        private static void WarmUpIAPStore()
        {
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.WarmUp();
            }
        }

        public void OnRestorePurchasesButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.RestorePurchases();
            }
        }

        public void OnWatchAdForCoinsButtonClicked()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForCoins();
            }
            else
            {
                Debug.LogError("[HomeContoller] AdsManager.Instance is null!");
            }
        }

        public void OnShareButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();

            string text = "Check out Arrows Legend! Can you beat my level?";
            string url = "https://play.google.com/store/apps/details?id=" + Application.identifier;

            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
            {
                text = RemoteConfigManager.Instance.ShareText;
                url = RemoteConfigManager.Instance.ShareUrl;
            }

            NativeShare.Share(text, url, "Arrows Legend");
        }

        public void OnPlayButtonClicked()
        {
            try
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
                
                string levelName = $"Level{UserDataManager.Instance.CurrentLevel}";
                
                SwitchToGameUI();
                
                GameManager manager = (gameManager != null) ? gameManager : GameManager.Instance;
                
                if (manager != null)
                {
                    manager.p_isLevelProgression = true;
                    manager.StartLevel(levelName);
                }
                else
                {
                    Debug.LogError("[HomeContoller] GameManager reference missing! Please link it in the Inspector.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HomeContoller] Play Button Error: {ex.Message}");
            }
        }

        public void OnCalenderPlayButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            if (m_MonthlyChallengeController == null)
            {
                Debug.LogError("[HomeContoller] MonthlyChallengeController reference missing!");
                return;
            }

            int month = m_MonthlyChallengeController.p_CurrentMonth;
            int day = m_MonthlyChallengeController.p_CurrentDay;
            int year = m_MonthlyChallengeController.p_CurrentYear;

            string levelName = GameManager.GetChallengeLevelId(month, day, year);
            Debug.Log($"[HomeContoller] Calendar Play clicked. Starting Challenge: {levelName}");

            SwitchToGameUI();

            GameManager manager = (gameManager != null) ? gameManager : GameManager.Instance;
            if (manager != null)
            {
                manager.StartChallengeLevel(levelName, year, month, day);
            }
            else
            {
                Debug.LogError("[HomeContoller] GameManager reference missing for Challenge Level!");
            }
        }

        public void OnLevelStreakButtonClicked()
        {
            var unlockView = m_LevelStreakIcon?.GetComponent<FeatureUnlockView>();
            if (unlockView != null && unlockView.IsLocked())
            {
                // FeatureUnlockView handles its own tooltip
                return;
            }

            if (SoundManager.Instance != null) 
            {
                SoundManager.Instance.PlayClick();
            }
            
            if (m_StreakPopup != null)
            {
                m_StreakPopup.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[HomeContoller] m_StreakPopup reference is missing!");
            }
        }

        private void SwitchToGameUI()
        {
            CleanupFireAnimation();

            if (GameManager.Instance != null && GameManager.Instance.m_GameUI != null)
            {
                GameManager.Instance.m_GameUI.SetGameUIVisible(true, notifyLobbyWhenHidden: false);
                return;
            }

            GameObject lobby = m_LobbyUI;
            GameObject game = m_GameUI;
            if (lobby == null && GameManager.Instance != null) lobby = GameManager.Instance.m_LobbyUI;

            if (lobby != null) lobby.SetActive(false);
            if (game != null) game.SetActive(true);
        }

        private void CheckForUpdates()
        {
            if (RemoteConfigManager.Instance != null)
            {
                RemoteConfigManager.Instance.OnConfigInitialized -= CheckForUpdates;
            }

            string currentVersion = Application.version;
            bool isForce = false;
            bool isSoft = false;

            #if UNITY_ANDROID
            string forceVersion = RemoteConfigManager.Instance.ForceUpdateVersionAndroid;
            string softVersion = RemoteConfigManager.Instance.SoftUpdateVersionAndroid;

            if (CompareVersions(forceVersion, currentVersion) > 0)
            {
                Debug.Log("[CheckForUpdates] Android Force detected");
                isForce = true;
            }
            else if (CompareVersions(softVersion, currentVersion) > 0)
            {
                Debug.Log("[CheckForUpdates] Android Soft detected");
                isSoft = true;
            }
            #elif UNITY_IOS
            string forceVersion = RemoteConfigManager.Instance.ForceUpdateVersionIOS;
            string softVersion = RemoteConfigManager.Instance.SoftUpdateVersionIOS;

            if (CompareVersions(forceVersion, currentVersion) > 0)
            {
                Debug.Log("[CheckForUpdates] iOS Force detected");
                isForce = true;
            }
            else if (CompareVersions(softVersion, currentVersion) > 0)
            {
                Debug.Log("[CheckForUpdates] iOS Soft detected");
                isSoft = true;
            }
            #endif

            if (isForce || isSoft)
            {
                GameObject popupPrefab = Resources.Load<GameObject>("SoftForcePopup");
                if (popupPrefab != null)
                {
                    // Use m_LobbyUI's parent to ensure it's on the Canvas level
                    Transform parent = m_LobbyUI != null ? m_LobbyUI.transform.parent : transform;
                    GameObject popupInstance = Instantiate(popupPrefab, parent, false);
                    
                    popupInstance.SetActive(true);
                    popupInstance.transform.SetAsLastSibling();

                    RectTransform rect = popupInstance.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.localPosition = Vector3.zero;
                        rect.localScale = Vector3.one;
                    }

                    SoftForceUpdateView view = popupInstance.GetComponent<SoftForceUpdateView>();
                    if (view != null)
                    {
                        view.Setup(isForce);
                        Debug.Log("[CheckForUpdates] Popup initialized successfully.");
                    }
                    else
                    {
                        Debug.LogError("[CheckForUpdates] SoftForceUpdateView component missing on prefab!");
                    }
                }
                else
                {
                    Debug.LogError("[CheckForUpdates] SoftForcePopup prefab not found in Resources!");
                }
            }
        }

        /// <summary>
        /// Compares two version strings (format: XX.KK.PP).
        /// Returns 1 if v1 > v2, -1 if v1 < v2, 0 if equal.
        /// </summary>
        private int CompareVersions(string v1, string v2)
        {
            if (string.IsNullOrEmpty(v1) || string.IsNullOrEmpty(v2)) return 0;

            string[] parts1 = v1.Split('.');
            string[] parts2 = v2.Split('.');

            for (int i = 0; i < Mathf.Max(parts1.Length, parts2.Length); i++)
            {
                int num1 = i < parts1.Length ? (int.TryParse(parts1[i], out int n1) ? n1 : 0) : 0;
                int num2 = i < parts2.Length ? (int.TryParse(parts2[i], out int n2) ? n2 : 0) : 0;

                if (num1 > num2) return 1;
                if (num1 < num2) return -1;
            }

            return 0;
        }

        public void OnLockedChallengeButtonClicked()
        {
            if (m_LockedChallengeTooltip == null) return;

            if (m_TooltipCoroutine != null) StopCoroutine(m_TooltipCoroutine);
            m_TooltipCoroutine = StartCoroutine(ShowTooltipCoroutine());
        }

        private IEnumerator ShowTooltipCoroutine()
        {
            m_LockedChallengeTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_LockedChallengeTooltip.SetActive(false);
            m_TooltipCoroutine = null;
        }

        private void CheckForRateUsPopup()
        {
            if (UserDataManager.Instance == null) return;
            
            // Only check if we just came from a win
            if (!UserDataManager.Instance.IsRateUsCheckPending) return;
            
            // Current level must be above 15
            if (UserDataManager.Instance.CurrentLevel <= 15) return;

            // Check timing condition (45 days)
            bool shouldShow = false;
            if (UserDataManager.Instance.LastRateUsDate == System.DateTime.MinValue)
            {
                shouldShow = true;
            }
            else
            {
                System.TimeSpan diff = System.DateTime.Now - UserDataManager.Instance.LastRateUsDate;
                if (diff.TotalDays >= 45)
                {
                    shouldShow = true;
                }
            }

            if (shouldShow)
            {
                GameObject popupPrefab = Resources.Load<GameObject>("RateUsPopup");
                if (popupPrefab != null)
                {
                    Transform parent = m_LobbyUI != null ? m_LobbyUI.transform.parent : transform;
                    GameObject popupInstance = Instantiate(popupPrefab, parent, false);
                    popupInstance.SetActive(true);
                    popupInstance.transform.SetAsLastSibling();

                    RectTransform rect = popupInstance.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.localPosition = Vector3.zero;
                        rect.localScale = Vector3.one;
                    }

                    UserDataManager.Instance.MarkRateUsSeen();
                    Debug.Log("[HomeContoller] Rate Us Popup shown.");
                }
            }
            else
            {
                // If we shouldn't show it (e.g. within 45 days), just clear the pending flag
                UserDataManager.Instance.IsRateUsCheckPending = false;
            }
        }

        private void SnapTabBackground()
        {
            if (m_SelectedTabBg == null) return;
            
            RectTransform targetTab = m_HomeTab;
            if (m_ShopLayer != null && m_ShopLayer.activeInHierarchy) targetTab = m_ShopTab;
            else if (m_CalanderLayer != null && m_CalanderLayer.activeInHierarchy) targetTab = m_CalendarTab;

            if (targetTab != null)
            {
                // Defer to next frame so LayoutGroups have updated positions
                StartCoroutine(SnapTabBackgroundCoroutine(targetTab));
            }
        }

        private IEnumerator SnapTabBackgroundCoroutine(RectTransform targetTab)
        {
            yield return new WaitForEndOfFrame();
            if (m_SelectedTabBg != null && targetTab != null)
            {
                Vector3 newPos = m_SelectedTabBg.position;
                newPos.x = targetTab.position.x;
                m_SelectedTabBg.position = newPos;
            }

            Vector3 textSelectedScale = new Vector3(1.2f, 1.2f, 1.2f);
            Vector3 iconSelectedScale = new Vector3(1.5f, 1.5f, 1.5f);
            float iconSelectedY = 87f;
            float iconDeselectedY = 67f;


            
            UpdateTabImmediate(m_HomeIcon, m_HomeText, targetTab == m_HomeTab, iconSelectedScale, textSelectedScale, iconSelectedY, iconDeselectedY);
            UpdateTabImmediate(m_CalendarIcon, m_CalendarText, targetTab == m_CalendarTab, iconSelectedScale, textSelectedScale, iconSelectedY, iconDeselectedY);
            UpdateTabImmediate(m_ShopIcon, m_ShopText, targetTab == m_ShopTab, iconSelectedScale, textSelectedScale, iconSelectedY, iconDeselectedY);
        }

        private void UpdateTabImmediate(RectTransform icon, RectTransform text, bool isSelected, Vector3 iconScale, Vector3 textScale, float iconSelectedY, float iconDeselectedY)
        {
            if (icon != null)
            {
                icon.localScale = isSelected ? iconScale : Vector3.one;
                Vector2 pos = icon.anchoredPosition;
                pos.y = isSelected ? iconSelectedY : iconDeselectedY;
                icon.anchoredPosition = pos;
            }
            if (text != null)
            {
                text.localScale = isSelected ? textScale : Vector3.one;
            }
        }

        private void SlideTabBackground(RectTransform targetTab)
        {
            if (m_SelectedTabBg == null || targetTab == null) return;
            
            if (m_TabSlideCoroutine != null)
                StopCoroutine(m_TabSlideCoroutine);

            m_TabSlideCoroutine = StartCoroutine(AnimateTabBackground(targetTab));
        }

        private IEnumerator AnimateTabBackground(RectTransform targetTab)
        {
            float duration = 0.25f;
            float elapsed = 0f;
            Vector3 startPos = m_SelectedTabBg.position;
            Vector3 targetWorldPosition = targetTab.position;



            // Capture start states
            Vector3 hIconScaleS = m_HomeIcon != null ? m_HomeIcon.localScale : Vector3.one;
            Vector3 cIconScaleS = m_CalendarIcon != null ? m_CalendarIcon.localScale : Vector3.one;
            Vector3 sIconScaleS = m_ShopIcon != null ? m_ShopIcon.localScale : Vector3.one;

            float hIconYS = m_HomeIcon != null ? m_HomeIcon.anchoredPosition.y : 67f;
            float cIconYS = m_CalendarIcon != null ? m_CalendarIcon.anchoredPosition.y : 67f;
            float sIconYS = m_ShopIcon != null ? m_ShopIcon.anchoredPosition.y : 67f;

            Vector3 hTextScaleS = m_HomeText != null ? m_HomeText.localScale : Vector3.one;
            Vector3 cTextScaleS = m_CalendarText != null ? m_CalendarText.localScale : Vector3.one;
            Vector3 sTextScaleS = m_ShopText != null ? m_ShopText.localScale : Vector3.one;

            Vector3 textSelScale = new Vector3(1.2f, 1.2f, 1.2f);
            Vector3 iconSelScale = new Vector3(1.65f, 1.65f, 1.65f);
            float iconSelY = 97f;
            float iconDesY = 67f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                Vector3 newPos = m_SelectedTabBg.position;
                newPos.x = Mathf.Lerp(startPos.x, targetWorldPosition.x, easedT);
                m_SelectedTabBg.position = newPos;
                
                // Animate Home
                if (m_HomeIcon != null)
                {
                    m_HomeIcon.localScale = Vector3.Lerp(hIconScaleS, (targetTab == m_HomeTab) ? iconSelScale : Vector3.one, easedT);
                    Vector2 p = m_HomeIcon.anchoredPosition;
                    p.y = Mathf.Lerp(hIconYS, (targetTab == m_HomeTab) ? iconSelY : iconDesY, easedT);
                    m_HomeIcon.anchoredPosition = p;
                }
                if (m_HomeText != null) m_HomeText.localScale = Vector3.Lerp(hTextScaleS, (targetTab == m_HomeTab) ? textSelScale : Vector3.one, easedT);

                // Animate Calendar
                if (m_CalendarIcon != null)
                {
                    m_CalendarIcon.localScale = Vector3.Lerp(cIconScaleS, (targetTab == m_CalendarTab) ? iconSelScale : Vector3.one, easedT);
                    Vector2 p = m_CalendarIcon.anchoredPosition;
                    p.y = Mathf.Lerp(cIconYS, (targetTab == m_CalendarTab) ? iconSelY : iconDesY, easedT);
                    m_CalendarIcon.anchoredPosition = p;
                }
                if (m_CalendarText != null) m_CalendarText.localScale = Vector3.Lerp(cTextScaleS, (targetTab == m_CalendarTab) ? textSelScale : Vector3.one, easedT);

                // Animate Shop
                if (m_ShopIcon != null)
                {
                    m_ShopIcon.localScale = Vector3.Lerp(sIconScaleS, (targetTab == m_ShopTab) ? iconSelScale : Vector3.one, easedT);
                    Vector2 p = m_ShopIcon.anchoredPosition;
                    p.y = Mathf.Lerp(sIconYS, (targetTab == m_ShopTab) ? iconSelY : iconDesY, easedT);
                    m_ShopIcon.anchoredPosition = p;
                }
                if (m_ShopText != null) m_ShopText.localScale = Vector3.Lerp(sTextScaleS, (targetTab == m_ShopTab) ? textSelScale : Vector3.one, easedT);

                yield return null;
            }

            // Final state
            Vector3 fPos = m_SelectedTabBg.position;
            fPos.x = targetWorldPosition.x;
            m_SelectedTabBg.position = fPos;
            
            UpdateTabImmediate(m_HomeIcon, m_HomeText, targetTab == m_HomeTab, iconSelScale, textSelScale, iconSelY, iconDesY);
            UpdateTabImmediate(m_CalendarIcon, m_CalendarText, targetTab == m_CalendarTab, iconSelScale, textSelScale, iconSelY, iconDesY);
            UpdateTabImmediate(m_ShopIcon, m_ShopText, targetTab == m_ShopTab, iconSelScale, textSelScale, iconSelY, iconDesY);

            m_TabSlideCoroutine = null;
        }

        private void CheckForNoAdsOffer()
        {
            if (!GameManager.g_IsFromGame) return;
            GameManager.g_IsFromGame = false;

            if (IAPManager.Instance == null || IAPManager.Instance.HasNoAds) return;

            if (UserDataManager.Instance == null || UserDataManager.Instance.CurrentLevel <= 16 || UserDataManager.Instance.ArrowsCurrency >= 1200) return;

            string lastSeenTimeStr = PlayerPrefs.GetString("LastNoAdsOfferTime", string.Empty);
            if (!string.IsNullOrEmpty(lastSeenTimeStr))
            {
                if (long.TryParse(lastSeenTimeStr, out long binaryTime))
                {
                    System.DateTime lastSeenTime = System.DateTime.FromBinary(binaryTime);
                    if ((System.DateTime.Now - lastSeenTime).TotalMinutes < 30)
                    {
                        Debug.Log($"[HomeContoller] Skipping No Ads offer (seen { (int)(System.DateTime.Now - lastSeenTime).TotalMinutes } mins ago)");
                        return;
                    }
                }
            }

            if (m_NoAdsLayer != null)
            {
                Debug.Log("[HomeContoller] Special No Ads Offer conditions met: showing No Ads layer!");

                WarmUpIAPStore();

                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
                m_DonateLayer.SetActive(false);
                m_NoAdsLayer.SetActive(true);

                PlayerPrefs.SetString("LastNoAdsOfferTime", System.DateTime.Now.ToBinary().ToString());
                PlayerPrefs.Save();
            }
        }
        private IEnumerator AnimateStreakFire(int targetStreak)
        {
            yield return new WaitForSeconds(m_FireJumpStartDelay);
            
            if (m_LevelStreakIcon == null) yield break;

            // Create fire sprite as child of the icon for perfect arrival
            m_ActiveFireSprite = new GameObject("FireAnimation", typeof(RectTransform), typeof(Image));
            m_ActiveFireSprite.transform.SetParent(m_LevelStreakIcon.transform, false);
            
            Image fireImage = m_ActiveFireSprite.GetComponent<Image>();
            fireImage.sprite = m_LevelStreakActiveSprite;
            fireImage.SetNativeSize();
            
            RectTransform fireRect = m_ActiveFireSprite.GetComponent<RectTransform>();
            fireRect.localScale = Vector3.one * 0.5f;

            // Start position (Local Offset)
            Vector2 startPosLocal = new Vector2(
                Screen.width * m_FireJumpStartOffsetXPercent,
                Screen.height * m_FireJumpStartOffsetYPercent
            );
            
            fireRect.anchoredPosition = startPosLocal;
            fireRect.localScale = Vector3.one * m_FireJumpStartScale;
            Vector2 targetPosLocal = Vector2.zero; // Destination is the parent icon
            
            // Animation
            float duration = m_FireJumpDuration;
            float elapsed = 0f;
            float arcHeight = Screen.height * m_FireJumpArcHeightPercent;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float tSlow = 1f - Mathf.Pow(1f - t, 3); // Ease out
                
                Vector2 currentPos = Vector2.Lerp(startPosLocal, targetPosLocal, tSlow);
                
                // Parabolic arc (local upward)
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                fireRect.anchoredPosition = currentPos + new Vector2(0, arc);
                
                fireRect.localScale = Vector3.Lerp(Vector3.one * m_FireJumpStartScale, Vector3.one * m_FireJumpEndScale, t);
                fireRect.localEulerAngles = new Vector3(0, 0, t * m_FireJumpRotation);

                yield return null;
            }

            // Arrival
            if (m_ActiveFireSprite != null)
            {
                Destroy(m_ActiveFireSprite);
                m_ActiveFireSprite = null;
            }
            
            SetLevelStreakText(targetStreak);

            // Update Icon state
            bool isStreakActive = targetStreak >= 6;
            var iconImage = m_LevelStreakIcon.GetComponent<UnityEngine.UI.Image>();
            if (iconImage != null && m_LevelStreakActiveSprite != null && m_LevelStreakInactiveSprite != null)
            {
                iconImage.sprite = isStreakActive ? m_LevelStreakActiveSprite : m_LevelStreakInactiveSprite;
            }
            
            var fireSkew = m_LevelStreakIcon.GetComponent<Assets.Scripts.GameUI.UIFireSkew>();
            if (fireSkew != null)
            {
                fireSkew.enabled = isStreakActive;
                m_LevelStreakIcon.GetComponent<UnityEngine.UI.Graphic>()?.SetVerticesDirty();
            }

            // Arrival Feedback
            VibrationManager.VibrateSuccess();
            StartCoroutine(ScalePunch(m_LevelStreakIcon.transform));
            if (SoundManager.Instance != null) SoundManager.Instance.PlayFireOn();
            
            m_ActiveFireSprite = null;
            m_FireAnimationCoroutine = null;

            UserDataManager.Instance.NeedsLevelStreakAnimation = false;
        }

        private void CleanupFireAnimation()
        {
            if (m_FireAnimationCoroutine != null)
            {
                StopCoroutine(m_FireAnimationCoroutine);
                m_FireAnimationCoroutine = null;
            }

            if (m_ActiveFireSprite != null)
            {
                Destroy(m_ActiveFireSprite);
                m_ActiveFireSprite = null;
            }
        }
    }
}
