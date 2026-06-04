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
    public const string KEY_AD_COOLDOWN = "adCooldown";
    public const string KEY_ALL_LEVELS_TIMER = "AllLevelsTimer";
    public const string KEY_PTS_MUL = "PTS_Mul";
    public const string KEY_IS_POST_WIN_LEVEL_CHOICE_ENABLED = "isPostWinLevelChoiceEnabled";
    public const string KEY_ONE_LIFE_PLAY_ON = "OneLifePlayOn";
    public const string KEY_IS_SHUFFLE_ON = "isShuffleOn";
    public const string KEY_NETFLIX_EFFECT = "NetflixEffect";

    private readonly Dictionary<string, object> defaults = new Dictionary<string, object>();

    private bool isConfigReady = false;
    private bool isFirebaseNativeReady = false;

    public bool IsConfigReady => isConfigReady;
    public bool IsFirebaseNativeReady => isFirebaseNativeReady;


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
        BuildDefaultValues();
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

    private void BuildDefaultValues()
    {
        defaults.Clear();
        defaults[KEY_FORCE_UPDATE_VERSION_ANDROID] = "1.0.0";
        defaults[KEY_FORCE_UPDATE_VERSION_IOS] = "1.0.0";
        defaults[KEY_SOFT_UPDATE_VERSION_ANDROID] = "1.0.0";
        defaults[KEY_SOFT_UPDATE_VERSION_IOS] = "1.0.0";
        defaults[KEY_FIRST_PLAY_ON] = 1600L;
        defaults[KEY_SEC_PLAY_ON] = 3200L;
        defaults[KEY_THIRD_PLAY_ON] = 4200L;
        defaults[KEY_COINS_REWARDED_AD] = 2000L;
        defaults[KEY_REWARDED_AD_COINS_COOLDOWN] = 240L;
        defaults[KEY_SHARE_TEXT] = "Check out Arrows Legend! Can you beat my level?";
        defaults[KEY_SHARE_URL] = "https://play.google.com/store/apps/details?id=com.Arrows.Master";
        defaults[KEY_IS_INTERSTITIAL_ACTIVE] = true;
        defaults[KEY_IS_DYNAMIC_MAX_ZOOM] = true;
        defaults[KEY_AD_COOLDOWN] = 60L;
        defaults[KEY_ALL_LEVELS_TIMER] = false;
        defaults[KEY_PTS_MUL] = 0.28d;
        defaults[KEY_IS_POST_WIN_LEVEL_CHOICE_ENABLED] = true;
        defaults[KEY_ONE_LIFE_PLAY_ON] = false;
        defaults[KEY_IS_SHUFFLE_ON] = true;
        defaults[KEY_NETFLIX_EFFECT] = true;
    }

    /// <summary>
    /// Use baked-in defaults when Firebase native libs are missing (common in Editor on macOS).
    /// </summary>
    public void ApplyDefaultsOnly(string reason = null)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning("[RemoteConfigManager] Using baked-in defaults. " + reason);
        }

        isFirebaseNativeReady = false;
        isConfigReady = true;
        ApplyUserDataFromDefaults();
        OnConfigInitialized?.Invoke();
    }

    public void Initialize()
    {
        Debug.Log("[RemoteConfigManager] Initializing Remote Config...");

        try
        {
            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    ApplyDefaultsOnly(task.Exception?.GetBaseException()?.Message);
                    return;
                }

                isFirebaseNativeReady = true;
                FetchData();
            });
        }
        catch (Exception e)
        {
            ApplyDefaultsOnly(e.Message);
        }
    }

    public void FetchData()
    {
        if (!isFirebaseNativeReady)
        {
            ApplyDefaultsOnly("Firebase native layer unavailable.");
            return;
        }

        Debug.Log("[RemoteConfigManager] Fetching Remote Config...");

        try
        {
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
                    isConfigReady = true;
                    ApplyUserDataFromDefaults();
                    OnConfigInitialized?.Invoke();
                }
            });
        }
        catch (Exception e)
        {
            ApplyDefaultsOnly(e.Message);
        }
    }

    private void ActivateConfig()
    {
        if (!isFirebaseNativeReady)
        {
            ApplyDefaultsOnly("Firebase native layer unavailable.");
            return;
        }

        try
        {
            FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(activateTask =>
            {
                if (activateTask.IsCompleted && !activateTask.IsFaulted)
                {
                    Debug.Log($"[RemoteConfigManager] Activate completed. Result: {activateTask.Result}");
                    ApplyUserDataFromRemoteOrDefaults();
                }
                else
                {
                    Debug.LogError($"[RemoteConfigManager] Activate failed: {activateTask.Exception}");
                    ApplyUserDataFromDefaults();
                }

                isConfigReady = true;
                OnConfigInitialized?.Invoke();
            });
        }
        catch (Exception e)
        {
            ApplyDefaultsOnly(e.Message);
        }
    }

    private void ApplyUserDataFromDefaults()
    {
        if (Assets.Scripts.Core.UserDataManager.Instance == null) return;

        Assets.Scripts.Core.UserDataManager.Instance.IsInterstitialActive = GetBool(KEY_IS_INTERSTITIAL_ACTIVE);
        Assets.Scripts.Core.UserDataManager.Instance.IsDynamicMaxZoom = GetBool(KEY_IS_DYNAMIC_MAX_ZOOM);
    }

    private void ApplyUserDataFromRemoteOrDefaults()
    {
        ApplyUserDataFromDefaults();
    }

    #region Accessors

    public string GetString(string key)
    {
        if (!isFirebaseNativeReady)
        {
            return defaults.TryGetValue(key, out object value) ? Convert.ToString(value) : string.Empty;
        }

        try
        {
            return FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] GetString fallback for '{key}': {e.Message}");
            return defaults.TryGetValue(key, out object value) ? Convert.ToString(value) : string.Empty;
        }
    }

    public bool GetBool(string key)
    {
        if (!isFirebaseNativeReady)
        {
            return defaults.TryGetValue(key, out object value) && Convert.ToBoolean(value);
        }

        try
        {
            return FirebaseRemoteConfig.DefaultInstance.GetValue(key).BooleanValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] GetBool fallback for '{key}': {e.Message}");
            return defaults.TryGetValue(key, out object value) && Convert.ToBoolean(value);
        }
    }

    public long GetLong(string key)
    {
        if (!isFirebaseNativeReady)
        {
            return defaults.TryGetValue(key, out object value) ? Convert.ToInt64(value) : 0L;
        }

        try
        {
            return FirebaseRemoteConfig.DefaultInstance.GetValue(key).LongValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] GetLong fallback for '{key}': {e.Message}");
            return defaults.TryGetValue(key, out object value) ? Convert.ToInt64(value) : 0L;
        }
    }

    public double GetDouble(string key)
    {
        if (!isFirebaseNativeReady)
        {
            return defaults.TryGetValue(key, out object value) ? Convert.ToDouble(value) : 0.0;
        }

        try
        {
            return FirebaseRemoteConfig.DefaultInstance.GetValue(key).DoubleValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] GetDouble fallback for '{key}': {e.Message}");
            return defaults.TryGetValue(key, out object value) ? Convert.ToDouble(value) : 0.0;
        }
    }

    public string ForceUpdateVersionAndroid => GetString(KEY_FORCE_UPDATE_VERSION_ANDROID);
    public string ForceUpdateVersionIOS => GetString(KEY_FORCE_UPDATE_VERSION_IOS);
    public string SoftUpdateVersionAndroid => GetString(KEY_SOFT_UPDATE_VERSION_ANDROID);
    public string SoftUpdateVersionIOS => GetString(KEY_SOFT_UPDATE_VERSION_IOS);
    public string ShareText => GetString(KEY_SHARE_TEXT);
    public string ShareUrl => GetString(KEY_SHARE_URL);
    public bool IsInterstitialActive => GetBool(KEY_IS_INTERSTITIAL_ACTIVE);
    public bool IsDynamicMaxZoom => GetBool(KEY_IS_DYNAMIC_MAX_ZOOM);

    public int FirstPlayOn => (int)GetLong(KEY_FIRST_PLAY_ON);
    public int SecPlayOn => (int)GetLong(KEY_SEC_PLAY_ON);
    public int ThirdPlayOn => (int)GetLong(KEY_THIRD_PLAY_ON);
    public int CoinsRewardedAd => (int)GetLong(KEY_COINS_REWARDED_AD);
    public int RewardedAdCoinsCooldown => (int)GetLong(KEY_REWARDED_AD_COINS_COOLDOWN);
    public int AdCooldown => (int)GetLong(KEY_AD_COOLDOWN);
    public bool AllLevelsTimer => GetBool(KEY_ALL_LEVELS_TIMER);
    public bool IsPostWinLevelChoiceEnabled => GetBool(KEY_IS_POST_WIN_LEVEL_CHOICE_ENABLED);
    public bool OneLifePlayOn => GetBool(KEY_ONE_LIFE_PLAY_ON);
    public bool IsShuffleOn => GetBool(KEY_IS_SHUFFLE_ON);
    public bool IsNetflixEffectEnabled => GetBool(KEY_NETFLIX_EFFECT);
    public float PtsMul => (float)GetDouble(KEY_PTS_MUL);

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
