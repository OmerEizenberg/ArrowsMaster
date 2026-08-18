using Firebase.RemoteConfig;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class RemoteConfigManager : MonoBehaviour
{
    public static RemoteConfigManager Instance { get; private set; }

    private const string PREF_KEY_HAS_CACHE = "RC_HasCache";
    private const string PREF_PREFIX = "RC_";
    private const string PREF_IND_PREFIX = "RC_IND_";
    private const string LEGACY_UPDATE_VERSION_DEFAULT = "1.0.0";

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
    public const string KEY_IS_GAE = "isGAE";
    public const string KEY_IS_OFFLINE = "isOffline";
    public const string KEY_TOURNAMENT_ON = "TournamentOn";
    public const string KEY_DIFFICULTY_CURVE = "DifficultyCurve";

    private readonly Dictionary<string, object> defaults = new Dictionary<string, object>();
    private readonly Dictionary<string, object> activeValues = new Dictionary<string, object>();

    private bool isConfigReady = false;
    private bool isFirebaseNativeReady = false;
    private bool configInitializedEventFired = false;

    public bool IsConfigReady => isConfigReady;
    public bool IsFirebaseNativeReady => isFirebaseNativeReady;

    public event Action OnConfigInitialized;
    public event Action OnConfigValuesUpdated;
    public event Action OnUpdateVersionsChanged;

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
        InitializeActiveValuesFromDefaults();
        MigrateLegacyUpdateVersionIndications();
        TryLoadCachedValuesFromPlayerPrefs();

        if (HasCachedConfig())
        {
            isConfigReady = true;
            ApplyUserDataFromActiveValues();
            FireConfigInitializedOnce();
            Debug.Log("[RemoteConfigManager] Loaded cached remote config from PlayerPrefs.");
        }
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
        defaults[KEY_FORCE_UPDATE_VERSION_ANDROID] = string.Empty;
        defaults[KEY_FORCE_UPDATE_VERSION_IOS] = string.Empty;
        defaults[KEY_SOFT_UPDATE_VERSION_ANDROID] = string.Empty;
        defaults[KEY_SOFT_UPDATE_VERSION_IOS] = string.Empty;
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
        defaults[KEY_NETFLIX_EFFECT] = false;
        defaults[KEY_IS_GAE] = true;
        defaults[KEY_IS_OFFLINE] = false;
        defaults[KEY_TOURNAMENT_ON] = true;
        defaults[KEY_DIFFICULTY_CURVE] = 1L;
    }

    private void InitializeActiveValuesFromDefaults()
    {
        activeValues.Clear();
        foreach (KeyValuePair<string, object> kvp in defaults)
        {
            activeValues[kvp.Key] = kvp.Value;
        }
    }

    private static string GetPrefKey(string key) => PREF_PREFIX + key;

    private static string GetIndicationPrefKey(string key) => PREF_IND_PREFIX + key;

    private static bool IsUpdateVersionKey(string key) =>
        key == KEY_FORCE_UPDATE_VERSION_ANDROID ||
        key == KEY_FORCE_UPDATE_VERSION_IOS ||
        key == KEY_SOFT_UPDATE_VERSION_ANDROID ||
        key == KEY_SOFT_UPDATE_VERSION_IOS;

    private bool HasUpdateVersionIndication(string key) =>
        PlayerPrefs.GetInt(GetIndicationPrefKey(key), 0) == 1;

    private void SetUpdateVersionIndication(string key, bool hasIndication)
    {
        PlayerPrefs.SetInt(GetIndicationPrefKey(key), hasIndication ? 1 : 0);
    }

    private void MigrateLegacyUpdateVersionIndications()
    {
        foreach (string key in GetUpdateVersionKeys())
        {
            if (HasUpdateVersionIndication(key))
            {
                continue;
            }

            string prefKey = GetPrefKey(key);
            if (!PlayerPrefs.HasKey(prefKey))
            {
                continue;
            }

            string cachedVersion = PlayerPrefs.GetString(prefKey, string.Empty);
            if (string.IsNullOrEmpty(cachedVersion) || cachedVersion == LEGACY_UPDATE_VERSION_DEFAULT)
            {
                continue;
            }

            SetUpdateVersionIndication(key, true);
        }
    }

    private static IEnumerable<string> GetUpdateVersionKeys()
    {
        yield return KEY_FORCE_UPDATE_VERSION_ANDROID;
        yield return KEY_FORCE_UPDATE_VERSION_IOS;
        yield return KEY_SOFT_UPDATE_VERSION_ANDROID;
        yield return KEY_SOFT_UPDATE_VERSION_IOS;
    }

    private bool HasCachedConfig() => PlayerPrefs.GetInt(PREF_KEY_HAS_CACHE, 0) == 1;

    private void TryLoadCachedValuesFromPlayerPrefs()
    {
        foreach (KeyValuePair<string, object> kvp in defaults)
        {
            if (IsUpdateVersionKey(kvp.Key) && !HasUpdateVersionIndication(kvp.Key))
            {
                continue;
            }

            string prefKey = GetPrefKey(kvp.Key);
            if (!PlayerPrefs.HasKey(prefKey))
            {
                continue;
            }

            if (TryReadValueFromPlayerPrefs(kvp.Key, kvp.Value, out object loaded))
            {
                activeValues[kvp.Key] = loaded;
            }
        }
    }

    private static bool TryReadValueFromPlayerPrefs(string key, object defaultValue, out object loaded)
    {
        loaded = null;
        string prefKey = GetPrefKey(key);

        try
        {
            switch (defaultValue)
            {
                case bool _:
                    loaded = PlayerPrefs.GetInt(prefKey, 0) == 1;
                    return true;
                case long _:
                    loaded = long.Parse(PlayerPrefs.GetString(prefKey, "0"), CultureInfo.InvariantCulture);
                    return true;
                case double _:
                    loaded = double.Parse(PlayerPrefs.GetString(prefKey, "0"), CultureInfo.InvariantCulture);
                    return true;
                case string _:
                    loaded = PlayerPrefs.GetString(prefKey, string.Empty);
                    return true;
                default:
                    Debug.LogWarning($"[RemoteConfigManager] Unsupported cached type for '{key}'.");
                    return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] Failed to read cached value for '{key}': {e.Message}");
            return false;
        }
    }

    private void SaveValueToPlayerPrefs(string key, object value)
    {
        string prefKey = GetPrefKey(key);

        switch (value)
        {
            case bool boolValue:
                PlayerPrefs.SetInt(prefKey, boolValue ? 1 : 0);
                break;
            case long longValue:
                PlayerPrefs.SetString(prefKey, longValue.ToString(CultureInfo.InvariantCulture));
                break;
            case double doubleValue:
                PlayerPrefs.SetString(prefKey, doubleValue.ToString(CultureInfo.InvariantCulture));
                break;
            case string stringValue:
                PlayerPrefs.SetString(prefKey, stringValue);
                break;
            default:
                Debug.LogWarning($"[RemoteConfigManager] Unsupported type when saving '{key}'.");
                break;
        }
    }

    private static bool ValuesEqual(object current, object incoming)
    {
        if (current == null && incoming == null) return true;
        if (current == null || incoming == null) return false;

        if (current is double currentDouble && incoming is double incomingDouble)
        {
            return Math.Abs(currentDouble - incomingDouble) < 0.000001d;
        }

        return current.Equals(incoming);
    }

    private object ReadRemoteValue(string key)
    {
        if (!defaults.TryGetValue(key, out object defaultValue))
        {
            return null;
        }

        try
        {
            ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
            switch (defaultValue)
            {
                case bool _:
                    return configValue.BooleanValue;
                case long _:
                    return configValue.LongValue;
                case double _:
                    return configValue.DoubleValue;
                case string _:
                    return configValue.StringValue;
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemoteConfigManager] Failed to read remote value for '{key}': {e.Message}");
            return null;
        }
    }

    private void SyncFromFirebase()
    {
        if (!isFirebaseNativeReady)
        {
            return;
        }

        int changedCount = 0;
        bool updateVersionsChanged = false;

        foreach (string key in defaults.Keys)
        {
            object remoteValue = ReadRemoteValue(key);
            if (remoteValue == null)
            {
                continue;
            }

            if (IsUpdateVersionKey(key))
            {
                string remoteVersion = Convert.ToString(remoteValue);
                if (string.IsNullOrEmpty(remoteVersion))
                {
                    continue;
                }
            }

            if (!activeValues.TryGetValue(key, out object currentValue) || !ValuesEqual(currentValue, remoteValue))
            {
                activeValues[key] = remoteValue;
                SaveValueToPlayerPrefs(key, remoteValue);

                if (IsUpdateVersionKey(key))
                {
                    SetUpdateVersionIndication(key, true);
                    updateVersionsChanged = true;
                }

                changedCount++;
                Debug.Log($"[RemoteConfigManager] Remote config updated: {key}");
            }
        }

        if (!HasCachedConfig() || changedCount > 0)
        {
            PlayerPrefs.SetInt(PREF_KEY_HAS_CACHE, 1);
            PlayerPrefs.Save();
        }

        if (changedCount > 0)
        {
            Debug.Log($"[RemoteConfigManager] Synced {changedCount} remote config value(s) to PlayerPrefs.");
            OnConfigValuesUpdated?.Invoke();
        }
        else
        {
            Debug.Log("[RemoteConfigManager] Remote config sync complete. No PlayerPrefs changes needed.");
        }

        if (updateVersionsChanged)
        {
            OnUpdateVersionsChanged?.Invoke();
        }
    }

    private void MarkConfigReadyForNewUserIfNeeded()
    {
        if (isConfigReady)
        {
            return;
        }

        isConfigReady = true;
        ApplyUserDataFromActiveValues();
        FireConfigInitializedOnce();
    }

    private void FireConfigInitializedOnce()
    {
        if (configInitializedEventFired)
        {
            return;
        }

        configInitializedEventFired = true;
        OnConfigInitialized?.Invoke();
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
        MarkConfigReadyForNewUserIfNeeded();
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
                    if (isConfigReady)
                    {
                        Debug.LogWarning($"[RemoteConfigManager] SetDefaults failed, keeping cached values. {task.Exception?.GetBaseException()?.Message}");
                        return;
                    }

                    ApplyDefaultsOnly(task.Exception?.GetBaseException()?.Message);
                    return;
                }

                isFirebaseNativeReady = true;
                FetchData();
            });
        }
        catch (Exception e)
        {
            if (isConfigReady)
            {
                Debug.LogWarning($"[RemoteConfigManager] Initialize failed, keeping cached values. {e.Message}");
                return;
            }

            ApplyDefaultsOnly(e.Message);
        }
    }

    public void FetchData()
    {
        if (!isFirebaseNativeReady)
        {
            if (!isConfigReady)
            {
                ApplyDefaultsOnly("Firebase native layer unavailable.");
            }
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
                    MarkConfigReadyForNewUserIfNeeded();
                }
            });
        }
        catch (Exception e)
        {
            if (isConfigReady)
            {
                Debug.LogWarning($"[RemoteConfigManager] Fetch failed, keeping cached values. {e.Message}");
                return;
            }

            ApplyDefaultsOnly(e.Message);
        }
    }

    private void ActivateConfig()
    {
        if (!isFirebaseNativeReady)
        {
            if (!isConfigReady)
            {
                ApplyDefaultsOnly("Firebase native layer unavailable.");
            }
            return;
        }

        try
        {
            FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(activateTask =>
            {
                if (activateTask.IsCompleted && !activateTask.IsFaulted)
                {
                    Debug.Log($"[RemoteConfigManager] Activate completed. Result: {activateTask.Result}");
                    SyncFromFirebase();
                }
                else
                {
                    Debug.LogError($"[RemoteConfigManager] Activate failed: {activateTask.Exception}");
                }

                MarkConfigReadyForNewUserIfNeeded();
            });
        }
        catch (Exception e)
        {
            if (isConfigReady)
            {
                Debug.LogWarning($"[RemoteConfigManager] Activate failed, keeping cached values. {e.Message}");
                return;
            }

            ApplyDefaultsOnly(e.Message);
        }
    }

    private void ApplyUserDataFromActiveValues()
    {
        if (Assets.Scripts.Core.UserDataManager.Instance == null) return;

        Assets.Scripts.Core.UserDataManager.Instance.IsInterstitialActive = GetBool(KEY_IS_INTERSTITIAL_ACTIVE);
        Assets.Scripts.Core.UserDataManager.Instance.IsDynamicMaxZoom = GetBool(KEY_IS_DYNAMIC_MAX_ZOOM);
    }

    #region Accessors

    private object GetActiveValue(string key)
    {
        if (activeValues.TryGetValue(key, out object value))
        {
            return value;
        }

        return defaults.TryGetValue(key, out object defaultValue) ? defaultValue : null;
    }

    public string GetString(string key)
    {
        object value = GetActiveValue(key);
        return value != null ? Convert.ToString(value) : string.Empty;
    }

    public bool GetBool(string key)
    {
        object value = GetActiveValue(key);
        return value != null && Convert.ToBoolean(value);
    }

    public long GetLong(string key)
    {
        object value = GetActiveValue(key);
        return value != null ? Convert.ToInt64(value) : 0L;
    }

    public double GetDouble(string key)
    {
        object value = GetActiveValue(key);
        return value != null ? Convert.ToDouble(value) : 0.0;
    }

    public bool HasForceUpdateVersionIndication =>
        HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_ANDROID) ||
        HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_IOS);

    public bool HasSoftUpdateVersionIndication =>
        HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_ANDROID) ||
        HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_IOS);

    public bool HasCurrentPlatformForceUpdateIndication
    {
        get
        {
#if UNITY_ANDROID
            return HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_ANDROID);
#elif UNITY_IOS
            return HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_IOS);
#else
            return false;
#endif
        }
    }

    public bool HasCurrentPlatformSoftUpdateIndication
    {
        get
        {
#if UNITY_ANDROID
            return HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_ANDROID);
#elif UNITY_IOS
            return HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_IOS);
#else
            return false;
#endif
        }
    }

    public string ForceUpdateVersionAndroid =>
        HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_ANDROID)
            ? GetString(KEY_FORCE_UPDATE_VERSION_ANDROID)
            : string.Empty;

    public string ForceUpdateVersionIOS =>
        HasUpdateVersionIndication(KEY_FORCE_UPDATE_VERSION_IOS)
            ? GetString(KEY_FORCE_UPDATE_VERSION_IOS)
            : string.Empty;

    public string SoftUpdateVersionAndroid =>
        HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_ANDROID)
            ? GetString(KEY_SOFT_UPDATE_VERSION_ANDROID)
            : string.Empty;

    public string SoftUpdateVersionIOS =>
        HasUpdateVersionIndication(KEY_SOFT_UPDATE_VERSION_IOS)
            ? GetString(KEY_SOFT_UPDATE_VERSION_IOS)
            : string.Empty;
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
    public bool IsGAEEnabled => GetBool(KEY_IS_GAE);
    /// <summary>
    /// When true, the app allows offline play. When false, a reconnect popup is shown after sustained loss of connectivity.
    /// </summary>
    public bool IsOfflineSupported => GetBool(KEY_IS_OFFLINE);

    /// <summary>
    /// Master switch for Golden Tournament. Defaults to true. Cached to disk after each Firebase fetch.
    /// </summary>
    public bool IsTournamentOn => GetBool(KEY_TOURNAMENT_ON);

    /// <summary>
    /// 0 = Easy (LevelsEasy), 1 = Hard (Levels), 2 = Harder (LevelsHard).
    /// Defaults to 1 when unavailable or out of range. Affects normal progression only.
    /// </summary>
    public int DifficultyCurve
    {
        get
        {
            int value = (int)GetLong(KEY_DIFFICULTY_CURVE);
            if (value < 0 || value > 2)
            {
                return 1;
            }
            return value;
        }
    }

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
            return string.Empty;
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
            return string.Empty;
#endif
        }
    }

    #endregion
}
