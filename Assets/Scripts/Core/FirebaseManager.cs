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
using Assets.Scripts.Core;


public class FirebaseManager : MonoBehaviour, SingularLinkHandler, SingularDeferredDeepLinkHandler, SingularDeviceAttributionCallbackHandler
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
    public const string EVENT_SESSION_7 = "session7";
    public const string EVENT_RET_2 = "Ret_2";
    public const string EVENT_RET_7 = "Ret_7";
    public const string EVENT_RET_14 = "Ret_14";
    public const string EVENT_RET_21 = "Ret_21";
    public const string EVENT_RET_30 = "Ret_30";
    public const string EVENT_RET_60 = "Ret_60";
    public const string EVENT_RET_90 = "Ret_90";

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

        // --- Singular: Initialize and Configure early for iOS support ---
        #if !UNITY_EDITOR
        SingularSDK.registeredSingularLinkHandler = this;
        SingularSDK.registeredDDLHandler = this;
        SingularSDK.SetSingularDeviceAttributionCallbackHandler(this);
        #endif

        #if UNITY_IOS && !UNITY_EDITOR
        var singular = FindFirstObjectByType<SingularSDK>();
        if (singular != null)
        {
            // Set ATT timeout BEFORE initialization
            singular.waitForTrackingAuthorizationWithTimeoutInterval = 300;
            Debug.Log("[FirebaseManager] Configured Singular ATT timeout (300s) in Awake.");
        }
        #endif
        // -----------------------------------------------------------------
    }

    void Start()
    {

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

                // --- Singular: Track standard login event on startup ---
                LogEvent(Events.sngLogin);
                // --------------------------------------------------------

                // --- Track Session 7 Event (Send Once) ---
                if (UserDataManager.Instance.SessionCount == 7 && !UserDataManager.Instance.HasSentSession7)
                {
                    LogEvent(EVENT_SESSION_7);
                    UserDataManager.Instance.MarkSession7EventSent();
                }
                // -----------------------------------------

                // --- Track Retention Events (Send Once per day) ---
                int retDay = UserDataManager.Instance.GetRetentionDay();
                int[] milestoneDays = { 2, 7, 14, 21, 30, 60, 90 };
                foreach (int day in milestoneDays)
                {
                    if (retDay == day && !UserDataManager.Instance.HasSentRetentionEvent(day))
                    {
                        LogEvent("Ret_" + day);
                        UserDataManager.Instance.MarkRetentionEventSent(day);
                    }
                }
                // --------------------------------------------------

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
        
        // --- Singular: Standard Event Mapping ---
        string singularEvent = MapToSingularEvent(eventName);
        if (!string.IsNullOrEmpty(singularEvent)) {
            SingularSDK.Event(singularEvent);
        }
    }

    public void LogEvent(string eventName, string parameterName, string parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        
        string singularEvent = MapToSingularEvent(eventName);
        SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, singularEvent);
    }

    public void LogEvent(string eventName, string parameterName, long parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        
        string singularEvent = MapToSingularEvent(eventName);
        SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, singularEvent);
    }

    public void LogEvent(string eventName, string parameterName, double parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
        
        string singularEvent = MapToSingularEvent(eventName);
        if (!string.IsNullOrEmpty(singularEvent)) {
            SingularSDK.Event(new Dictionary<string, object> { { parameterName, parameterValue } }, singularEvent);
        }
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameters);

        string singularEvent = MapToSingularEvent(eventName);
        
        if (!string.IsNullOrEmpty(singularEvent)) {
            SingularSDK.Event(singularEvent);
        }
    }

    private string MapToSingularEvent(string eventName)
    {
        // Skip these events for Singular logging via wrapper because they are handled specifically 
        // with Revenue tracking elsewhere (IAPManager/AdsManager)
        if (eventName == EVENT_PURCHASE || eventName == EVENT_AD_IMPRESSION) return null;

        return eventName switch
        {
            EVENT_TUTORIAL_COMPLETE => Events.sngTutorialComplete,
            EVENT_TUTORIAL_BEGIN => "sng_tutorial_begin",
            _ => eventName
        };
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
        
        // --- Singular: Track standard login event ---
        SingularSDK.Event(Events.sngLogin);
    }

    // --- Singular Link Handlers ---
    public void OnSingularLinkResolved(SingularLinkParams linkParams)
    {
        Debug.Log($"[Singular] Deep Link Resolved: {linkParams.Deeplink}");
        // Add your custom logic here (e.g., navigate to a specific level)
    }

    public void OnDeferredDeepLink(string deepLink)
    {
        Debug.Log($"[Singular] Deferred Deep Link Received: {deepLink}");
        // Add your custom logic here (e.g., show a welcome reward)
    }

    public void OnSingularDeviceAttributionCallback(Dictionary<string, object> attributionInfo)
    {
        Debug.Log("[Singular] Device Attribution Callback Triggered.");
        if (attributionInfo != null)
        {
            foreach (var kvp in attributionInfo)
            {
                Debug.Log($"[Singular] Attribution Data - {kvp.Key}: {kvp.Value}");
            }

            // You can extract specific fields like network, campaign, sub_adnetwork, etc.
            string network = attributionInfo.ContainsKey("network") ? attributionInfo["network"].ToString() : "organic";
            string campaign = attributionInfo.ContainsKey("campaign") ? attributionInfo["campaign"].ToString() : "unknown";
            string subAdNetwork = attributionInfo.ContainsKey("sub_adnetwork") ? attributionInfo["sub_adnetwork"].ToString() : "unknown";

            Debug.Log($"[Singular] User Acquired via Network: {network}, Campaign: {campaign}, Source: {subAdNetwork}");
            
            // Note: You can log this data to Firebase Analytics as User Properties if you'd like
            SetUserProperty("acquisition_network", network);
            SetUserProperty("acquisition_campaign", campaign);
            SetUserProperty("acquisition_source", subAdNetwork);
        }
    }
    // ----------------------------
    #endregion
}