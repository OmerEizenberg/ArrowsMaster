using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace LiftEngine.Context
{
    [Serializable]
    internal class ContextPayload
    {
        public string os;
        public string country_code;
        public string install_type;
        public string brand;
        public string device_model;
        public int day_num;
        public int hour_of_day;
        public string media_source;
        public int wifi;
        public int idfa_approved;
        public int has_made_deposit;
        public int days_since_installed;
        public float ltv_gross_up_to_date;
        public long days_from_install_to_ftd;
        public float ftd_amount;
        public int days_since_last_purchase;
        public int payer_ind;
        public int ad_number_life_time;
        public int ad_number_life_time_ad_type;
        public int daily_ad_number;
        public int daily_ad_number_ad_type;
        public float daily_ad_type_share;
        public int session_ad_number;
        public int session_ad_number_ad_type;
        /// <summary>
        /// Model / format this payload was built for (<c>banner</c> / <c>interstitial</c> / <c>rewarded</c>).
        /// Omitted on context-only reports (no format).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ad_type;
        /// <summary>
        /// Recent eCPM values (USD per 1,000 impressions), newest first — for <see cref="ad_type"/> only.
        /// When <see cref="ad_type"/> is set this is never null (empty array if no impressions yet),
        /// so backends cannot substitute another format's history for a missing field.
        /// Omitted only on context-only reports (no format).
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float[] ecpm_history;
        public long sec_from_last_ad;
        public int device_memory;
        public string app_version;
    }

    /// <summary>
    /// Per-format eCPM history. Each ad type has its own PlayerPrefs key.
    /// Legacy shared <c>le_ctx_ecpm</c> is ignored (not migrated, not wiped on upgrade).
    /// </summary>
    internal static class EcpmHistoryBuffer
    {
        private const int MaxEntries = 15;

        public static string GetAdTypeName(LiftEngineAdFormat format) => format switch
        {
            LiftEngineAdFormat.Banner => "banner",
            LiftEngineAdFormat.Interstitial => "interstitial",
            LiftEngineAdFormat.Rewarded => "rewarded",
            _ => null
        };

        public static string PrefsKey(LiftEngineAdFormat format)
        {
            var name = GetAdTypeName(format);
            return name == null ? null : "le_ctx_ecpm_" + name;
        }

        public static void Push(LiftEngineAdFormat format, float ecpm)
        {
            var key = PrefsKey(format);
            if (key == null)
                return;

            var list = LoadList(key);
            list.Insert(0, ecpm);
            if (list.Count > MaxEntries)
                list.RemoveAt(list.Count - 1);

            PlayerPrefs.SetString(key, SerializeList(list));
            PlayerPrefs.Save();
        }

        /// <summary>Returns this format's history only. Never falls back to another format.</summary>
        public static float[] GetForFormat(LiftEngineAdFormat format)
        {
            var key = PrefsKey(format);
            if (key == null)
                return Array.Empty<float>();

            var list = LoadList(key);
            return list.Count == 0 ? Array.Empty<float>() : list.ToArray();
        }

        public static void ClearAllFormats()
        {
            foreach (LiftEngineAdFormat format in new[]
                     {
                         LiftEngineAdFormat.Banner,
                         LiftEngineAdFormat.Interstitial,
                         LiftEngineAdFormat.Rewarded
                     })
            {
                var key = PrefsKey(format);
                if (key != null)
                    PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
        }

        private static List<float> LoadList(string prefsKey)
        {
            var json = PlayerPrefs.GetString(prefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new List<float>();

            try
            {
                return JsonConvert.DeserializeObject<List<float>>(json) ?? new List<float>();
            }
            catch
            {
                return new List<float>();
            }
        }

        private static string SerializeList(List<float> list) =>
            JsonConvert.SerializeObject(list ?? new List<float>());
    }

    internal static class DeviceBrandProvider
    {
        public static string GetBrand()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var buildClass = new AndroidJavaClass("android.os.Build");
                return buildClass.GetStatic<string>("BRAND") ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
#else
            return SystemInfo.deviceModel?.Split(' ')?[0]?.ToLowerInvariant() ?? "unknown";
#endif
        }
    }

    internal static class DeviceOsProvider
    {
        public static string GetOs()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "android";
#elif UNITY_IOS && !UNITY_EDITOR
            return "ios";
#elif UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget switch
            {
                UnityEditor.BuildTarget.Android => "android",
                UnityEditor.BuildTarget.iOS => "ios",
                _ => "unknown"
            };
#else
            return Application.platform switch
            {
                RuntimePlatform.Android => "android",
                RuntimePlatform.IPhonePlayer => "ios",
                _ => "unknown"
            };
#endif
        }
    }

    internal static class DeviceCountryProvider
    {
        public static string DetectCountryCode()
        {
            var code = GetAndroidCountryCode() ?? GetCultureCountryCode();
            return NormalizeCountryCode(code);
        }

        public static string NormalizeCountryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            code = code.Trim().ToUpperInvariant();
            return code.Length == 2 ? code : null;
        }

        private static string GetAndroidCountryCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var localeClass = new AndroidJavaClass("java.util.Locale");
                using var defaultLocale = localeClass.CallStatic<AndroidJavaObject>("getDefault");
                var country = defaultLocale.Call<string>("getCountry");
                if (!string.IsNullOrWhiteSpace(country))
                    return country;
            }
            catch
            {
                // Fall through to telephony lookup.
            }

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var telephony = activity.Call<AndroidJavaObject>("getSystemService", "phone");
                var simCountry = telephony.Call<string>("getSimCountryIso");
                if (!string.IsNullOrWhiteSpace(simCountry))
                    return simCountry;

                var networkCountry = telephony.Call<string>("getNetworkCountryIso");
                if (!string.IsNullOrWhiteSpace(networkCountry))
                    return networkCountry;
            }
            catch
            {
                // Fall through to culture lookup.
            }
#endif
            return null;
        }

        private static string GetCultureCountryCode()
        {
            try
            {
                var culture = CultureInfo.CurrentCulture;
                if (!string.IsNullOrWhiteSpace(culture.Name) && culture.Name.Contains("-", StringComparison.Ordinal))
                {
                    var region = new RegionInfo(culture.Name);
                    return region.TwoLetterISORegionName;
                }

                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch
            {
                return null;
            }
        }
    }
}
