namespace LiftEngine
{
    internal static class LiftEngineLogger
    {
        private static bool _verbose;

        public static void SetVerbose(bool verbose) => _verbose = verbose;

        public static void Log(string message)
        {
            if (_verbose)
                UnityEngine.Debug.Log($"[LiftEngine] {message}");
        }

        /// <summary>Outgoing client request — always logged.</summary>
        public static void LogClient(string message) =>
            UnityEngine.Debug.Log($"[LiftEngine] [CL] {message}");

        /// <summary>Incoming backend response — always logged.</summary>
        public static void LogBackend(string message) =>
            UnityEngine.Debug.Log($"[LiftEngine] [BE] {message}");

        public static void LogClientWarning(string message) =>
            UnityEngine.Debug.LogWarning($"[LiftEngine] [CL] {message}");

        public static void LogBackendWarning(string message) =>
            UnityEngine.Debug.LogWarning($"[LiftEngine] [BE] {message}");

        /// <summary>Ad fill attempt — always logged. attempt -1 = fallback load loop.</summary>
        public static void LogAttempt(int attempt, string message) =>
            UnityEngine.Debug.Log($"[LiftEngine] [Attempt {attempt}] {message}");

        public static void LogAttemptWarning(int attempt, string message) =>
            UnityEngine.Debug.LogWarning($"[LiftEngine] [Attempt {attempt}] {message}");

        public static void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[LiftEngine] {message}");
        }

        public static void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[LiftEngine] {message}");
        }
    }
}
