using UnityEngine;

namespace LiftEngine
{
    public static class LiftEngineSdk
    {
        private static LiftEngineController _controller;

        public static bool IsInitialized => _controller != null && _controller.IsInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeIfEnabled()
        {
            var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
            if (settings != null && settings.autoInitialize)
                Initialize(settings);
        }

        public static void Initialize()
        {
            var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
            if (settings == null)
            {
                LiftEngineLogger.LogError(
                    $"Missing {LiftEngineSettings.DefaultResourcePath} in Resources. " +
                    "Open Window > LiftEngine > Integration Manager to create it.");
                LiftEngineSdkCallbacks.RaiseInitialized(LiftEngineInitializationStatus.Failed);
                return;
            }

            Initialize(settings);
        }

        public static void Initialize(LiftEngineSettings settings)
        {
            if (_controller != null && _controller.IsInitialized)
                return;

            _controller = new LiftEngineController();
            LiftEngineHost.Instance.AttachController(_controller);
            _controller.Initialize(settings, LiftEngineHost.Instance);
        }

        public static void CheckHealth(System.Action<bool> callback) =>
            CheckHealth((ok, _) => callback?.Invoke(ok));

        public static void CheckHealth(System.Action<bool, string> callback)
        {
            if (_controller != null)
            {
                _controller.CheckHealth(callback);
                return;
            }

            var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
            if (settings == null)
            {
                callback?.Invoke(false, "LiftEngineSettings not found in Resources.");
                return;
            }

            var host = LiftEngineHost.Instance;
            var client = new Api.LiftEngineApiClient(settings, host);
            client.CheckHealth(callback);
        }

        public static void SetAttribution(string appsFlyerInstallType, string mediaSource) =>
            _controller?.SetAttribution(appsFlyerInstallType, mediaSource);

        public static void SetIdfaApproved(bool approved) =>
            _controller?.SetIdfaApproved(approved);

        public static void NotifyPurchase(float amountUsd) =>
            _controller?.NotifyPurchase(amountUsd);

        public static void LoadAd(LiftEngineAdFormat format) =>
            _controller?.LoadAd(format);

        public static bool IsAdReady(LiftEngineAdFormat format) =>
            _controller != null && _controller.IsAdReady(format);

        public static AdPrewarmState GetPrewarmState(LiftEngineAdFormat format) =>
            _controller?.GetPrewarmState(format) ?? AdPrewarmState.Idle;

        public static void ShowAd(LiftEngineAdFormat format, LiftEngineShowAdParams parameters = null,
            LiftEngineShowAdCallbacks callbacks = null) =>
            _controller?.ShowAd(format, parameters, callbacks);

        public static void HideBanner() => _controller?.HideBanner();

        public static void DestroyBanner() => _controller?.DestroyBanner();

        public static void ClearDebugContext() => _controller?.ClearDebugContext();

        public static void SetVerboseLogging(bool enabled) => LiftEngineLogger.SetVerbose(enabled);

        internal static LiftEngineController Controller => _controller;
    }
}
