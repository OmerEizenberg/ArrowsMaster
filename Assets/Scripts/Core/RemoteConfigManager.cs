using Firebase.RemoteConfig;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class RemoteConfigManager : MonoBehaviour
{
    public static RemoteConfigManager Instance { get; private set; }

    // Remote Config Keys
    public const string KEY_FORCE_UPDATE_VERSION_ANDROID = "ForceUpdateVersionAndroid";
    public const string KEY_FORCE_UPDATE_VERSION_IOS = "ForceUpdateVersioniOS";
    public const string KEY_SOFT_UPDATE_VERSION_ANDROID = "SoftUpdateVersionAndroid";
    public const string KEY_SOFT_UPDATE_VERSION_IOS = "SoftUpdateVersioniOS";
    public const string KEY_FIRST_PLAY_ON = "FirstPlayOn";
    public const string KEY_SEC_PLAY_ON = "SecPlayOn";
    public const string KEY_THIRD_PLAY_ON = "ThirdPlayOn";
    public const string KEY_COINS_REWARDED_AD = "CoinsRewardedAd";
    public const string KEY_REWARDED_AD_COINS_COOLDOWN = "RewardedAdCoinsCooldown";
    public const string KEY_SHARE_TEXT = "ShareText";
    public const string KEY_SHARE_URL = "ShareUrl";
    public const string KEY_IS_INTERSTITIAL_ACTIVE = "isInterstitialActive";
    public const string KEY_IS_DYNAMIC_MAX_ZOOM = "isDynamicMaxZoom";



    private bool isConfigReady = false;
    public bool IsConfigReady => isConfigReady;

    public event Action OnConfigInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("RemoteConfigManager");
            go.AddComponent<RemoteConfigManager>();
        }
    }

    public void Initialize()
    {
        Debug.Log("[RemoteConfigManager] Initializing Remote Config...");

        // Set default values
        Dictionary<string, object> defaults = new Dictionary<string, object>
        {
            { KEY_FORCE_UPDATE_VERSION_ANDROID, "1.0.0" },
            { KEY_FORCE_UPDATE_VERSION_IOS, "1.0.0" },
            { KEY_SOFT_UPDATE_VERSION_ANDROID, "1.0.0" },
            { KEY_SOFT_UPDATE_VERSION_IOS, "1.0.0" },
            { KEY_FIRST_PLAY_ON, 1600 },
            { KEY_SEC_PLAY_ON, 3200 },
            { KEY_THIRD_PLAY_ON, 4200 },
            { KEY_COINS_REWARDED_AD, 2000 },
            { KEY_REWARDED_AD_COINS_COOLDOWN, 240 },
            { KEY_SHARE_TEXT, "Check out Arrows Legend! Can you beat my level?" },
            { KEY_SHARE_URL, "https://play.google.com/store/apps/details?id=com.Arrows.Master" },
            { KEY_IS_INTERSTITIAL_ACTIVE, true },
            { KEY_IS_DYNAMIC_MAX_ZOOM, true }
        };



        FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
        {
            FetchData();
        });
    }

    public void FetchData()
    {
        Debug.Log("[RemoteConfigManager] Fetching Remote Config...");
        
        // FetchAsync(TimeSpan.Zero) ensures we get the latest values without waiting for the default cache expiration (12 hours)
        // Note: For production, you might want to use a longer cache time to avoid throttling.
        FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero).ContinueWithOnMainThread(fetchTask =>
        {
            if (fetchTask.IsCompleted && !fetchTask.IsFaulted)
            {
                Debug.Log("[RemoteConfigManager] Fetch completed successfully.");
                ActivateConfig();
            }
            else
            {
                Debug.LogError($"[RemoteConfigManager] Fetch failed: {fetchTask.Exception}");
                // Even if fetch fails, we can try to activate what we have (cached or defaults)
                isConfigReady = true;
                OnConfigInitialized?.Invoke();
            }
        });
    }

    private void ActivateConfig()
    {
        FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(activateTask =>
        {
            if (activateTask.IsCompleted && !activateTask.IsFaulted)
            {
                Debug.Log($"[RemoteConfigManager] Activate completed. Result: {activateTask.Result}");
                
                // Update UserDataManager with the latest value
                Assets.Scripts.Core.UserDataManager.Instance.IsInterstitialActive = IsInterstitialActive;
                Assets.Scripts.Core.UserDataManager.Instance.IsDynamicMaxZoom = IsDynamicMaxZoom;
            }


            else
            {
                Debug.LogError($"[RemoteConfigManager] Activate failed: {activateTask.Exception}");
            }

            isConfigReady = true;
            OnConfigInitialized?.Invoke();
        });
    }

    #region Accessors

    public string GetString(string key)
    {
        return FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
    }

    public string ForceUpdateVersionAndroid => GetString(KEY_FORCE_UPDATE_VERSION_ANDROID);
    public string ForceUpdateVersionIOS => GetString(KEY_FORCE_UPDATE_VERSION_IOS);
    public string SoftUpdateVersionAndroid => GetString(KEY_SOFT_UPDATE_VERSION_ANDROID);
    public string SoftUpdateVersionIOS => GetString(KEY_SOFT_UPDATE_VERSION_IOS);
    public string ShareText => GetString(KEY_SHARE_TEXT);
    public string ShareUrl => GetString(KEY_SHARE_URL);
    public bool IsInterstitialActive => FirebaseRemoteConfig.DefaultInstance.GetValue(KEY_IS_INTERSTITIAL_ACTIVE).BooleanValue;
    public bool IsDynamicMaxZoom => FirebaseRemoteConfig.DefaultInstance.GetValue(KEY_IS_DYNAMIC_MAX_ZOOM).BooleanValue;



    public long GetLong(string key)
    {
        return FirebaseRemoteConfig.DefaultInstance.GetValue(key).LongValue;
    }

    public int FirstPlayOn => (int)GetLong(KEY_FIRST_PLAY_ON);
    public int SecPlayOn => (int)GetLong(KEY_SEC_PLAY_ON);
    public int ThirdPlayOn => (int)GetLong(KEY_THIRD_PLAY_ON);
    public int CoinsRewardedAd => (int)GetLong(KEY_COINS_REWARDED_AD);
    public int RewardedAdCoinsCooldown => (int)GetLong(KEY_REWARDED_AD_COINS_COOLDOWN);

    #endregion

    #region Helpers for other platforms

    public string CurrentForceUpdateVersion
    {
        get
        {
            #if UNITY_ANDROID
            return ForceUpdateVersionAndroid;
            #elif UNITY_IOS
            return ForceUpdateVersionIOS;
            #else
            return "1.0.0";
            #endif
        }
    }

    public string CurrentSoftUpdateVersion
    {
        get
        {
            #if UNITY_ANDROID
            return SoftUpdateVersionAndroid;
            #elif UNITY_IOS
            return SoftUpdateVersionIOS;
            #else
            return "1.0.0";
            #endif
        }
    }

    #endregion
}
