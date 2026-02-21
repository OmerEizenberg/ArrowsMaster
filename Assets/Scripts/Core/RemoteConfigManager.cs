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
            { KEY_SOFT_UPDATE_VERSION_IOS, "1.0.0" }
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
