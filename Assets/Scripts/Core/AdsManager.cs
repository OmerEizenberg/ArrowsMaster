using Unity.Services.LevelPlay;
using UnityEngine;
using System;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd rewardedAd;
        private bool isInitialized = false;
        
        public event Action OnRewardGranted;

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
#if UNITY_ANDROID
                return "if9z8hp6gm6ukwvh"; // play_on_rewarded
#elif UNITY_IPHONE
                return "if9z8hp6gm6ukwvh"; // play_on_rewarded
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
            
            InitializeSDK();
        }

        private void InitializeSDK()
        {
            if (isInitialized) return;

            Debug.Log("[AdsManager] Initializing LevelPlay SDK...");
            LevelPlay.OnInitSuccess += OnSdkInitSuccess;
            LevelPlay.OnInitFailed += OnSdkInitFailed;
            LevelPlay.Init(AppKey);
        }

        private void OnSdkInitSuccess(LevelPlayConfiguration config)
        {
            Debug.Log("[AdsManager] LevelPlay SDK Initialized Successfully.");
            isInitialized = true;
            CreateInterstitialAd();
            CreateRewardedAd();
        }

        private void OnSdkInitFailed(LevelPlayInitError error)
        {
            Debug.LogError($"[AdsManager] LevelPlay SDK Initialization Failed: {error}");
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

            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            if (!isInitialized) return;
            Debug.Log("[AdsManager] Loading Interstitial Ad...");
            interstitialAd.LoadAd();
        }

        public void ShowInterstitial()
        {
            if (interstitialAd != null && interstitialAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Interstitial Ad.");
                interstitialAd.ShowAd();
            }
            else
            {
                Debug.Log("[AdsManager] Interstitial Ad is not ready. Loading one now.");
                LoadInterstitial();
            }
        }

        private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
        {
            Debug.Log($"[AdsManager] Interstitial Ad Loaded: {adInfo}");
        }

        private void OnInterstitialLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"[AdsManager] Interstitial Ad Load Failed: {error}");
        }

        private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Closed. Loading next one.");
            LoadInterstitial();
        }

        private void OnInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[AdsManager] Interstitial Ad Display Failed: {error}");
            LoadInterstitial();
        }

        private void CreateRewardedAd()
        {
            if (rewardedAd != null)
            {
                rewardedAd.DestroyAd();
            }

            rewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            
            rewardedAd.OnAdLoaded += OnRewardedLoaded;
            rewardedAd.OnAdLoadFailed += OnRewardedLoadFailed;
            rewardedAd.OnAdClosed += OnRewardedClosed;
            rewardedAd.OnAdDisplayFailed += OnRewardedDisplayFailed;
            rewardedAd.OnAdRewarded += OnRewardedAdRewarded;

            LoadRewarded();
        }

        public void LoadRewarded()
        {
            if (!isInitialized) return;
            Debug.Log("[AdsManager] Loading Rewarded Ad...");
            rewardedAd.LoadAd();
        }

        public void ShowRewarded()
        {
            if (rewardedAd != null && rewardedAd.IsAdReady())
            {
                Debug.Log("[AdsManager] Showing Rewarded Ad.");
                rewardedAd.ShowAd();
            }
            else
            {
                Debug.Log("[AdsManager] Rewarded Ad is not ready. Loading one now.");
                LoadRewarded();
            }
        }

        private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
        {
            Debug.Log($"[AdsManager] Rewarded Ad Loaded: {adInfo}");
        }

        private void OnRewardedLoadFailed(LevelPlayAdError error)
        {
            Debug.LogWarning($"[AdsManager] Rewarded Ad Load Failed: {error}");
        }

        private void OnRewardedClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Closed. Loading next one.");
            LoadRewarded();
        }

        private void OnRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"[AdsManager] Rewarded Ad Display Failed: {error}");
            LoadRewarded();
        }

        private void OnRewardedAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            Debug.Log("[AdsManager] Reward Granted!");
            OnRewardGranted?.Invoke();
        }

        private void OnDestroy()
        {
            if (interstitialAd != null)
            {
                interstitialAd.DestroyAd();
            }
            if (rewardedAd != null)
            {
                rewardedAd.DestroyAd();
            }
        }
    }
}
