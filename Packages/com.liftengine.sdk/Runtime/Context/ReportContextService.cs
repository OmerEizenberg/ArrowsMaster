using System;
using System.Collections.Generic;
using System.Globalization;
using LiftEngine;
using LiftEngine.Mediation;
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

        public void BeginIpCountryLookup(MonoBehaviour host)
        {
            if (host == null || !string.IsNullOrEmpty(_store.CountryCodeOverride))
                return;

            host.StartCoroutine(IpCountryResolver.FetchCountryCode(code =>
            {
                if (string.IsNullOrEmpty(code))
                    return;

                _store.SaveIpCountryCode(code);
                LiftEngineLogger.Log($"Country code resolved from IP: {code}");
            }));
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

        public void RecordAdImpression(LiftEngineAdFormat format)
        {
            _session.EnsureDailyRollover(_store);
            _store.IncrementAdShown(format);
        }

        /// <param name="revenueUsd">Per-impression revenue in USD (same unit as track <c>rev</c>).</param>
        public void RecordAdRevenue(LiftEngineAdFormat format, double revenueUsd)
        {
            if (revenueUsd <= 0d)
            {
                LiftEngineLogger.Log($"ecpm_history {format} — skipped (revenue <= 0)");
                return;
            }

            var ecpm = PredictDataNormalizers.RevenuePerImpressionToEcpm(revenueUsd);
            EcpmHistoryBuffer.Push(format, ecpm);

            var snapshot = EcpmHistoryBuffer.GetForFormat(format);
            LiftEngineLogger.Log(
                $"ecpm_history {format} ({EcpmHistoryBuffer.GetAdTypeName(format)}) — " +
                $"rev={revenueUsd.ToString(CultureInfo.InvariantCulture)} → " +
                $"ecpm={ecpm.ToString(CultureInfo.InvariantCulture)}, history=[{FormatHistory(snapshot)}]");
        }

        public PredictDataPayload BuildPayload(LiftEngineAdFormat format) =>
            BuildPayload((LiftEngineAdFormat?)format);

        /// <param name="format">
        /// When null (context-only report), format-specific fields stay at defaults and
        /// <c>ecpm_history</c> is omitted — no interstitial/auto format fallback.
        /// </param>
        public PredictDataPayload BuildPayload(LiftEngineAdFormat? format)
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
            var dailyAdNumber = _store.GetDailyTotalRaw();
            var countryCode = ResolveCountryCode();

            int typeRaw = 0;
            int typeDailyRaw = 0;
            int typeSessionRaw = 0;
            float[] ecpmHistory = null;
            string adType = null;
            if (format.HasValue)
            {
                adType = EcpmHistoryBuffer.GetAdTypeName(format.Value);
                typeRaw = _store.GetLifetimeRaw(format.Value);
                typeDailyRaw = _store.GetDailyRaw(format.Value);
                typeSessionRaw = _store.GetSessionRaw(format.Value);
                // Per-format PlayerPrefs key only. Empty → [] (never null) so backends cannot
                // substitute another format's history for a missing field.
                ecpmHistory = EcpmHistoryBuffer.GetForFormat(format.Value) ?? Array.Empty<float>();
            }

            var payload = new PredictDataPayload
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
                days_from_install_to_ftd = daysToFtd,
                ftd_amount = _store.FtdAmount,
                days_since_last_purchase = daysSinceLastPurchase,
                payer_ind = PredictDataNormalizers.PayerInd(ltv),
                ad_number_life_time = _store.GetLifetimeTotalRaw(),
                ad_number_life_time_ad_type = typeRaw,
                daily_ad_number = dailyAdNumber,
                daily_ad_number_ad_type = typeDailyRaw,
                daily_ad_type_share = PredictDataNormalizers.DailyAdTypeShare(
                    dailyAdNumber, typeDailyRaw),
                session_ad_number = _store.GetSessionTotalRaw(),
                session_ad_number_ad_type = typeSessionRaw,
                ad_type = adType,
                ecpm_history = ecpmHistory,
                sec_from_last_ad = PredictDataNormalizers.SecFromLastAd(_store.LastAdUtc),
                device_memory = PredictDataNormalizers.DeviceMemoryGb(),
                app_version = Application.version
            };

            if (format.HasValue)
            {
                LiftEngineLogger.Log(
                    $"BuildPayload {format.Value} — ad_type={adType}, " +
                    $"ecpm_history=[{FormatHistory(ecpmHistory)}] (len={ecpmHistory?.Length ?? 0})");
            }

            return payload;
        }

        public (string keyword, string auctionId) GetAuctionContext(LiftEngineAdFormat format) =>
            _store.GetAuctionContext(format);

        public bool HasValidAuctionContext(LiftEngineAdFormat format)
        {
            var (_, auctionId) = _store.GetAuctionContext(format);
            return !string.IsNullOrEmpty(auctionId);
        }

        /// <summary>
        /// Ensures track/view and track/activeview can always be sent when an ad fills without a predict auction id
        /// (optimization timeout/failure with fallback load).
        /// </summary>
        /// <param name="force">
        /// When true, always mint a new fallback id (e.g. optimize failed and prior predict must not
        /// be reused for the next load). When false, keep an existing valid auction id.
        /// </param>
        public void EnsureFallbackAuctionContext(LiftEngineAdFormat format, bool force = false)
        {
            if (!force && HasValidAuctionContext(format))
                return;

            var suffix = format.ToString().ToLowerInvariant();
            var auctionId =
                $"fb_{suffix}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Math.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode())}";
            SetAuctionContext(format, "fallback", auctionId, payloadKey: null);
            LiftEngineLogger.LogBackendWarning(
                $"{format} — using fallback auction context (auction_id={auctionId})");
        }

        public string GetPayloadKey(LiftEngineAdFormat format) =>
            _payloadKeys.TryGetValue(format, out var payloadKey) ? payloadKey : null;

        /// <summary>
        /// Index into predict <c>multipliers</c> that produced the current fill (-1 = fallback / none).
        /// </summary>
        public int GetWinningMultiplierIndex(LiftEngineAdFormat format) =>
            _winningMultiplierIndexes.TryGetValue(format, out var index) ? index : -1;

        /// <summary>
        /// Payload floor value sent to MAX for the current fill
        /// (<c>prediction * multipliers[i]</c>, or <c>0</c> on fallback).
        /// </summary>
        public float GetWinningFloor(LiftEngineAdFormat format) =>
            _winningFloors.TryGetValue(format, out var flr) ? flr : 0f;

        public void SetWinningMultiplierIndex(LiftEngineAdFormat format, int mulIndex, float flr = 0f)
        {
            _winningMultiplierIndexes[format] = mulIndex;
            _winningFloors[format] = flr;
        }

        public string GetMaxPlacement(LiftEngineAdFormat format)
        {
            if (_maxPlacements.TryGetValue(format, out var placement))
                return placement;

            placement = ResolveFallbackPlacement(format);
            _maxPlacements[format] = placement;
            return placement;
        }

        private readonly Dictionary<LiftEngineAdFormat, string> _payloadKeys = new();
        private readonly Dictionary<LiftEngineAdFormat, string> _maxPlacements = new();
        private readonly Dictionary<LiftEngineAdFormat, int> _winningMultiplierIndexes = new();
        private readonly Dictionary<LiftEngineAdFormat, float> _winningFloors = new();

        /// <summary>
        /// Stores predict auction context independently per ad format.
        /// Rewarded, interstitial, and banner each have their own payload key and MAX placement.
        /// MAX placement is selected from the predict <c>treatment</c> field when present;
        /// otherwise weighted random from saved <c>group_ratios</c>.
        /// </summary>
        public void SetAuctionContext(LiftEngineAdFormat format, string keyword, string auctionId,
            string payloadKey, string treatment = null, Dictionary<string, int> groupRatios = null)
        {
            _store.SaveAuctionContext(format, keyword, auctionId);
            // New auction: winning multiplier / floor is unknown until a load attempt fills.
            _winningMultiplierIndexes.Remove(format);
            _winningFloors.Remove(format);

            if (!string.IsNullOrEmpty(payloadKey))
                _payloadKeys[format] = payloadKey;
            else
                _payloadKeys.Remove(format);

            if (groupRatios != null && groupRatios.Count > 0)
                _store.SaveGroupRatios(groupRatios);

            if (!string.IsNullOrEmpty(treatment))
            {
                _maxPlacements[format] = LiftEngineMaxPlacement.GetPlacementByTreatment(format, treatment);
                LiftEngineLogger.LogClient(
                    $"{format} — route selected; MAX placement={_maxPlacements[format]}");
            }
            else
            {
                _maxPlacements.Remove(format);
                LiftEngineLogger.LogClient(
                    $"{format} — using weighted route fallback for MAX placement");
            }
        }

        private string ResolveFallbackPlacement(LiftEngineAdFormat format)
        {
            if (_store.HasGroupRatios())
            {
                var (ml, algo, baseWeight) = _store.GetGroupRatios();
                var treatment = LiftEngineMaxPlacement.SelectTreatmentByWeight(ml, algo, baseWeight);
                var placement = LiftEngineMaxPlacement.GetPlacementByTreatment(format, treatment);
                LiftEngineLogger.LogClient(
                    $"{format} — route fallback (ml={ml}, algo={algo}, base={baseWeight}) " +
                    $"→ MAX placement={placement}");
                return placement;
            }

            var defaultPlacement = LiftEngineMaxPlacement.GetPlacementByTreatment(format, "base");
            LiftEngineLogger.LogClient(
                $"{format} — no group_ratios saved; default MAX placement={defaultPlacement}");
            return defaultPlacement;
        }

        public void ClearAuctionContext(LiftEngineAdFormat format)
        {
            _store.ClearAuctionContext(format);
            _payloadKeys.Remove(format);
            _maxPlacements.Remove(format);
            _winningMultiplierIndexes.Remove(format);
            _winningFloors.Remove(format);
        }

        public void ClearContextData() => _store.ClearAll();

        public ReportContextStore StoreForDebug => _store;

        private string ResolveCountryCode()
        {
            if (!string.IsNullOrEmpty(_store.CountryCodeOverride))
                return _store.CountryCodeOverride;

            if (!string.IsNullOrEmpty(_store.IpCountryCode))
                return _store.IpCountryCode;

            return DeviceCountryProvider.DetectCountryCode();
        }

        private int ResolveIdfaApproved()
        {
            if (_store.IdfaApprovedOverride.HasValue)
                return _store.IdfaApprovedOverride.Value;

            return 0;
        }

        private static string FormatHistory(float[] history)
        {
            if (history == null || history.Length == 0)
                return string.Empty;

            var parts = new string[history.Length];
            for (var i = 0; i < history.Length; i++)
                parts[i] = history[i].ToString(CultureInfo.InvariantCulture);

            return string.Join(", ", parts);
        }
    }
}
