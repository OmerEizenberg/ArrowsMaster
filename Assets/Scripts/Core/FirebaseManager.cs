using Firebase;
using Firebase.Crashlytics;
using Firebase.Analytics;
using Firebase.Messaging;
using Firebase.RemoteConfig;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Singular;


public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }
    private bool isInitialized = false;

    // Event Names
    public const string EVENT_LEVEL_START = "level_start";
    public const string EVENT_LEVEL_END = "level_end";
    public const string EVENT_PURCHASE = "purchase";
    public const string EVENT_AD_IMPRESSION = "ad_impression";
    public const string EVENT_TUTORIAL_BEGIN = "tutorial_begin";
    public const string EVENT_TUTORIAL_COMPLETE = "tutorial_complete";

    // Parameter Names
    public const string PARAM_LEVEL_ID = "level_id";
    public const string PARAM_ATTEMPT_COUNT = "attempt_count";
    public const string PARAM_SUCCESS = "success";
    public const string PARAM_SCORE = "score";
    public const string PARAM_VALUE = "value";
    public const string PARAM_CURRENCY = "currency";
    public const string PARAM_ITEM_ID = "item_id";
    public const string PARAM_AD_PLATFORM = "ad_platform";
    public const string PARAM_AD_SOURCE = "ad_source";
    public const string PARAM_AD_UNIT_NAME = "ad_unit_name";
    public const string PARAM_AD_FORMAT = "ad_format";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("FirebaseManager");
            go.AddComponent<FirebaseManager>();
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
    }

    void Start()
    {
        // --- Singular: Set ATT timeout for iOS (Recommended: 300s) ---
        #if UNITY_IOS && !UNITY_EDITOR
        var singular = FindFirstObjectByType<SingularSDK>();
        if (singular != null && singular.waitForTrackingAuthorizationWithTimeoutInterval == 0)
        {
            singular.waitForTrackingAuthorizationWithTimeoutInterval = 300;
            Debug.Log("[FirebaseManager] Set Singular ATT timeout to 300s.");
        }
        #endif
        // -------------------------------------------------------------

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            DependencyStatus dependencyStatus = task.Result;
            
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Initialize Firebase
                FirebaseApp app = FirebaseApp.DefaultInstance;

                // Enable Crashlytics collection
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                
                // Initialize Analytics
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                
                // Initialize Messaging
                FirebaseMessaging.TokenReceived += OnTokenReceived;
                FirebaseMessaging.MessageReceived += OnMessageReceived;

                // Request permission for push notifications (required for iOS and Android 13+)
                FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task => {
                    if (task.IsCompleted && !task.IsFaulted) {
                        Debug.Log("[FirebaseManager] Messaging permission request completed.");
                    } else {
                        Debug.LogError("[FirebaseManager] Messaging permission request failed: " + task.Exception);
                    }
                });

                isInitialized = true;
                Debug.Log("[FirebaseManager] Firebase App, Crashlytics, Analytics, and Messaging are ready.");

                // Initialize Remote Config
                RemoteConfigManager.Instance.Initialize();

                // Request notification permission for Android 13+
                #if UNITY_ANDROID && !UNITY_EDITOR
                RequestNotificationPermission();
                #endif

                // Request token explicitly to ensure registration
                FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(tokenTask => {
                    if (tokenTask.IsCompleted && !tokenTask.IsFaulted) {
                        Debug.Log("[FirebaseManager] Initial Registration Token: " + tokenTask.Result);
                    }
                });
            }
            else
            {
                Debug.LogError($"[FirebaseManager] Could not resolve Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    private void OnTokenReceived(object sender, TokenReceivedEventArgs token)
    {
        Debug.Log("[FirebaseManager] Registration Token Received: " + token.Token);
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Debug.Log("[FirebaseManager] Received a new message from: " + e.Message.From);
        
        if (e.Message.Notification != null)
        {
            Debug.Log("[FirebaseManager] Notification: " + e.Message.Notification.Title + " - " + e.Message.Notification.Body);
        }

        if (e.Message.Data.Count > 0)
        {
            foreach (KeyValuePair<string, string> iter in e.Message.Data)
            {
                Debug.Log("[FirebaseManager] Data Key: " + iter.Key + ", Value: " + iter.Value);
            }
        }
    }
    
    #if UNITY_ANDROID && !UNITY_EDITOR
    private void RequestNotificationPermission()
    {
        try
        {
            // Check if we are on Android 13 (API 33) or higher
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                int sdkInt = version.GetStatic<int>("SDK_INT");
                if (sdkInt >= 33)
                {
                    if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                    {
                        Debug.Log("[FirebaseManager] Requesting POST_NOTIFICATIONS permission.");
                        UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[FirebaseManager] Error requesting notification permission: " + e.Message);
        }
    }
    #endif

    #region Analytics Helpers
    public void LogEvent(string eventName)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName);
        SingularSDK.Event(eventName);
    }

    public void LogEvent(string eventName, string parameterName, string parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, eventName);
    }

    public void LogEvent(string eventName, string parameterName, long parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, eventName);
    }

    public void LogEvent(string eventName, string parameterName, double parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, eventName);
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameters);

        // --- Singular: Log the same event (without params for now due to Parameter being write-only) ---
        SingularSDK.Event(eventName);
        // ------------------------------------
    }

    public void SetUserProperty(string propertyName, string propertyValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.SetUserProperty(propertyName, propertyValue);
    }

    public void SetUserId(string userId)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.SetUserId(userId);
        SingularSDK.SetCustomUserId(userId);
    }
    #endregion
}