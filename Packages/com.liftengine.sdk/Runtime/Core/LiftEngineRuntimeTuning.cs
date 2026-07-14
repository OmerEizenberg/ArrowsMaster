namespace LiftEngine
{
    /// <summary>
    /// Internal runtime defaults — not exposed to integrators.
    /// </summary>
    internal static class LiftEngineRuntimeTuning
    {
        public const float OptimizationTimeoutSeconds = 8f;
        public const float DefaultOptimizationFallback = 0f;
        public const bool TreatEditorLoadAsFilled = true;
        public const float LoadAttemptTimeoutSeconds = 15f;
        public const float ReadinessCheckIntervalSeconds = 2f;
        public const bool PrewarmOnInit = true;
        public const bool PrewarmAfterShow = true;
        public const float PrewarmMaxDurationSeconds = 90f;
        public const float PrewarmRetryIntervalSeconds = 20f;
        public const int MaxFallbackLoadRounds = 5;
        public const float ShowWaitMaxSeconds = 30f;
    }
}
