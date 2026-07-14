namespace LiftEngine
{
    /// <summary>
    /// Raised when LiftEngine completes an optimization request for an ad format.
    /// </summary>
    public sealed class LiftEngineOptimizationEventArgs
    {
        public LiftEngineAdFormat Format { get; internal set; }
        public bool Succeeded { get; internal set; }
    }

    /// <summary>
    /// Error details for failed LiftEngine API operations.
    /// </summary>
    public sealed class LiftEngineOperationError
    {
        public int StatusCode { get; internal set; }
        public string Message { get; internal set; }
    }
}
