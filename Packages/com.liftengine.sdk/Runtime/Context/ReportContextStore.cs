using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiftEngine.Context
{
    internal sealed class ReportContextStore
    {
        private const string Prefix = "le_ctx_";

        public string InstallTypeRaw { get; set; }
        public string MediaSource { get; set; }
        public int? IdfaApprovedOverride { get; set; }

        public DateTime? InstallUtc
        {
            get => GetDateTime("install_utc");
            set => SetDateTime("install_utc", value);
        }

        public DateTime? LastAdUtc
        {
            get => GetDateTime("last_ad_utc");
            set => SetDateTime("last_ad_utc", value);
        }

        public DateTime? LastPurchaseUtc
        {
            get => GetDateTime("last_purchase_utc");
            set => SetDateTime("last_purchase_utc", value);
        }

        public DateTime? FirstPurchaseUtc
        {
            get => GetDateTime("first_purchase_utc");
            set => SetDateTime("first_purchase_utc", value);
        }

        public float LtvGross
        {
            get => PlayerPrefs.GetFloat(Prefix + "ltv", 0f);
            set { PlayerPrefs.SetFloat(Prefix + "ltv", value); PlayerPrefs.Save(); }
        }

        public float FtdAmount
        {
            get => PlayerPrefs.GetFloat(Prefix + "ftd_amount", 0f);
            set { PlayerPrefs.SetFloat(Prefix + "ftd_amount", value); PlayerPrefs.Save(); }
        }

        public string DailyDateKey
        {
            get => PlayerPrefs.GetString(Prefix + "daily_date", string.Empty);
            set { PlayerPrefs.SetString(Prefix + "daily_date", value); PlayerPrefs.Save(); }
        }

        public Dictionary<string, List<float>> EcpmHistory
        {
            get => EcpmHistoryBuffer.Deserialize(PlayerPrefs.GetString(Prefix + "ecpm", string.Empty));
            set
            {
                PlayerPrefs.SetString(Prefix + "ecpm", EcpmHistoryBuffer.Serialize(value));
                PlayerPrefs.Save();
            }
        }

        public void Load()
        {
            InstallTypeRaw = PlayerPrefs.GetString(Prefix + "install_type", null);
            MediaSource = PlayerPrefs.GetString(Prefix + "media_source", null);
            if (PlayerPrefs.HasKey(Prefix + "idfa_override"))
                IdfaApprovedOverride = PlayerPrefs.GetInt(Prefix + "idfa_override");
        }

        public void SaveAttribution(string normalizedInstallType, string mediaSource)
        {
            InstallTypeRaw = normalizedInstallType;
            MediaSource = mediaSource;
            PlayerPrefs.SetString(Prefix + "install_type", normalizedInstallType ?? string.Empty);
            PlayerPrefs.SetString(Prefix + "media_source", mediaSource ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void SetIdfaOverride(bool approved)
        {
            IdfaApprovedOverride = approved ? 1 : 0;
            PlayerPrefs.SetInt(Prefix + "idfa_override", approved ? 1 : 0);
            PlayerPrefs.Save();
        }

        public int GetRawCount(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(Prefix + key, defaultValue);
        }

        public void SetRawCount(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public void IncrementRawCount(string key)
        {
            SetRawCount(key, GetRawCount(key) + 1);
        }

        public void ClearAll()
        {
            var keys = new List<string>();
            // PlayerPrefs has no enumerate — clear known keys
            foreach (var suffix in new[]
            {
                "install_utc", "last_ad_utc", "last_purchase_utc", "first_purchase_utc",
                "ltv", "ftd_amount", "daily_date", "ecpm", "install_type", "media_source", "idfa_override",
                "life_banner", "life_interstitial", "life_rewarded",
                "daily_banner", "daily_interstitial", "daily_rewarded",
                "sess_banner", "sess_interstitial", "sess_rewarded"
            })
            {
                PlayerPrefs.DeleteKey(Prefix + suffix);
            }
            PlayerPrefs.Save();
            Load();
        }

        private static DateTime? GetDateTime(string suffix)
        {
            var key = Prefix + suffix;
            if (!PlayerPrefs.HasKey(key))
                return null;

            var ticks = long.Parse(PlayerPrefs.GetString(key, "0"));
            return ticks <= 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }

        private static void SetDateTime(string suffix, DateTime? value)
        {
            var key = Prefix + suffix;
            if (!value.HasValue)
            {
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                PlayerPrefs.SetString(key, value.Value.Ticks.ToString());
            }
            PlayerPrefs.Save();
        }

        private static string LifeKey(LiftEngineAdFormat format) =>
            "life_" + format.ToString().ToLowerInvariant();

        private static string DailyKey(LiftEngineAdFormat format) =>
            "daily_" + format.ToString().ToLowerInvariant();

        private static string SessionKey(LiftEngineAdFormat format) =>
            "sess_" + format.ToString().ToLowerInvariant();

        public int GetLifetimeRaw(LiftEngineAdFormat format) => GetRawCount(LifeKey(format));
        public int GetDailyRaw(LiftEngineAdFormat format) => GetRawCount(DailyKey(format));
        public int GetSessionRaw(LiftEngineAdFormat format) => GetRawCount(SessionKey(format));

        public int GetLifetimeTotalRaw() =>
            GetLifetimeRaw(LiftEngineAdFormat.Banner) +
            GetLifetimeRaw(LiftEngineAdFormat.Interstitial) +
            GetLifetimeRaw(LiftEngineAdFormat.Rewarded);

        public int GetDailyTotalRaw() =>
            GetDailyRaw(LiftEngineAdFormat.Banner) +
            GetDailyRaw(LiftEngineAdFormat.Interstitial) +
            GetDailyRaw(LiftEngineAdFormat.Rewarded);

        public int GetSessionTotalRaw() =>
            GetSessionRaw(LiftEngineAdFormat.Banner) +
            GetSessionRaw(LiftEngineAdFormat.Interstitial) +
            GetSessionRaw(LiftEngineAdFormat.Rewarded);

        public void IncrementAdShown(LiftEngineAdFormat format)
        {
            IncrementRawCount(LifeKey(format));
            IncrementRawCount(DailyKey(format));
            IncrementRawCount(SessionKey(format));
            LastAdUtc = DateTime.UtcNow;
        }
    }
}
