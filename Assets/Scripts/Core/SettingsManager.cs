using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using Firebase.Messaging;
using Firebase.Extensions;
#endif

namespace Assets.Scripts.Core
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("UI Toggles")]
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private GameObject pushNotificationToggle;

        private const string SoundKey = "SoundEnabled";
        private const string VibrationKey = "VibrationEnabled";

        public bool IsSoundEnabled { get; private set; } = true;
        public bool IsVibrationEnabled { get; private set; } = true;

        private bool isSyncing = false;

        private void Awake()
        {
            Instance = this;
            LoadSettings();

            CheckPushNotificationPermission();
        }

        private void Start()
        {
            // Re-apply to ensure SoundManager.Instance is caught if it wasn't ready in Awake
            ApplySettings();
        }

        private void OnEnable()
        {
            SyncToggles();
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowSettingsBanner();
            }
        }

        private void OnDisable()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.HideSettingsBanner();
            }
        }

        private void SyncToggles()
        {
            isSyncing = true;
            if (soundToggle != null) soundToggle.isOn = IsSoundEnabled;
            if (vibrationToggle != null) vibrationToggle.isOn = IsVibrationEnabled;
            isSyncing = false;
        }

        private void LoadSettings()
        {
            IsSoundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            IsVibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            
            ApplySettings();
        }

        public void onResetClicked()
        {
            UserDataManager.Instance.ResetProgress();
        }

        public void OnSoundToggleChanged(bool _)
        {
            if (soundToggle == null) return;
            bool enabled = soundToggle.isOn;

            // Play click BEFORE applying so it's heard even when muting
            if (!isSyncing && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

            IsSoundEnabled = enabled;
            PlayerPrefs.SetInt(SoundKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplySettings();
        }

        public void OnVibrationToggleChanged(bool _)
        {
            if (vibrationToggle == null) return;
            bool enabled = vibrationToggle.isOn;

            if (!isSyncing && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

            IsVibrationEnabled = enabled;
            PlayerPrefs.SetInt(VibrationKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplySettings();
        }

        public void PushNotificationClicked()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }

#if UNITY_ANDROID
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    if (sdkInt >= 33)
                    {
                        // Trigger native Android permission request flow
                        UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                        // We hide the button for this session to let the system handle the flow
                        if (pushNotificationToggle != null) pushNotificationToggle.SetActive(false);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SettingsManager] Error requesting Android notification permission: " + e.Message);
            }
#elif UNITY_IOS
            FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task => {
                if (task.IsCompleted && !task.IsFaulted) {
                    // Hide the toggle once requested successfully
                    if (pushNotificationToggle != null) 
                    {
                        pushNotificationToggle.SetActive(false);
                    }
                }
            });
#endif
        }


        private void CheckPushNotificationPermission()
        {
            if (pushNotificationToggle == null) return;

#if UNITY_EDITOR
            pushNotificationToggle.SetActive(false);
#elif UNITY_ANDROID
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdkInt = version.GetStatic<int>("SDK_INT");
                    // On Android 13 (API 33) and above, we need to check POST_NOTIFICATIONS permission
                    if (sdkInt >= 33)
                    {
                        bool hasPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS");
                        pushNotificationToggle.SetActive(!hasPermission);
                    }
                    else
                    {
                        // On older Android versions, permission is granted on install
                        pushNotificationToggle.SetActive(false);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SettingsManager] Error checking Android notification permission: " + e.Message);
                pushNotificationToggle.SetActive(false);
            }
#elif UNITY_IOS
            FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task => {
                if (task.IsCompleted && !task.IsFaulted) {
                    // On iOS, if we can't check the exact status without native plugins, 
                    // we'll hide the toggle if the request completes immediately, 
                    // which often indicates it's already been handled.
                    if (pushNotificationToggle != null)
                    {
                        pushNotificationToggle.SetActive(false);
                    }
                } else {
                    if (pushNotificationToggle != null)
                    {
                        pushNotificationToggle.SetActive(true);
                    }
                }
            });
#else
            pushNotificationToggle.SetActive(false);
#endif
        }

        private void ApplySettings()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetMute(!IsSoundEnabled);
            }

            VibrationManager.SetVibrationEnabled(IsVibrationEnabled);
        }
    }
}
