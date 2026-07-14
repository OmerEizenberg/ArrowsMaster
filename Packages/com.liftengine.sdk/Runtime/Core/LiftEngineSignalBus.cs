using System;

namespace LiftEngine
{
    public sealed class OptimizationUnavailableSignal
    {
        public LiftEngineAdFormat Format { get; }

        public OptimizationUnavailableSignal(LiftEngineAdFormat format)
        {
            Format = format;
        }
    }

    public sealed class AdPrewarmCompletedSignal
    {
        public LiftEngineAdFormat Format { get; }
        public bool Success { get; }

        public AdPrewarmCompletedSignal(LiftEngineAdFormat format, bool success)
        {
            Format = format;
            Success = success;
        }
    }

    public sealed class AdReadyStateChangedSignal
    {
        public LiftEngineAdFormat Format { get; }
        public bool IsReady { get; }

        public AdReadyStateChangedSignal(LiftEngineAdFormat format, bool isReady)
        {
            Format = format;
            IsReady = isReady;
        }
    }

    public static class LiftEngineSignalBus
    {
        public static event Action<OptimizationUnavailableSignal> OptimizationUnavailable;
        public static event Action<AdPrewarmCompletedSignal> AdPrewarmCompleted;
        public static event Action<AdReadyStateChangedSignal> AdReadyStateChanged;

        internal static void Publish(OptimizationUnavailableSignal signal) =>
            OptimizationUnavailable?.Invoke(signal);

        internal static void Publish(AdPrewarmCompletedSignal signal) =>
            AdPrewarmCompleted?.Invoke(signal);

        internal static void Publish(AdReadyStateChangedSignal signal) =>
            AdReadyStateChanged?.Invoke(signal);
    }
}
