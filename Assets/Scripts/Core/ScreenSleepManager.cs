using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Prevents the device from auto-locking while the app is in the foreground.
    /// Uses Unity's cross-platform API (WakeLock on Android, idle timer disabled on iOS).
    /// </summary>
    public static class ScreenSleepManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreventScreenSleep()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
