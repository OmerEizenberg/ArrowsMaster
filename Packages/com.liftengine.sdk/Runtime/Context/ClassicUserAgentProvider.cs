using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LiftEngine.Context
{
    /// <summary>
    /// Browser-style User-Agent strings (WebView / mobile Safari) for LiftEngine API requests.
    /// </summary>
    internal static class ClassicUserAgentProvider
    {
        // Kept in sync with a recent stable mobile Chrome WebView — update occasionally.
        private const string ChromeVersion = "131.0.6778.135";

        public static string Build()
        {
            return DeviceOsProvider.GetOs() switch
            {
                "ios" => BuildIos(),
                "android" => BuildAndroid(),
                _ => BuildAndroid()
            };
        }

        private static string BuildAndroid()
        {
            var androidVersion = GetAndroidReleaseVersion();
            var device = GetAndroidDeviceToken();
            return "Mozilla/5.0 (Linux; Android " + androidVersion + "; " + device + "; wv) " +
                   "AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/" + ChromeVersion +
                   " Mobile Safari/537.36";
        }

        private static string BuildIos()
        {
            var deviceType = GetIosDeviceType();
            var iosVersion = GetIosVersionUnderscore();
            var safariVersion = iosVersion.Replace('_', '.');
            var cpuSegment = deviceType == "iPad"
                ? "CPU OS " + iosVersion
                : "CPU iPhone OS " + iosVersion;

            return "Mozilla/5.0 (" + deviceType + "; " + cpuSegment + " like Mac OS X) " +
                   "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/" + safariVersion +
                   " Mobile/15E148 Safari/604.1";
        }

        private static string GetAndroidReleaseVersion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                var release = version.GetStatic<string>("RELEASE");
                if (!string.IsNullOrWhiteSpace(release))
                    return release;
            }
            catch
            {
                // Fall through to SystemInfo parsing.
            }
#endif
            return ParseAndroidFromSystemInfo();
        }

        private static string GetAndroidDeviceToken()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var build = new AndroidJavaClass("android.os.Build");
                var model = build.GetStatic<string>("MODEL");
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
            catch
            {
                // Fall through.
            }
#endif
            var fallback = SystemInfo.deviceModel;
            return string.IsNullOrWhiteSpace(fallback) ? "Mobile" : fallback;
        }

        private static string ParseAndroidFromSystemInfo()
        {
            var os = SystemInfo.operatingSystem ?? string.Empty;
            var match = Regex.Match(os, @"Android OS (\d+(?:\.\d+)?)");
            if (match.Success)
                return match.Groups[1].Value;

            match = Regex.Match(os, @"Android (\d+(?:\.\d+)?)");
            if (match.Success)
                return match.Groups[1].Value;

            return "10";
        }

        private static string GetIosDeviceType()
        {
            var model = SystemInfo.deviceModel ?? string.Empty;
            return model.StartsWith("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad" : "iPhone";
        }

        private static string GetIosVersionUnderscore()
        {
            var os = SystemInfo.operatingSystem ?? string.Empty;
            var match = Regex.Match(os, @"(?:iOS|iPadOS|iPhone OS)\s+(\d+(?:\.\d+)*)");
            if (match.Success)
                return match.Groups[1].Value.Replace('.', '_');

            return "17_0";
        }
    }
}
