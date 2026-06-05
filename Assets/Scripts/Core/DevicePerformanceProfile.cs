using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Central runtime profile for sustained play on low-end devices (40+ minute sessions).
    /// </summary>
    public static class DevicePerformanceProfile
    {
        public static bool IsLowEnd { get; private set; }
        public static int TargetFrameRate { get; private set; } = 120;
        public static bool UseInstantEntrance { get; private set; }
        public static bool UseSimplifiedWinEffects { get; private set; }
        public static int BaselineArrowCount { get; private set; } = 70;
        public static int BaselineSegmentMultiplier { get; private set; } = 6;
        public static int MaxEffectPoolPerKey { get; private set; } = 48;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Apply(DetectLowEnd());
        }

        public static void Apply(bool lowEnd)
        {
            IsLowEnd = lowEnd;

            if (IsLowEnd)
            {
                TargetFrameRate = 60;
                UseInstantEntrance = true;
                UseSimplifiedWinEffects = true;
                BaselineArrowCount = 40;
                BaselineSegmentMultiplier = 5;
                MaxEffectPoolPerKey = 24;
            }
            else
            {
                TargetFrameRate = 120;
                UseInstantEntrance = false;
                UseSimplifiedWinEffects = false;
                BaselineArrowCount = 70;
                BaselineSegmentMultiplier = 6;
                MaxEffectPoolPerKey = 48;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }

        private static bool DetectLowEnd()
        {
            // Editor/dev builds keep the high-quality path unless forced.
#if UNITY_EDITOR
            return false;
#else
            int systemMemoryMb = SystemInfo.systemMemorySize;
            if (systemMemoryMb > 0 && systemMemoryMb <= 3072) return true;

            int gpuMemoryMb = SystemInfo.graphicsMemorySize;
            if (gpuMemoryMb > 0 && gpuMemoryMb <= 1024) return true;

            if (SystemInfo.processorCount > 0 && SystemInfo.processorCount <= 4) return true;

            return false;
#endif
        }
    }
}
