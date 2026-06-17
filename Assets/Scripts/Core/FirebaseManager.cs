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


public class FirebaseManager : MonoBehaviour, SingularLinkHandler, SingularDeferredDeepLinkHandler, SingularDeviceAttributionCallbackHandler, SingularConversionValuesUpdatedHandler
{
    public static FirebaseManager Instance { get; private set; }
    private bool isInitialized = false;
    private string pendingAnalyticsUserId;
    private static readonly Queue<PendingFunnelEvent> pendingFunnelEvents = new Queue<PendingFunnelEvent>();

    private readonly struct PendingFunnelEvent
    {
        public readonly string EventName;
        public readonly Parameter[] Parameters;

        public PendingFunnelEvent(string eventName, Parameter[] parameters)
        {
            EventName = eventName;
            Parameters = parameters;
        }
    }

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
    public const string EVENT_BOOSTER_HINT_CLICKED = "booster_hint_clicked";
    public const string EVENT_BOOSTER_MAGIC_CLICKED = "booster_magic_clicked";
    public const string EVENT_BOOSTER_REFILL_CLICKED = "booster_refill_clicked";
    public const string EVENT_BOOSTER_SHUFFLE_CLICKED = "booster_shuffle_clicked";
    public const string EVENT_EARN = "earn";
    public const string EVENT_SPEND = "spend";
    public const string EVENT_PASSED_TERMS = "passed_terms";
    public const string EVENT_PASSED_CONSENT_APPROVE = "passed_consent_approve";
    public const string EVENT_PASSED_CONSENT_DENY = "passed_consent_deny";
    public const string EVENT_MAX_SDK_INITIALIZED = "max_sdk_initialized";
    public const string EVENT_MAX_SDK_INIT_FAILED = "max_sdk_init_failed";
    public const string EVENT_LIFTENGINE_SAFEGUARD = "liftengine_safeguard";

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
    public const string PARAM_COINS = "coins";
    public const string PARAM_HINT = "hint";
    public const string PARAM_SHUFFLE = "shuffle";
    public const string PARAM_MAGICWAND = "magicwand";
    public const string PARAM_REFILL = "refill";
    public const string PARAM_REASON = "reason";

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
        SingularSDK.registeredConversionValuesUpdatedHandler = this;
        #endif
        // Singular init + ATT ordering is handled by IOSAttributionBootstrap on iOS.
        // -----------------------------------------------------------------
    }

    void Start()
    {
        StartCoroutine(InitializeFirebaseDeferred());
    }

    private IEnumerator InitializeFirebaseDeferred()
    {
        // Let the app render at least one frame before Firebase dependency checks and SDK setup.
        yield return null;
        yield return new WaitForSecondsRealtime(0.5f);

#if UNITY_EDITOR
        // macOS Editor needs FirebaseCppApp-*.bundle, which is not checked into git (see .gitignore).
        // Touching Firebase APIs here throws DllNotFoundException; use baked-in Remote Config defaults instead.
        Debug.Log("[FirebaseManager] Skipping Firebase native init in Editor. Use a device build for full Firebase.");
        if (RemoteConfigManager.Instance != null)
        {
            RemoteConfigManager.Instance.ApplyDefaultsOnly("Firebase native SDK not loaded in Unity Editor.");
        }
        yield break;
#endif

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            DependencyStatus dependencyStatus = task.Result;
            
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Initialize Firebase
                FirebaseApp app = FirebaseApp.DefaultInstance;

                // Enable Crashlytics collection
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                Crashlytics.IsCrashlyticsCollectionEnabled = true;
                
                // Initialize Analytics
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                
                // Initialize Messaging
                FirebaseMessaging.TokenReceived += OnTokenReceived;
                FirebaseMessaging.MessageReceived += OnMessageReceived;
                FirebaseMessaging.TokenRegistrationOnInitEnabled = true;

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

                FlushPendingFunnelEvents();

                SingularAttributionBridge.NotifyFirebaseReady(this);
                SingularAttributionBridge.EnsureAnalyticsUserIdLinked();

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
                if (RemoteConfigManager.Instance != null)
                {
                    RemoteConfigManager.Instance.ApplyDefaultsOnly($"Firebase dependencies unavailable: {dependencyStatus}");
                }
            }
        });
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (isInitialized)
        {
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
        }
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

    public void LogFunnelEvent(string eventName)
    {
        LogFunnelEvent(eventName, null);
    }

    /// <summary>
    /// Logs a funnel event once per call. Queues until Firebase Analytics is ready (terms/consent can fire first).
    /// </summary>
    public void LogFunnelEvent(string eventName, params Parameter[] parameters)
    {
        if (string.IsNullOrEmpty(eventName))
            return;

        if (!isInitialized)
        {
            pendingFunnelEvents.Enqueue(new PendingFunnelEvent(eventName, parameters));
            return;
        }

        if (parameters == null || parameters.Length == 0)
            LogEvent(eventName);
        else
            LogEvent(eventName, parameters);
    }

    private void FlushPendingFunnelEvents()
    {
        while (pendingFunnelEvents.Count > 0)
        {
            var pending = pendingFunnelEvents.Dequeue();
            if (pending.Parameters == null || pending.Parameters.Length == 0)
                LogEvent(pending.EventName);
            else
                LogEvent(pending.EventName, pending.Parameters);
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

    public void LogEarnEvent(string reason, int shuffle = 0, int hint = 0, int magicwand = 0, int refill = 0, int coins = 0)
    {
        LogResourceEvent(EVENT_EARN, reason, shuffle, hint, magicwand, refill, coins);
    }

    public void LogSpendEvent(string reason, int shuffle = 0, int hint = 0, int magicwand = 0, int refill = 0, int coins = 0)
    {
        LogResourceEvent(EVENT_SPEND, reason, shuffle, hint, magicwand, refill, coins);
    }

    private void LogResourceEvent(string eventName, string reason, int shuffle, int hint, int magicwand, int refill, int coins)
    {
        if (!isInitialized || string.IsNullOrEmpty(reason)) return;
        LogEvent(eventName,
            new Parameter(PARAM_REASON, reason),
            new Parameter(PARAM_SHUFFLE, shuffle),
            new Parameter(PARAM_HINT, hint),
            new Parameter(PARAM_MAGICWAND, magicwand),
            new Parameter(PARAM_REFILL, refill),
            new Parameter(PARAM_COINS, coins));
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
        if (!isInitialized || string.IsNullOrWhiteSpace(propertyName))
            return;

        FirebaseAnalytics.SetUserProperty(propertyName, TruncateForFirebaseUserProperty(propertyValue));
    }

    /// <summary>Sets Firebase user id when ready; also queues for Singular via SingularAttributionBridge.</summary>
    public void SetUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        pendingAnalyticsUserId = userId.Trim();
        ApplyAnalyticsUserId(pendingAnalyticsUserId, fireLoginEvent: true);
    }

    internal void ApplyAnalyticsUserId(string userId, bool fireLoginEvent)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        pendingAnalyticsUserId = userId.Trim();

#if !UNITY_EDITOR
        if (SingularSDK.Initialized)
            SingularSDK.SetCustomUserId(pendingAnalyticsUserId);
#endif

        if (!isInitialized)
            return;

        FirebaseAnalytics.SetUserId(pendingAnalyticsUserId);

        if (fireLoginEvent)
            LogEvent(Events.sngLogin);
    }

    internal void ApplyAttributionUserProperties(SingularAttributionSnapshot snapshot)
    {
        if (!isInitialized || snapshot == null || !snapshot.HasAnyData)
            return;

        SetUserPropertyIfNotEmpty("acquisition_network", snapshot.Network);
        SetUserPropertyIfNotEmpty("acquisition_campaign", snapshot.Campaign);
        SetUserPropertyIfNotEmpty("acquisition_source", snapshot.SubAdNetwork);
        SetUserPropertyIfNotEmpty("acquisition_sub_campaign", snapshot.SubCampaign);
        SetUserPropertyIfNotEmpty("acquisition_campaign_id", snapshot.CampaignId);
        SetUserPropertyIfNotEmpty("acquisition_type", snapshot.AcquisitionType);
        SetUserPropertyIfNotEmpty("acquisition_creative", snapshot.Creative);
        SetUserPropertyIfNotEmpty("acquisition_tracker", snapshot.TrackerName);
        SetUserPropertyIfNotEmpty("acquisition_view_through", snapshot.IsViewThrough);
    }

    private void SetUserPropertyIfNotEmpty(string propertyName, string propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyValue))
            return;

        SetUserProperty(propertyName, propertyValue);
    }

    private static string TruncateForFirebaseUserProperty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim();
        const int maxLen = 36;
        return value.Length <= maxLen ? value : value.Substring(0, maxLen);
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

    public void OnConversionValuesUpdated(int value, int coarse, bool _lock)
    {
        Debug.Log($"[Singular] SKAN conversion updated: value={value}, coarse={coarse}, locked={_lock}");
    }

    public void OnSingularDeviceAttributionCallback(Dictionary<string, object> attributionInfo)
    {
        Debug.Log("[Singular] Device Attribution Callback Triggered.");
        SingularAttributionBridge.HandleDeviceAttributionCallback(attributionInfo);
    }
    // ----------------------------
    #endregion
}