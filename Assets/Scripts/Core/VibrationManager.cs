using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Assets.Scripts.Core
{
    public static class VibrationManager
    {
        private static bool isVibrationEnabled = true;

        #if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _triggerHapticFeedback(int type);
        #endif

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
        /// Triggers a subtle selection vibration.
        /// </summary>
        public static void VibrateSelection()
        {
            if (!isVibrationEnabled) return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(15, 60); // Very short, low amplitude
            #elif UNITY_IOS && !UNITY_EDITOR
            _triggerHapticFeedback(0); // Selection Feedback
            #endif
        }

        /// <summary>
        /// Triggers a success feedback.
        /// </summary>
        public static void VibrateSuccess()
        {
            if (!isVibrationEnabled) return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(50, 180); 
            #elif UNITY_IOS && !UNITY_EDITOR
            _triggerHapticFeedback(4); 
            #endif
        }

        /// <summary>
        /// Triggers a warning feedback.
        /// </summary>
        public static void VibrateWarning()
        {
            if (!isVibrationEnabled) return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(40, 150); 
            #elif UNITY_IOS && !UNITY_EDITOR
            _triggerHapticFeedback(5); 
            #endif
        }

        /// <summary>
        /// Triggers an error feedback.
        /// </summary>
        public static void VibrateError()
        {
            if (!isVibrationEnabled) return;

            #if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(80, 200); 
            #elif UNITY_IOS && !UNITY_EDITOR
            _triggerHapticFeedback(6); 
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass vibrationEffectClass;
        private static int apiLevel = -1;

        private static void VibrateAndroid(long milliseconds, int amplitude = -1)
        {
            try
            {
                if (vibrator == null)
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }

                if (apiLevel == -1)
                {
                    using (AndroidJavaClass buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        apiLevel = buildVersion.GetStatic<int>("SDK_INT");
                    }
                }

                if (vibrator != null)
                {
                    if (apiLevel >= 26)
                    {
                        if (vibrationEffectClass == null)
                        {
                            vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        }
                        
                        // Use createOneShot with amplitude if supported
                        // amplitude is 1-255, or -1 for default
                        AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                        vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VibrationManager] Android Vibration failed: {e.Message}");
            }
        }
        #endif
    }
}


