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

        private enum RewardAdType { None, GameReward, CoinsReward, MultiplyReward, HintReward, PlayOnReward, MagicReward, LifeReward }
        private RewardAdType pendingRewardType = RewardAdType.None;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        public event Action OnRewardReceived;
        public event Action OnCoinsRewardReceived;
        public event Action OnMultiplyRewardReceived;
        public event Action OnHintRewardReceived;
        public event Action OnPlayOnRewardReceived;
        public event Action OnMagicRewardReceived;
        public event Action OnLifeRewardReceived;
        public event Action OnAdOpened;
        public event Action OnAdClosed;
        /// <summary>Fired when any cached ad-ready flag changes (avoids per-frame native IsAdReady calls).</summary>
        public event Action OnAdReadinessChanged;

        private bool _rewardedReady;
        private bool _interstitialReady;
        private bool _bannerReady;
        private bool _bannerCreated;

        private GameObject _coinsExplosionPrefab;
        private Coroutine _deferredWorkCoroutine;
        private float _lastAdCloseTime = -999f;
        private bool _showNextInterstitial = true;

        private const float HealthCheckIntervalSeconds = 20f;
        private const float PostAdLoadCooldownSeconds = 4f;
        private const float DeferredLoadDelaySeconds = 1.5f;

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

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnDestroy()
        {
            if (_deferredWorkCoroutine != null)
            {
                StopCoroutine(_deferredWorkCoroutine);
                _deferredWorkCoroutine = null;
            }

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

            if (_bannerCreated) MaxSdk.DestroyBanner(BannerAdUnitId);
        }

        // ════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════

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

        private void ScheduleDeferredLoad(Action loadAction)
        {
            if (loadAction == null) return;
            if (_deferredWorkCoroutine != null)
                StopCoroutine(_deferredWorkCoroutine);
            _deferredWorkCoroutine = StartCoroutine(DeferredLoadRoutine(loadAction));
        }

        private IEnumerator DeferredLoadRoutine(Action loadAction)
        {
            yield return new WaitForSecondsRealtime(DeferredLoadDelaySeconds);
            if (this == null || loadAction == null) yield break;
            if (Time.realtimeSinceStartup - _lastAdCloseTime < PostAdLoadCooldownSeconds) yield break;
            loadAction.Invoke();
            _deferredWorkCoroutine = null;
        }

        private void NotifyAdClosed()
        {
            _lastAdCloseTime = Time.realtimeSinceStartup;
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

                    if (Time.realtimeSinceStartup - _lastAdCloseTime < PostAdLoadCooldownSeconds)
                        continue;

                    if (!_interstitialReady) LoadInterstitial();
                    if (!_rewardedReady) LoadRewarded();
                    if (!_bannerCreated || !_bannerReady) LoadSettingsBanner();
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
            if (IAPManager.Instance == null)
                Debug.LogWarning("[AdsManager] IAPManager.Instance is null. Proceeding without IAP check.");

            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: User has No Ads.");
                return;
            }
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: IsInterstitialActive is false (Remote Config).");
                return;
            }
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
        }

        private void OnInterstitialDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Interstitial Ad Display Failed (code: {errorInfo.Code})");
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
                pendingRewardType = RewardAdType.None;

            ScheduleDeferredLoad(LoadInterstitial);
        }

        private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Closed. Scheduling reload.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
            {
                Debug.Log("[AdsManager] Interstitial was used as a fallback. Giving pending reward.");
                ProcessPendingReward(fromRewardedAdUnit: false);
            }

            ScheduleDeferredLoad(LoadInterstitial);
        }

        private void OnInterstitialClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Clicked.");
        }

        private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
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

        public void ShowRewarded()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad (GameReward).");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Both Rewarded Ad and Interstitial Ad are not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForCoins()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad (CoinsReward).");
                pendingRewardType = RewardAdType.CoinsReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for coins. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.CoinsReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForMultiply()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad (MultiplyReward).");
                pendingRewardType = RewardAdType.MultiplyReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for multiply. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.MultiplyReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForHint()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Hint (HintReward).");
                pendingRewardType = RewardAdType.HintReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for hint. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.HintReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready for Hint. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForPlayOn()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for PlayOn (PlayOnReward).");
                pendingRewardType = RewardAdType.PlayOnReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for playon. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.PlayOnReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready for PlayOn. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForMagic()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Magic (MagicReward).");
                pendingRewardType = RewardAdType.MagicReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for magic. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.MagicReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready for Magic. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        public void ShowRewardedForLife()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Life (LifeReward).");
                pendingRewardType = RewardAdType.LifeReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowRewardedAd(RewardedAdUnitId);
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for life. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.LifeReward;
                OnAdOpened?.Invoke();
                MaxSdk.ShowInterstitial(InterstitialAdUnitId);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad and Interstitial are not ready for Life. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadRewarded();
                    LoadInterstitial();
                }
            }
        }

        #region Rewarded Callbacks

        private void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Loaded.");
            rewardedRetryAttempt = 0;
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
        }

        private void OnRewardedAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Rewarded Ad Display Failed (code: {errorInfo.Code})");
            pendingRewardType = RewardAdType.None;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            ScheduleDeferredLoad(LoadRewarded);
        }

        private void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Closed. Scheduling reload.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            ScheduleDeferredLoad(LoadRewarded);
        }

        private void OnRewardedAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Clicked.");
        }

        private void OnRewardedAdReceivedReward(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received.");
            ProcessPendingReward(fromRewardedAdUnit: true);
        }

        private void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
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

            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Banner Show: User has No Ads.");
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
            SetCachedReady(ref _bannerReady, true);
        }

        private void OnBannerAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"[AdsManager] Settings Banner Load Failed (code: {errorInfo.Code}).");
            SetCachedReady(ref _bannerReady, false);
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
        private void ProcessPendingReward(bool fromRewardedAdUnit)
        {
            RewardAdType rewardType = pendingRewardType;
            pendingRewardType = RewardAdType.None;

            if (rewardType != RewardAdType.None && fromRewardedAdUnit)
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

                default:
                    Debug.Log("[AdsManager] ProcessPendingReward: No pending reward (None). Ignoring.");
                    break;
            }
        }

        // ════════════════════════════════════════════
        //  REVENUE TRACKING (Firebase + Singular)
        // ════════════════════════════════════════════

        private void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, string adFormat)
        {
            if (adInfo == null)
            {
                Debug.LogWarning("[AdsManager] TrackAdRevenue: adInfo is null");
                return;
            }

            double revenueUsd = adInfo.Revenue;

            if (revenueUsd <= 0)
            {
                Debug.LogWarning(
                    $"[AdsManager] TrackAdRevenue: no revenue (network={adInfo.NetworkName}, " +
                    $"unit={adInfo.AdUnitIdentifier}, format={adFormat}). " +
                    "Verify impression-level revenue is enabled in the AppLovin dashboard.");
                return;
            }

            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "AppLovinMAX"),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, adInfo.NetworkName),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, adInfo.AdUnitIdentifier),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, adFormat),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, revenueUsd),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
            }

            SingularAdData singularAdData = new SingularAdData("AppLovin", "USD", revenueUsd);
            singularAdData.WithNetworkName(adInfo.NetworkName)
                          .WithAdUnitName(adInfo.AdUnitIdentifier)
                          .WithAdType(adFormat);
            SingularSDK.AdRevenue(singularAdData);
            Debug.Log($"[AdsManager] Ad Revenue: ${revenueUsd:F6} USD, network={adInfo.NetworkName}, format={adFormat}");
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
