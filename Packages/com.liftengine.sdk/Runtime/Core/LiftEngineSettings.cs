using UnityEngine;

namespace LiftEngine
{
    [CreateAssetMenu(fileName = "LiftEngineSettings", menuName = "LiftEngine/Settings")]
    public class LiftEngineSettings : ScriptableObject
    {
        public const string DefaultResourcePath = "LiftEngineSettings";

        [Header("LiftEngine API")]
        [Tooltip("Target LiftEngine backend. Staging for testing, Production for live traffic.")]
        public LiftEngineEnvironment environment = LiftEngineEnvironment.Staging;

        [Tooltip("Used only when Environment is Custom. Base URL for predict/report/track (e.g. https://api-stg.liftengine.ai/).")]
        public string customApiBaseUrl = "https://api-stg.liftengine.ai/";

        [Tooltip("Bearer token for LiftEngine API (predict, report, track). NOT the AppLovin MAX SDK key. Staging mock often accepts any non-empty value such as test-api-key.")]
        public string apiKey = "";

        [Header("Mediation")]
        [Tooltip("Mediation SDK used to load and show ads. AppLovin MAX must already be initialized by your game before LiftEngineSdk.Initialize().")]
        public LiftEngineMediationPlatform mediationPlatform = LiftEngineMediationPlatform.AppLovinMax;

        [Header("MAX Ad Unit IDs — iOS")]
        [Tooltip("AppLovin MAX banner ad unit ID for iOS. Required to load/show banners via MAX.")]
        public string iosBannerAdUnitId = "";

        [Tooltip("AppLovin MAX interstitial ad unit ID for iOS.")]
        public string iosInterstitialAdUnitId = "";

        [Tooltip("AppLovin MAX rewarded ad unit ID for iOS.")]
        public string iosRewardedAdUnitId = "";

        [Header("MAX Ad Unit IDs — Android")]
        [Tooltip("AppLovin MAX banner ad unit ID for Android.")]
        public string androidBannerAdUnitId = "";

        [Tooltip("AppLovin MAX interstitial ad unit ID for Android.")]
        public string androidInterstitialAdUnitId = "";

        [Tooltip("AppLovin MAX rewarded ad unit ID for Android.")]
        public string androidRewardedAdUnitId = "";

        [Header("LiftEngine Placement IDs (track API only)")]
        [Tooltip("Logical placement name sent to LiftEngine track endpoints (/v1/track/*). NOT a MAX setting. Identifies where in your app the ad was shown (e.g. shop_banner).")]
        public string bannerPlacementId = "banner-default";

        [Tooltip("LiftEngine track placement ID for interstitial impressions.")]
        public string interstitialPlacementId = "interstitial-default";

        [Tooltip("LiftEngine track placement ID for rewarded impressions.")]
        public string rewardedPlacementId = "rewarded-default";

        [Header("Predict & Load")]
        [Tooltip("Max seconds to wait for LiftEngine predict HTTP response before failing over to bid-floor fallback.")]
        public float predictTimeoutSeconds = 8f;

        [Tooltip("Base eCPM prediction used when the API response omits the prediction field (e.g. staging mock). Bid floor = this value × each multiplier.")]
        public float defaultPredictionFallback = 1f;

        [Tooltip("Unity Editor MAX mock ads load with revenue=-1. Treat IsReady as fill during multiplier phase so [Attempt 0] can succeed.")]
        public bool treatEditorLoadAsFilledForMultiplierPhase = true;

        [Tooltip("Max seconds to wait for MAX to finish a single load attempt during multiplier / bid-0 waterfall.")]
        public float loadAttemptTimeoutSeconds = 15f;

        [Header("Prewarm")]
        [Tooltip("After LiftEngine init, automatically predict + load all ad types in the background.")]
        public bool prewarmOnInit = true;

        [Tooltip("After an ad is dismissed, immediately start the next predict + load cycle for that type.")]
        public bool prewarmAfterShow = true;

        [Tooltip("Seconds between readiness checks while waiting for an ad load to complete.")]
        public float readinessCheckIntervalSeconds = 2f;

        [Tooltip("Max seconds for a full predict+load prewarm cycle before marking the format as failed.")]
        public float prewarmMaxDurationSeconds = 90f;

        [Tooltip("Seconds between automatic retries when a format did not get a fill.")]
        public float prewarmRetryIntervalSeconds = 20f;

        [Tooltip("Max bid-0 load rounds per prewarm before giving up until the next retry.")]
        public int maxBidZeroRounds = 5;

        [Tooltip("Max seconds ShowAd waits for a fill before reporting display failed.")]
        public float showWaitMaxSeconds = 30f;

        [Header("Runtime")]
        [Tooltip("If enabled, LiftEngineSdk.Initialize() runs automatically at app start. Usually leave off and call Initialize from AdsManager after consent.")]
        public bool autoInitialize = false;

        [Tooltip("Log detailed [LiftEngine] messages to the Unity console.")]
        public bool verboseLogging = false;

        [Tooltip("Unlocks the Debug tab tools (API ping, show ad, simulate purchase, etc.).")]
        public bool debugMode = false;

        public string ApiBaseUrl
        {
            get
            {
                return environment switch
                {
                    LiftEngineEnvironment.Staging => "https://api-stg.liftengine.ai/",
                    LiftEngineEnvironment.Production => "https://api.liftengine.ai/",
                    _ => string.IsNullOrEmpty(customApiBaseUrl) ? "https://api-stg.liftengine.ai/" : customApiBaseUrl
                };
            }
        }

        public string GetAdUnitId(LiftEngineAdFormat format)
        {
#if UNITY_IOS || UNITY_IPHONE
            return format switch
            {
                LiftEngineAdFormat.Banner => iosBannerAdUnitId,
                LiftEngineAdFormat.Interstitial => iosInterstitialAdUnitId,
                LiftEngineAdFormat.Rewarded => iosRewardedAdUnitId,
                _ => string.Empty
            };
#else
            return format switch
            {
                LiftEngineAdFormat.Banner => androidBannerAdUnitId,
                LiftEngineAdFormat.Interstitial => androidInterstitialAdUnitId,
                LiftEngineAdFormat.Rewarded => androidRewardedAdUnitId,
                _ => string.Empty
            };
#endif
        }

        public string GetPlacementId(LiftEngineAdFormat format)
        {
            return format switch
            {
                LiftEngineAdFormat.Banner => bannerPlacementId,
                LiftEngineAdFormat.Interstitial => interstitialPlacementId,
                LiftEngineAdFormat.Rewarded => rewardedPlacementId,
                _ => string.Empty
            };
        }

        public string GetModelName(LiftEngineAdFormat format)
        {
            return format switch
            {
                LiftEngineAdFormat.Banner => "banner",
                LiftEngineAdFormat.Interstitial => "interstitial",
                LiftEngineAdFormat.Rewarded => "rewarded",
                _ => "interstitial"
            };
        }

        public string[] GetAllModelNames()
        {
            return new[]
            {
                GetModelName(LiftEngineAdFormat.Banner),
                GetModelName(LiftEngineAdFormat.Interstitial),
                GetModelName(LiftEngineAdFormat.Rewarded)
            };
        }
    }
}
