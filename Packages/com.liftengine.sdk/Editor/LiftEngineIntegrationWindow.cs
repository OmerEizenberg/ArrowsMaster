using UnityEditor;
using UnityEngine;

namespace LiftEngine.Editor
{
    public class LiftEngineIntegrationWindow : EditorWindow
    {
        private enum Tab { Settings, Integration, Debug }

        private Tab _tab = Tab.Settings;
        private LiftEngineSettings _settings;
        private Vector2 _scroll;
        private string _debugStatus = "";
        private string _lastHealthResult = "";
        private string _lastIpCountryResult = "";
        private string _lastPredictResult = "";
        private string _predictPreviewJson = "";
        private LiftEngineAdFormat _debugFormat = LiftEngineAdFormat.Rewarded;
        private string _debugInstallType = "Organic";
        private string _debugMediaSource = "test_source";
        private float _debugPurchaseAmount = 4.99f;
        private bool _subscribedToSdkEvents;

        private const string LogTag = "[LiftEngine Debug]";

        private static void Log(string message) => Debug.Log($"{LogTag} {message}");

        private static void LogWarning(string message) => Debug.LogWarning($"{LogTag} {message}");

        [MenuItem("Window/LiftEngine/Integration Manager")]
        public static void ShowWindow()
        {
            GetWindow<LiftEngineIntegrationWindow>("LiftEngine");
        }

        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnsubscribeSdkEvents();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                _debugStatus = "";
                _lastHealthResult = "";
                _lastPredictResult = "";
                UnsubscribeSdkEvents();
            }

            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (_tab != Tab.Debug || !Application.isPlaying)
                return;

            Repaint();
        }

        private void SubscribeSdkEvents()
        {
            if (_subscribedToSdkEvents || !Application.isPlaying)
                return;

            LiftEngineSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            LiftEngineSdkCallbacks.OnPredictSuccessEvent += OnPredictSuccess;
            LiftEngineSdkCallbacks.OnPredictFailedEvent += OnPredictFailed;
            LiftEngineSdkCallbacks.OnAdLoadedEvent += OnAdLoaded;
            LiftEngineSdkCallbacks.OnAdDisplayedEvent += OnAdDisplayed;
            LiftEngineSdkCallbacks.OnAdHiddenEvent += OnAdHidden;
            LiftEngineSdkCallbacks.OnAdRewardedEvent += OnAdRewarded;
            LiftEngineSdkCallbacks.OnAdRevenuePaidEvent += OnAdRevenuePaid;
            LiftEngineSignalBus.BidFloorPredictionFailed += OnBidFloorPredictionFailed;
            LiftEngineSignalBus.AdPrewarmCompleted += OnAdPrewarmCompleted;
            LiftEngineSignalBus.AdReadyStateChanged += OnAdReadyStateChanged;
            _subscribedToSdkEvents = true;
            Log("Subscribed to SDK debug events.");
        }

        private void UnsubscribeSdkEvents()
        {
            if (!_subscribedToSdkEvents)
                return;

            LiftEngineSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            LiftEngineSdkCallbacks.OnPredictSuccessEvent -= OnPredictSuccess;
            LiftEngineSdkCallbacks.OnPredictFailedEvent -= OnPredictFailed;
            LiftEngineSdkCallbacks.OnAdLoadedEvent -= OnAdLoaded;
            LiftEngineSdkCallbacks.OnAdDisplayedEvent -= OnAdDisplayed;
            LiftEngineSdkCallbacks.OnAdHiddenEvent -= OnAdHidden;
            LiftEngineSdkCallbacks.OnAdRewardedEvent -= OnAdRewarded;
            LiftEngineSdkCallbacks.OnAdRevenuePaidEvent -= OnAdRevenuePaid;
            LiftEngineSignalBus.BidFloorPredictionFailed -= OnBidFloorPredictionFailed;
            LiftEngineSignalBus.AdPrewarmCompleted -= OnAdPrewarmCompleted;
            LiftEngineSignalBus.AdReadyStateChanged -= OnAdReadyStateChanged;
            _subscribedToSdkEvents = false;
        }

        private void OnSdkInitialized(LiftEngineInitializationStatus status)
        {
            _debugStatus = status == LiftEngineInitializationStatus.Success
                ? "SDK initialized."
                : "SDK initialization failed. Ensure MAX is initialized first.";

            if (status == LiftEngineInitializationStatus.Success)
                Log("SDK initialized successfully.");
            else
                LogWarning("SDK initialization failed. Ensure AppLovin MAX is initialized before LiftEngine.");

            Repaint();
        }

        private void OnPredictSuccess(LiftEnginePredictEventArgs args)
        {
            _lastPredictResult = $"Optimization OK — format={args?.Format}, succeeded={args?.Succeeded}";
            Log($"Optimization response received — format={args?.Format}");
            Repaint();
        }

        private void OnPredictFailed(LiftEngineOperationError error)
        {
            _lastPredictResult = $"Optimization failed — {error?.StatusCode}: {error?.Message}";
            LogWarning($"Optimization request failed — status={error?.StatusCode}, message={error?.Message}");
            Repaint();
        }

        private void OnAdLoaded(LiftEngineAdInfo info)
        {
            Log($"Ad loaded — format={info?.Format}, unit={info?.AdUnitId}, network={info?.NetworkName}, revenue={info?.Revenue:F4}");
            Repaint();
        }

        private void OnAdDisplayed(LiftEngineAdInfo info)
        {
            Log($"Ad displayed — format={info?.Format}, unit={info?.AdUnitId}, network={info?.NetworkName}");
            Repaint();
        }

        private void OnAdHidden(LiftEngineAdInfo info)
        {
            Log($"Ad hidden — format={info?.Format}, unit={info?.AdUnitId}");
            Repaint();
        }

        private void OnAdRewarded(LiftEngineAdInfo info)
        {
            Log($"Ad reward granted — format={info?.Format}, unit={info?.AdUnitId}");
            Repaint();
        }

        private void OnAdRevenuePaid(LiftEngineAdInfo info)
        {
            Log($"Ad revenue paid — format={info?.Format}, unit={info?.AdUnitId}, revenue=${info?.Revenue:F4}, network={info?.NetworkName}");
            Repaint();
        }

        private void OnBidFloorPredictionFailed(BidFloorPredictionFailedSignal signal)
        {
            LogWarning($"[Attempt -1] Optimization unavailable for {signal.Format}. Entering fallback fill loop.");
            Repaint();
        }

        private void OnAdPrewarmCompleted(AdPrewarmCompletedSignal signal)
        {
            Log($"Prewarm finished — format={signal.Format}, success={signal.Success}");
            Repaint();
        }

        private void OnAdReadyStateChanged(AdReadyStateChangedSignal signal)
        {
            Log($"Ad ready state changed — format={signal.Format}, isReady={signal.IsReady}");
            Repaint();
        }

        private void LoadSettings()
        {
            _settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
        }

        private void OnGUI()
        {
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Settings", "Integration", "Debug" });

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
                case Tab.Integration:
                    DrawIntegrationTab();
                    break;
                case Tab.Debug:
                    DrawDebugTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("LiftEngine SDK Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hover any field label for a tooltip.\n" +
                "Init order: your game calls MaxSdk.InitializeSdk() first, then LiftEngineSdk.Initialize().",
                MessageType.Info);

            if (_settings == null)
            {
                EditorGUILayout.HelpBox("No LiftEngineSettings asset found in Resources.", MessageType.Warning);
                if (GUILayout.Button("Create Settings Asset"))
                    CreateSettingsAsset();
                return;
            }

            var so = new SerializedObject(_settings);
            so.Update();

            DrawProperty(so, "environment");
            DrawProperty(so, "customApiBaseUrl");
            DrawProperty(so, "apiKey");
            EditorGUILayout.Space();
            DrawProperty(so, "mediationPlatform");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("iOS Ad Units (MAX)", EditorStyles.boldLabel);
            DrawProperty(so, "iosBannerAdUnitId");
            DrawProperty(so, "iosInterstitialAdUnitId");
            DrawProperty(so, "iosRewardedAdUnitId");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Android Ad Units (MAX)", EditorStyles.boldLabel);
            DrawProperty(so, "androidBannerAdUnitId");
            DrawProperty(so, "androidInterstitialAdUnitId");
            DrawProperty(so, "androidRewardedAdUnitId");
            EditorGUILayout.Space();
            DrawProperty(so, "predictTimeoutSeconds");
            DrawProperty(so, "defaultPredictionFallback");
            DrawProperty(so, "loadAttemptTimeoutSeconds");
            DrawProperty(so, "readinessCheckIntervalSeconds");
            DrawProperty(so, "prewarmOnInit");
            DrawProperty(so, "prewarmAfterShow");
            DrawProperty(so, "autoInitialize");
            DrawProperty(so, "verboseLogging");
            DrawProperty(so, "debugMode");

            so.ApplyModifiedProperties();

            if (GUILayout.Button("Save Settings"))
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static void DrawProperty(SerializedObject so, string propertyName)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, true);
        }

        private void DrawIntegrationTab()
        {
            EditorGUILayout.LabelField("Integration Checklist", EditorStyles.boldLabel);
            DrawCheck("Settings asset exists", _settings != null);
            DrawCheck("LiftEngine API key set", _settings != null && !string.IsNullOrEmpty(_settings.apiKey));
            DrawCheck("Interstitial MAX ad unit configured",
                _settings != null && !string.IsNullOrEmpty(_settings.GetAdUnitId(LiftEngineAdFormat.Interstitial)));
            DrawCheck("Rewarded MAX ad unit configured",
                _settings != null && !string.IsNullOrEmpty(_settings.GetAdUnitId(LiftEngineAdFormat.Rewarded)));
            DrawCheck("Banner MAX ad unit configured",
                _settings != null && !string.IsNullOrEmpty(_settings.GetAdUnitId(LiftEngineAdFormat.Banner)));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "MAX SDK key is NOT configured here. Your game (e.g. AdsManager) must call MaxSdk.SetSdkKey() and MaxSdk.InitializeSdk() before LiftEngineSdk.Initialize().",
                MessageType.Info);

            if (_settings != null && _settings.mediationPlatform == LiftEngineMediationPlatform.LevelPlay)
            {
                EditorGUILayout.HelpBox("LevelPlay is selected but not implemented yet. Use AppLovin MAX for testing.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Create / Refresh Settings Asset"))
                CreateSettingsAsset();
        }

        private void DrawDebugTab()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Create a settings asset first (Settings or Integration tab).", MessageType.Warning);
                return;
            }

            if (!_settings.debugMode)
            {
                EditorGUILayout.HelpBox("Enable Debug Mode in the Settings tab to use these tools.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run live tests.", MessageType.Info);
                EditorGUILayout.Space();
            }

            DrawPlayModeStatus();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SDK", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Initialize LiftEngine SDK"))
                {
                    Log("Initialize LiftEngine SDK — requested.");
                    SubscribeSdkEvents();
                    LiftEngineSdk.SetVerboseLogging(true);
                    LiftEngineSdk.Initialize(_settings);
                    _debugStatus = LiftEngineSdk.IsInitialized
                        ? "SDK initialized."
                        : "Initializing… (MAX must be initialized first).";

                    if (LiftEngineSdk.IsInitialized)
                        Log("Initialize LiftEngine SDK — completed immediately (already initialized).");
                    else
                        Log("Initialize LiftEngine SDK — waiting for mediation…");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("API", EditorStyles.boldLabel);

            if (GUILayout.Button("Test IP Country Lookup (Edit Mode OK)"))
            {
                Log("Test IP Country Lookup — fetching from Cloudflare trace…");
                _lastIpCountryResult = LiftEngineDebugHelper.TestIpCountryLookup();
                if (_lastIpCountryResult == "IL")
                    Log($"Test IP Country Lookup — OK: {_lastIpCountryResult}");
                else if (_lastIpCountryResult.StartsWith("FAILED"))
                    LogWarning($"Test IP Country Lookup — {_lastIpCountryResult}");
                else
                    Log($"Test IP Country Lookup — resolved: {_lastIpCountryResult}");
            }

            EditorGUILayout.LabelField("IP country:", string.IsNullOrEmpty(_lastIpCountryResult) ? "—" : _lastIpCountryResult);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Ping Health"))
                {
                    var url = _settings.ApiBaseUrl.TrimEnd('/') + "/health/live";
                    Log($"[CL] Ping Health — GET {url}");
                    _lastHealthResult = "Pinging…";
                    LiftEngineSdk.CheckHealth((ok, body) =>
                    {
                        _lastHealthResult = ok ? $"OK — {body}" : $"FAILED — {body}";
                        if (ok)
                            Log($"[BE] Ping Health — {body}");
                        else
                            LogWarning($"[BE] Ping Health failed — {body}");
                        Repaint();
                    });
                }
            }

            EditorGUILayout.LabelField("Health:", string.IsNullOrEmpty(_lastHealthResult) ? "—" : _lastHealthResult);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optimization & Ads", EditorStyles.boldLabel);

            _debugFormat = (LiftEngineAdFormat)EditorGUILayout.EnumPopup("Ad Format", _debugFormat);

            if (GUILayout.Button("Preview Context Payload (Edit Mode OK)"))
            {
                Log($"Preview Context Payload — building local preview for {_debugFormat} (no network request).");
                _predictPreviewJson = LiftEngineDebugHelper.BuildPredictPayloadPreview(
                    _debugFormat, _debugInstallType, _debugMediaSource);
                Log($"Preview Context Payload — ready ({_predictPreviewJson.Length} chars). See window text area.");
            }

            if (!string.IsNullOrEmpty(_predictPreviewJson))
            {
                EditorGUILayout.LabelField("Context payload preview:");
                EditorGUILayout.TextArea(_predictPreviewJson, GUILayout.MinHeight(120));
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Run Prewarm"))
                {
                    if (!RequireSdkInitialized())
                        return;

                    Log($"[CL] Run Prewarm — {_debugFormat}");
                    LiftEngineSdk.LoadAd(_debugFormat);
                    _lastPredictResult = $"Prewarm started for {_debugFormat}…";
                }

                if (GUILayout.Button("Show Ad"))
                {
                    if (!RequireSdkInitialized())
                        return;

                    Log($"Show Ad — requesting {_debugFormat}. ready={LiftEngineSdk.IsAdReady(_debugFormat)}, state={LiftEngineSdk.GetPrewarmState(_debugFormat)}");
                    LiftEngineSdk.ShowAd(_debugFormat, null, new LiftEngineShowAdCallbacks
                    {
                        OnAdDisplayed = () =>
                        {
                            _debugStatus = $"Ad displayed ({_debugFormat}).";
                            Log($"Show Ad — callback: displayed ({_debugFormat}).");
                            Repaint();
                        },
                        OnAdHidden = () =>
                        {
                            _debugStatus = $"Ad hidden ({_debugFormat}).";
                            Log($"Show Ad — callback: hidden ({_debugFormat}).");
                            Repaint();
                        },
                        OnAdDisplayFailed = msg =>
                        {
                            _debugStatus = $"Show failed: {msg}";
                            LogWarning($"Show Ad — callback: display failed ({_debugFormat}): {msg}");
                            Repaint();
                        }
                    });
                }
            }

            if (Application.isPlaying && LiftEngineSdk.IsInitialized)
            {
                EditorGUILayout.LabelField("Prewarm state:", LiftEngineSdk.GetPrewarmState(_debugFormat).ToString());
                EditorGUILayout.LabelField("Ad ready:", LiftEngineSdk.IsAdReady(_debugFormat).ToString());
            }

            EditorGUILayout.LabelField("Optimization:", string.IsNullOrEmpty(_lastPredictResult) ? "—" : _lastPredictResult);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Context", EditorStyles.boldLabel);

            _debugInstallType = EditorGUILayout.TextField("AppsFlyer Install Type", _debugInstallType);
            _debugMediaSource = EditorGUILayout.TextField("Media Source", _debugMediaSource);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Set Attribution"))
                {
                    if (!RequireSdkInitialized())
                        return;

                    Log($"Set Attribution — installType={_debugInstallType}, mediaSource={_debugMediaSource}");
                    LiftEngineSdk.SetAttribution(_debugInstallType, _debugMediaSource);
                    _debugStatus = "Attribution saved.";
                    Log("Set Attribution — saved to PlayerPrefs.");
                }

                _debugPurchaseAmount = EditorGUILayout.FloatField("Purchase Amount", _debugPurchaseAmount);

                if (GUILayout.Button("Simulate Purchase"))
                {
                    if (!RequireSdkInitialized())
                        return;

                    Log($"Simulate Purchase — recording ${_debugPurchaseAmount:F2}");
                    LiftEngineSdk.NotifyPurchase(_debugPurchaseAmount);
                    _debugStatus = $"Purchase recorded: ${_debugPurchaseAmount:F2}";
                    Log("Simulate Purchase — LTV / payer fields updated.");
                }

                if (GUILayout.Button("Clear Context Prefs"))
                {
                    if (!RequireSdkInitialized())
                        return;

                    Log("Clear Context Prefs — resetting all le_ctx_* PlayerPrefs keys.");
                    LiftEngineSdk.ClearDebugContext();
                    _debugStatus = "Context prefs cleared.";
                    Log("Clear Context Prefs — done.");
                }
            }
        }

        private void DrawPlayModeStatus()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Play Mode", Application.isPlaying ? "Running" : "Stopped");
            EditorGUILayout.LabelField("LiftEngine SDK", Application.isPlaying && LiftEngineSdk.IsInitialized ? "Initialized" : "Not initialized");
            if (!string.IsNullOrEmpty(_debugStatus))
                EditorGUILayout.LabelField("Last action", _debugStatus, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private bool RequireSdkInitialized()
        {
            if (!Application.isPlaying)
            {
                _debugStatus = "Enter Play Mode first.";
                LogWarning("Action blocked — enter Play Mode first.");
                return false;
            }

            if (_settings == null)
            {
                _debugStatus = "Create settings asset first.";
                LogWarning("Action blocked — LiftEngineSettings asset is missing.");
                return false;
            }

            if (LiftEngineSdk.IsInitialized)
                return true;

            Log("SDK not initialized — attempting auto-initialize before action…");
            SubscribeSdkEvents();
            LiftEngineSdk.SetVerboseLogging(true);
            LiftEngineSdk.Initialize(_settings);

            if (LiftEngineSdk.IsInitialized)
            {
                Log("Auto-initialize succeeded.");
                return true;
            }

            _debugStatus = "SDK not ready yet. Click 'Initialize LiftEngine SDK' after MAX has initialized, then retry.";
            LogWarning("Auto-initialize did not complete. Initialize MAX first, then click 'Initialize LiftEngine SDK'.");
            return false;
        }

        private static void DrawCheck(string label, bool ok)
        {
            EditorGUILayout.LabelField(ok ? "✓" : "✗", label);
        }

        private void CreateSettingsAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var path = "Assets/Resources/LiftEngineSettings.asset";
            _settings = AssetDatabase.LoadAssetAtPath<LiftEngineSettings>(path);
            if (_settings == null)
            {
                _settings = CreateInstance<LiftEngineSettings>();
                AssetDatabase.CreateAsset(_settings, path);
            }

            _settings.environment = LiftEngineEnvironment.Staging;
            _settings.apiKey = "test-api-key";
            _settings.iosInterstitialAdUnitId = "9625ca772cf7c819";
            _settings.androidInterstitialAdUnitId = "8cf59aa021b449bf";
            _settings.iosRewardedAdUnitId = "39cd5fd76b5da61f";
            _settings.androidRewardedAdUnitId = "a9110da25686aa62";
            _settings.iosBannerAdUnitId = "b4b98419050ba611";
            _settings.androidBannerAdUnitId = "b3d625776838cd3e";
            _settings.prewarmOnInit = true;
            _settings.prewarmAfterShow = true;
            _settings.autoInitialize = false;
            _settings.debugMode = true;

            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            LoadSettings();
            EditorUtility.DisplayDialog("LiftEngine", "Settings asset created at Assets/Resources/LiftEngineSettings.asset", "OK");
        }
    }
}
