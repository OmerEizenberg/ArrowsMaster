using System;
using UnityEngine;

namespace LiftEngine.Context
{
    internal sealed class SessionTracker
    {
        private bool _sessionStarted;

        public void BeginSession()
        {
            if (_sessionStarted)
                return;

            _sessionStarted = true;
            ResetSessionCounters();
        }

        public void EnsureDailyRollover(ReportContextStore store)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (store.DailyDateKey == today)
                return;

            store.DailyDateKey = today;
            store.SetRawCount("daily_banner", 0);
            store.SetRawCount("daily_interstitial", 0);
            store.SetRawCount("daily_rewarded", 0);
        }

        private static void ResetSessionCounters()
        {
            PlayerPrefs.SetInt("le_ctx_sess_banner", 0);
            PlayerPrefs.SetInt("le_ctx_sess_interstitial", 0);
            PlayerPrefs.SetInt("le_ctx_sess_rewarded", 0);
            PlayerPrefs.Save();
        }
    }

    internal sealed class ReportContextService
    {
        private readonly ReportContextStore _store = new ReportContextStore();
        private readonly SessionTracker _session = new SessionTracker();

        public void Initialize()
        {
            _store.Load();
            _session.BeginSession();

            if (!_store.InstallUtc.HasValue)
                _store.InstallUtc = DateTime.UtcNow;

            _store.RecordActiveDay();
            _session.EnsureDailyRollover(_store);
        }

        public void SetAttribution(string appsFlyerInstallType, string mediaSource)
        {
            var normalized = PredictDataNormalizers.NormalizeInstallType(appsFlyerInstallType);
            _store.SaveAttribution(normalized, mediaSource);
        }

        public void SetIdfaApproved(bool approved) => _store.SetIdfaOverride(approved);

        public void SetCountryCode(string countryCode) => _store.SaveCountryCode(countryCode);

        public void NotifyPurchase(float amountUsd)
        {
            if (amountUsd <= 0f)
                return;

            var now = DateTime.UtcNow;
            _store.LtvGross += amountUsd;

            if (!_store.FirstPurchaseUtc.HasValue)
            {
                _store.FirstPurchaseUtc = now;
                _store.FtdAmount = amountUsd;
            }

            _store.LastPurchaseUtc = now;
        }

        public void RecordAdDisplayed(LiftEngineAdFormat format, double revenueUsd)
        {
            _session.EnsureDailyRollover(_store);
            _store.IncrementAdShown(format);

            if (revenueUsd > 0)
            {
                var history = _store.EcpmHistory;
                EcpmHistoryBuffer.Push(history, format, (float)(revenueUsd * 1000d));
                _store.EcpmHistory = history;
            }
        }

        public PredictDataPayload BuildPayload(LiftEngineAdFormat format)
        {
            _session.EnsureDailyRollover(_store);
            _store.RecordActiveDay();

            var nowUtc = DateTime.UtcNow;
            var installUtc = _store.InstallUtc ?? nowUtc;
            var daysSinceInstall = Math.Max(0, (int)(nowUtc - installUtc).TotalDays);

            long daysToFtd = -1;
            if (_store.FirstPurchaseUtc.HasValue)
                daysToFtd = (long)(_store.FirstPurchaseUtc.Value - installUtc).TotalDays;

            int daysSinceLastPurchase = -1;
            if (_store.LastPurchaseUtc.HasValue)
                daysSinceLastPurchase = Math.Max(0, (int)(nowUtc - _store.LastPurchaseUtc.Value).TotalDays);

            var ltv = _store.LtvGross;
            var typeRaw = _store.GetLifetimeRaw(format);
            var typeDailyRaw = _store.GetDailyRaw(format);
            var typeSessionRaw = _store.GetSessionRaw(format);
            var dailyAdNumberWire = PredictDataNormalizers.ToWireCount(_store.GetDailyTotalRaw());
            var dailyAdNumberByTypeWire = PredictDataNormalizers.ToWireCount(typeDailyRaw);
            var countryCode = !string.IsNullOrEmpty(_store.CountryCodeOverride)
                ? _store.CountryCodeOverride
                : DeviceCountryProvider.DetectCountryCode();

            return new PredictDataPayload
            {
                os = DeviceOsProvider.GetOs(),
                country_code = countryCode,
                install_type = string.IsNullOrEmpty(_store.InstallTypeRaw) ? null : _store.InstallTypeRaw,
                brand = DeviceBrandProvider.GetBrand(),
                device_model = SystemInfo.deviceModel,
                day_num = Math.Max(1, _store.ActiveDayCount),
                hour_of_day = nowUtc.Hour,
                media_source = string.IsNullOrEmpty(_store.MediaSource) ? null : _store.MediaSource,
                wifi = PredictDataNormalizers.WifiFlag(),
                idfa_approved = ResolveIdfaApproved(),
                has_made_deposit = PredictDataNormalizers.HasMadeDeposit(daysToFtd),
                days_since_installed = daysSinceInstall,
                ltv_gross_up_to_date = ltv,
                days_from_install_to_FTD = daysToFtd,
                ftd_amount = _store.FtdAmount,
                days_since_last_purchase = daysSinceLastPurchase,
                payer_ind = PredictDataNormalizers.PayerInd(ltv),
                ad_number_life_time = PredictDataNormalizers.ToWireCount(_store.GetLifetimeTotalRaw()),
                ad_number_life_time_ad_type = PredictDataNormalizers.ToWireCount(typeRaw),
                daily_ad_number = dailyAdNumberWire,
                daily_ad_number_ad_type = dailyAdNumberByTypeWire,
                daily_ad_type_share = PredictDataNormalizers.DailyAdTypeShare(
                    dailyAdNumberWire, dailyAdNumberByTypeWire),
                session_ad_number = PredictDataNormalizers.ToWireCount(_store.GetSessionTotalRaw()),
                session_ad_number_ad_type = PredictDataNormalizers.ToWireCount(typeSessionRaw),
                ecpm_history = EcpmHistoryBuffer.GetForFormat(_store.EcpmHistory, format),
                sec_from_last_ad = PredictDataNormalizers.SecFromLastAd(_store.LastAdUtc),
                device_memory = PredictDataNormalizers.DeviceMemoryGb()
            };
        }

        public (string keyword, string auctionId) GetAuctionContext(LiftEngineAdFormat format) =>
            _store.GetAuctionContext(format);

        public string GetBidFloorParam(LiftEngineAdFormat format) =>
            _bidFloorParam.TryGetValue(format, out var param) ? param : null;

        private readonly System.Collections.Generic.Dictionary<LiftEngineAdFormat, string> _bidFloorParam = new();

        public void SetAuctionContext(LiftEngineAdFormat format, string keyword, string auctionId, string param)
        {
            _store.SaveAuctionContext(format, keyword, auctionId);
            if (!string.IsNullOrEmpty(param))
                _bidFloorParam[format] = param;
        }

        public void ClearContextData() => _store.ClearAll();

        public ReportContextStore StoreForDebug => _store;

        private int ResolveIdfaApproved()
        {
            if (_store.IdfaApprovedOverride.HasValue)
                return _store.IdfaApprovedOverride.Value;

            return 0;
        }
    }
}
