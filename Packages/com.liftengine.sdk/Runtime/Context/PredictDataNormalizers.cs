using System;
using System.Globalization;

namespace LiftEngine.Context
{
    internal static class PredictDataNormalizers
    {
        public static string NormalizeInstallType(string appsFlyerValue)
        {
            if (string.IsNullOrEmpty(appsFlyerValue))
                return null;

            return appsFlyerValue switch
            {
                "Organic" => "organic",
                "Non-organic" => "acquired",
                _ => null
            };
        }

        /// <summary>Converts per-impression revenue (USD) to eCPM (USD per 1,000 impressions).</summary>
        public static float RevenuePerImpressionToEcpm(double revenueUsd) =>
            revenueUsd > 0d ? (float)(revenueUsd * 1000d) : 0f;

        public static int PayerInd(float ltv) => ltv > 0f ? 1 : 0;

        public static int HasMadeDeposit(long daysFromInstallToFtd) => daysFromInstallToFtd >= 0 ? 1 : 0;

        public static float DailyAdTypeShare(int dailyAdNumber, int dailyAdNumberByType) =>
            dailyAdNumber <= 0 ? 0f : dailyAdNumberByType / (float)dailyAdNumber;

        public static long SecFromLastAd(DateTime? lastAdUtc)
        {
            if (!lastAdUtc.HasValue)
                return -1;

            return (long)(DateTime.UtcNow - lastAdUtc.Value).TotalSeconds;
        }

        public static int DeviceMemoryGb()
        {
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(UnityEngine.SystemInfo.systemMemorySize / 1024f));
        }

        public static int WifiFlag()
        {
            return UnityEngine.Application.internetReachability == UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork ? 1 : 0;
        }

        public static string FormatPayloadValue(float value) =>
            value.ToString(CultureInfo.InvariantCulture);

        public static long UnixTimestampSeconds() =>
            (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}
