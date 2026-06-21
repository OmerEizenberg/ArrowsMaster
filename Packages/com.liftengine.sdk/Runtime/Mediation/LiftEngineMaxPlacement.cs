using System;

namespace LiftEngine.Mediation
{
    internal static class LiftEngineMaxPlacement
    {
        public static bool ShouldUse(string param, string message = null) =>
            ContainsCpm(param) || ContainsCpm(message);

        public static string GetPlacement(LiftEngineAdFormat format) =>
            format switch
            {
                LiftEngineAdFormat.Rewarded => "RV_LiftEngine",
                LiftEngineAdFormat.Interstitial => "Inter_LiftEngine",
                LiftEngineAdFormat.Banner => "Bnr_LiftEngine",
                _ => null
            };

        private static bool ContainsCpm(string value) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf("cpm", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
