namespace Assets.Scripts.Core
{
    /// <summary>
    /// Chooses interstitial over rewarded when the loaded interstitial pays more (higher eCPM),
    /// so the player sees a shorter ad and revenue is maximized. Never substitutes rewarded for interstitial.
    /// </summary>
    public static class AdMonetizationOptimizer
    {
        private const double MinRevenueUsd = 0.000001;

        private static double _interstitialRevenuePerImpression;
        private static double _rewardedRevenuePerImpression;

        public static void RecordInterstitialAd(MaxSdkBase.AdInfo adInfo) => RecordRevenue(adInfo, isInterstitial: true);
        public static void RecordRewardedAd(MaxSdkBase.AdInfo adInfo) => RecordRevenue(adInfo, isInterstitial: false);

        /// <summary>
        /// True when a user-initiated rewarded placement should show an interstitial instead.
        /// </summary>
        public static bool ShouldShowInterstitialInsteadOfRewarded(bool interstitialReady, bool rewardedReady)
        {
            if (!interstitialReady || !rewardedReady)
                return false;

            double interstitialEcpm = ToEcpm(_interstitialRevenuePerImpression);
            double rewardedEcpm = ToEcpm(_rewardedRevenuePerImpression);

            if (interstitialEcpm <= 0 || rewardedEcpm <= 0)
                return false;

            return interstitialEcpm > rewardedEcpm;
        }

        public static double InterstitialEcpm => ToEcpm(_interstitialRevenuePerImpression);
        public static double RewardedEcpm => ToEcpm(_rewardedRevenuePerImpression);

        private static void RecordRevenue(MaxSdkBase.AdInfo adInfo, bool isInterstitial)
        {
            if (adInfo == null)
                return;

            double revenue = adInfo.Revenue;
            if (revenue < MinRevenueUsd)
                return;

            if (isInterstitial)
                _interstitialRevenuePerImpression = revenue;
            else
                _rewardedRevenuePerImpression = revenue;
        }

        private static double ToEcpm(double revenuePerImpressionUsd)
        {
            return revenuePerImpressionUsd > 0 ? revenuePerImpressionUsd * 1000.0 : 0;
        }
    }
}
