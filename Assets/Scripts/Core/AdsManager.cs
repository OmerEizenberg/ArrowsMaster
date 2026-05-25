using Unity.Services.LevelPlay;
using Unity.Services.Core;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections;
using Singular;
using System.Collections.Generic;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd RewardedAd;
        private LevelPlayRewardedAd coinsRewardedAd;
        private LevelPlayRewardedAd multiplyRewardedAd;
        private LevelPlayBannerAd settingsBannerAd;
        public bool IsInitialized => isInitialized;
        private bool isInitialized = false;

        private bool isInitializing = false;
        private int sdkInitRetryCount = 0;
        private float lastAdShowTime = -60f;

        // Track which rewarded ad type is currently being shown
        private enum RewardAdType { None, GameReward, CoinsReward, MultiplyReward, HintReward, PlayOnReward, MagicReward, LifeReward }
        private RewardAdType pendingRewardType = RewardAdType.None;

        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

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

        // Cached readiness — updated on load/close/display events only (not every frame).
        private bool _rewardedReady;
        private bool _multiplyRewardedReady;
        private bool _coinsRewardedReady;
        private bool _interstitialReady;

        private GameObject _coinsExplosionPrefab;
        private Coroutine _deferredWorkCoroutine;
        private float _lastAdCloseTime = -999f;

        private const float HealthCheckIntervalSeconds = 20f;
        private const float PostAdLoadCooldownSeconds = 4f;
        private const float DeferredLoadDelaySeconds = 1.5f;

        private bool sharesGameRewardedUnitForCoins;

        public bool IsRewardedReady => _rewardedReady;
        public bool IsMultiplyRewardedReady => _multiplyRewardedReady;
        public bool IsCoinsRewardedReady => sharesGameRewardedUnitForCoins ? _rewardedReady : _coinsRewardedReady;
        public bool IsInterstitialReady => _interstitialReady;
        public bool IsAnyRewardedOrInterstitialReady =>
            _rewardedReady || _interstitialReady || _coinsRewardedReady || _multiplyRewardedReady;

        private string AppKey
        {
            get
            {
#if (UNITY_ANDROID || UNITY_EDITOR) && !UNITY_IOS
                return "24f080a95"; 
#elif UNITY_IOS || UNITY_IPHONE
                return "252e4a28d";
#else
                return "unexpected_platform";
#endif
            }
        }

        private string InterstitialAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "88alfrvdudilhun7"; // iOS back_to_lobby_interstitial
#else
                return "dctkavzgndg9gm8m"; // Android back_to_lobby_interstitial
#endif
            }
        }

        private string RewardedAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "lmgxqjtfhmyikgzm"; // iOS ad_rewarded
#else
                return "if9z8hp6gm6ukwvh"; // Android ad_rewarded
#endif
            }
        }

        private string CoinsRewardedAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "ncnu1ipmqxwjbszr"; // iOS ad_rewarded
#else
                return "if9z8hp6gm6ukwvh"; // Android ad_rewarded fallback
#endif
            }
        }

        private string MultiplyRewardedAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "zgyen7itv8su1vt9"; // iOS ad_rewarded_multiply
#else
                return "xl1zg79un0qwx6u8"; // Android ad_rewarded_multiply
#endif
            }
        }

        private string SettingsBannerAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "jbr5jpvpbixrle5a"; // iOS banner
#else
                return "rd82j6gdgow61x63"; // Android banner
#endif
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject adsGO = new GameObject("AdsManager");
                adsGO.AddComponent<AdsManager>();
                DontDestroyOnLoad(adsGO);
                // Instance is set in Awake
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

#if (UNITY_ANDROID || UNITY_EDITOR) && !UNITY_IOS
            sharesGameRewardedUnitForCoins = RewardedAdUnitId == CoinsRewardedAdUnitId;
#else
            sharesGameRewardedUnitForCoins = false;
#endif

            _coinsExplosionPrefab = Resources.Load<GameObject>("CoinsSmallExplosion");

            _ = InitializeSDK();
            StartCoroutine(AdHealthCheckRoutine());
        }

        private void SetCachedReady(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            OnAdReadinessChanged?.Invoke();
        }

        private static bool QueryNativeReady(LevelPlayInterstitialAd ad)
        {
            if (ad == null) return false;
            try { return ad.IsAdReady(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] Error checking interstitial readiness: {e.Message}");
                return false;
            }
        }

        private static bool QueryNativeReady(LevelPlayRewardedAd ad)
        {
            if (ad == null) return false;
            try { return ad.IsAdReady(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] Error checking rewarded readiness: {e.Message}");
                return false;
            }
        }

        private void RefreshInterstitialReady() =>
            SetCachedReady(ref _interstitialReady, QueryNativeReady(interstitialAd));

        private void RefreshRewardedReady() =>
            SetCachedReady(ref _rewardedReady, QueryNativeReady(RewardedAd));

        private void RefreshCoinsRewardedReady()
        {
            if (sharesGameRewardedUnitForCoins)
            {
                SetCachedReady(ref _coinsRewardedReady, _rewardedReady);
                return;
            }
            SetCachedReady(ref _coinsRewardedReady, QueryNativeReady(coinsRewardedAd));
        }

        private void RefreshMultiplyRewardedReady() =>
            SetCachedReady(ref _multiplyRewardedReady, QueryNativeReady(multiplyRewardedAd));

        private void RefreshAllReadiness()
        {
            RefreshInterstitialReady();
            RefreshRewardedReady();
            RefreshCoinsRewardedReady();
            RefreshMultiplyRewardedReady();
        }

        private void ScheduleDeferredLoad(Action loadAction)
        {
            if (loadAction == null) return;
            if (_deferredWorkCoroutine != null)
            {
                StopCoroutine(_deferredWorkCoroutine);
            }
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

                    // Avoid stacking native load work right after an ad closes.
                    if (Time.realtimeSinceStartup - _lastAdCloseTime < PostAdLoadCooldownSeconds)
                    {
                        continue;
                    }

                    if (interstitialAd != null && !_interstitialReady)
                    {
                        LoadInterstitial();
                    }

                    if (RewardedAd != null && !_rewardedReady)
                    {
                        LoadRewarded();
                    }

                    if (!sharesGameRewardedUnitForCoins && coinsRewardedAd != null && !_coinsRewardedReady)
                    {
                        LoadCoinsRewarded();
                    }

                    if (multiplyRewardedAd != null && !_multiplyRewardedReady)
                    {
                        LoadMultiplyRewarded();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AdsManager] HealthCheck error: {e.Message}");
                }
            }
        }

        private async Task InitializeSDK()
        {
            if (isInitialized || isInitializing) return;

            isInitializing = true;
            try
            {
                await IAPManager.EnsureUnityServicesInitializedAsync();
                
                Debug.Log("[AdsManager] Unity Services Initialized.");

#if UNITY_IOS && !UNITY_EDITOR
                // Request ATT before consent/ads init (required for IDFA on iOS 14+)
                bool attFinished = false;
                EnqueueAction(() =>
                {
                    IOSAdsHelper.RequestATT();
                    StartCoroutine(IOSAdsHelper.PollATTStatus(_ => attFinished = true));
                });
                float attWaitStart = Time.time;
                while (!attFinished && Time.time - attWaitStart < 30f)
                {
                    await Task.Yield();
                }
                if (!attFinished)
                {
                    Debug.LogWarning("[AdsManager] ATT flow timed out. Proceeding with ads initialization.");
                }
#endif
                
                // Request Consent (GDPR/UMP) and wait for completion
                bool consentFinished = false;
                
                // UMP requires main thread, so we dispatch it
                EnqueueAction(() => 
                {
                    ConsentManager.RequestConsent(() => 
                    {
                        consentFinished = true;
                        Debug.Log("[AdsManager] Consent gathering finished. Continuing SDK Init.");
                    });
                });

                // Wait for UMP flow to finish. This might include showing UI.
                // We give it a generous timeout just in case it hangs, but normally it completes when the user dismisses the form.
                float waitTimeout = 60.0f; 
                float waitStart = Time.time;
                while (!consentFinished && Time.time - waitStart < waitTimeout) 
                {
                    await Task.Yield();
                }
                
                if (!consentFinished)
                {
                    Debug.LogWarning("[AdsManager] Consent flow timed out. Proceeding with SDK initialization anyway.");
                }

                string currentAppKey = AppKey;
                Debug.Log($"[AdsManager] Initializing LevelPlay SDK with AppKey: {currentAppKey} (Platform: {Application.platform})");
                
                if (currentAppKey.Length <= 7 && int.TryParse(currentAppKey, out _))
                {
                    Debug.LogWarning("[AdsManager] WARNING: The provided AppKey looks like a Unity Game ID. LevelPlay requires an ironSource App Key.");
                }

                // Unsubscribe first to avoid duplicate registrations on retry
                LevelPlay.OnInitSuccess -= OnSdkInitSuccess;
                LevelPlay.OnInitFailed -= OnSdkInitFailed;
                LevelPlay.OnInitSuccess += OnSdkInitSuccess;
                LevelPlay.OnInitFailed += OnSdkInitFailed;

                LevelPlay.OnImpressionDataReady -= OnImpressionDataReady;
                LevelPlay.OnImpressionDataReady += OnImpressionDataReady;
                
                LevelPlay.Init(currentAppKey, SystemInfo.deviceUniqueIdentifier);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdsManager] SDK Initialization Process Failed: {e.Message}");
                isInitializing = false;
                _ = RetrySDKInitialization(20000); // Retry in 20s on critical failure
            }
        }

        private async Task RetrySDKInitialization(int delayMs)
        {
            if (isInitialized || isInitializing) return;
            
            Debug.Log($"[AdsManager] Retrying SDK Initialization in {delayMs/1000}s... (Attempt {sdkInitRetryCount + 1})");
            await Task.Delay(delayMs);
            
            if (this != null && !isInitialized && !isInitializing)
            {
                sdkInitRetryCount++;
                EnqueueAction(() => _ = InitializeSDK());
            }
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            // IronSource/LevelPlay requires this for proper tracking and ad state management on device
            // If the IronSource namespace is available, it should be called:
            // IronSource.Agent.onApplicationPause(isPaused);
            // Since we are using the LPM-based SDK, we ensure the agent is informed if the class exists.
        }

        private void EnqueueAction(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void OnImpressionDataReady(LevelPlayImpressionData impressionData)
        {
            EnqueueAction(() => {
                if (impressionData == null)
                {
                    Debug.LogWarning("[AdsManager] ImpressionDataReady: impressionData is null");
                    return;
                }

                if (impressionData.Revenue == null || impressionData.Revenue.Value <= 0)
                {
                    Debug.LogWarning(
                        $"[AdsManager] ImpressionDataReady: no revenue (network={impressionData.AdNetwork}, " +
                        $"unit={impressionData.MediationAdUnitName}, format={impressionData.AdFormat}). " +
                        "Enable impression-level revenue in LevelPlay/ironSource if this persists.");
                    return;
                }

                double revenueUsd = impressionData.Revenue.Value;

                if (FirebaseManager.Instance != null)
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, impressionData.AdNetwork),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, impressionData.MediationAdUnitName),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, impressionData.AdFormat),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, revenueUsd),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
                }

                // Singular docs require platform name "IronSource" (not "ironSource")
                SingularAdData singularAdData = new SingularAdData("IronSource", "USD", revenueUsd);
                singularAdData.WithNetworkName(impressionData.AdNetwork)
                              .WithAdUnitName(impressionData.MediationAdUnitName)
                              .WithAdType(impressionData.AdFormat);
                SingularSDK.AdRevenue(singularAdData);
                Debug.Log($"[AdsManager] Singular AdRevenue: ${revenueUsd:F6} USD, network={impressionData.AdNetwork}");
            });
        }

        private void OnSdkInitSuccess(LevelPlayConfiguration config)
        {
            EnqueueAction(() => {
                Debug.Log("[AdsManager] LevelPlay SDK Initialized Successfully.");
                isInitialized = true;
                isInitializing = false;
                sdkInitRetryCount = 0;
                StartCoroutine(FinishSdkInitAfterAdapterDelay());
            });
        }

        private IEnumerator FinishSdkInitAfterAdapterDelay()
        {
            Debug.Log("[AdsManager] Waiting 2s for adapters to stabilize before pre-loading ads...");
            yield return new WaitForSeconds(2f);

            if (this == null) yield break;

            CreateInterstitialAd();
            CreateRewardedAd();
            CreateCoinsRewardedAd();
            CreateMultiplyRewardedAd();
            CreateSettingsBannerAd();
            RefreshAllReadiness();
        }

        private void OnSdkInitFailed(LevelPlayInitError error)
        {
            EnqueueAction(() => {
                Debug.LogError($"[AdsManager] LevelPlay SDK Initialization Failed: {error}");
                isInitializing = false;
                
                // Exponential backoff for retries: 15s, 30s, 60s, 120s...
                int retryDelay = 15000 * (int)Mathf.Pow(2, Mathf.Min(sdkInitRetryCount, 4));
                _ = RetrySDKInitialization(retryDelay);
            });
        }

        private void CreateInterstitialAd()
        {
            if (interstitialAd != null)
            {
                interstitialAd.DestroyAd();
            }

            interstitialAd = new LevelPlayInterstitialAd(InterstitialAdUnitId);
            
            interstitialAd.OnAdLoaded += OnInterstitialLoaded;
            interstitialAd.OnAdLoadFailed += OnInterstitialLoadFailed;
            interstitialAd.OnAdClosed += OnInterstitialClosed;
            interstitialAd.OnAdDisplayFailed += OnInterstitialDisplayFailed;
            interstitialAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Interstitial Ad Displayed: {info}");
                    SetCachedReady(ref _interstitialReady, false);
                });
            };
            interstitialAd.OnAdClicked += (info) => Debug.Log($"[AdsManager] Interstitial Ad Clicked: {info}");

            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            if (!isInitialized || interstitialAd == null) 
            {
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                return;
            }
            if (IAPManager.Instance == null)
                Debug.LogWarning("[AdsManager] IAPManager.Instance is null. Proceeding without IAP check.");


            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: User has No Ads."+IAPManager.Instance.HasNoAds);
                return;
            }
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: IsInterstitialActive is false (Remote Config).");
                return;
            }
            Debug.Log("[AdsManager] Loading Interstitial Ad...");

            interstitialAd.LoadAd();
        }

        private bool _showNextInterstitial = true;

        public void ShowInterstitial(bool isAuto = false)
        {
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: IsInterstitialActive is false (Remote Config).");
                return;
            }

            // 50% Frequency Logic: Inverse the switch on every trigger (only if toggle is on)
            bool shouldShowThisTime = _showNextInterstitial;
            _showNextInterstitial = !_showNextInterstitial;

            if (!shouldShowThisTime)
            {
                Debug.Log("[AdsManager] Skipping Interstitial due to 50% frequency rule.");
                return;
            }

            // Allow showing ads from the unlock level
            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < GameManager.ADS_START_LEVEL)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial Show: User Level {UserDataManager.Instance.CurrentLevel} < {GameManager.ADS_START_LEVEL}.");
                return;
            }

            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: User has No Ads."+IAPManager.Instance.HasNoAds);
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
                interstitialAd.ShowAd();
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Interstitial Ad is not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else LoadInterstitial();
            }
        }

        private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
        {
            EnqueueAction(() => {
                Debug.Log($"[AdsManager] Interstitial Ad Loaded: {adInfo}");
                RefreshInterstitialReady();
            });
        }

        private void OnInterstitialLoadFailed(LevelPlayAdError error)
        {
            EnqueueAction(() => {
                Debug.LogWarning($"[AdsManager] Interstitial Ad Load Failed: {error}. Retrying in 15s...");
                _ = RetryLoadInterstitial(15000);
            });
        }

        private async Task RetryLoadInterstitial(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !IsInterstitialReady)
            {
                EnqueueAction(LoadInterstitial);
            }
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            EnqueueAction(() => {
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
            });
        }

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            EnqueueAction(() => {
                Debug.LogError($"[AdsManager] Interstitial Ad Display Failed: {error}");
                SetCachedReady(ref _interstitialReady, false);
                NotifyAdClosed();
                
                if (pendingRewardType != RewardAdType.None)
                {
                    pendingRewardType = RewardAdType.None;
                }

                ScheduleDeferredLoad(LoadInterstitial);
            });
        }

        // ---  Rewarded Ad ---
        private void CreateRewardedAd()
        {
            if (RewardedAd != null) RewardedAd.DestroyAd();
            RewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            
            RewardedAd.OnAdClosed += (info) => { 
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Rewarded Ad Closed. Scheduling reload.");
                    lastAdShowTime = Time.time;
                    SetCachedReady(ref _rewardedReady, false);
                    if (sharesGameRewardedUnitForCoins) SetCachedReady(ref _coinsRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadRewarded);
                });
            };
            
            RewardedAd.OnAdDisplayFailed += (info, err) => { 
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    SetCachedReady(ref _rewardedReady, false);
                    if (sharesGameRewardedUnitForCoins) SetCachedReady(ref _coinsRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadRewarded);
                });
            };
            
            RewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Rewarded Ad Displayed: {info}");
                    SetCachedReady(ref _rewardedReady, false);
                    if (sharesGameRewardedUnitForCoins) SetCachedReady(ref _coinsRewardedReady, false);
                });
            };

            RewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward(fromRewardedAdUnit: true);
                });
            };
            
            RewardedAd.OnAdLoaded += (info) => EnqueueAction(() => {
                Debug.Log($"[AdsManager] Rewarded Ad Loaded: {info}");
                RefreshRewardedReady();
                if (sharesGameRewardedUnitForCoins) RefreshCoinsRewardedReady();
            });
            RewardedAd.OnAdLoadFailed += (info) => {
                EnqueueAction(() => {
                     Debug.LogWarning($"[AdsManager] Rewarded Ad Load Failed: {info}. Retrying in 15s...");
                     _ = RetryLoadRewarded(15000);
                });
            };

            LoadRewarded();
        }

        private async Task RetryLoadRewarded(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !IsRewardedReady)
            {
                EnqueueAction(LoadRewarded);
            }
        }

        public void LoadRewarded()
        {
            if (!isInitialized || RewardedAd == null) 
            {
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                return;
            }
            RewardedAd.LoadAd();
        }

        public void ShowRewarded()
        {
            if (IsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad (GameReward).");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            // Rewarded fallbacks use interstitial even for No Ads buyers so they still receive the reward.
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
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

        /// <summary>
        /// Central reward processor. Captures and resets pendingRewardType atomically,
        /// then dispatches the correct reward. Safe if called multiple times — second call is a no-op.
        /// </summary>
        private void ProcessPendingReward(bool fromRewardedAdUnit)
        {
            // Capture and immediately reset so duplicate callbacks are no-ops
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

                    // --- Analytics: ad_reward_coins ---
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

        // --- Coins Rewarded Ad ---
        private void CreateCoinsRewardedAd()
        {
            if (sharesGameRewardedUnitForCoins)
            {
                coinsRewardedAd = null;
                RefreshCoinsRewardedReady();
                Debug.Log("[AdsManager] Coins rewarded uses shared game rewarded unit on Android — no duplicate ad instance.");
                return;
            }

            if (coinsRewardedAd != null) coinsRewardedAd.DestroyAd();
            coinsRewardedAd = new LevelPlayRewardedAd(CoinsRewardedAdUnitId);

            coinsRewardedAd.OnAdClosed += (info) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Coins Rewarded Ad Closed. Scheduling reload.");
                    lastAdShowTime = Time.time;
                    SetCachedReady(ref _coinsRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadCoinsRewarded);
                });
            };

            coinsRewardedAd.OnAdDisplayFailed += (info, err) => {
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Coins Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    SetCachedReady(ref _coinsRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadCoinsRewarded);
                });
            };

            coinsRewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Coins Rewarded Ad Displayed: {info}");
                });
            };

            coinsRewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Coins Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward(fromRewardedAdUnit: true);
                });
            };

            coinsRewardedAd.OnAdLoaded += (info) => EnqueueAction(() => {
                Debug.Log($"[AdsManager] Coins Rewarded Ad Loaded: {info}");
                RefreshCoinsRewardedReady();
            });
            coinsRewardedAd.OnAdLoadFailed += (info) => {
                EnqueueAction(() => {
                    Debug.LogWarning($"[AdsManager] Coins Rewarded Ad Load Failed: {info}. Retrying in 15s...");
                    _ = RetryLoadCoinsRewarded(15000);
                });
            };

            LoadCoinsRewarded();
        }

        private async Task RetryLoadCoinsRewarded(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !IsCoinsRewardedReady)
            {
                EnqueueAction(LoadCoinsRewarded);
            }
        }

        public void LoadCoinsRewarded()
        {
            if (sharesGameRewardedUnitForCoins)
            {
                LoadRewarded();
                return;
            }

            if (!isInitialized || coinsRewardedAd == null) 
            {
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                return;
            }
            coinsRewardedAd.LoadAd();
        }

        public void ShowRewardedForCoins()
        {
            if (IsCoinsRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Coins Rewarded Ad (CoinsReward).");
                pendingRewardType = RewardAdType.CoinsReward;
                OnAdOpened?.Invoke();
                if (sharesGameRewardedUnitForCoins)
                {
                    RewardedAd.ShowAd();
                }
                else
                {
                    coinsRewardedAd.ShowAd();
                }
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Coins Rewarded Ad is not ready. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.CoinsReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Coins Rewarded Ad and Interstitial are not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadCoinsRewarded();
                    LoadInterstitial();
                }
            }
        }

        // --- Multiply Rewarded Ad ---
        private void CreateMultiplyRewardedAd()
        {
            if (multiplyRewardedAd != null) multiplyRewardedAd.DestroyAd();
            multiplyRewardedAd = new LevelPlayRewardedAd(MultiplyRewardedAdUnitId);

            multiplyRewardedAd.OnAdClosed += (info) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Multiply Rewarded Ad Closed. Scheduling reload.");
                    lastAdShowTime = Time.time;
                    SetCachedReady(ref _multiplyRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadMultiplyRewarded);
                });
            };

            multiplyRewardedAd.OnAdDisplayFailed += (info, err) => {
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Multiply Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    SetCachedReady(ref _multiplyRewardedReady, false);
                    NotifyAdClosed();
                    ScheduleDeferredLoad(LoadMultiplyRewarded);
                });
            };

            multiplyRewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Multiply Rewarded Ad Displayed: {info}");
                });
            };

            multiplyRewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Multiply Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward(fromRewardedAdUnit: true);
                });
            };

            multiplyRewardedAd.OnAdLoaded += (info) => EnqueueAction(() => {
                Debug.Log($"[AdsManager] Multiply Rewarded Ad Loaded: {info}");
                RefreshMultiplyRewardedReady();
            });
            multiplyRewardedAd.OnAdLoadFailed += (info) => {
                EnqueueAction(() => {
                    Debug.LogWarning($"[AdsManager] Multiply Rewarded Ad Load Failed: {info}. Retrying in 15s...");
                    _ = RetryLoadMultiplyRewarded(15000);
                });
            };

            LoadMultiplyRewarded();
        }

        private async Task RetryLoadMultiplyRewarded(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !IsMultiplyRewardedReady)
            {
                EnqueueAction(LoadMultiplyRewarded);
            }
        }

        public void LoadMultiplyRewarded()
        {
            if (!isInitialized || multiplyRewardedAd == null)
            {
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                return;
            }
            multiplyRewardedAd.LoadAd();
        }

        public void ShowRewardedForMultiply()
        {
            if (IsMultiplyRewardedReady)
            {
                Debug.Log("[AdsManager] Showing Multiply Rewarded Ad (MultiplyReward).");
                pendingRewardType = RewardAdType.MultiplyReward;
                OnAdOpened?.Invoke();
                multiplyRewardedAd.ShowAd();
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Multiply Rewarded Ad is not ready. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.MultiplyReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Multiply Rewarded Ad and Interstitial are not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else
                {
                    LoadMultiplyRewarded();
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
                RewardedAd.ShowAd();
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for hint. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.HintReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
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
                RewardedAd.ShowAd();
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for playon. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.PlayOnReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
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
                RewardedAd.ShowAd();
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for magic. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.MagicReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
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
                RewardedAd.ShowAd();
            }
            else if (IsInterstitialReady)
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready for life. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.LifeReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd();
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

        public void SpawnCoinsSmallExplosion()
        {
            StartCoroutine(SpawnCoinsExplosionDeferred());
        }

        private IEnumerator SpawnCoinsExplosionDeferred()
        {
            // Let the OS finish tearing down the fullscreen ad before spawning VFX.
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

        // --- Banner Ad ---
        private void CreateSettingsBannerAd()
        {
            if (settingsBannerAd != null) settingsBannerAd.DestroyAd();
            settingsBannerAd = new LevelPlayBannerAd(SettingsBannerAdUnitId);
            
            settingsBannerAd.OnAdLoaded += (info) => Debug.Log($"[AdsManager] Settings Banner Loaded: {info}");
            settingsBannerAd.OnAdLoadFailed += (error) => Debug.LogError($"[AdsManager] Settings Banner Load Failed: {error}");
            settingsBannerAd.OnAdDisplayed += (info) => {
                Debug.Log($"[AdsManager] Settings Banner Displayed: {info}");
            };

            Debug.Log("[AdsManager] Pre-loading Settings Banner Ad (Hidden).");
            settingsBannerAd.LoadAd();
            settingsBannerAd.HideAd();
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

            if (settingsBannerAd == null)
            {
                CreateSettingsBannerAd();
            }

            Debug.Log("[AdsManager] Showing Settings Banner Ad.");
            settingsBannerAd.ShowAd();
            // Ad is already pre-loaded or loading in background
        }

        public void HideSettingsBanner()
        {
            if (settingsBannerAd != null)
            {
                Debug.Log("[AdsManager] Hiding Settings Banner Ad.");
                settingsBannerAd.HideAd();
            }
        }

        private void OnDestroy()
        {
            if (_deferredWorkCoroutine != null)
            {
                StopCoroutine(_deferredWorkCoroutine);
                _deferredWorkCoroutine = null;
            }

            LevelPlay.OnInitSuccess -= OnSdkInitSuccess;
            LevelPlay.OnInitFailed -= OnSdkInitFailed;
            LevelPlay.OnImpressionDataReady -= OnImpressionDataReady;

            if (interstitialAd != null) interstitialAd.DestroyAd();
            if (RewardedAd != null) RewardedAd.DestroyAd();
            if (coinsRewardedAd != null) coinsRewardedAd.DestroyAd();
            if (multiplyRewardedAd != null) multiplyRewardedAd.DestroyAd();
            if (settingsBannerAd != null) settingsBannerAd.DestroyAd();
        }
    }
}
