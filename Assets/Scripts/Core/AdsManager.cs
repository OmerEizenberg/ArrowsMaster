using Unity.Services.LevelPlay;
using UnityEngine;
using System;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private LevelPlayInterstitialAd interstitialAd;
        private bool isInitialized = false;

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

        private void OnDestroy()
        {
            if (interstitialAd != null)
            {
                interstitialAd.DestroyAd();
            }
        }
    }
}
