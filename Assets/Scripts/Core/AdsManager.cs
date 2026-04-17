using Unity.Services.LevelPlay;
using Unity.Services.Core;
using UnityEngine;
using System;
using System.Threading.Tasks;
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
        private float lastAdShowTime = -30f;
        private const float AD_COOLDOWN = 120f;

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
        public bool IsRewardedReady => RewardedAd != null && RewardedAd.IsAdReady();
        public bool IsMultiplyRewardedReady => multiplyRewardedAd != null && multiplyRewardedAd.IsAdReady();
        public bool IsCoinsRewardedReady => coinsRewardedAd != null && coinsRewardedAd.IsAdReady();
        public bool IsInterstitialReady => interstitialAd != null && interstitialAd.IsAdReady();

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
            
            _ = InitializeSDK();
        }

        private async Task InitializeSDK()
        {
            if (isInitialized || isInitializing) return;

            isInitializing = true;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log($"[AdsManager] Initializing Unity Services... (Current State: {UnityServices.State})");
                    await UnityServices.InitializeAsync();
                }
                
                Debug.Log("[AdsManager] Unity Services Initialized.");
                
                // Request ATT and wait for choice to set proper LevelPlay consent status
                bool attChoiceMade = false;
                IOSAdsHelper.RequestATT(); // Start the system request
                StartCoroutine(IOSAdsHelper.PollATTStatus((authorized) => {
                    attChoiceMade = true;
                    // Note: LevelPlay consent is set inside PollATTStatus
                    Debug.Log($"[AdsManager] ATT choice made: {authorized}. Continuing SDK Init.");
                }));

                // Wait for choice slightly to ensure IDFA is ready, but don't block indefinitely online
                float waitStart = Time.time;
                while (!attChoiceMade && Time.time - waitStart < 1.0f) // Max 1s wait here if needed, but the init can continue
                {
                    await Task.Yield();
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

        private void OnSdkInitSuccess(LevelPlayConfiguration config)
        {
            EnqueueAction(() => {
                Debug.Log("[AdsManager] LevelPlay SDK Initialized Successfully.");
                isInitialized = true;
                isInitializing = false;
                sdkInitRetryCount = 0;
                
                CreateInterstitialAd();
                CreateRewardedAd();
                CreateCoinsRewardedAd();
                CreateMultiplyRewardedAd();
                CreateSettingsBannerAd();
            });


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
                Debug.Log($"[AdsManager] Interstitial Ad Displayed: {info}");

                // --- Analytics: ad_impression (ILRD) ---
                if (FirebaseManager.Instance != null)
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, info.AdNetwork),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, info.AdUnitName),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, "interstitial"),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, info.Revenue ?? 0),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD")); // Revenue is in USD
                }
                
                // --- Singular: Ad Revenue tracking ---
                SingularAdData singularAdData = new SingularAdData("ironSource", "USD", info.Revenue ?? 0);
                singularAdData.WithNetworkName(info.AdNetwork)
                              .WithAdUnitName(info.AdUnitName)
                              .WithAdType("interstitial");
                SingularSDK.AdRevenue(singularAdData);
                // -------------------------------------
                // --------------------------------
            };
            interstitialAd.OnAdClicked += (info) => Debug.Log($"[AdsManager] Interstitial Ad Clicked: {info}");

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
                Debug.Log("[AdsManager] Skipping Interstitial Load: User has No Ads."+IAPManager.Instance.HasNoAds);
                return;
            }
            Debug.Log("[AdsManager] Loading Interstitial Ad...");
            interstitialAd.LoadAd();
        }

        public void ShowInterstitial(bool isAuto = false)
        {
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

            float timeSinceLastAd = Time.time - lastAdShowTime;
            if (timeSinceLastAd < AD_COOLDOWN)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial due to cooldown. Last ad was {timeSinceLastAd:F1}s ago.");
                return;
            }

            if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (this != null && !interstitialAd.IsAdReady())
            {
                EnqueueAction(LoadInterstitial);
            }
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            EnqueueAction(() => {
                Debug.Log("[AdsManager] Interstitial Ad Closed. Loading next one.");
                lastAdShowTime = Time.time;
                OnAdClosed?.Invoke();
                
                if (pendingRewardType != RewardAdType.None)
                {
                    Debug.Log("[AdsManager] Interstitial was used as a fallback. Giving pending reward.");
                    ProcessPendingReward();
                }

                LoadInterstitial();
            });
        }

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            EnqueueAction(() => {
                Debug.LogError($"[AdsManager] Interstitial Ad Display Failed: {error}");
                OnAdClosed?.Invoke();
                
                if (pendingRewardType != RewardAdType.None)
                {
                    pendingRewardType = RewardAdType.None;
                }

                LoadInterstitial();
            });
        }

        // ---  Rewarded Ad ---
        private void CreateRewardedAd()
        {
            if (RewardedAd != null) RewardedAd.DestroyAd();
            RewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            
            RewardedAd.OnAdClosed += (info) => { 
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Rewarded Ad Closed. Requesting next.");
                    lastAdShowTime = Time.time;
                    OnAdClosed?.Invoke();
                    LoadRewarded(); 
                });
            };
            
            RewardedAd.OnAdDisplayFailed += (info, err) => { 
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    OnAdClosed?.Invoke();
                    LoadRewarded(); 
                });
            };
            
            RewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Rewarded Ad Displayed: {info}");
                    
                    // --- Analytics: ad_impression (ILRD) ---
                    if (FirebaseManager.Instance != null)
                    {
                        FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, info.AdNetwork),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, info.AdUnitName),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, "rewarded"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, info.Revenue ?? 0),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
                    }

                    // --- Singular: Ad Revenue tracking ---
                    SingularAdData singularAdData = new SingularAdData("ironSource", "USD", info.Revenue ?? 0);
                    singularAdData.WithNetworkName(info.AdNetwork)
                                  .WithAdUnitName(info.AdUnitName)
                                  .WithAdType("rewarded_multiply");
                    SingularSDK.AdRevenue(singularAdData);
                    // -------------------------------------
                    
                    // --- Singular: Ad Revenue tracking ---
                    singularAdData = new SingularAdData("ironSource", "USD", info.Revenue ?? 0);
                    singularAdData.WithNetworkName(info.AdNetwork)
                                  .WithAdUnitName(info.AdUnitName)
                                  .WithAdType("rewarded");
                    SingularSDK.AdRevenue(singularAdData);
                    // -------------------------------------
                    // --------------------------------
                });
            };

            RewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward();
                });
            };
            
            RewardedAd.OnAdLoaded += (info) => EnqueueAction(() => Debug.Log($"[AdsManager] Rewarded Ad Loaded: {info}"));
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
            if (this != null && !RewardedAd.IsAdReady())
            {
                EnqueueAction(LoadRewarded);
            }
        }

        public void LoadRewarded()
        {
            if (!isInitialized) 
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            RewardedAd.LoadAd();
        }

        public void ShowRewarded()
        {
            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad (GameReward).");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
            {
                Debug.LogWarning("[AdsManager] Rewarded Ad is not ready. Falling back to Interstitial.");
                pendingRewardType = RewardAdType.GameReward;
                OnAdOpened?.Invoke();
                interstitialAd.ShowAd(); // Direct ShowAd skips cooldowns & No Ads checks
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
        private void ProcessPendingReward()
        {
            // Capture and immediately reset so duplicate callbacks are no-ops
            RewardAdType rewardType = pendingRewardType;
            pendingRewardType = RewardAdType.None;

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
                    SpawnCoinsSmallExplosion();

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
            if (coinsRewardedAd != null) coinsRewardedAd.DestroyAd();
            coinsRewardedAd = new LevelPlayRewardedAd(CoinsRewardedAdUnitId);

            coinsRewardedAd.OnAdClosed += (info) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Coins Rewarded Ad Closed. Requesting next.");
                    lastAdShowTime = Time.time;
                    OnAdClosed?.Invoke();
                    LoadCoinsRewarded();
                });
            };

            coinsRewardedAd.OnAdDisplayFailed += (info, err) => {
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Coins Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    OnAdClosed?.Invoke();
                    LoadCoinsRewarded();
                });
            };

            coinsRewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Coins Rewarded Ad Displayed: {info}");

                    // --- Analytics: ad_impression (ILRD) ---
                    if (FirebaseManager.Instance != null)
                    {
                        FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, info.AdNetwork),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, info.AdUnitName),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, "rewarded_coins"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, info.Revenue ?? 0),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
                    }

                    // --- Singular: Ad Revenue tracking ---
                    SingularAdData singularAdData = new SingularAdData("ironSource", "USD", info.Revenue ?? 0);
                    singularAdData.WithNetworkName(info.AdNetwork)
                                  .WithAdUnitName(info.AdUnitName)
                                  .WithAdType("rewarded_coins");
                    SingularSDK.AdRevenue(singularAdData);
                    // -------------------------------------
                    // --------------------------------
                });
            };

            coinsRewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Coins Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward();
                });
            };

            coinsRewardedAd.OnAdLoaded += (info) => EnqueueAction(() => Debug.Log($"[AdsManager] Coins Rewarded Ad Loaded: {info}"));
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
            if (this != null && coinsRewardedAd != null && !coinsRewardedAd.IsAdReady())
            {
                EnqueueAction(LoadCoinsRewarded);
            }
        }

        public void LoadCoinsRewarded()
        {
            if (!isInitialized || coinsRewardedAd == null) 
            {
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                return;
            }
            coinsRewardedAd.LoadAd();
        }

        public void ShowRewardedForCoins()
        {
            if (coinsRewardedAd != null && coinsRewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Coins Rewarded Ad (CoinsReward).");
                pendingRewardType = RewardAdType.CoinsReward;
                OnAdOpened?.Invoke();
                coinsRewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
                    Debug.Log("[AdsManager] Multiply Rewarded Ad Closed. Requesting next.");
                    lastAdShowTime = Time.time;
                    OnAdClosed?.Invoke();
                    LoadMultiplyRewarded();
                });
            };

            multiplyRewardedAd.OnAdDisplayFailed += (info, err) => {
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Multiply Rewarded Ad Display Failed: {err}. ");
                    pendingRewardType = RewardAdType.None;
                    OnAdClosed?.Invoke();
                    LoadMultiplyRewarded();
                });
            };

            multiplyRewardedAd.OnAdDisplayed += (info) => {
                EnqueueAction(() => {
                    Debug.Log($"[AdsManager] Multiply Rewarded Ad Displayed: {info}");

                    // --- Analytics: ad_impression (ILRD) ---
                    if (FirebaseManager.Instance != null)
                    {
                        FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, info.AdNetwork),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, info.AdUnitName),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, "rewarded_multiply"),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, info.Revenue ?? 0),
                            new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
                    }

                    // --- Singular: Ad Revenue tracking ---
                    SingularAdData singularAdData = new SingularAdData("ironSource", "USD", info.Revenue ?? 0);
                    singularAdData.WithNetworkName(info.AdNetwork)
                                  .WithAdUnitName(info.AdUnitName)
                                  .WithAdType("rewarded_multiply");
                    SingularSDK.AdRevenue(singularAdData);
                    // -------------------------------------
                });
            };

            multiplyRewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Multiply Rewarded Ad Rewarded Event Received.");
                    ProcessPendingReward();
                });
            };

            multiplyRewardedAd.OnAdLoaded += (info) => EnqueueAction(() => Debug.Log($"[AdsManager] Multiply Rewarded Ad Loaded: {info}"));
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
            if (this != null && multiplyRewardedAd != null && !multiplyRewardedAd.IsAdReady())
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
            if (multiplyRewardedAd != null && multiplyRewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Multiply Rewarded Ad (MultiplyReward).");
                pendingRewardType = RewardAdType.MultiplyReward;
                OnAdOpened?.Invoke();
                multiplyRewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Hint (HintReward).");
                pendingRewardType = RewardAdType.HintReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for PlayOn (PlayOnReward).");
                pendingRewardType = RewardAdType.PlayOnReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Magic (MagicReward).");
                pendingRewardType = RewardAdType.MagicReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad for Life (LifeReward).");
                pendingRewardType = RewardAdType.LifeReward;
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else if (interstitialAd != null && interstitialAd.IsAdReady())
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
            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < 11) return;

            GameObject prefab = Resources.Load<GameObject>("CoinsSmallExplosion");
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

                // --- Analytics: ad_impression (ILRD) ---
                if (FirebaseManager.Instance != null)
                {
                    FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "ironSource"),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, info.AdNetwork),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, info.AdUnitName),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, "banner"),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, info.Revenue ?? 0),
                        new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
                }
                // --------------------------------
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
            if (interstitialAd != null) interstitialAd.DestroyAd();
            if (RewardedAd != null) RewardedAd.DestroyAd();
            if (coinsRewardedAd != null) coinsRewardedAd.DestroyAd();
            if (multiplyRewardedAd != null) multiplyRewardedAd.DestroyAd();
            if (settingsBannerAd != null) settingsBannerAd.DestroyAd();
        }
    }
}
