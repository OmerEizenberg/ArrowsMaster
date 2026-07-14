using LiftEngine.Ads;
using LiftEngine.Context;
using LiftEngine.Mediation;
using UnityEngine;

namespace LiftEngine.Api
{
    internal static class LiftEngineTrackReporter
    {
        public static void ReportError(
            LiftEngineApiClient api,
            LiftEngineSettings settings,
            ReportContextService context,
            LiftEngineAdFormat format,
            string errorCode,
            string errorMessage,
            MediationAdInfo info = null,
            string adUnitId = null)
        {
            if (api == null)
                return;

            var (keyword, auctionId) = context.GetAuctionContext(format);
            var resolvedAdUnitId = info?.AdUnitId ?? adUnitId ?? settings?.GetAdUnitId(format);
            var placementId = info?.MaxPlacement ?? context?.GetMaxPlacement(format);

            var request = new LiftEngineTrackErrorParams
            {
                BundleId = Application.identifier,
                DeviceId = DeviceIdProvider.GetDeviceId(),
                AppVersion = Application.version,
                AuctionId = auctionId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                AdType = settings?.GetModelName(format),
                PlacementId = placementId,
                Keyword = keyword,
                AdUnitId = resolvedAdUnitId,
                Timestamp = PredictDataNormalizers.UnixTimestampSeconds()
            };

            LiftEngineLogger.LogClient(
                $"Track error — code={errorCode}, format={format}, bundle={request.BundleId}, " +
                $"device={request.DeviceId}, app_version={request.AppVersion}, ad_type={request.AdType}, " +
                $"placement={request.PlacementId}, keyword={request.Keyword}, auction_id={request.AuctionId}, " +
                $"ad_unit={request.AdUnitId}, timestamp={request.Timestamp}, message={errorMessage}");

            api.TrackError(request);
        }
    }
}
