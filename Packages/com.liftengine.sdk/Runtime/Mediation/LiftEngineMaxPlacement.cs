namespace LiftEngine.Mediation
{
    internal static class LiftEngineMaxPlacement
    {
        public static string GetPlacementByTreatment(LiftEngineAdFormat format, string treatment)
        {
            var suffix = GetSuffix(format);
            if (suffix == null)
                return null;

            var normalized = treatment?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "algo" or "a" => $"LiftEngine_a_{suffix}",
                "ml" or "m" => $"LiftEngine_m_{suffix}",
                "base" or "b" => $"Base_{suffix}",
                _ => $"Base_{suffix}"
            };
        }

        public static string SelectTreatmentByWeight(int mlWeight, int algoWeight, int baseWeight)
        {
            var roll = UnityEngine.Random.Range(0, 100);
            if (roll < mlWeight)
                return "m";
            if (roll < mlWeight + algoWeight)
                return "a";
            return "b";
        }

        private static string GetSuffix(LiftEngineAdFormat format) =>
            format switch
            {
                LiftEngineAdFormat.Rewarded => "rv",
                LiftEngineAdFormat.Interstitial => "int",
                LiftEngineAdFormat.Banner => "bnr",
                _ => null
            };
    }
}
