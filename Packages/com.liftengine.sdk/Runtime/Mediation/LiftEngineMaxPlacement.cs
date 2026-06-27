namespace LiftEngine.Mediation
{
    internal static class LiftEngineMaxPlacement
    {
        // LiftEngine_* placements apply when predict JSON contained a "cpm" key.
        // Base_* placements apply otherwise (direct MAX or LiftEngine without cpm).
        public static string GetPlacement(LiftEngineAdFormat format) =>
            format switch
            {
                LiftEngineAdFormat.Rewarded => "LiftEngine_rv",
                LiftEngineAdFormat.Interstitial => "LiftEngine_int",
                LiftEngineAdFormat.Banner => "LiftEngine_bnr",
                _ => null
            };

        public static string GetBasePlacement(LiftEngineAdFormat format) =>
            format switch
            {
                LiftEngineAdFormat.Rewarded => "Base_rv",
                LiftEngineAdFormat.Interstitial => "Base_int",
                LiftEngineAdFormat.Banner => "Base_bnr",
                _ => null
            };
    }
}
