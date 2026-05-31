using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LiftEngine.Context
{
    [Serializable]
    public class PredictDataPayload
    {
        public string install_type;
        public string brand;
        public string device_model;
        public string media_source;
        public int wifi;
        public int idfa_approved;
        public int days_since_installed;
        public float ltv_gross_up_to_date;
        public long days_from_install_to_FTD;
        public float ftd_amount;
        public int days_since_last_purchase;
        public int payer_ind;
        public int ad_number_life_time;
        public int ad_number_life_time_ad_type;
        public int daily_ad_number;
        public int daily_ad_number_ad_type;
        public int session_ad_number;
        public int session_ad_number_ad_type;
        public float[] ecpm_history;
        public long sec_from_last_ad;
        public int device_memory;
    }

    internal static class EcpmHistoryBuffer
    {
        private const int MaxEntries = 15;

        public static void Push(Dictionary<string, List<float>> store, LiftEngineAdFormat format, float ecpm)
        {
            string key = FormatKey(format);
            if (!store.TryGetValue(key, out var list))
            {
                list = new List<float>();
                store[key] = list;
            }

            list.Insert(0, ecpm);
            if (list.Count > MaxEntries)
                list.RemoveAt(list.Count - 1);
        }

        public static float[] GetForFormat(Dictionary<string, List<float>> store, LiftEngineAdFormat format)
        {
            if (!store.TryGetValue(FormatKey(format), out var list) || list.Count == 0)
                return Array.Empty<float>();

            return list.ToArray();
        }

        public static Dictionary<string, List<float>> Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, List<float>>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, List<float>>>(json)
                       ?? new Dictionary<string, List<float>>();
            }
            catch
            {
                return new Dictionary<string, List<float>>();
            }
        }

        public static string Serialize(Dictionary<string, List<float>> store) =>
            JsonConvert.SerializeObject(store);

        private static string FormatKey(LiftEngineAdFormat format) => format.ToString().ToLowerInvariant();
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
}
