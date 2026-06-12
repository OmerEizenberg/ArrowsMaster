using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Guards banner ads on Android devices with a pre-2019 System WebView (major &lt; 73),
    /// which can crash when WebResourceResponse receives an empty HTTP/2 reason phrase.
    /// </summary>
    public static class AndroidWebViewSupport
    {
        public const int MinimumSupportedWebViewMajorVersion = 73;

        private static bool? _bannerAdsSupported;

        public static bool AreBannerAdsSupported
        {
            get
            {
                if (!_bannerAdsSupported.HasValue)
                    _bannerAdsSupported = EvaluateBannerSupport();
                return _bannerAdsSupported.Value;
            }
        }

        private static bool EvaluateBannerSupport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            int? majorVersion = TryGetInstalledWebViewMajorVersion();
            if (!majorVersion.HasValue)
            {
                Debug.LogWarning("[AndroidWebViewSupport] Could not determine System WebView version; allowing banner ads.");
                return true;
            }

            if (majorVersion.Value >= MinimumSupportedWebViewMajorVersion)
                return true;

            Debug.LogWarning(
                $"[AndroidWebViewSupport] System WebView {majorVersion.Value} is below " +
                $"{MinimumSupportedWebViewMajorVersion}; banner ads disabled on this device.");
            return false;
#else
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int? TryGetInstalledWebViewMajorVersion()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                        return null;

                    string versionName = TryGetWebViewVersionViaWebViewCompat(activity);
                    if (string.IsNullOrEmpty(versionName))
                        versionName = TryGetWebViewVersionViaPackageManager(activity);

                    return ParseMajorVersion(versionName);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AndroidWebViewSupport] WebView version lookup failed: {e.Message}");
                return null;
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
                return null;
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
