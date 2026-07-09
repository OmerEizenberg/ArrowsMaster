using UnityEngine;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Firebase.Analytics;
using LiftEngine;
using Singular;

namespace Assets.Scripts.Core
{
    public class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        public bool IsInitialized => isInitialized;
        private bool isInitialized = false;
        private bool isInitializing = false;
        private int sdkInitRetryCount = 0;
        private float lastAdShowTime = -60f;

        private int interstitialRetryAttempt;
        private int rewardedRetryAttempt;

        private enum RewardAdType { None, GameReward, CoinsReward, MultiplyReward, HintReward, PlayOnReward, MagicReward, LifeReward, ShuffleReward }
        private RewardAdType pendingRewardType = RewardAdType.None;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        public event Action OnRewardReceived;
        public event Action OnCoinsRewardReceived;
        public event Action OnMultiplyRewardReceived;
        public event Action OnHintRewardReceived;
        public event Action OnPlayOnRewardReceived;
        public event Action OnMagicRewardReceived;
        public event Action OnLifeRewardReceived;
        public event Action OnShuffleRewardReceived;
        public event Action OnAdOpened;
        public event Action OnAdClosed;
        /// <summary>Fired when any cached ad-ready flag changes (avoids per-frame native IsAdReady calls).</summary>
        public event Action OnAdReadinessChanged;

        private bool _rewardedReady;
        private bool _interstitialReady;
        private bool _bannerReady;
        private bool _bannerCreated;
        private bool _bannerCreateInProgress;
        private bool _bannerCreateRequestedForShow;
        private bool _bannerShowRequested;
        private bool _bannerDestroyPending;
        private bool _applicationPaused;
        private Coroutine _bannerCreateCoroutine;
        private Coroutine _bannerDestroyCoroutine;
        private Coroutine _bannerResumeSyncCoroutine;

        private GameObject _coinsExplosionPrefab;
        private int _bannerRetryAttempt;
        private bool _showNextInterstitial = true;
        private bool _isFlushingPendingSingularRevenue;
        private bool _liftEngineEnabled;
        private bool _liftEngineReady;
        private bool _liftEngineInitSettled;
        private bool _liftEnginePermanentlyFailed;
        private int _liftEngineInitRetryAttempt;
        private LiftEngineSettings _liftEngineSettings;
        private Coroutine _liftEngineInitRetryCoroutine;
        private bool _liftEngineCallbacksSubscribed;
        private bool _liftEngineStartRequested;

        private bool UseLiftEngineAdPath => _liftEngineEnabled && _liftEngineReady;
        private bool UseDirectMaxAdPath => !_liftEngineEnabled || _liftEnginePermanentlyFailed;
        private bool IsLiftEngineInitPending =>
            _liftEngineEnabled && !_liftEngineReady && !_liftEnginePermanentlyFailed;
        private bool _fullscreenMaxCallbacksSubscribed;
        private bool _maxSdkInitSuccessLogged;
        private bool _maxSdkInitFailedLogged;

        private static readonly ConcurrentQueue<SingularAdRevenuePayload> _pendingSingularRevenue =
            new ConcurrentQueue<SingularAdRevenuePayload>();

        private readonly struct SingularAdRevenuePayload
        {
            public readonly double RevenueUsd;
            public readonly string NetworkName;
            public readonly string AdUnitId;
            public readonly string AdFormat;
            public readonly string RevenuePrecision;

            public SingularAdRevenuePayload(
                double revenueUsd,
                string networkName,
                string adUnitId,
                string adFormat,
                string revenuePrecision)
            {
                RevenueUsd = revenueUsd;
                NetworkName = networkName;
                AdUnitId = adUnitId;
                AdFormat = adFormat;
                RevenuePrecision = revenuePrecision;
            }
        }

        private const float HealthCheckIntervalSeconds = 20f;
        // Ultimate cap on waiting for a GDPR user to finish the consent form before init.
        private const float ConsentGatherTimeoutSeconds = 60f;
        // Cap on the UMP status roundtrip itself — a hung network must not cost the session's init.
        private const float ConsentStatusRoundtripTimeoutSeconds = 15f;
        // How long to wait for OnSdkInitializedEvent before assuming the callback was lost.
        private const float MaxSdkInitCallbackTimeoutSeconds = 30f;
        private const float AttGatherTimeoutSeconds = 30f;
        private const float RewardedPlacementWaitSeconds = 12f;
        private const float BannerCreateSettleDelaySeconds = 0.25f;
        private const float BannerCreateSettleDelayLowEndSeconds = 0.75f;

        private enum FullscreenAdChoice
        {
            None = 0,
            Interstitial = 1,
            Rewarded = 2
        }

        // ──────────────────────────────────────────────────────────────────
        // AppLovin MAX SDK Key
        // Set your key via AppLovin > Integration Manager in the Unity editor,
        // or find it at: https://dash.applovin.com/o/account#keys
        // ──────────────────────────────────────────────────────────────────
        private const string MaxSdkKey = "ghH9pVPTzdwgPfawqgCRPHWUMcR85KmpzpPlRCJwUDO8Uv4Xgn4oi52lcNTPuCb3ysqbfIUBUfNkrVdmvmuqTI";

        private const string BaseBannerPlacement = "Base_bnr";
        private const string BaseInterstitialPlacement = "Base_int";
        private const string BaseRewardedPlacement = "Base_rv";

        #region Ad Unit IDs

        private string InterstitialAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "9625ca772cf7c819";
#else
                return "8cf59aa021b449bf";
#endif
            }
        }

        private string RewardedAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "39cd5fd76b5da61f";
#else
                return "a9110da25686aa62";
#endif
            }
        }

        private string BannerAdUnitId
        {
            get
            {
#if UNITY_IOS || UNITY_IPHONE
                return "b4b98419050ba611";
#else
                return "b3d625776838cd3e";
#endif
            }
        }

        #endregion

        public bool IsRewardedReady => _rewardedReady;
        public bool IsMultiplyRewardedReady => _rewardedReady;
        public bool IsCoinsRewardedReady => _rewardedReady;
        public bool IsInterstitialReady => _interstitialReady;
        public bool IsAnyRewardedOrInterstitialReady => _rewardedReady || _interstitialReady;

        // ════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject adsGO = new GameObject("AdsManager");
                adsGO.AddComponent<AdsManager>();
                DontDestroyOnLoad(adsGO);
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

            _coinsExplosionPrefab = Resources.Load<GameObject>("CoinsSmallExplosion");

            _ = InitializeSDK();
            StartCoroutine(AdHealthCheckRoutine());
        }

        private void Start()
        {
            SubscribeToNoAdsStatus();
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (!paused)
                ScheduleBannerResumeSync();
        }

        private void OnDestroy()
        {
            CancelLiftEngineInitRetry();
            UnsubscribeLiftEngineCallbacks();

            MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxSdkInitialized;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnInterstitialClicked;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnInterstitialRevenuePaid;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnRewardedAdClicked;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRewardedAdRevenuePaid;

            MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerAdLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent -= OnBannerAdClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerAdRevenuePaid;

            UnsubscribeFromNoAdsStatus();
            CancelDeferredBannerCreate();
            if (_bannerResumeSyncCoroutine != null)
            {
                StopCoroutine(_bannerResumeSyncCoroutine);
                _bannerResumeSyncCoroutine = null;
            }
            if (_bannerDestroyCoroutine != null)
            {
                StopCoroutine(_bannerDestroyCoroutine);
                _bannerDestroyCoroutine = null;
            }
            ExecuteDestroyBannerImmediate();
        }

        // ════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════

        private bool UserHasNoAds =>
            IAPManager.Instance != null && IAPManager.Instance.HasNoAds;

        private bool AreBannerAdsSupported => AndroidWebViewSupport.AreBannerAdsSupported;
        private bool IsBannerEnvironmentReady() => AndroidWebViewSupport.EnsureWebViewReady();

        private void SubscribeToNoAdsStatus()
        {
            if (IAPManager.Instance == null) return;
            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
            IAPManager.Instance.OnNoAdsStatusChanged += HandleNoAdsStatusChanged;
            if (IAPManager.Instance.HasNoAds)
                HandleNoAdsStatusChanged(true);
        }

        private void UnsubscribeFromNoAdsStatus()
        {
            if (IAPManager.Instance == null) return;
            IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
        }

        private void HandleNoAdsStatusChanged(bool hasNoAds)
        {
            if (!hasNoAds) return;
            Debug.Log("[AdsManager] No Ads purchased — hiding and destroying banner.");
            DestroyBanner();
        }

        private void DestroyBanner()
        {
            CancelDeferredBannerCreate();
            _bannerShowRequested = false;

            if (!_bannerCreated)
            {
                CancelDeferredBannerDestroy();
                _bannerDestroyPending = false;
                return;
            }

            _bannerDestroyPending = true;
            if (_applicationPaused) return;

            ScheduleDeferredBannerDestroy();
        }

        private void CancelDeferredBannerDestroy()
        {
            if (_bannerDestroyCoroutine == null) return;

            StopCoroutine(_bannerDestroyCoroutine);
            _bannerDestroyCoroutine = null;
        }

        private void ScheduleDeferredBannerDestroy()
        {
            if (_bannerDestroyCoroutine != null) return;
            _bannerDestroyCoroutine = StartCoroutine(DestroyBannerDeferred());
        }

        private IEnumerator DestroyBannerDeferred()
        {
            // destroyAdView → ViewGroup.removeView runs on the Android UI thread and can ANR
            // if called during heavy frames; defer until after the current frame settles.
            yield return null;
            yield return new WaitForEndOfFrame();

            _bannerDestroyCoroutine = null;

            if (this == null || !_bannerDestroyPending || _applicationPaused)
                yield break;

            // Cancel destroy only when the banner is wanted again (not for No Ads teardown).
            if (_bannerShowRequested && !UserHasNoAds)
                yield break;

            ExecuteDestroyBannerImmediate();
        }

        private void ExecuteDestroyBanner()
        {
            if (!_bannerCreated) return;
            if (_applicationPaused)
            {
                _bannerDestroyPending = true;
                return;
            }

            _bannerDestroyPending = true;
            ScheduleDeferredBannerDestroy();
        }

        private void ExecuteDestroyBannerImmediate()
        {
            if (!_bannerCreated && !_liftEngineReady) return;

            try
            {
                if (_liftEngineReady)
                    LiftEngineSdk.DestroyBanner();
                else
                {
                    MaxSdk.HideBanner(BannerAdUnitId);
                    MaxSdk.DestroyBanner(BannerAdUnitId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] DestroyBanner failed: {e.Message}");
            }

            _bannerCreated = false;
            _bannerDestroyPending = false;
            SetCachedReady(ref _bannerReady, false);
        }

        private void ScheduleBannerResumeSync()
        {
            if (_bannerResumeSyncCoroutine != null)
                StopCoroutine(_bannerResumeSyncCoroutine);

            _bannerResumeSyncCoroutine = StartCoroutine(SyncBannerNativeStateAfterResume());
        }

        private IEnumerator SyncBannerNativeStateAfterResume()
        {
            // Wait for Unity/Android to finish the pause handshake before touching native ad views.
            yield return null;
            yield return new WaitForEndOfFrame();

            _bannerResumeSyncCoroutine = null;
            SyncBannerNativeState();
        }

        /// <summary>
        /// Applies banner create/show/hide/destroy to MAX. Skipped while the app is paused to avoid
        /// a Unity/Android deadlock (Unity thread posts to UI thread during Activity.onPause).
        /// </summary>
        private void SyncBannerNativeState()
        {
            if (_applicationPaused || !isInitialized) return;

            if (!AreBannerAdsSupported)
            {
                if (_bannerCreated || _bannerDestroyPending || _bannerCreateInProgress)
                {
                    CancelDeferredBannerCreate();
                    ExecuteDestroyBanner();
                }
                return;
            }

            if (_liftEngineReady)
            {
                SyncLiftEngineBannerState();
                return;
            }

            if (UseDirectMaxAdPath)
                SyncLegacyBannerState();
        }

        private void SyncLiftEngineBannerState()
        {
            if (UserHasNoAds)
            {
                if (_bannerCreated || _bannerDestroyPending)
                    ExecuteDestroyBanner();
                return;
            }

            if (_bannerDestroyPending)
            {
                ExecuteDestroyBanner();
                return;
            }

            if (_bannerCreateInProgress)
                return;

            if (!_bannerShowRequested)
            {
                if (_bannerCreated)
                    LiftEngineSdk.HideBanner();
                return;
            }

            if (!LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Banner))
            {
                InitializeBannerAds(requestedForShow: true);
                return;
            }

            ShowLiftEngineBanner();
        }

        private void SyncLegacyBannerState()
        {
            if (UserHasNoAds)
            {
                if (_bannerCreated || _bannerDestroyPending)
                    ExecuteDestroyBanner();
                return;
            }

            if (_bannerDestroyPending)
            {
                ExecuteDestroyBanner();
                return;
            }

            if (_bannerCreateInProgress) return;

            if (!_bannerShowRequested)
            {
                if (_bannerCreated)
                {
                    try
                    {
                        MaxSdk.HideBanner(BannerAdUnitId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[AdsManager] HideBanner failed: {e.Message}");
                    }
                }
                return;
            }

            if (!_bannerCreated)
            {
                InitializeBannerAds(requestedForShow: true);
                return;
            }

            try
            {
                MaxSdk.ShowBanner(BannerAdUnitId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] ShowBanner failed: {e.Message}");
            }
        }

        private void ShowLiftEngineBanner()
        {
            _bannerCreated = true;
            LiftEngineSdk.ShowAd(LiftEngineAdFormat.Banner, null, new LiftEngineShowAdCallbacks
            {
                OnAdDisplayed = () =>
                {
                    SetCachedReady(ref _bannerReady, true);
                    Debug.Log("[AdsManager] Settings Banner displayed (LiftEngine).");
                },
                OnAdDisplayFailed = message =>
                    Debug.LogWarning($"[AdsManager] Settings Banner display failed (LiftEngine): {message}")
            });
        }

        private void EnqueueAction(Action action) => _mainThreadQueue.Enqueue(action);

        private void SetCachedReady(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            OnAdReadinessChanged?.Invoke();
        }

        private void RefreshInterstitialReady()
        {
            bool ready = false;
            try
            {
                ready = _liftEngineReady
                    ? LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Interstitial)
                    : MaxSdk.IsInterstitialReady(InterstitialAdUnitId);
            }
            catch (Exception e) { Debug.LogWarning($"[AdsManager] Error checking interstitial readiness: {e.Message}"); }
            SetCachedReady(ref _interstitialReady, ready);
        }

        private void RefreshRewardedReady()
        {
            bool ready = false;
            try
            {
                ready = _liftEngineReady
                    ? LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Rewarded)
                    : MaxSdk.IsRewardedAdReady(RewardedAdUnitId);
            }
            catch (Exception e) { Debug.LogWarning($"[AdsManager] Error checking rewarded readiness: {e.Message}"); }
            SetCachedReady(ref _rewardedReady, ready);
        }

        private void RefreshBannerReady()
        {
            if (_liftEngineReady)
            {
                bool ready = false;
                try { ready = LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Banner); }
                catch (Exception e) { Debug.LogWarning($"[AdsManager] Error checking banner readiness: {e.Message}"); }
                SetCachedReady(ref _bannerReady, ready);
                _bannerCreated = ready || _bannerCreated;
                return;
            }

            if (!_bannerCreated)
                SetCachedReady(ref _bannerReady, false);
        }

        private void RefreshAllReadiness()
        {
            RefreshInterstitialReady();
            RefreshRewardedReady();
            RefreshBannerReady();
        }

        /// <summary>
        /// Preloads the next interstitial, rewarded, and banner after any ad is shown or dismissed.
        /// MAX recommends loading before the current ad finishes; we also call again on close as a fallback.
        /// </summary>
        private void PrepareAllAdsAfterClose()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }

            Debug.Log("[AdsManager] Preparing next ads (rewarded, interstitial, banner).");
            LoadRewarded();
            LoadInterstitial();
            PrepareBannerAd();
        }

        private void PrepareBannerAd()
        {
            if (!isInitialized) return;
            if (!AreBannerAdsSupported || UserHasNoAds) return;

            if (_liftEngineReady)
            {
                if (!_bannerShowRequested && !_bannerCreated)
                    return;

                if (LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Banner))
                    return;

                LoadSettingsBanner();
                return;
            }

            if (!UseDirectMaxAdPath)
                return;

            if (!_bannerCreated)
            {
                if (!_bannerShowRequested) return;
                LoadSettingsBanner();
                return;
            }

            // Banner view already exists; MAX retries loading automatically.
            // Avoid destroy/recreate — that allocates new Android views on the main thread under GC pressure.
            if (_bannerReady) return;
        }

        private void NotifyAdClosed()
        {
            OnAdClosed?.Invoke();
        }

        private IEnumerator AdHealthCheckRoutine()
        {
            var wait = new WaitForSeconds(HealthCheckIntervalSeconds);
            while (true)
            {
                yield return wait;

                try
                {
                    if (!isInitialized && !isInitializing)
                    {
                        Debug.Log("[AdsManager] HealthCheck: SDK not initialized. Attempting to initialize.");
                        _ = InitializeSDK();
                        continue;
                    }

                    if (!isInitialized) continue;

                    EnsureFullscreenAdsLoaded();
                    if (!_bannerCreated || !_bannerReady) PrepareBannerAd();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AdsManager] HealthCheck error: {e.Message}");
                }
            }
        }

        // ════════════════════════════════════════════
        //  SDK INITIALIZATION
        // ════════════════════════════════════════════

        private async Task InitializeSDK()
        {
            if (isInitialized || isInitializing) return;

            isInitializing = true;
            try
            {
                // Unity Gaming Services is only required for IAP — IAPManager.WarmUp() handles it independently.
                // Do not block MAX init on UGS; a slow/hung UGS init must never stall the ads chain.

#if UNITY_IOS && !UNITY_EDITOR
                bool attFinished = false;
                EnqueueAction(() =>
                {
                    IOSAdsHelper.RequestATT();
                    StartCoroutine(IOSAdsHelper.PollATTStatus(_ => attFinished = true));
                });
                float attWaitStart = Time.time;
                while (!attFinished && Time.time - attWaitStart < AttGatherTimeoutSeconds)
                    await Task.Yield();
                if (!attFinished)
                    Debug.LogWarning("[AdsManager] ATT flow timed out. Proceeding with ads initialization.");
#endif

                // Consent gathering runs in parallel and almost never blocks init:
                // - Returning users with a stored resolution: init immediately.
                // - Users outside GDPR scope: init the moment the UMP status roundtrip says so.
                // - UMP failure/offline: init immediately (nothing to wait for; retried next session).
                // - Only a first-session GDPR user with the consent form on screen delays init,
                //   because consent there is legally required before serving ads.
                EnqueueAction(() =>
                {
                    ConsentManager.RequestConsent(() =>
                        Debug.Log("[AdsManager] Consent gathering finished."));
                });

                float consentWaitStart = Time.time;
                while (ShouldWaitForConsentBeforeInit(consentWaitStart))
                    await Task.Yield();
                Debug.Log(
                    $"[AdsManager] Proceeding with MAX init (consent state: {ConsentManager.State}, " +
                    $"waited {Time.time - consentWaitStart:F1}s).");

                EnqueueAction(() =>
                {
                    // CCPA flag + non-GDPR consent healing. GDPR users are governed by the
                    // IAB TCF string that Google UMP writes (read automatically by MAX).
                    ConsentManager.ApplyConsentToMax();

                    // We gather consent via Google UMP ourselves — keep AppLovin's built-in
                    // consent flow disabled to avoid duplicate dialogs (Jun-19 tier-1 regression).
                    MaxSdk.SetExtraParameter("consent_flow_enabled", "false");

                    // MAX events (incl. ILRD revenue callbacks) must reach Unity APIs and
                    // Singular on the main thread.
                    MaxSdkBase.InvokeEventsOnUnityMainThread = true;

                    Debug.Log("[AdsManager] Initializing AppLovin MAX SDK...");

                    MaxSdkCallbacks.OnSdkInitializedEvent -= OnMaxSdkInitialized;
                    MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxSdkInitialized;

                    MaxSdk.SetSdkKey(MaxSdkKey);
                    MaxSdk.InitializeSdk();
                    StartCoroutine(WaitForMaxSdkInitCallback());
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdsManager] SDK Initialization Process Failed: {e.Message}");
                LogMaxSdkInitFailed(e.Message);
                isInitializing = false;
                _ = RetrySDKInitialization(20000);
            }
        }

        /// <summary>
        /// Watchdog: without it, a lost init callback leaves isInitializing=true forever and
        /// the health check never retries — the session silently produces no ads and no MAX DAU.
        /// </summary>
        private IEnumerator WaitForMaxSdkInitCallback()
        {
            float deadline = Time.realtimeSinceStartup + MaxSdkInitCallbackTimeoutSeconds;
            while (!isInitialized && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (isInitialized)
                yield break;

            bool nativeInitialized = false;
            try
            {
                nativeInitialized = MaxSdk.IsInitialized();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] Could not query native MAX init state: {e.Message}");
            }

            if (nativeInitialized)
            {
                Debug.Log("[AdsManager] MAX initialized natively but the callback was missed; replaying handler.");
                OnMaxSdkInitialized(null);
                yield break;
            }

            Debug.LogWarning(
                $"[AdsManager] MAX init callback timed out after {MaxSdkInitCallbackTimeoutSeconds}s. Retrying.");
            LogMaxSdkInitFailed("callback_timeout");
            isInitializing = false;
            _ = RetrySDKInitialization(5000);
        }

        /// <summary>
        /// Init waits only while consent is genuinely unresolved: the short UMP status
        /// roundtrip for everyone, and the on-screen GDPR form for first-session EEA users.
        /// NotRequired / Completed / Failed states release init immediately.
        /// </summary>
        private static bool ShouldWaitForConsentBeforeInit(float waitStartTime)
        {
            float waited = Time.time - waitStartTime;
            if (waited >= ConsentGatherTimeoutSeconds)
            {
                Debug.LogWarning("[AdsManager] Consent flow timed out. Proceeding with SDK initialization anyway.");
                return false;
            }

            switch (ConsentManager.State)
            {
                case ConsentManager.ConsentGatherState.Pending:
                    return waited < ConsentStatusRoundtripTimeoutSeconds;
                case ConsentManager.ConsentGatherState.FormRequired:
                    return true;
                default:
                    return false;
            }
        }

        private void LogMaxSdkInitialized()
        {
            if (_maxSdkInitSuccessLogged)
                return;

            _maxSdkInitSuccessLogged = true;
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogFunnelEvent(
                    FirebaseManager.EVENT_MAX_SDK_INITIALIZED,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_APP_VERSION, Application.version));
            }
        }

        private void LogMaxSdkInitFailed(string reason)
        {
            if (_maxSdkInitSuccessLogged || _maxSdkInitFailedLogged)
                return;

            _maxSdkInitFailedLogged = true;
            if (FirebaseManager.Instance == null)
                return;

            string safeReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason.Trim();
            if (safeReason.Length > 100)
                safeReason = safeReason.Substring(0, 100);

            FirebaseManager.Instance.LogFunnelEvent(
                FirebaseManager.EVENT_MAX_SDK_INIT_FAILED,
                new Parameter(FirebaseManager.PARAM_REASON, safeReason));
        }

        private async Task RetrySDKInitialization(int delayMs)
        {
            if (isInitialized || isInitializing) return;

            Debug.Log($"[AdsManager] Retrying SDK Initialization in {delayMs / 1000}s... (Attempt {sdkInitRetryCount + 1})");
            await Task.Delay(delayMs);

            if (this != null && !isInitialized && !isInitializing)
            {
                sdkInitRetryCount++;
                EnqueueAction(() => _ = InitializeSDK());
            }
        }

        private void OnMaxSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            if (isInitialized)
            {
                Debug.Log("[AdsManager] MAX SDK init callback ignored (already handled).");
                return;
            }

            Debug.Log("[AdsManager] AppLovin MAX SDK Initialized Successfully.");
            isInitialized = true;
            isInitializing = false;
            sdkInitRetryCount = 0;
            LogMaxSdkInitialized();

            TryStartLiftEngine();
            EnsureFullscreenMaxCallbacks();
            if (!_liftEngineEnabled)
            {
                LoadInterstitial();
                LoadRewarded();
            }

            SubscribeToNoAdsStatus();
            RefreshAllReadiness();
            // Banner prewarm is handled by HomeController after the lobby settles (~2s).
            // Early prewarm here raced with scene load and could native-crash on Android WebView.
        }

        // ════════════════════════════════════════════
        //  INTERSTITIAL ADS
        // ════════════════════════════════════════════

        private void EnsureFullscreenMaxCallbacks()
        {
            if (_fullscreenMaxCallbacksSubscribed)
                return;

            _fullscreenMaxCallbacksSubscribed = true;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClicked;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaid;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClicked;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaid;
        }

        private void InitializeInterstitialAds()
        {
            EnsureFullscreenMaxCallbacks();
            LoadInterstitial();
        }

        public void LoadInterstitial()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: IsInterstitialActive is false (Remote Config).");
                return;
            }
            // Loaded even for No Ads buyers so interstitials remain available as a rewarded fallback.
            Debug.Log("[AdsManager] Loading Interstitial Ad...");
            if (UseLiftEngineAdPath)
            {
                LiftEngineSdk.LoadAd(LiftEngineAdFormat.Interstitial);
                return;
            }

            if (!UseDirectMaxAdPath)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Load: LiftEngine init still in progress.");
                return;
            }

            MaxSdk.LoadInterstitial(InterstitialAdUnitId);
        }

        public void ShowInterstitial(bool isAuto = false)
        {
            if (UserDataManager.Instance != null && !UserDataManager.Instance.IsInterstitialActive)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: IsInterstitialActive is false (Remote Config).");
                return;
            }

            bool shouldShowThisTime = _showNextInterstitial;
            _showNextInterstitial = !_showNextInterstitial;

            if (!shouldShowThisTime)
            {
                Debug.Log("[AdsManager] Skipping Interstitial due to 50% frequency rule.");
                return;
            }

            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < GameManager.ADS_START_LEVEL)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial Show: User Level {UserDataManager.Instance.CurrentLevel} < {GameManager.ADS_START_LEVEL}.");
                return;
            }

            if (IAPManager.Instance != null && IAPManager.Instance.HasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Interstitial Show: User has No Ads.");
                return;
            }

            float currentAdCooldown = 60f;
            if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
            {
                currentAdCooldown = RemoteConfigManager.Instance.AdCooldown;
            }

            float timeSinceLastAd = Time.time - lastAdShowTime;
            if (timeSinceLastAd < currentAdCooldown)
            {
                Debug.Log($"[AdsManager] Skipping Interstitial due to cooldown. Last ad was {timeSinceLastAd:F1}s ago (Cooldown: {currentAdCooldown}s).");
                return;
            }

            if (IsInterstitialReady)
            {
                Debug.Log("[AdsManager] Showing Interstitial Ad.");
                OnAdOpened?.Invoke();
                if (UseLiftEngineAdPath)
                    ShowLiftEngineAd(LiftEngineAdFormat.Interstitial);
                else if (UseDirectMaxAdPath)
                    MaxSdk.ShowInterstitial(InterstitialAdUnitId, BaseInterstitialPlacement);
            }
            else
            {
                Debug.LogWarning($"[AdsManager] Interstitial Ad is not ready. Initialized: {isInitialized}");
                if (!isInitialized && !isInitializing) _ = InitializeSDK();
                else LoadInterstitial();
            }
        }

        #region Interstitial Callbacks

        private void OnInterstitialLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Loaded.");
            interstitialRetryAttempt = 0;
            AdMonetizationOptimizer.RecordInterstitialAd(adInfo);
            RefreshInterstitialReady();
        }

        private void OnInterstitialLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            interstitialRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, interstitialRetryAttempt));
            Debug.LogWarning($"[AdsManager] Interstitial Ad Load Failed (code: {errorInfo.Code}). Retrying in {retryDelay}s...");
            _ = RetryLoadInterstitial((int)(retryDelay * 1000));
        }

        private async Task RetryLoadInterstitial(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_interstitialReady)
                EnqueueAction(LoadInterstitial);
        }

        private void OnInterstitialDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Displayed.");
            SetCachedReady(ref _interstitialReady, false);
            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Interstitial Ad Display Failed (code: {errorInfo.Code})");
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
                pendingRewardType = RewardAdType.None;

            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Closed. Preparing next ads.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _interstitialReady, false);
            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None)
            {
                Debug.Log("[AdsManager] Interstitial fulfilled a rewarded placement. Granting pending reward.");
                ProcessPendingReward();
            }

            PrepareAllAdsAfterClose();
        }

        private void OnInterstitialClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Interstitial Ad Clicked.");
        }

        private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdMonetizationOptimizer.RecordInterstitialAd(adInfo);
            TrackAdRevenue(adInfo, "interstitial");
        }

        #endregion

        // ════════════════════════════════════════════
        //  REWARDED ADS
        // ════════════════════════════════════════════

        private void InitializeRewardedAds()
        {
            EnsureFullscreenMaxCallbacks();
            LoadRewarded();
        }

        public void LoadRewarded()
        {
            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }
            Debug.Log("[AdsManager] Loading Rewarded Ad...");
            if (UseLiftEngineAdPath)
            {
                LiftEngineSdk.LoadAd(LiftEngineAdFormat.Rewarded);
                return;
            }

            if (!UseDirectMaxAdPath)
            {
                Debug.Log("[AdsManager] Skipping Rewarded Load: LiftEngine init still in progress.");
                return;
            }

            MaxSdk.LoadRewardedAd(RewardedAdUnitId);
        }

        public void LoadCoinsRewarded() => LoadRewarded();
        public void LoadMultiplyRewarded() => LoadRewarded();

        public void ShowRewarded() => ShowRewardedForType(RewardAdType.GameReward);

        public void ShowRewardedForCoins() => ShowRewardedForType(RewardAdType.CoinsReward);

        public void ShowRewardedForMultiply() => ShowRewardedForType(RewardAdType.MultiplyReward);

        public void ShowRewardedForHint() => ShowRewardedForType(RewardAdType.HintReward);

        public void ShowRewardedForPlayOn() => ShowRewardedForType(RewardAdType.PlayOnReward);

        public void ShowRewardedForMagic() => ShowRewardedForType(RewardAdType.MagicReward);

        public void ShowRewardedForLife() => ShowRewardedForType(RewardAdType.LifeReward);

        public void ShowRewardedForShuffle() => ShowRewardedForType(RewardAdType.ShuffleReward);

        /// <summary>
        /// Shows the best ad for a user-initiated reward: interstitial when its eCPM beats rewarded
        /// (shorter ad, higher revenue), otherwise rewarded, with interstitial as a readiness fallback.
        /// Intentionally bypasses the No Ads gate — interstitials here are opt-in substitutes for rewarded.
        /// </summary>
        private void ShowRewardedForType(RewardAdType rewardType)
        {
            pendingRewardType = rewardType;
            StartCoroutine(ShowRewardedPlacementRoutine(rewardType));
        }

        private IEnumerator ShowRewardedPlacementRoutine(RewardAdType rewardType)
        {
            float deadline = Time.time + RewardedPlacementWaitSeconds;

            while (Time.time <= deadline)
            {
                RefreshAllReadiness();
                var choice = ResolveBestRewardAdChoice();
                if (choice != FullscreenAdChoice.None)
                {
                    ShowFullscreenAdChoice(choice, rewardType);
                    yield break;
                }

                EnsureFullscreenAdsLoaded();
                yield return new WaitForSeconds(0.5f);
            }

            Debug.LogWarning(
                $"[AdsManager] Rewarded placement timed out for {rewardType} — no interstitial or rewarded fill.");
            pendingRewardType = RewardAdType.None;

            if (!isInitialized && !isInitializing)
                _ = InitializeSDK();
        }

        /// <summary>
        /// Picks interstitial when it pays more and both are ready; otherwise rewarded, then interstitial fallback.
        /// </summary>
        private FullscreenAdChoice ResolveBestRewardAdChoice()
        {
            bool interstitialReady = IsInterstitialReady;
            bool rewardedReady = IsRewardedReady;

            if (AdMonetizationOptimizer.ShouldShowInterstitialInsteadOfRewarded(interstitialReady, rewardedReady))
                return FullscreenAdChoice.Interstitial;

            if (rewardedReady)
                return FullscreenAdChoice.Rewarded;

            if (interstitialReady)
                return FullscreenAdChoice.Interstitial;

            return FullscreenAdChoice.None;
        }

        private void ShowFullscreenAdChoice(FullscreenAdChoice choice, RewardAdType rewardType)
        {
            switch (choice)
            {
                case FullscreenAdChoice.Interstitial:
                    if (AdMonetizationOptimizer.ShouldShowInterstitialInsteadOfRewarded(
                            IsInterstitialReady, IsRewardedReady))
                    {
                        Debug.Log(
                            $"[AdsManager] Monetization optimizer: interstitial eCPM ${AdMonetizationOptimizer.InterstitialEcpm:F2} > " +
                            $"rewarded ${AdMonetizationOptimizer.RewardedEcpm:F2}. Showing interstitial for {rewardType}.");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[AdsManager] Rewarded not ready for {rewardType}. Falling back to interstitial.");
                    }

                    OnAdOpened?.Invoke();
                    ShowFullscreenAd(LiftEngineAdFormat.Interstitial);
                    break;

                case FullscreenAdChoice.Rewarded:
                    Debug.Log($"[AdsManager] Showing Rewarded Ad ({rewardType}).");
                    OnAdOpened?.Invoke();
                    ShowFullscreenAd(LiftEngineAdFormat.Rewarded);
                    break;
            }
        }

        private void ShowFullscreenAd(LiftEngineAdFormat format)
        {
            if (UseLiftEngineAdPath)
            {
                ShowLiftEngineAd(format);
                return;
            }

            if (!UseDirectMaxAdPath)
            {
                Debug.LogWarning($"[AdsManager] Skipping {format} show: LiftEngine init still in progress.");
                return;
            }

            EnsureFullscreenMaxCallbacks();
            if (format == LiftEngineAdFormat.Interstitial)
                MaxSdk.ShowInterstitial(InterstitialAdUnitId, BaseInterstitialPlacement);
            else
                MaxSdk.ShowRewardedAd(RewardedAdUnitId, BaseRewardedPlacement);
        }

        private void EnsureFullscreenAdsLoaded()
        {
            if (_liftEngineReady)
            {
                EnsureLiftEngineFormatLoaded(LiftEngineAdFormat.Interstitial, LoadInterstitial);
                EnsureLiftEngineFormatLoaded(LiftEngineAdFormat.Rewarded, LoadRewarded);
                return;
            }

            if (!_interstitialReady) LoadInterstitial();
            if (!_rewardedReady) LoadRewarded();
        }

        private void EnsureLiftEngineFormatLoaded(LiftEngineAdFormat format, Action loadAction)
        {
            if (LiftEngineSdk.IsAdReady(format))
                return;

            var state = LiftEngineSdk.GetPrewarmState(format);
            if (state == AdPrewarmState.Predicting || state == AdPrewarmState.Loading)
                return;

            loadAction?.Invoke();
        }

        #region Rewarded Callbacks

        private void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Loaded.");
            rewardedRetryAttempt = 0;
            AdMonetizationOptimizer.RecordRewardedAd(adInfo);
            RefreshRewardedReady();
        }

        private void OnRewardedAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            rewardedRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, rewardedRetryAttempt));
            Debug.LogWarning($"[AdsManager] Rewarded Ad Load Failed (code: {errorInfo.Code}). Retrying in {retryDelay}s...");
            _ = RetryLoadRewarded((int)(retryDelay * 1000));
        }

        private async Task RetryLoadRewarded(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_rewardedReady)
                EnqueueAction(LoadRewarded);
        }

        private void OnRewardedAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Displayed.");
            SetCachedReady(ref _rewardedReady, false);
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            Debug.LogError($"[AdsManager] Rewarded Ad Display Failed (code: {errorInfo.Code})");
            pendingRewardType = RewardAdType.None;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Closed. Preparing next ads.");
            lastAdShowTime = Time.time;
            SetCachedReady(ref _rewardedReady, false);
            NotifyAdClosed();
            PrepareAllAdsAfterClose();
        }

        private void OnRewardedAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Clicked.");
        }

        private void OnRewardedAdReceivedReward(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Rewarded Ad Rewarded Event Received.");
            ProcessPendingReward();
        }

        private void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdMonetizationOptimizer.RecordRewardedAd(adInfo);
            TrackAdRevenue(adInfo, "rewarded");
        }

        #endregion

        // ════════════════════════════════════════════
        //  BANNER ADS
        // ════════════════════════════════════════════

        private void SubscribeBannerCallbacks()
        {
            MaxSdkCallbacks.Banner.OnAdLoadedEvent -= OnBannerAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= OnBannerAdLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent -= OnBannerAdClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerAdRevenuePaid;

            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoaded;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdLoadFailed;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClicked;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaid;
        }

        private void InitializeBannerAds(bool requestedForShow = false)
        {
            if (!AreBannerAdsSupported) return;
            if (_bannerCreated || _bannerCreateInProgress) return;
            if (_bannerCreateCoroutine != null) return;

            CancelDeferredBannerDestroy();
            _bannerDestroyPending = false;
            _bannerCreateRequestedForShow = requestedForShow;

            _bannerCreateCoroutine = StartCoroutine(CreateBannerDeferred());
        }

        private void CancelDeferredBannerCreate()
        {
            if (_bannerCreateCoroutine == null) return;

            StopCoroutine(_bannerCreateCoroutine);
            _bannerCreateCoroutine = null;
            _bannerCreateInProgress = false;
            _bannerCreateRequestedForShow = false;
        }

        private IEnumerator CreateBannerDeferred()
        {
            _bannerCreateInProgress = true;

            // createAdView allocates on the Android UI thread; under heap pressure the main thread
            // can block in WaitForGcToComplete. Wait for frames + a settle delay before creating.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            float settleDelay = DevicePerformanceProfile.IsLowEnd
                ? BannerCreateSettleDelayLowEndSeconds
                : BannerCreateSettleDelaySeconds;
            if (settleDelay > 0f)
                yield return new WaitForSeconds(settleDelay);

            _bannerCreateCoroutine = null;

            if (this == null || _bannerCreated || !AreBannerAdsSupported || UserHasNoAds || !isInitialized)
            {
                _bannerCreateInProgress = false;
                yield break;
            }

            if (_applicationPaused)
            {
                _bannerCreateInProgress = false;
                yield break;
            }

            if (!IsBannerEnvironmentReady())
            {
                _bannerCreateInProgress = false;
                yield break;
            }

            if (_liftEngineEnabled && IsLiftEngineInitPending)
                yield return WaitForLiftEngineInitOrPermanentFailure(60f);

            try
            {
                if (UseLiftEngineAdPath)
                {
                    Debug.Log("[AdsManager] Loading Settings Banner via LiftEngine (predict + multipliers)...");
                    LiftEngineSdk.LoadAd(LiftEngineAdFormat.Banner);
                }
                else if (UseDirectMaxAdPath)
                {
                    SubscribeBannerCallbacks();
                    Debug.Log("[AdsManager] Creating Settings Banner Ad (direct MAX fallback)...");
                    MaxSdk.CreateBanner(BannerAdUnitId, MaxSdkBase.BannerPosition.BottomCenter);
                    MaxSdk.SetBannerPlacement(BannerAdUnitId, BaseBannerPlacement);
                    MaxSdk.SetBannerBackgroundColor(BannerAdUnitId, Color.clear);
                    _bannerCreated = true;
                }
                else
                {
                    Debug.Log("[AdsManager] Deferring banner create: LiftEngine init still in progress.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AdsManager] CreateBanner failed: {e.Message}");
                _bannerCreated = false;
            }
            finally
            {
                _bannerCreateInProgress = false;
                _bannerCreateRequestedForShow = false;
            }

            if (!UseDirectMaxAdPath)
                yield break;

            SyncBannerNativeState();
        }

        private IEnumerator WaitForLiftEngineInitOrPermanentFailure(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (IsLiftEngineInitPending && Time.realtimeSinceStartup < deadline)
                yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// Creates the banner ad view during idle time (hidden). Call from lobby after startup settles
        /// so game-over/settings only need ShowBanner, not createAdView on a heavy frame.
        /// </summary>
        public void PrewarmSettingsBanner()
        {
            LoadSettingsBanner();
        }

        public void LoadSettingsBanner()
        {
            if (!AreBannerAdsSupported || UserHasNoAds) return;

            if (!isInitialized)
            {
                if (!isInitializing) _ = InitializeSDK();
                return;
            }

            if (UseLiftEngineAdPath)
            {
                if (LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Banner))
                    return;

                Debug.Log("[AdsManager] Loading Settings Banner via LiftEngine...");
                LiftEngineSdk.LoadAd(LiftEngineAdFormat.Banner);
                return;
            }

            if (IsLiftEngineInitPending)
            {
                Debug.Log("[AdsManager] Skipping Settings Banner load: LiftEngine init still in progress.");
                return;
            }

            if (!_bannerCreated)
            {
                Debug.Log("[AdsManager] Creating Settings Banner Ad...");
                InitializeBannerAds();
            }
        }

        public void ShowSettingsBanner()
        {
            _bannerShowRequested = true;

            if (!isInitialized)
            {
                Debug.LogWarning("[AdsManager] Cannot show banner: SDK not initialized.");
                if (!isInitializing) _ = InitializeSDK();
                return;
            }

            if (!AreBannerAdsSupported)
            {
                Debug.Log("[AdsManager] Skipping Banner Show: System WebView is unavailable for banner ads.");
                return;
            }

            if (UserHasNoAds)
            {
                Debug.Log("[AdsManager] Skipping Banner Show: User has No Ads.");
                HideSettingsBanner();
                return;
            }

            Debug.Log("[AdsManager] Showing Settings Banner Ad.");
            SyncBannerNativeState();
        }

        public void HideSettingsBanner()
        {
            _bannerShowRequested = false;

            // User closed the screen before deferred create finished — don't allocate a native view.
            if (_bannerCreateInProgress && _bannerCreateRequestedForShow)
                CancelDeferredBannerCreate();

            if (_bannerCreated || _bannerCreateInProgress)
                Debug.Log("[AdsManager] Hiding Settings Banner Ad.");

            SyncBannerNativeState();
        }

        #region Banner Callbacks

        private void OnBannerAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Settings Banner Loaded.");
            _bannerRetryAttempt = 0;
            SetCachedReady(ref _bannerReady, true);
        }

        private void OnBannerAdLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            Debug.LogWarning($"[AdsManager] Settings Banner Load Failed (code: {errorInfo.Code}).");
            SetCachedReady(ref _bannerReady, false);
            _bannerRetryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _bannerRetryAttempt));
            _ = RetryPrepareBanner((int)(retryDelay * 1000));
        }

        private async Task RetryPrepareBanner(int delayMs)
        {
            await Task.Delay(delayMs);
            if (this != null && !_bannerReady)
                EnqueueAction(PrepareBannerAd);
        }

        private void OnBannerAdClicked(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            Debug.Log("[AdsManager] Settings Banner Clicked.");
        }

        private void OnBannerAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            TrackAdRevenue(adInfo, "banner");
        }

        #endregion

        // ════════════════════════════════════════════
        //  REWARD PROCESSING
        // ════════════════════════════════════════════

        /// <summary>
        /// Central reward processor. Captures and resets pendingRewardType atomically,
        /// then dispatches the correct reward. Safe if called multiple times — second call is a no-op.
        /// </summary>
        private void ProcessPendingReward()
        {
            RewardAdType rewardType = pendingRewardType;
            pendingRewardType = RewardAdType.None;

            if (rewardType != RewardAdType.None)
                Assets.Scripts.LiveOps.DailyMissionsLiveOpService.NotifyAdWatched();

            switch (rewardType)
            {
                case RewardAdType.GameReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: GameReward → firing OnRewardReceived.");
                    OnRewardReceived?.Invoke();
                    break;

                case RewardAdType.CoinsReward:
                    int rewardAmount = 2000;
                    if (RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsConfigReady)
                    {
                        rewardAmount = RemoteConfigManager.Instance.CoinsRewardedAd;
                    }
                    Debug.Log($"[AdsManager] ProcessPendingReward: CoinsReward → granting {rewardAmount} coins.");
                    if (UserDataManager.Instance != null)
                    {
                        UserDataManager.Instance.AddArrowsCurrency(rewardAmount, ResourceAnalyticsReasons.CoinsAdBtn);
                    }
                    OnCoinsRewardReceived?.Invoke();
                    StartCoroutine(SpawnCoinsExplosionDeferred());

                    if (FirebaseManager.Instance != null)
                    {
                        FirebaseManager.Instance.LogEvent("ad_reward_coins",
                            new Firebase.Analytics.Parameter("reward_amount", rewardAmount));
                    }
                    break;

                case RewardAdType.MultiplyReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: MultiplyReward → firing OnMultiplyRewardReceived.");
                    OnMultiplyRewardReceived?.Invoke();
                    break;

                case RewardAdType.HintReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: HintReward → firing OnHintRewardReceived.");
                    OnHintRewardReceived?.Invoke();
                    break;

                case RewardAdType.PlayOnReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: PlayOnReward → firing OnPlayOnRewardReceived.");
                    OnPlayOnRewardReceived?.Invoke();
                    break;

                case RewardAdType.MagicReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: MagicReward → firing OnMagicRewardReceived.");
                    OnMagicRewardReceived?.Invoke();
                    break;

                case RewardAdType.LifeReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: LifeReward → firing OnLifeRewardReceived.");
                    OnLifeRewardReceived?.Invoke();
                    break;

                case RewardAdType.ShuffleReward:
                    Debug.Log("[AdsManager] ProcessPendingReward: ShuffleReward → firing OnShuffleRewardReceived.");
                    OnShuffleRewardReceived?.Invoke();
                    break;

                default:
                    Debug.Log("[AdsManager] ProcessPendingReward: No pending reward (None). Ignoring.");
                    break;
            }
        }

        // ════════════════════════════════════════════
        //  LIFTENGINE
        // ════════════════════════════════════════════

        private void TryStartLiftEngine()
        {
            if (_liftEngineStartRequested)
                return;

            var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
            if (settings == null || string.IsNullOrWhiteSpace(settings.apiKey))
            {
                _liftEngineEnabled = false;
                Debug.Log("[AdsManager] LiftEngine settings missing or apiKey empty — using direct MAX for fullscreen ads.");
                return;
            }

            _liftEngineStartRequested = true;
            _liftEngineEnabled = true;
            _liftEngineSettings = settings;
            SubscribeLiftEngineCallbacks();
            LiftEngineSdk.Initialize(settings);
            ApplyLiftEngineContext();
            Debug.Log("[AdsManager] LiftEngine init requested (report/predict run inside SDK).");
        }

        private void CancelLiftEngineInitRetry()
        {
            if (_liftEngineInitRetryCoroutine == null)
                return;

            StopCoroutine(_liftEngineInitRetryCoroutine);
            _liftEngineInitRetryCoroutine = null;
        }

        private void ScheduleLiftEngineInitRetry()
        {
            CancelLiftEngineInitRetry();
            _liftEngineInitRetryCoroutine = StartCoroutine(RetryLiftEngineInitRoutine());
        }

        private IEnumerator RetryLiftEngineInitRoutine()
        {
            int maxAttempts = _liftEngineSettings != null ? _liftEngineSettings.maxInitRetryAttempts : 3;
            float baseDelay = _liftEngineSettings != null ? _liftEngineSettings.initRetryBaseDelaySeconds : 5f;
            double retryDelay = baseDelay * Math.Pow(2, Math.Min(4, _liftEngineInitRetryAttempt - 1));

            Debug.LogWarning(
                $"[AdsManager] LiftEngine init retry {_liftEngineInitRetryAttempt}/{maxAttempts - 1} in {retryDelay:F0}s.");

            yield return new WaitForSeconds((float)retryDelay);

            _liftEngineInitRetryCoroutine = null;

            if (!isInitialized || _liftEnginePermanentlyFailed || _liftEngineReady || _liftEngineSettings == null)
                yield break;

            _liftEngineInitSettled = false;
            LiftEngineSdk.Initialize(_liftEngineSettings);
            ApplyLiftEngineContext();
        }

        private void BeginDirectMaxFallback(string reason)
        {
            if (_liftEnginePermanentlyFailed)
                return;

            _liftEnginePermanentlyFailed = true;
            CancelLiftEngineInitRetry();
            Debug.LogWarning($"[AdsManager] {reason} — falling back to direct MAX for all ad formats.");
            EnsureFullscreenMaxCallbacks();
            LoadInterstitial();
            LoadRewarded();
            LoadSettingsBanner();
            RefreshAllReadiness();
        }

        private void ApplyLiftEngineContext()
        {
            if (!_liftEngineEnabled)
                return;

#if UNITY_IOS && !UNITY_EDITOR
            if (IOSAdsHelper.TryGetAttAuthorization(out bool isAuthorized))
                LiftEngineSdk.SetIdfaApproved(isAuthorized);
#endif
            ApplyLiftEngineAttributionFromSnapshot();
            LiftEngineSdk.SendReport();
        }

        private void ApplyLiftEngineAttributionFromSnapshot()
        {
            if (!_liftEngineEnabled)
                return;

            if (!SingularAttributionBridge.TryGetCachedSnapshot(out SingularAttributionSnapshot snapshot))
                return;

            string installType = IsOrganicAttribution(snapshot) ? "Organic" : "Non-organic";
            LiftEngineSdk.SetAttribution(installType, snapshot.Network);
        }

        private static bool IsOrganicAttribution(SingularAttributionSnapshot snapshot)
        {
            if (snapshot == null)
                return true;

            string network = snapshot.Network?.Trim();
            if (string.IsNullOrEmpty(network))
                return true;

            return network.Equals("Organic", StringComparison.OrdinalIgnoreCase);
        }

        public static void NotifyAttributionUpdated(SingularAttributionSnapshot snapshot)
        {
            if (Instance == null)
                return;

            Instance.EnqueueAction(() =>
            {
                Instance.ApplyLiftEngineAttributionFromSnapshot();
                LiftEngineSdk.SendReport();
            });
        }

        private void SubscribeLiftEngineCallbacks()
        {
            if (_liftEngineCallbacksSubscribed)
                return;

            LiftEngineSdkCallbacks.OnSdkInitializedEvent += OnLiftEngineSdkInitialized;
            LiftEngineSdkCallbacks.OnAdLoadedEvent += OnLiftEngineAdLoaded;
            LiftEngineSdkCallbacks.OnAdRevenuePaidEvent += OnLiftEngineAdRevenuePaid;
            LiftEngineSignalBus.AdReadyStateChanged += OnLiftEngineAdReadyStateChanged;
            _liftEngineCallbacksSubscribed = true;
        }

        private void UnsubscribeLiftEngineCallbacks()
        {
            if (!_liftEngineCallbacksSubscribed)
                return;

            LiftEngineSdkCallbacks.OnSdkInitializedEvent -= OnLiftEngineSdkInitialized;
            LiftEngineSdkCallbacks.OnAdLoadedEvent -= OnLiftEngineAdLoaded;
            LiftEngineSdkCallbacks.OnAdRevenuePaidEvent -= OnLiftEngineAdRevenuePaid;
            LiftEngineSignalBus.AdReadyStateChanged -= OnLiftEngineAdReadyStateChanged;
            _liftEngineCallbacksSubscribed = false;
        }

        private void OnLiftEngineAdReadyStateChanged(AdReadyStateChangedSignal signal)
        {
            if (!_liftEngineReady || signal == null)
                return;

            RefreshAllReadiness();
        }

        private void OnLiftEngineSdkInitialized(LiftEngineInitializationStatus status)
        {
            if (_liftEngineInitSettled)
            {
                Debug.Log("[AdsManager] LiftEngine init callback ignored (already handled).");
                return;
            }

            _liftEngineInitSettled = true;
            CancelLiftEngineInitRetry();

            if (status == LiftEngineInitializationStatus.Success)
            {
                _liftEngineReady = true;
                _liftEngineInitRetryAttempt = 0;
                Debug.Log("[AdsManager] LiftEngine SDK initialized — all ad formats routed through predict/track flow.");
                EnqueueAction(LoadSettingsBanner);
            }
            else
            {
                _liftEngineReady = false;
                _liftEngineInitRetryAttempt++;
                int maxAttempts = _liftEngineSettings != null ? _liftEngineSettings.maxInitRetryAttempts : 3;
                if (_liftEngineInitRetryAttempt < maxAttempts)
                {
                    ScheduleLiftEngineInitRetry();
                    RefreshAllReadiness();
                    return;
                }

                BeginDirectMaxFallback(
                    $"LiftEngine init failed after {_liftEngineInitRetryAttempt} attempt(s)");
                return;
            }

            RefreshAllReadiness();
        }

        private void OnLiftEngineAdLoaded(LiftEngineAdInfo info)
        {
            if (info == null)
                return;

            if (info.Format == LiftEngineAdFormat.Interstitial)
            {
                interstitialRetryAttempt = 0;
                AdMonetizationOptimizer.RecordInterstitialRevenue(info.Revenue);
            }
            else if (info.Format == LiftEngineAdFormat.Rewarded)
            {
                rewardedRetryAttempt = 0;
                AdMonetizationOptimizer.RecordRewardedRevenue(info.Revenue);
            }
            else if (info.Format == LiftEngineAdFormat.Banner)
            {
                _bannerRetryAttempt = 0;
                _bannerCreated = true;
                SetCachedReady(ref _bannerReady, true);
                if (_bannerShowRequested)
                    EnqueueAction(SyncBannerNativeState);
            }

            RefreshAllReadiness();
        }

        private void OnLiftEngineAdRevenuePaid(LiftEngineAdInfo info)
        {
            if (info == null)
                return;

            if (info.Format == LiftEngineAdFormat.Interstitial)
                AdMonetizationOptimizer.RecordInterstitialRevenue(info.Revenue);
            else if (info.Format == LiftEngineAdFormat.Rewarded)
                AdMonetizationOptimizer.RecordRewardedRevenue(info.Revenue);

            TrackLiftEngineAdRevenue(info);
        }

        private void ShowLiftEngineAd(LiftEngineAdFormat format)
        {
            LiftEngineSdk.ShowAd(format, null, new LiftEngineShowAdCallbacks
            {
                OnAdDisplayed = () => HandleLiftEngineAdDisplayed(format),
                OnAdHidden = () => HandleLiftEngineAdHidden(format),
                OnAdDisplayFailed = message => HandleLiftEngineAdDisplayFailed(format, message),
                OnAdRewarded = () => HandleLiftEngineAdRewarded(format),
                OnAdClicked = () => Debug.Log($"[AdsManager] {format} ad clicked (LiftEngine).")
            });
        }

        private void HandleLiftEngineAdRewarded(LiftEngineAdFormat format)
        {
            if (format != LiftEngineAdFormat.Rewarded || pendingRewardType == RewardAdType.None)
                return;

            Debug.Log($"[AdsManager] Rewarded ad completed for {pendingRewardType} (LiftEngine).");
            ProcessPendingReward();
        }

        private void HandleLiftEngineAdDisplayed(LiftEngineAdFormat format)
        {
            Debug.Log($"[AdsManager] {format} displayed (LiftEngine).");
            if (format == LiftEngineAdFormat.Interstitial)
                SetCachedReady(ref _interstitialReady, false);
            else if (format == LiftEngineAdFormat.Rewarded)
                SetCachedReady(ref _rewardedReady, false);

            PrepareAllAdsAfterClose();
        }

        private void HandleLiftEngineAdHidden(LiftEngineAdFormat format)
        {
            Debug.Log($"[AdsManager] {format} hidden (LiftEngine).");
            lastAdShowTime = Time.time;

            if (format == LiftEngineAdFormat.Interstitial)
                SetCachedReady(ref _interstitialReady, false);
            else if (format == LiftEngineAdFormat.Rewarded)
                SetCachedReady(ref _rewardedReady, false);

            NotifyAdClosed();

            if (pendingRewardType != RewardAdType.None &&
                format == LiftEngineAdFormat.Interstitial)
            {
                Debug.Log(
                    $"[AdsManager] Interstitial fulfilled rewarded placement ({pendingRewardType}). Granting reward.");
                ProcessPendingReward();
            }

            PrepareAllAdsAfterClose();
            RefreshAllReadiness();
        }

        private void HandleLiftEngineAdDisplayFailed(LiftEngineAdFormat format, string message)
        {
            Debug.LogError($"[AdsManager] {format} display failed (LiftEngine): {message}");

            if (format == LiftEngineAdFormat.Rewarded && pendingRewardType != RewardAdType.None)
            {
                RefreshAllReadiness();
                if (IsInterstitialReady)
                {
                    Debug.LogWarning(
                        $"[AdsManager] Rewarded display failed ({message}); falling back to interstitial for pending reward.");
                    ShowLiftEngineAd(LiftEngineAdFormat.Interstitial);
                    return;
                }
            }

            if (format == LiftEngineAdFormat.Interstitial)
                SetCachedReady(ref _interstitialReady, false);
            else if (format == LiftEngineAdFormat.Rewarded)
            {
                pendingRewardType = RewardAdType.None;
                SetCachedReady(ref _rewardedReady, false);
            }

            NotifyAdClosed();
            PrepareAllAdsAfterClose();
            RefreshAllReadiness();
        }

        private void TrackLiftEngineAdRevenue(LiftEngineAdInfo info)
        {
            if (info == null || info.Revenue <= 0)
                return;

            string adFormat = info.Format switch
            {
                LiftEngineAdFormat.Interstitial => "interstitial",
                LiftEngineAdFormat.Rewarded => "rewarded",
                LiftEngineAdFormat.Banner => "banner",
                _ => "unknown"
            };

            var payload = new SingularAdRevenuePayload(
                info.Revenue,
                info.NetworkName,
                info.AdUnitId,
                adFormat,
                string.Empty);

            EnqueueAction(() => ReportAdRevenueOnMainThread(payload));
        }

        // ════════════════════════════════════════════
        //  REVENUE TRACKING (Firebase + Singular)
        // ════════════════════════════════════════════

        /// <summary>
        /// Sends AppLovin MAX impression-level revenue (ILRD) to Firebase and Singular.
        /// MAX fires OnAdRevenuePaidEvent off the main thread unless InvokeEventsOnUnityMainThread is set.
        /// </summary>
        private void TrackAdRevenue(MaxSdkBase.AdInfo adInfo, string adFormat)
        {
            if (adInfo == null)
            {
                Debug.LogWarning("[AdsManager] TrackAdRevenue: adInfo is null");
                return;
            }

            double revenueUsd = adInfo.Revenue;
            string resolvedFormat = string.IsNullOrEmpty(adInfo.AdFormat) ? adFormat : adInfo.AdFormat;

            if (revenueUsd <= 0)
            {
                Debug.LogWarning(
                    $"[AdsManager] TrackAdRevenue: no revenue (network={adInfo.NetworkName}, " +
                    $"unit={adInfo.AdUnitIdentifier}, format={resolvedFormat}, precision={adInfo.RevenuePrecision}). " +
                    "ILRD is delivered via OnAdRevenuePaidEvent when MAX has revenue data—common causes: test ads, " +
                    "revenue=-1 (error), or network not fully configured in MAX Mediation.");
                return;
            }

            var payload = new SingularAdRevenuePayload(
                revenueUsd,
                adInfo.NetworkName,
                adInfo.AdUnitIdentifier,
                resolvedFormat,
                adInfo.RevenuePrecision);

            EnqueueAction(() => ReportAdRevenueOnMainThread(payload));
        }

        private void ReportAdRevenueOnMainThread(SingularAdRevenuePayload payload)
        {
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_AD_IMPRESSION,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_PLATFORM, "AppLovinMAX"),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_SOURCE, payload.NetworkName),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_UNIT_NAME, payload.AdUnitId),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_AD_FORMAT, payload.AdFormat),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, payload.RevenueUsd),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, "USD"));
            }

#if !UNITY_EDITOR
            if (!SingularSDK.Initialized)
            {
                _pendingSingularRevenue.Enqueue(payload);
                EnsurePendingSingularRevenueFlushScheduled();
                Debug.LogWarning(
                    $"[AdsManager] Singular not initialized yet; queued ad revenue ${payload.RevenueUsd:F6} " +
                    $"(network={payload.NetworkName}, format={payload.AdFormat}).");
                return;
            }

            SendSingularAdRevenue(payload);
#endif

            Debug.Log(
                $"[AdsManager] Ad Revenue: ${payload.RevenueUsd:F6} USD, network={payload.NetworkName}, " +
                $"format={payload.AdFormat}, precision={payload.RevenuePrecision}");
        }

        private void SendSingularAdRevenue(SingularAdRevenuePayload payload)
        {
            SingularAdData singularAdData = new SingularAdData("AppLovin", "USD", payload.RevenueUsd);
            singularAdData.WithNetworkName(payload.NetworkName)
                          .WithAdUnitName(payload.AdUnitId)
                          .WithAdType(payload.AdFormat)
                          .WithPrecision(payload.RevenuePrecision);

            if (!singularAdData.HasRequiredParams())
            {
                Debug.LogWarning(
                    "[AdsManager] Singular ad revenue skipped: missing required params " +
                    $"(network={payload.NetworkName}, unit={payload.AdUnitId}, revenue={payload.RevenueUsd}).");
                return;
            }

            SingularSDK.AdRevenue(singularAdData);
            Debug.Log(
                $"[AdsManager] Singular ad revenue sent: ${payload.RevenueUsd:F6} USD, " +
                $"network={payload.NetworkName}, format={payload.AdFormat}");
        }

        /// <summary>Called when Singular SDK becomes ready (auto-init with ATT wait).</summary>
        public static void NotifySingularSdkInitialized()
        {
            if (Instance == null)
                return;

            Instance.EnqueueAction(Instance.FlushPendingSingularRevenueIfReady);
        }

        private void EnsurePendingSingularRevenueFlushScheduled()
        {
            if (_isFlushingPendingSingularRevenue)
                return;

            _isFlushingPendingSingularRevenue = true;
            StartCoroutine(FlushPendingSingularRevenueWhenReady());
        }

        private IEnumerator FlushPendingSingularRevenueWhenReady()
        {
            const float timeoutSeconds = 120f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!SingularSDK.Initialized && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!SingularSDK.Initialized)
            {
                Debug.LogWarning(
                    $"[AdsManager] Singular still not initialized after {timeoutSeconds}s; " +
                    $"dropping {_pendingSingularRevenue.Count} queued ad revenue event(s).");
                while (_pendingSingularRevenue.TryDequeue(out _)) { }
                _isFlushingPendingSingularRevenue = false;
                yield break;
            }

            FlushPendingSingularRevenueIfReady();
            _isFlushingPendingSingularRevenue = false;
        }

        private void FlushPendingSingularRevenueIfReady()
        {
#if !UNITY_EDITOR
            if (!SingularSDK.Initialized)
                return;

            int flushedCount = 0;
            while (_pendingSingularRevenue.TryDequeue(out SingularAdRevenuePayload payload))
            {
                SendSingularAdRevenue(payload);
                flushedCount++;
            }

            if (flushedCount > 0)
                Debug.Log($"[AdsManager] Flushed {flushedCount} queued Singular ad revenue event(s).");
#endif
        }

        // ════════════════════════════════════════════
        //  VFX
        // ════════════════════════════════════════════

        public void SpawnCoinsSmallExplosion()
        {
            StartCoroutine(SpawnCoinsExplosionDeferred());
        }

        private IEnumerator SpawnCoinsExplosionDeferred()
        {
            yield return null;
            yield return null;

            if (UserDataManager.Instance != null && UserDataManager.Instance.CurrentLevel < 11) yield break;

            GameObject prefab = _coinsExplosionPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("CoinsSmallExplosion");
                _coinsExplosionPrefab = prefab;
            }

            if (prefab != null)
            {
                Vector3 spawnPos = new Vector3(-0.5f, 2.4f, 60.2f);
                GameObject explosion = Instantiate(prefab, spawnPos, prefab.transform.rotation);
                Destroy(explosion, 3.0f);
            }
            else
            {
                Debug.LogWarning("[AdsManager] CoinsSmallExplosion prefab not found in Resources.");
            }
        }
    }
}
