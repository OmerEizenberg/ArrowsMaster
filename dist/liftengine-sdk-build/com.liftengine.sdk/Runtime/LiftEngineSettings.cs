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

        [Tooltip("Used only when Environment is Custom. Base URL for LiftEngine API services.")]
        public string customApiBaseUrl = "https://api-stg.liftengine.ai/";

        [Tooltip("Bearer token for LiftEngine API. NOT the AppLovin MAX SDK key.")]
        public string apiKey = "";

        [Header("Mediation")]
        [Tooltip("Mediation SDK used to load and show ads. AppLovin MAX must already be initialized by your game before LiftEngineSdk.Initialize().")]
        public LiftEngineMediationPlatform mediationPlatform = LiftEngineMediationPlatform.AppLovinMax;

        [Header("MAX Ad Unit IDs — iOS")]
        [Tooltip("AppLovin MAX banner ad unit ID for iOS.")]
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

        internal string GetModelName(LiftEngineAdFormat format)
        {
            return format switch
            {
                LiftEngineAdFormat.Banner => "banner",
                LiftEngineAdFormat.Interstitial => "interstitial",
                LiftEngineAdFormat.Rewarded => "rewarded",
                _ => "interstitial"
            };
        }

        internal string[] GetAllModelNames()
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
