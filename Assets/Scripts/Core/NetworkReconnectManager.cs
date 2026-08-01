using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// When remote config <c>isOffline</c> is false, shows a blocking reconnect popup after sustained
    /// connectivity loss. Defaults to allowing offline play until the first remote/cached config is read.
    /// No Ads purchasers are always treated as offline-supported.
    /// </summary>
    public class NetworkReconnectManager : MonoBehaviour
    {
        private const float OfflinePopupDelaySeconds = 5f;
        private const float OnlinePopupHideDelaySeconds = 1f;
        private const float ConnectivityPollIntervalSeconds = 0.25f;

        private static NetworkReconnectManager _instance;
        private static Transform _overlayRoot;

        private GameObject _popupInstance;
        private ReconnectPopupView _popupView;
        private Coroutine _monitorCoroutine;
        private Coroutine _bootstrapCoroutine;
        private float _offlineElapsedSeconds;
        private float _onlineElapsedSeconds;
        private bool _wasReachable = true;
        private bool _dependenciesSubscribed;

        public static bool IsPopupVisible =>
            _instance != null && _instance._popupInstance != null && _instance._popupInstance.activeSelf;

        /// <summary>
        /// True when Unity reports any network path (Wi‑Fi, cellular, or LAN).
        /// Not a real internet ping — matches reconnect-popup reachability.
        /// </summary>
        public static bool IsOnline => IsNetworkReachable();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null)
                return;

            var go = new GameObject("NetworkReconnectManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetworkReconnectManager>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            _wasReachable = IsNetworkReachable();
        }

        private void OnEnable()
        {
            if (_bootstrapCoroutine != null)
                StopCoroutine(_bootstrapCoroutine);

            _bootstrapCoroutine = StartCoroutine(BootstrapWhenDependenciesReady());
        }

        private void OnDisable()
        {
            if (_bootstrapCoroutine != null)
            {
                StopCoroutine(_bootstrapCoroutine);
                _bootstrapCoroutine = null;
            }

            UnsubscribeFromDependencies();
            StopMonitoring();
        }

        private IEnumerator BootstrapWhenDependenciesReady()
        {
            while (RemoteConfigManager.Instance == null)
                yield return null;

            SubscribeToRemoteConfig();

            while (IAPManager.Instance == null)
                yield return null;

            SubscribeToNoAdsStatus();
            _bootstrapCoroutine = null;
            LogMonitoringState("bootstrap complete");
            RestartMonitoring();
        }

        private void UnsubscribeFromDependencies()
        {
            if (!_dependenciesSubscribed)
                return;

            UnsubscribeFromRemoteConfig();
            UnsubscribeFromNoAdsStatus();
            _dependenciesSubscribed = false;
        }

        private void SubscribeToRemoteConfig()
        {
            if (RemoteConfigManager.Instance == null)
                return;

            RemoteConfigManager.Instance.OnConfigInitialized -= HandleRemoteConfigUpdated;
            RemoteConfigManager.Instance.OnConfigValuesUpdated -= HandleRemoteConfigUpdated;
            RemoteConfigManager.Instance.OnConfigInitialized += HandleRemoteConfigUpdated;
            RemoteConfigManager.Instance.OnConfigValuesUpdated += HandleRemoteConfigUpdated;
            _dependenciesSubscribed = true;
        }

        private void UnsubscribeFromRemoteConfig()
        {
            if (RemoteConfigManager.Instance == null)
                return;

            RemoteConfigManager.Instance.OnConfigInitialized -= HandleRemoteConfigUpdated;
            RemoteConfigManager.Instance.OnConfigValuesUpdated -= HandleRemoteConfigUpdated;
        }

        private void SubscribeToNoAdsStatus()
        {
            if (IAPManager.Instance == null)
                return;

            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
            IAPManager.Instance.OnNoAdsStatusChanged += HandleNoAdsStatusChanged;

            if (IAPManager.Instance.HasNoAds)
                HandleNoAdsStatusChanged(true);
        }

        private void UnsubscribeFromNoAdsStatus()
        {
            if (IAPManager.Instance == null)
                return;

            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
        }

        private void HandleNoAdsStatusChanged(bool hasNoAds)
        {
            if (!hasNoAds)
                return;

            Debug.Log("[NetworkReconnectManager] No Ads purchased — allowing offline play.");
            ResetConnectionTracking();
            HidePopup();
            RestartMonitoring();
        }

        private void HandleRemoteConfigUpdated()
        {
            if (AllowsOfflinePlay())
            {
                ResetConnectionTracking();
                HidePopup();
            }

            LogMonitoringState("remote config updated");
            RestartMonitoring();
        }

        private void RestartMonitoring()
        {
            StopMonitoring();

            if (!ShouldEnforceConnection())
            {
                LogMonitoringState("monitoring not required");
                return;
            }

            _monitorCoroutine = StartCoroutine(MonitorConnectivityCoroutine());
            Debug.Log("[NetworkReconnectManager] Started connectivity monitoring (isOffline=false).");
        }

        private void StopMonitoring()
        {
            if (_monitorCoroutine != null)
            {
                StopCoroutine(_monitorCoroutine);
                _monitorCoroutine = null;
            }
        }

        private IEnumerator MonitorConnectivityCoroutine()
        {
            var wait = new WaitForSecondsRealtime(ConnectivityPollIntervalSeconds);

            while (true)
            {
                if (!ShouldEnforceConnection())
                {
                    ResetConnectionTracking();
                    HidePopup();
                    yield break;
                }

                bool isReachable = IsNetworkReachable();

                if (isReachable)
                {
                    if (!_wasReachable)
                        Debug.Log("[NetworkReconnectManager] Connection restored.");

                    ResetOfflineTracking();

                    if (_popupInstance != null)
                    {
                        _onlineElapsedSeconds += ConnectivityPollIntervalSeconds;
                        if (_onlineElapsedSeconds >= OnlinePopupHideDelaySeconds)
                        {
                            Debug.Log("[NetworkReconnectManager] Stable connection confirmed — hiding reconnect popup.");
                            ResetOnlineTracking();
                            HidePopup();
                        }
                    }
                    else
                    {
                        ResetOnlineTracking();
                    }
                }
                else
                {
                    ResetOnlineTracking();
                    _offlineElapsedSeconds += ConnectivityPollIntervalSeconds;

                    if (_offlineElapsedSeconds >= OfflinePopupDelaySeconds)
                        ShowPopupIfNeeded();
                }

                _wasReachable = isReachable;
                yield return wait;
            }
        }

        private void ResetOfflineTracking()
        {
            _offlineElapsedSeconds = 0f;
        }

        private void ResetOnlineTracking()
        {
            _onlineElapsedSeconds = 0f;
        }

        private void ResetConnectionTracking()
        {
            ResetOfflineTracking();
            ResetOnlineTracking();
        }

        private static bool UserHasNoAds =>
            IAPManager.Instance != null && IAPManager.Instance.HasNoAds;

        private bool ShouldEnforceConnection()
        {
            return !AllowsOfflinePlay();
        }

        private bool AllowsOfflinePlay()
        {
            if (UserHasNoAds)
                return true;

            return RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.IsOfflineSupported;
        }

        /// <summary>
        /// Uses Unity's <see cref="Application.internetReachability"/> — any active network path
        /// (Wi‑Fi, cellular, or other LAN) counts as reachable. This is not a real internet ping test.
        /// </summary>
        private static bool IsNetworkReachable()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        private static string GetReachabilityLabel()
        {
            switch (Application.internetReachability)
            {
                case NetworkReachability.ReachableViaCarrierDataNetwork:
                    return "cellular";
                case NetworkReachability.ReachableViaLocalAreaNetwork:
                    return "wifi/lan";
                default:
                    return "none";
            }
        }

        private void ShowPopupIfNeeded()
        {
            if (_popupInstance != null)
                return;

            EnsureOverlayCanvas();
            EnsureEventSystem();

            var prefab = Resources.Load<GameObject>("ReconnectPopup");
            if (prefab != null)
            {
                _popupInstance = Instantiate(prefab, _overlayRoot, false);
            }
            else
            {
                _popupInstance = new GameObject("ReconnectPopup", typeof(RectTransform), typeof(ReconnectPopupView));
                _popupInstance.transform.SetParent(_overlayRoot, false);
            }

            _popupInstance.SetActive(true);

            var rect = _popupInstance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero;
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            _popupView = _popupInstance.GetComponent<ReconnectPopupView>();
            if (_popupView == null)
                _popupView = _popupInstance.AddComponent<ReconnectPopupView>();

            _popupView.OnRetryClicked -= HandleRetryClicked;
            _popupView.OnRetryClicked += HandleRetryClicked;
            _popupView.Initialize();

            Debug.Log("[NetworkReconnectManager] Showing reconnect popup after sustained offline period.");
        }

        private void HidePopup()
        {
            if (_popupInstance == null)
                return;

            if (_popupView != null)
                _popupView.OnRetryClicked -= HandleRetryClicked;

            Destroy(_popupInstance);
            _popupInstance = null;
            _popupView = null;
        }

        private void HandleRetryClicked()
        {
            if (IsNetworkReachable())
            {
                Debug.Log("[NetworkReconnectManager] Retry succeeded — connection is available.");
                ResetConnectionTracking();
                HidePopup();
                return;
            }

            Debug.Log("[NetworkReconnectManager] Retry failed — still offline.");
        }

        private static void EnsureOverlayCanvas()
        {
            if (_overlayRoot != null)
                return;

            var canvasGo = new GameObject("NetworkReconnectOverlay");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGo);
            _overlayRoot = canvasGo.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("NetworkReconnectEventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystemGo);
        }

        private void LogMonitoringState(string reason)
        {
            bool hasNoAds = UserHasNoAds;
            bool isOfflineSupported = RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.IsOfflineSupported;
            bool enforce = ShouldEnforceConnection();
            bool reachable = IsNetworkReachable();

            Debug.Log(
                $"[NetworkReconnectManager] {reason} | enforce={enforce}, reachable={reachable} ({GetReachabilityLabel()}), " +
                $"isOfflineSupported={isOfflineSupported}, hasNoAds={hasNoAds}, " +
                $"offlineTimer={_offlineElapsedSeconds:F1}s, onlineTimer={_onlineElapsedSeconds:F1}s");
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: force the reconnect popup for layout testing.</summary>
        [ContextMenu("Debug/Show Reconnect Popup")]
        private void DebugShowReconnectPopup()
        {
            ResetConnectionTracking();
            HidePopup();
            ShowPopupIfNeeded();
        }
#endif
    }
}
