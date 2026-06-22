namespace LiftEngine.Mediation
{
    internal static class LiftEngineMaxPlacement
    {
        // Per ad type. Only applied when the decoded predict JSON contained a "cpm" key.
        public static string GetPlacement(LiftEngineAdFormat format) =>
            format switch
            {
                LiftEngineAdFormat.Rewarded => "RV_LiftEngine",
                LiftEngineAdFormat.Interstitial => "Inter_LiftEngine",
                LiftEngineAdFormat.Banner => "Bnr_LiftEngine",
                _ => null
            };
    }
}
