using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Guards banner ads on Android devices where System WebView is missing, too old, or fails to
    /// initialize. AppLovin banner fills create an AppLovinAdView backed by WebView; a broken
    /// provider can crash inside Chromium AwContents during first init.
    /// </summary>
    public static class AndroidWebViewSupport
    {
        public const int MinimumSupportedWebViewMajorVersion = 73;

        private static bool? _bannerAdsSupported;
        private static bool _webViewPrewarmAttempted;
        private static bool _webViewPrewarmSucceeded;

        public static bool AreBannerAdsSupported
        {
            get
            {
                EnsureBannerSupportEvaluated();
                return _bannerAdsSupported.Value;
            }
        }

        /// <summary>
        /// Validates WebView support and performs a one-time create/destroy prewarm before the first
        /// banner ad touches native WebView code.
        /// </summary>
        public static bool EnsureWebViewReady()
        {
            EnsureBannerSupportEvaluated();
            if (!_bannerAdsSupported.Value)
                return false;

            if (_webViewPrewarmSucceeded)
                return true;

            if (_webViewPrewarmAttempted)
                return false;

            _webViewPrewarmAttempted = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            _webViewPrewarmSucceeded = TryPrewarmWebView();
            if (!_webViewPrewarmSucceeded)
                _bannerAdsSupported = false;

            return _webViewPrewarmSucceeded;
#else
            _webViewPrewarmSucceeded = true;
            return true;
#endif
        }

        private static void EnsureBannerSupportEvaluated()
        {
            if (!_bannerAdsSupported.HasValue)
                _bannerAdsSupported = EvaluateBannerSupport();
        }

        private static bool EvaluateBannerSupport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                    {
                        Debug.LogWarning("[AndroidWebViewSupport] Activity unavailable; banner ads disabled.");
                        return false;
                    }

                    string versionName = TryGetWebViewVersionViaWebViewCompat(activity);
                    if (string.IsNullOrEmpty(versionName))
                    {
                        Debug.LogWarning(
                            "[AndroidWebViewSupport] System WebView provider not available; banner ads disabled.");
                        return false;
                    }

                    int? majorVersion = ParseMajorVersion(versionName);
                    if (!majorVersion.HasValue)
                    {
                        Debug.LogWarning(
                            $"[AndroidWebViewSupport] Could not parse WebView version '{versionName}'; banner ads disabled.");
                        return false;
                    }

                    if (majorVersion.Value < MinimumSupportedWebViewMajorVersion)
                    {
                        Debug.LogWarning(
                            $"[AndroidWebViewSupport] System WebView {majorVersion.Value} is below " +
                            $"{MinimumSupportedWebViewMajorVersion}; banner ads disabled on this device.");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AndroidWebViewSupport] Banner support evaluation failed: {e.Message}");
                return false;
            }
#else
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TryPrewarmWebView()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                        return false;

                    // JNI from Unity can run on the render thread (nativeRender). WebView must be
                    // created on the Android UI thread — delegate to a Java helper that uses runOnUiThread.
                    using (var helper = new AndroidJavaClass("com.everybodygames.arrowsmaster.WebViewPrewarmHelper"))
                    {
                        bool prewarmed = helper.CallStatic<bool>("prewarm", activity);
                        if (prewarmed)
                            Debug.Log("[AndroidWebViewSupport] WebView prewarm succeeded.");
                        else
                            Debug.LogWarning("[AndroidWebViewSupport] WebView prewarm failed; banner ads disabled.");

                        return prewarmed;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AndroidWebViewSupport] WebView prewarm failed; banner ads disabled: {e.Message}");
                return false;
            }
        }

        private static string TryGetWebViewVersionViaWebViewCompat(AndroidJavaObject activity)
        {
            try
            {
                using (var webViewCompat = new AndroidJavaClass("androidx.webkit.WebViewCompat"))
                using (var packageInfo = webViewCompat.CallStatic<AndroidJavaObject>("getCurrentWebViewPackage", activity))
                {
                    return packageInfo?.Get<string>("versionName");
                }
            }
            catch (Exception)
            {
                return TryGetWebViewVersionViaPackageManager(activity);
            }
        }

        private static string TryGetWebViewVersionViaPackageManager(AndroidJavaObject activity)
        {
            string[] packageNames = { "com.google.android.webview", "com.android.webview" };

            try
            {
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    foreach (string packageName in packageNames)
                    {
                        try
                        {
                            using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0))
                            {
                                string versionName = packageInfo?.Get<string>("versionName");
                                if (!string.IsNullOrEmpty(versionName))
                                    return versionName;
                            }
                        }
                        catch (Exception)
                        {
                            // Package not installed on this device.
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        private static int? ParseMajorVersion(string versionName)
        {
            if (string.IsNullOrEmpty(versionName))
                return null;

            int dotIndex = versionName.IndexOf('.');
            string majorPart = dotIndex >= 0 ? versionName.Substring(0, dotIndex) : versionName;
            return int.TryParse(majorPart, out int majorVersion) ? majorVersion : (int?)null;
        }
#endif
    }
}
