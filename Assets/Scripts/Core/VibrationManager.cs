using UnityEngine;

namespace Assets.Scripts.Core
{
    public static class VibrationManager
    {
        private static bool isVibrationEnabled = true;

        static VibrationManager()
        {
            // Initial load from PlayerPrefs
            isVibrationEnabled = PlayerPrefs.GetInt("VibrationEnabled", 1) == 1;
        }

        public static void SetVibrationEnabled(bool enabled)
        {
            isVibrationEnabled = enabled;
        }

        /// <summary>
        /// Triggers a small, subtle vibration suitable for tile selection or button click.
        /// </summary>
        public static void VibrateSelection()
        {
            if (!isVibrationEnabled) return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(50);
            #elif UNITY_IOS && !UNITY_EDITOR
            // Unity's default Handheld.Vibrate() is a bit long on iOS.
            // For a truly "small" vibration on iOS, one would typically use Taptic Engine via plugin.
            // We use the basic vibrate as a fallback.
            Handheld.Vibrate();
            #else
            // Editor or other platforms
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static void VibrateAndroid(long milliseconds)
        {
            if (vibrator == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
            }

            if (vibrator != null)
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
        #endif
    }
}
