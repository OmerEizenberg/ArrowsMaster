using UnityEngine;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Singular;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        public bool IsInitialized => isInitialized;
        private bool isInitialized = false;
        private bool isInitializing = false;
        private int sdkInitRetryCount = 0;
        private float lastAdShowTime = -60f;

        private int interstitialRetryAttempt;
        private int rewardedRetryAttempt;

        private enum RewardAdType { None, GameReward, CoinsReward, MultiplyReward, HintReward, PlayOnReward, MagicReward, LifeReward, ShuffleReward }
        private RewardAdType pendingRewardType = RewardAdType.None;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        public event Action OnRewardReceived;
        public event Action OnCoinsRewardReceived;
        public event Action OnMultiplyRewardReceived;
        public event Action OnHintRewardReceived;
        public event Action OnPlayOnRewardReceived;
        public event Action OnMagicRewardReceived;
        public event Action OnLifeRewardReceived;
        public event Action OnShuffleRewardReceived;
        public event Action OnAdOpened;
        public event Action OnAdClosed;
        /// <summary>Fired when any cached ad-ready flag changes (avoids per-frame native IsAdReady calls).</summary>
        public event Action OnAdReadinessChanged;

        private bool _rewardedReady;
        private bool _interstitialReady;
        private bool _bannerReady;
        private bool _bannerCreated;

        private GameObject _coinsExplosionPrefab;
        private int _bannerRetryAttempt;
        private bool _showNextInterstitial = true;
        private bool _isFlushingPendingSingularRevenue;

        private static readonly ConcurrentQueue<SingularAdRevenuePayload> _pendingSingularRevenue =
            new ConcurrentQueue<SingularAdRevenuePayload>();

        private readonly struct SingularAdRevenuePayload
        {
            public readonly double RevenueUsd;
            public readonly string NetworkName;
            public readonly string AdUnitId;
            public readonly string AdFormat;
            public readonly string RevenuePrecision;

            public SingularAdRevenuePayload(
                double revenueUsd,
                string networkName,
                string adUnitId,
                string adFormat,
                string revenuePrecision)
            {
                RevenueUsd = revenueUsd;
                NetworkName = networkName;
                AdUnitId = adUnitId;
                AdFormat = adFormat;
                RevenuePrecision = revenuePrecision;
            }
        }

        private const float HealthCheckIntervalSeconds = 20f;

        // ──────────────────────────────────────────────────────────────────
        // AppLovin MAX SDK Key
        // Set your key via AppLovin > Integration Manager in the Unity editor,
        // or find it at: https://dash.applovin.com/o/account#keys
        // ──────────────────────────────────────────────────────────────────
        private const string MaxSdkKey = "ghH9pVPTzdwgPfawqgCRPHWUMcR85KmpzpPlRCJwUDO8Uv4Xgn4oi52lcNTPuCb3ysqbfIUBUfNkrVdmvmuqTI";

        #region Ad Unit IDs

        private string InterstitialAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "9625ca772cf7c819";
#else
                return "8cf59aa021b449bf";
#endif
            }
        }

        private string RewardedAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "39cd5fd76b5da61f";
#else
                return "a9110da25686aa62";
#endif
            }
        }

        private string BannerAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "b4b98419050ba611";
#else
                return "b3d625776838cd3e";
#endif
            }
        }

        #endregion

        public bool IsRewardedReady => _rewardedReady;
        public bool IsMultiplyRewardedReady => _rewardedReady;
        public bool IsCoinsRewardedReady => _rewardedReady;
        public bool IsInterstitialReady => _interstitialReady;
        public bool IsAnyRewardedOrInterstitialReady => _rewardedReady || _interstitialReady;

        // ════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject adsGO = new GameObject("AdsManager");
                adsGO.AddComponent<AdsManager>();
                DontDestroyOnLoad(adsGO);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _coinsExplosionPrefab = Resources.Load<GameObject>("CoinsSmallExplosion");

            _ = InitializeSDK();
            StartCoroutine(AdHealthCheckRoutine());
        }

        private void Start()
        {
            SubscribeToNoAdsStatus();
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnDestroy()
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxSdkInitialized;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnInterstitialClicked;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnInterstitialRevenuePaid;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnRewardedAdClicked;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRewardedAdRevenuePaid;

            MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerAdLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent -= OnBannerAdClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerAdRevenuePaid;

            UnsubscribeFromNoAdsStatus();
            if (_bannerCreated) MaxSdk.DestroyBanner(BannerAdUnitId);
        }

        // ════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════

        private bool UserHasNoAds =>
            IAPManager.Instance != null && IAPManager.Instance.HasNoAds;

        private void SubscribeToNoAdsStatus()
        {
            if (IAPManager.Instance == null) return;
            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
            IAPManager.Instance.OnNoAdsStatusChanged += HandleNoAdsStatusChanged;
            if (IAPManager.Instance.HasNoAds)
                HandleNoAdsStatusChanged(true);
        }

        private void UnsubscribeFromNoAdsStatus()
        {
            if (IAPManager.Instance == null) return;
            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
        }

        private void HandleNoAdsStatusChanged(bool hasNoAds)
        {
            if (!hasNoAds) return;
            Debug.Log("[AdsManager] No Ads purchased — hiding and destroying banner.");
            DestroyBanner();
        }

        private void DestroyBanner()
        {
            if (!_bannerCreated) return;

            try
            {
                MaxSdk.HideBanner(BannerAdUnitId);
                MaxSdk.DestroyBanner(BannerAdUnitId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] DestroyBanner failed: {e.Message}");
            }

            _bannerCreated = false;
            SetCachedReady(ref _bannerReady, false);
        }

        private void EnqueueAction(Action action) => _mainThreadQueue.Enqueue(action);

        private void SetCachedReady(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            OnAdReadinessChanged?.Invoke();
        }

        private void RefreshInterstitialReady()
        {
            bool ready = false;
            try { ready = MaxSdk.IsInterstitialReady(InterstitialAdUnitId); }
            catch (Exception e) { Debug.LogWarning($"[AdsManager] Error checking interstitial readiness: {e.Message}"); }
            SetCachedReady(ref _interstitialReady, ready);
        }

        private void RefreshRewardedReady()
        {
            bool ready = false;
            try { ready = MaxSdk.IsRewardedAdReady(RewardedAdUnitId); }
            catch (Exception e) { Debug.LogWarning($"[AdsManager] Error checking rewarded readiness: {e.Message}"); }
            SetCachedReady(ref _rewardedReady, ready);
        }

        private void RefreshAllReadiness()
        {
            RefreshInterstitialReady();
            RefreshRewardedReady();
        }

        /// <summary>
        /// Preloads the next interstitial, rewarded, and banner after any ad is shown or dismissed.
        /// MAX recommends loading before the current ad finishes; we also call again on close as a fallback.
        /// </summary>
        private void PrepareAllAdsAfterClose()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }

            Debug.Log("[AdsManager] Preparing next ads (rewarded, interstitial, banner).");
            LoadRewarded();
            LoadInterstitial();
            PrepareBannerAd();
        }

        private void PrepareBannerAd()
        {
            if (!isInitialized) return;
            if (UserHasNoAds) return;

            if (!_bannerCreated)
            {
                LoadSettingsBanner();
                return;
            }

            if (_bannerReady) return;

            Debug.Log("[AdsManager] Banner not ready — recreating banner ad unit.");
            try
            {
                MaxSdk.DestroyBanner(BannerAdUnitId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] DestroyBanner failed: {e.Message}");
            }

            _bannerCreated = false;
            SetCachedReady(ref _bannerReady, false);
            InitializeBannerAds();
        }

        private void NotifyAdClosed()
        {
            OnAdClosed?.Invoke();
        }

        private IEnumerator AdHealthCheckRoutine()
        {
            var wait = new WaitForSeconds(HealthCheckIntervalSeconds);
            while (true)
            {
                yield return wait;

                try
                {
                    if (!isInitialized && !isInitializing)
                    {
                        Debug.Log("[AdsManager] HealthCheck: SDK not initialized. Attempting to initialize.");
                        _ = InitializeSDK();
                        continue;
                    }

                    if (!isInitialized) continue;

                    if (!_interstitialReady) LoadInterstitial();
                    if (!_rewardedReady) LoadRewarded();
                    if (!_bannerCreated || !_bannerReady) PrepareBannerAd();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AdsManager] HealthCheck error: {e.Message}");
                }
            }
        }

        // ════════════════════════════════════════════
        //  SDK INITIALIZATION
        // ════════════════════════════════════════════

        private async Task InitializeSDK()
        {
            if (isInitialized || isInitializing) return;

            isInitializing = true;
            try
            {
                await IAPManager.EnsureUnityServicesInitializedAsync();
                Debug.Log("[AdsManager] Unity Services Initialized.");

#if UNITY_IOS && !UNITY_EDITOR
                if (!IOSAttributionBootstrap.IsAttResolved)
                {
                    bool attFinished = false;
                    EnqueueAction(() =>
                    {
                        IOSAdsHelper.RequestATT();
                        StartCoroutine(IOSAdsHelper.PollATTStatus(_ => attFinished = true));
                    });
                    float attWaitStart = Time.time;
                    while (!attFinished && Time.time - attWaitStart < 30f)
                        await Task.Yield();
                    if (!attFinished)
                        Debug.LogWarning("[AdsManager] ATT flow timed out. Proceeding with ads initialization.");
                }
                else
                {
                    Debug.Log("[AdsManager] ATT already resolved by IOSAttributionBootstrap.");
                }
#endif

                bool consentFinished = false;
                EnqueueAction(() =>
                {
                    ConsentManager.RequestConsent(() =>
                    {
                        consentFinished = true;
                        Debug.Log("[AdsManager] Consent gathering finished. Continuing SDK Init.");
                    });
                });

                float waitTimeout = 60.0f;
                float waitStart = Time.time;
                while (!consentFinished && Time.time - waitStart < waitTimeout)
                    await Task.Yield();
                if (!consentFinished)
                    Debug.LogWarning("[AdsManager] Consent flow timed out. Proceeding with SDK initialization anyway.");

                // CCPA: false = user has NOT opted out of sale of personal info
                MaxSdk.SetDoNotSell(false);

                Debug.Log("[AdsManager] Initializing AppLovin MAX SDK...");

                // OnAdRevenuePaidEvent (ILRD) fires on a background thread by default; Singular requires main thread.
                MaxSdkBase.InvokeEventsOnUnityMainThread = true;

                MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxSdkInitialized;
                MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxSdkInitialized;

                MaxSdk.SetSdkKey(MaxSdkKey);
                MaxSdk.InitializeSdk();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdsManager] SDK Initialization Process Failed: {e.Message}");
                isInitializing = false;
                _ = RetrySDKInitialization(20000);
            }
        }

        private async Task RetrySDKInitialization(int delayMs)
        {
            if (isInitialized || isInitializing) return;

            Debug.Log($"[AdsManager] Retrying SDK Initialization in {delayMs / 1000}s... (Attempt {sdkInitRetryCount + 1})");
            await Task.Delay(delayMs);

            if (this != null && !isInitialized && !isInitializing)
            {
                sdkInitRetryCount++;
                EnqueueAction(() => _ = InitializeSDK());
            }
        }

        private void OnMaxSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            Debug.Log("[AdsManager] AppLovin MAX SDK Initialized Successfully.");
            isInitialized = true;
            isInitializing = false;
            sdkInitRetryCount = 0;

            InitializeInterstitialAds();
            InitializeRewardedAds();
            SubscribeToNoAdsStatus();
            if (!UserHasNoAds)
                InitializeBannerAds();
            RefreshAllReadiness();
        }

        // ════════════════════════════════════════════
        //  INTERSTITIAL ADS
        // ════════════════════════════════════════════

        private void InitializeInterstitialAds()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClicked;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaid;

            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: IsInterstitialActive is false (Remote Config).");
                return;
            }
            // Loaded even for No Ads buyers so interstitials remain available as a rewarded fallback.
            Debug.Log("[AdsManager] Loading Interstitial Ad...");
            MaxSdk.LoadInterstitial(InterstitialAdUnitId);
        }

        public void ShowInterstitial(bool isAuto = false)
        {
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: IsInterstitialActive is false (Remote Config).");
                return;
            }

            bool shouldShowThisTime = _showNextInterstitial;
            _showNextInterstitial = !_showNextInterstitial;

            if (!shouldShowThisTime)
            {
                Debug.Log("[AdsManager] Skipping Interstitial due to 50% frequency rule.");
                return;
            }

            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < GameManager.ADS_START_LEVEL)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial Show: User Level {UserDataManager.Instance.CurrentLevel} < {GameManager.ADS_START_LEVEL}.");
                return;
            }

            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: User has No Ads.");
                return;
            }

            float currentAdCooldown = 60f;
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
            {
                currentAdCooldown = RemoteConfigManager.Instance.AdCooldown;
            }

            float timeSinceLastAd = Time.time - lastAdShowTime;
            if (timeSinceLastAd < currentAdCooldown)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial due to cooldown. Last ad was {timeSinceLastAd:F1}s ago (Cooldown: {currentAdCooldown}s).");
                return;
            }

            if (IsInterstitialReady)
            {
                Debug.Log("[AdsManager] Showing Interstitial Ad.");
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Interstitial Ad is not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else LoadInterstitial();
            }
        }

        #region Interstitial Callbacks

        private void OnInterstitialLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Loaded.");
            interstitialRetryAttempt = 0;
            AdMonetizationOptimizer.RecordInterstitialAd(adInfo);
            RefreshInterstitialReady();
        }

        private void OnInterstitialLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            interstitialRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, interstitialRetryAttempt));
            Debug.LogWarning($"[AdsManager] Interstitial Ad Load Failed (code: {errorInfo.Code}). Retrying in {retryDelay}s...");
            _ = RetryLoadInterstitial((int)(retryDelay * 1000));
        }

        private async Task RetryLoadInterstitial(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_interstitialReady)
                EnqueueAction(LoadInterstitial);
        }

        private void OnInterstitialDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Displayed.");
            SetCachedReady(ref _interstitialReady, false);
            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Interstitial Ad Display Failed (code: {errorInfo.Code})");
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
                pendingRewardType = RewardAdType.None;

            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Closed. Preparing next ads.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
            {
                Debug.Log("[AdsManager] Interstitial fulfilled a rewarded placement. Granting pending reward.");
                ProcessPendingReward();
            }

            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Clicked.");
        }

        private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdMonetizationOptimizer.RecordInterstitialAd(adInfo);
            TrackAdRevenue(adInfo, "interstitial");
        }

        #endregion

        // ════════════════════════════════════════════
        //  REWARDED ADS
        // ════════════════════════════════════════════

        private void InitializeRewardedAds()
        {
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClicked;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaid;

            LoadRewarded();
        }

        public void LoadRewarded()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            Debug.Log("[AdsManager] Loading Rewarded Ad...");
            MaxSdk.LoadRewardedAd(RewardedAdUnitId);
        }

        public void LoadCoinsRewarded() => LoadRewarded();
        public void LoadMultiplyRewarded() => LoadRewarded();

        public void ShowRewarded() => ShowRewardedForType(RewardAdType.GameReward);

        public void ShowRewardedForCoins() => ShowRewardedForType(RewardAdType.CoinsReward);

        public void ShowRewardedForMultiply() => ShowRewardedForType(RewardAdType.MultiplyReward);

        public void ShowRewardedForHint() => ShowRewardedForType(RewardAdType.HintReward);

        public void ShowRewardedForPlayOn() => ShowRewardedForType(RewardAdType.PlayOnReward);

        public void ShowRewardedForMagic() => ShowRewardedForType(RewardAdType.MagicReward);

        public void ShowRewardedForLife() => ShowRewardedForType(RewardAdType.LifeReward);

        public void ShowRewardedForShuffle() => ShowRewardedForType(RewardAdType.ShuffleReward);

        /// <summary>
        /// Shows the best ad for a user-initiated reward: interstitial when its eCPM beats rewarded
        /// (shorter ad, higher revenue), otherwise rewarded, with interstitial as a readiness fallback.
        /// Intentionally bypasses the No Ads gate — interstitials here are opt-in substitutes for rewarded.
        /// </summary>
        private void ShowRewardedForType(RewardAdType rewardType)
        {
            pendingRewardType = rewardType;

            bool interstitialReady = IsInterstitialReady;
            bool rewardedReady = IsRewardedReady;

            if (AdMonetizationOptimizer.ShouldShowInterstitialInsteadOfRewarded(interstitialReady, rewardedReady))
            {
                Debug.Log(
                    $"[AdsManager] Monetization optimizer: interstitial eCPM ${AdMonetizationOptimizer.InterstitialEcpm:F2} > " +
                    $"rewarded ${AdMonetizationOptimizer.RewardedEcpm:F2}. Showing interstitial for {rewardType}.");
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
                return;
            }

            if (rewardedReady)
            {
                Debug.Log($"[AdsManager] Showing Rewarded Ad ({rewardType}).");
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
                return;
            }

            if (interstitialReady)
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad is not ready for {rewardType}. Falling back to Interstitial.");
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
                return;
            }

            Debug.LogWarning($"[AdsManager] Rewarded and Interstitial are not ready for {rewardType}. Initialized: {isInitialized}");
            pendingRewardType = RewardAdType.None;
            if (!isInitialized && !isInitializing)
                _ = InitializeSDK();
            else
            {
                LoadRewarded();
                LoadInterstitial();
            }
        }

        #region Rewarded Callbacks

        private void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Loaded.");
            rewardedRetryAttempt = 0;
            AdMonetizationOptimizer.RecordRewardedAd(adInfo);
            RefreshRewardedReady();
        }

        private void OnRewardedAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            rewardedRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, rewardedRetryAttempt));
            Debug.LogWarning($"[AdsManager] Rewarded Ad Load Failed (code: {errorInfo.Code}). Retrying in {retryDelay}s...");
            _ = RetryLoadRewarded((int)(retryDelay * 1000));
        }

        private async Task RetryLoadRewarded(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_rewardedReady)
                EnqueueAction(LoadRewarded);
        }

        private void OnRewardedAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Displayed.");
            SetCachedReady(ref _rewardedReady, false);
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Rewarded Ad Display Failed (code: {errorInfo.Code})");
            pendingRewardType = RewardAdType.None;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Closed. Preparing next ads.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Clicked.");
        }

        private void OnRewardedAdReceivedReward(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received.");
            ProcessPendingReward();
        }

        private void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdMonetizationOptimizer.RecordRewardedAd(adInfo);
            TrackAdRevenue(adInfo, "rewarded");
        }

        #endregion

        // ════════════════════════════════════════════
        //  BANNER ADS
        // ════════════════════════════════════════════

        private void InitializeBannerAds()
        {
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaid;

            MaxSdk.CreateBanner(BannerAdUnitId, MaxSdkBase.BannerPosition.BottomCenter);
            MaxSdk.SetBannerBackgroundColor(BannerAdUnitId, Color.clear);
            MaxSdk.HideBanner(BannerAdUnitId);
            _bannerCreated = true;
        }

        public void LoadSettingsBanner()
        {
            if (UserHasNoAds) return;

            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            if (!_bannerCreated)
            {
                Debug.Log("[AdsManager] Creating Settings Banner Ad...");
                InitializeBannerAds();
            }
        }

        public void ShowSettingsBanner()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[AdsManager] Cannot show banner: SDK not initialized.");
                return;
            }

            if (UserHasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Banner Show: User has No Ads.");
                HideSettingsBanner();
                return;
            }

            if (!_bannerCreated)
                InitializeBannerAds();

            Debug.Log("[AdsManager] Showing Settings Banner Ad.");
            MaxSdk.ShowBanner(BannerAdUnitId);
        }

        public void HideSettingsBanner()
        {
            if (_bannerCreated)
            {
                Debug.Log("[AdsManager] Hiding Settings Banner Ad.");
                MaxSdk.HideBanner(BannerAdUnitId);
            }
        }

        #region Banner Callbacks

        private void OnBannerAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Settings Banner Loaded.");
            _bannerRetryAttempt = 0;
            SetCachedReady(ref _bannerReady, true);
        }

        private void OnBannerAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"[AdsManager] Settings Banner Load Failed (code: {errorInfo.Code}).");
            SetCachedReady(ref _bannerReady, false);
            _bannerRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _bannerRetryAttempt));
            _ = RetryPrepareBanner((int)(retryDelay * 1000));
        }

        private async Task RetryPrepareBanner(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_bannerReady)
                EnqueueAction(PrepareBannerAd);
        }

        private void OnBannerAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Settings Banner Clicked.");
        }

        private void OnBannerAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            TrackAdRevenue(adInfo, "banner");
        }

        #endregion

        // ════════════════════════════════════════════
        //  REWARD PROCESSING
        // ════════════════════════════════════════════

        /// <summary>
        /// Central reward processor. Captures and resets pendingRewardType atomically,
        /// then dispatches the correct reward. Safe if called multiple times — second call is a no-op.
        /// </summary>
        private void ProcessPendingReward()
        {
            RewardAdType rewardType = pendingRewardType;
            pendingRewardType = RewardAdType.None;

            if (rewardType != RewardAdType.None)
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyAdWatched();

            switch (rewardType)
            {
                case RewardAdType.GameReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: GameReward → firing OnRewardReceived.");
                    OnRewardReceived?.Invoke();
                    break;

                case RewardAdType.CoinsReward:
                    int rewardAmount = 2000;
                    if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
                    {
                        rewardAmount = RemoteConfigManager.Instance.CoinsRewardedAd;
                    }
                    Debug.Log($"[AdsManager] ProcessPendingReward: CoinsReward → granting {rewardAmount} coins.");
                    if (UserDataManager.Instance != null)
                    {
                        UserDataManager.Instance.AddArrowsCurrency(rewardAmount);
                    }
                    OnCoinsRewardReceived?.Invoke();
                    StartCoroutine(SpawnCoinsExplosionDeferred());

                    if (FirebaseManager.Instance != null)
                    {
                        FirebaseManager.Instance.LogEvent("ad_reward_coins",
                            new Firebase.Analytics.Parameter("reward_amount", rewardAmount));
                    }
                    break;

                case RewardAdType.MultiplyReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: MultiplyReward → firing OnMultiplyRewardReceived.");
                    OnMultiplyRewardReceived?.Invoke();
                    break;

                case RewardAdType.HintReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: HintReward → firing OnHintRewardReceived.");
                    OnHintRewardReceived?.Invoke();
                    break;

                case RewardAdType.PlayOnReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: PlayOnReward → firing OnPlayOnRewardReceived.");
                    OnPlayOnRewardReceived?.Invoke();
                    break;

                case RewardAdType.MagicReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: MagicReward → firing OnMagicRewardReceived.");
                    OnMagicRewardReceived?.Invoke();
                    break;

                case RewardAdType.LifeReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: LifeReward → firing OnLifeRewardReceived.");
                    OnLifeRewardReceived?.Invoke();
                    break;

                case RewardAdType.ShuffleReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: ShuffleReward → firing OnShuffleRewardReceived.");
                    OnShuffleRewardReceived?.Invoke();
                    break;

                default:
                    Debug.Log("[AdsManager] ProcessPendingReward: No pending reward (None). Ignoring.");
                    break;
            }
        }

        // ════════════════════════════════════════════
        //  REVENUE TRACKING (Firebase + Singular)
        // ════════════════════════════════════════════

        /// <summary>
        /// Sends AppLovin MAX impression-level revenue (ILRD) to Firebase and Singular.
        /// MAX fires OnAdRevenuePaidEvent off the main thread unless InvokeEventsOnUnityMainThread is set.
        /// </summary>
        private void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, string adFormat)
        {
            if (adInfo == null)
            {
                Debug.LogWarning("[AdsManager] TrackAdRevenue: adInfo is null");
                return;
            }

            double revenueUsd = adInfo.Revenue;
            string resolvedFormat = string.IsNullOrEmpty(adInfo.AdFormat) ? adFormat : adInfo.AdFormat;

            if (revenueUsd <= 0)
            {
                Debug.LogWarning(
                    $"[AdsManager] TrackAdRevenue: no revenue (network={adInfo.NetworkName}, " +
                    $"unit={adInfo.AdUnitIdentifier}, format={resolvedFormat}, precision={adInfo.RevenuePrecision}). " +
                    "ILRD is delivered via OnAdRevenuePaidEvent when MAX has revenue data—common causes: test ads, " +
                    "revenue=-1 (error), or network not fully configured in MAX Mediation.");
                return;
            }

            var payload = new SingularAdRevenuePayload(
                revenueUsd,
                adInfo.NetworkName,
                adInfo.AdUnitIdentifier,
                resolvedFormat,
                adInfo.RevenuePrecision);

            EnqueueAction(() => ReportAdRevenueOnMainThread(payload));
        }

        private void ReportAdRevenueOnMainThread(SingularAdRevenuePayload payload)
        {
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "AppLovinMAX"),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, payload.NetworkName),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, payload.AdUnitId),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, payload.AdFormat),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, payload.RevenueUsd),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
            }

#if !UNITY_EDITOR
            if (!SingularSDK.Initialized)
            {
                _pendingSingularRevenue.Enqueue(payload);
                EnsurePendingSingularRevenueFlushScheduled();
                Debug.LogWarning(
                    $"[AdsManager] Singular not initialized yet; queued ad revenue ${payload.RevenueUsd:F6} " +
                    $"(network={payload.NetworkName}, format={payload.AdFormat}).");
                return;
            }

            SendSingularAdRevenue(payload);
#endif

            Debug.Log(
                $"[AdsManager] Ad Revenue: ${payload.RevenueUsd:F6} USD, network={payload.NetworkName}, " +
                $"format={payload.AdFormat}, precision={payload.RevenuePrecision}");
        }

        private void SendSingularAdRevenue(SingularAdRevenuePayload payload)
        {
            SingularAdData singularAdData = new SingularAdData("AppLovin", "USD", payload.RevenueUsd);
            singularAdData.WithNetworkName(payload.NetworkName)
                          .WithAdUnitName(payload.AdUnitId)
                          .WithAdType(payload.AdFormat)
                          .WithPrecision(payload.RevenuePrecision);

            if (!singularAdData.HasRequiredParams())
            {
                Debug.LogWarning(
                    "[AdsManager] Singular ad revenue skipped: missing required params " +
                    $"(network={payload.NetworkName}, unit={payload.AdUnitId}, revenue={payload.RevenueUsd}).");
                return;
            }

            SingularSDK.AdRevenue(singularAdData);
            Debug.Log(
                $"[AdsManager] Singular ad revenue sent: ${payload.RevenueUsd:F6} USD, " +
                $"network={payload.NetworkName}, format={payload.AdFormat}");
        }

        /// <summary>Called by IOSAttributionBootstrap after SingularSDK.InitializeSingularSDK succeeds.</summary>
        public static void NotifySingularSdkInitialized()
        {
            if (Instance == null)
                return;

            Instance.EnqueueAction(Instance.FlushPendingSingularRevenueIfReady);
        }

        private void EnsurePendingSingularRevenueFlushScheduled()
        {
            if (_isFlushingPendingSingularRevenue)
                return;

            _isFlushingPendingSingularRevenue = true;
            StartCoroutine(FlushPendingSingularRevenueWhenReady());
        }

        private IEnumerator FlushPendingSingularRevenueWhenReady()
        {
            const float timeoutSeconds = 120f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!SingularSDK.Initialized && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!SingularSDK.Initialized)
            {
                Debug.LogWarning(
                    $"[AdsManager] Singular still not initialized after {timeoutSeconds}s; " +
                    $"dropping {_pendingSingularRevenue.Count} queued ad revenue event(s).");
                while (_pendingSingularRevenue.TryDequeue(out _)) { }
                _isFlushingPendingSingularRevenue = false;
                yield break;
            }

            FlushPendingSingularRevenueIfReady();
            _isFlushingPendingSingularRevenue = false;
        }

        private void FlushPendingSingularRevenueIfReady()
        {
#if !UNITY_EDITOR
            if (!SingularSDK.Initialized)
                return;

            int flushedCount = 0;
            while (_pendingSingularRevenue.TryDequeue(out SingularAdRevenuePayload payload))
            {
                SendSingularAdRevenue(payload);
                flushedCount++;
            }

            if (flushedCount > 0)
                Debug.Log($"[AdsManager] Flushed {flushedCount} queued Singular ad revenue event(s).");
#endif
        }

        // ════════════════════════════════════════════
        //  VFX
        // ════════════════════════════════════════════

        public void SpawnCoinsSmallExplosion()
        {
            StartCoroutine(SpawnCoinsExplosionDeferred());
        }

        private IEnumerator SpawnCoinsExplosionDeferred()
        {
            yield return null;
            yield return null;

            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < 11) yield break;

            GameObject prefab = _coinsExplosionPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("CoinsSmallExplosion");
                _coinsExplosionPrefab = prefab;
            }

            if (prefab != null)
            {
                Vector3 spawnPos = new Vector3(-0.5f, 2.4f, 60.2f);
                GameObject explosion = Instantiate(prefab, spawnPos, prefab.transform.rotation);
                Destroy(explosion, 3.0f);
            }
            else
            {
                Debug.LogWarning("[AdsManager] CoinsSmallExplosion prefab not found in Resources.");
            }
        }
    }
}
