using Unity.Services.LevelPlay;
using Unity.Services.Core;
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd RewardedAd;
        private bool isInitialized = false;
        private float lastAdShowTime = -60f;
        private const float AD_COOLDOWN = 60f;

        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

        public event Action OnRewardReceived;
        public event Action OnAdOpened;
        public event Action OnAdClosed;

        private string AppKey
        {
            get
            {
#if UNITY_ANDROID
                return "6027951"; // Provided Android Game ID
#elif UNITY_IPHONE
                return "6027950"; // Provided Apple Game ID
#else
                return "unexpected_platform";
#endif
            }
        }

        private string InterstitialAdUnitId
        {
            get
            {
#if UNITY_ANDROID
                return "dctkavzgndg9gm8m"; // back_to_lobby_interstital
#elif UNITY_IPHONE
                return "dctkavzgndg9gm8m"; // back_to_lobby_interstital
#else
                return "unexpected_platform";
#endif
            }
        }

        private string RewardedAdUnitId
        {
            get
            {
#if UNITY_ANDROID || UNITY_EDITOR
                return "if9z8hp6gm6ukwvh"; // Android ad_rewarded
#elif UNITY_IPHONE
                return "if9z8hp6gm6ukwvh"; // iOS ad_rewarded
#else
                return "unexpected_platform";
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
            if (isInitialized) return;

            try
            {
                Debug.Log("[AdsManager] Initializing Unity Services...");
                await UnityServices.InitializeAsync();
                
                // Request ATT for iOS mandatory check
                IOSAdsHelper.RequestATT();

                string currentAppKey = AppKey;
                Debug.Log($"[AdsManager] Initializing LevelPlay SDK with AppKey: {currentAppKey}...");
                
                // Validation check for common confusion between Unity Game ID and ironSource App Key
                if (currentAppKey.Length <= 7 && int.TryParse(currentAppKey, out _))
                {
                    Debug.LogWarning("[AdsManager] WARNING: The provided AppKey looks like a Unity Game ID. LevelPlay requires an ironSource App Key (typically 8-10 characters). If ads don't load, please verify this in the ironSource dashboard.");
                }

                LevelPlay.OnInitSuccess += OnSdkInitSuccess;
                LevelPlay.OnInitFailed += OnSdkInitFailed;
                
                LevelPlay.Init(currentAppKey);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdsManager] Unity Services Initialization Failed: {e.Message}\n{e.StackTrace}");
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
                CreateInterstitialAd();
                CreateRewardedAd();
            });
        }

        private void OnSdkInitFailed(LevelPlayInitError error)
        {
            EnqueueAction(() => {
                Debug.LogError($"[AdsManager] LevelPlay SDK Initialization Failed: {error}");
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
                lastAdShowTime = Time.time;
            };
            interstitialAd.OnAdClicked += (info) => Debug.Log($"[AdsManager] Interstitial Ad Clicked: {info}");

            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            if (!isInitialized) return;
            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: User has No Ads.");
                return;
            }
            Debug.Log("[AdsManager] Loading Interstitial Ad...");
            interstitialAd.LoadAd();
        }

        public void ShowInterstitial(bool isAuto = false)
        {
            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: User has No Ads.");
                return;
            }

            if (isAuto)
            {
                float timeSinceLastAd = Time.time - lastAdShowTime;
                if (timeSinceLastAd < AD_COOLDOWN)
                {
                    Debug.Log($"[AdsManager] Skipping Auto Interstitial due to cooldown. Last ad was {timeSinceLastAd:F1}s ago.");
                    return;
                }
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
                LoadInterstitial();
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
                Debug.LogWarning($"[AdsManager] Interstitial Ad Load Failed: {error}");
            });
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            EnqueueAction(() => {
                Debug.Log("[AdsManager] Interstitial Ad Closed. Loading next one.");
                OnAdClosed?.Invoke();
                LoadInterstitial();
            });
        }

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            EnqueueAction(() => {
                Debug.LogError($"[AdsManager] Interstitial Ad Display Failed: {error}");
                OnAdClosed?.Invoke();
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
                    OnAdClosed?.Invoke();
                    LoadRewarded(); 
                });
            };
            
            RewardedAd.OnAdDisplayFailed += (info, err) => { 
                EnqueueAction(() => {
                    Debug.LogError($"[AdsManager] Rewarded Ad Display Failed: {err}. ");
                    OnAdClosed?.Invoke();
                    LoadRewarded(); 
                });
            };
            
            RewardedAd.OnAdDisplayed += (info) => {
                lastAdShowTime = Time.time;
                EnqueueAction(() => Debug.Log($"[AdsManager] Rewarded Ad Displayed: {info}"));
            };

            RewardedAd.OnAdRewarded += (info, reward) => {
                EnqueueAction(() => {
                    Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received. ");
                    OnRewardReceived?.Invoke();
                });
            };
            
            RewardedAd.OnAdLoaded += (info) => EnqueueAction(() => Debug.Log($"[AdsManager] Rewarded Ad Loaded: {info}"));
            RewardedAd.OnAdLoadFailed += (info) => EnqueueAction(() => Debug.LogWarning($"[AdsManager] Rewarded Ad Load Failed: {info}"));

            LoadRewarded();
        }

        public void LoadRewarded()
        {
            if (!isInitialized) return;
            RewardedAd.LoadAd();
        }

        public void ShowRewarded()
        {
            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Auto-rewarding: User has No Ads.");
                OnRewardReceived?.Invoke();
                return;
            }

            if (RewardedAd != null && RewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad.");
                OnAdOpened?.Invoke();
                RewardedAd.ShowAd();
            }
            else 
            {
                Debug.LogWarning($"[AdsManager] Rewarded Ad is not ready. Initialized: {isInitialized}");
                LoadRewarded();
            }
        }

        private void OnDestroy()
        {
            if (interstitialAd != null) interstitialAd.DestroyAd();
            if (RewardedAd != null) RewardedAd.DestroyAd();
        }
    }
}
