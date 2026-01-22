using Unity.Services.LevelPlay;
using UnityEngine;
using System;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private LevelPlayInterstitialAd interstitialAd;
        private LevelPlayRewardedAd playOnRewardedAd;
        private LevelPlayRewardedAd hintRewardedAd;
        private bool isInitialized = false;

        public enum AdRewardType { None, PlayOn, Hint }
        public AdRewardType m_CurrentRequestType = AdRewardType.PlayOn;
        
        public event Action OnRewardReceived;

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
                return "if9z8hp6gm6ukwvh"; // play_on_rewarded
#elif UNITY_IPHONE
                return "if9z8hp6gm6ukwvh"; // play_on_rewarded
#else
                return "unexpected_platform";
#endif
            }
        }

        private string HintAdUnitId
        {
            get
            {
#if UNITY_ANDROID || UNITY_EDITOR
                return "yawx693hhwww7my2"; // hint_rewarded
#elif UNITY_IPHONE
                return "yawx693hhwww7my2"; // hint_rewarded
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
            CreatePlayOnRewardedAd();
            CreateHintRewardedAd();
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

        // --- PlayOn Rewarded Ad ---
        private void CreatePlayOnRewardedAd()
        {
            if (playOnRewardedAd != null) playOnRewardedAd.DestroyAd();
            playOnRewardedAd = new LevelPlayRewardedAd(RewardedAdUnitId);
            playOnRewardedAd.OnAdClosed += (info) => { 
                Debug.Log($"[AdsManager] PlayOn Ad Closed. Request type was: {m_CurrentRequestType}");
                LoadPlayOnRewarded(); 
            };
            playOnRewardedAd.OnAdDisplayFailed += (info, err) => { 
                Debug.LogError($"[AdsManager] PlayOn Ad Display Failed: {err}. Request type was: {m_CurrentRequestType}");
                LoadPlayOnRewarded(); 
            };

            if(m_CurrentRequestType == AdRewardType.Hint)
            {
                CreateHintRewardedAd();
            }
            playOnRewardedAd.OnAdRewarded += (info, reward) => {
                Debug.Log($"[AdsManager] PlayOn Ad Rewarded Event Received. Current request: {m_CurrentRequestType}");
                if (m_CurrentRequestType == AdRewardType.PlayOn)
                {
                    Debug.Log("[AdsManager] Granting PlayOn reward event.");
                    OnRewardReceived?.Invoke();
                }
            };
            LoadPlayOnRewarded();
        }

        public void LoadPlayOnRewarded()
        {
            if (!isInitialized) return;
            playOnRewardedAd.LoadAd();
        }

        public void ShowPlayOnRewarded()
        {
            if (playOnRewardedAd != null && playOnRewardedAd.IsAdReady())
            {
                m_CurrentRequestType = AdRewardType.PlayOn;
                playOnRewardedAd.ShowAd();
            }
            else 
            {
                Debug.LogWarning("[AdsManager] PlayOn Ad is not ready yet. Loading one now.");
                LoadPlayOnRewarded();
            }
        }

        // --- Hint Rewarded Ad ---
        private void CreateHintRewardedAd()
        {
            if (hintRewardedAd != null) hintRewardedAd.DestroyAd();
            hintRewardedAd = new LevelPlayRewardedAd(HintAdUnitId);
            hintRewardedAd.OnAdClosed += (info) => { 
                Debug.Log($"[AdsManager] Hint Ad Closed. Request type was: {m_CurrentRequestType}");
                LoadHintRewarded(); 
            };
            hintRewardedAd.OnAdDisplayFailed += (info, err) => { 
                Debug.LogError($"[AdsManager] Hint Ad Display Failed: {err}. Request type was: {m_CurrentRequestType}");
                LoadHintRewarded(); 
            };
            if(m_CurrentRequestType == AdRewardType.PlayOn)
            {
                CreatePlayOnRewardedAd();
            }
            hintRewardedAd.OnAdRewarded += (info, reward) => {
                Debug.Log($"[AdsManager] Hint Ad Rewarded Event Received. Current request: {m_CurrentRequestType}");
                if (m_CurrentRequestType == AdRewardType.Hint)
                {
                    Debug.Log("[AdsManager] Granting Hint reward event.");
                    OnRewardReceived?.Invoke(); 
                }
            };
            LoadHintRewarded();
        }

        public void LoadHintRewarded()
        {
            if (!isInitialized) return;
            hintRewardedAd.LoadAd();
        }

        public void ShowHintRewarded()
        {
            if (hintRewardedAd != null && hintRewardedAd.IsAdReady())
            {
                m_CurrentRequestType = AdRewardType.Hint;
                hintRewardedAd.ShowAd();
            }
            else 
            {
                Debug.LogWarning("[AdsManager] Hint Ad is not ready yet. Loading one now.");
                LoadHintRewarded();
            }
        }

        private void OnDestroy()
        {
            if (interstitialAd != null) interstitialAd.DestroyAd();
            if (playOnRewardedAd != null) playOnRewardedAd.DestroyAd();
            if (hintRewardedAd != null) hintRewardedAd.DestroyAd();
        }
    }
}
