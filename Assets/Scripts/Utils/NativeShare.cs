using UnityEngine;
using System.Runtime.InteropServices;

namespace Assets.Scripts.Utils
{
    public static class NativeShare
    {
        #if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _NativeShare_Share(string text, string url);
        #endif

        public static void Share(string text, string url, string subject = "")
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
                AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
                intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intentObject.Call<AndroidJavaObject>("setType", "text/plain");
                intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
                intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text + (string.IsNullOrEmpty(url) ? "" : "\n" + url));
                
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share via");
                currentActivity.Call("startActivity", chooser);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NativeShare] Android Share Error: " + ex.Message);
            }
            #elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                _NativeShare_Share(text, url);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NativeShare] iOS Share Error: " + ex.Message);
            }
            #else
            Debug.Log($"[NativeShare] Share (Simulated): {text} {url}");
            #endif
        }
    }
}
