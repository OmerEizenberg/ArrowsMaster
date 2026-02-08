using Firebase;
using Firebase.Crashlytics;
using Firebase.Analytics;
using Firebase.Messaging;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
    public const string PARAM_AD_UNIT_NAME = "ad_unit_name";

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

                isInitialized = true;
                Debug.Log("[FirebaseManager] Firebase App, Crashlytics, Analytics, and Messaging are ready.");

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

    #region Analytics Helpers
    public void LogEvent(string eventName)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName);
    }

    public void LogEvent(string eventName, string parameterName, string parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
    }

    public void LogEvent(string eventName, string parameterName, long parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
    }

    public void LogEvent(string eventName, string parameterName, double parameterValue)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameterName, parameterValue);
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!isInitialized) return;
        FirebaseAnalytics.LogEvent(eventName, parameters);
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
    }
    #endregion
}