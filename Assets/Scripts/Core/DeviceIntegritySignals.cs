using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Collects device/install integrity signals for Firebase Analytics user properties only.
    /// Does not gate ads, init, or any gameplay logic.
    /// </summary>
    public static class DeviceIntegritySignals
    {
        public static void ReportToFirebase(FirebaseManager firebase)
        {
            if (firebase == null)
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            ReportAndroid(firebase);
#elif UNITY_IOS && !UNITY_EDITOR
            firebase.SetUserProperty("device_class", "physical");
            firebase.SetUserProperty("install_source", "app_store");
#else
            firebase.SetUserProperty("device_class", "editor");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void ReportAndroid(FirebaseManager firebase)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                    {
                        firebase.SetUserProperty("device_class", "unknown");
                        return;
                    }

                    string installSource = ResolveInstallSource(activity);
                    string deviceClass = ResolveDeviceClass();

                    firebase.SetUserProperty("install_source", installSource);
                    firebase.SetUserProperty("play_services", ResolvePlayServices(activity));
                    firebase.SetUserProperty("ad_id_status", ResolveAdvertisingIdStatus(activity));
                    firebase.SetUserProperty("device_class", deviceClass);
                    firebase.SetUserProperty("device_brand", ResolveDeviceBrand());
                    firebase.SetUserProperty("debug_build", Debug.isDebugBuild ? "yes" : "no");

                    Debug.Log(
                        "[DeviceIntegritySignals] Firebase user properties updated " +
                        $"(install={installSource}, class={deviceClass}).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DeviceIntegritySignals] Failed to collect signals: {e.Message}");
                firebase.SetUserProperty("device_class", "collect_error");
            }
        }

        private static string ResolveInstallSource(AndroidJavaObject activity)
        {
            try
            {
                string packageName = activity.Call<string>("getPackageName");
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    if (pm == null)
                        return "unknown";

                    string installer = pm.Call<string>("getInstallerPackageName", packageName);
                    if (string.IsNullOrEmpty(installer))
                        return "sideload";

                    if (installer == "com.android.vending")
                        return "play_store";

                    if (installer.Contains("packageinstaller") || installer.Contains("installer"))
                        return "sideload";

                    return "other_store";
                }
            }
            catch
            {
                return "unknown";
            }
        }

        private static string ResolvePlayServices(AndroidJavaObject activity)
        {
            try
            {
                using (var availability = new AndroidJavaClass("com.google.android.gms.common.GoogleApiAvailability"))
                {
                    var instance = availability.CallStatic<AndroidJavaObject>("getInstance");
                    if (instance == null)
                        return "no";

                    // ConnectionResult.SUCCESS == 0
                    int status = instance.Call<int>("isGooglePlayServicesAvailable", activity);
                    return status == 0 ? "yes" : "no";
                }
            }
            catch
            {
                return "unknown";
            }
        }

        private static string ResolveAdvertisingIdStatus(AndroidJavaObject activity)
        {
            try
            {
                using (var client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient"))
                {
                    var info = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity);
                    if (info == null)
                        return "missing";

                    string id = info.Call<string>("getId");
                    if (string.IsNullOrEmpty(id) ||
                        id == "00000000-0000-0000-0000-000000000000")
                        return "missing";

                    bool limited = info.Call<bool>("isLimitAdTrackingEnabled");
                    return limited ? "limited" : "available";
                }
            }
            catch
            {
                return "error";
            }
        }

        private static string ResolveDeviceClass()
        {
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string fingerprint = SafeStaticString(build, "FINGERPRINT");
                    string model = SafeStaticString(build, "MODEL");
                    string manufacturer = SafeStaticString(build, "MANUFACTURER");
                    string brand = SafeStaticString(build, "BRAND");
                    string product = SafeStaticString(build, "PRODUCT");
                    string hardware = SafeStaticString(build, "HARDWARE");
                    string device = SafeStaticString(build, "DEVICE");

                    if (LooksLikeEmulator(fingerprint, model, manufacturer, brand, product, hardware, device))
                        return "emulator";

                    return "physical";
                }
            }
            catch
            {
                return "unknown";
            }
        }

        private static string ResolveDeviceBrand()
        {
            try
            {
                using (var build = new AndroidJavaClass("android.os.Build"))
                {
                    string manufacturer = SafeStaticString(build, "MANUFACTURER");
                    if (string.IsNullOrWhiteSpace(manufacturer))
                        manufacturer = SafeStaticString(build, "BRAND");
                    return string.IsNullOrWhiteSpace(manufacturer) ? "unknown" : manufacturer.ToLowerInvariant();
                }
            }
            catch
            {
                return "unknown";
            }
        }

        private static string SafeStaticString(AndroidJavaClass javaClass, string fieldName)
        {
            try
            {
                return javaClass.GetStatic<string>(fieldName) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool LooksLikeEmulator(
            string fingerprint,
            string model,
            string manufacturer,
            string brand,
            string product,
            string hardware,
            string device)
        {
            string blob = string.Join("|", fingerprint, model, manufacturer, brand, product, hardware, device)
                .ToLowerInvariant();

            return blob.Contains("generic") ||
                   blob.Contains("unknown") ||
                   blob.Contains("emulator") ||
                   blob.Contains("goldfish") ||
                   blob.Contains("ranchu") ||
                   blob.Contains("vbox") ||
                   blob.Contains("genymotion") ||
                   blob.Contains("google_sdk") ||
                   blob.Contains("sdk_gphone") ||
                   blob.Contains("android sdk built for");
        }
#endif
    }
}
