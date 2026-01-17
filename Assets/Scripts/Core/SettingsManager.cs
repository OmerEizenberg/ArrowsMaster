using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Core
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("UI Toggles")]
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Toggle vibrationToggle;

        private const string SoundKey = "SoundEnabled";
        private const string VibrationKey = "VibrationEnabled";

        public bool IsSoundEnabled { get; private set; } = true;
        public bool IsVibrationEnabled { get; private set; } = true;

        private bool isSyncing = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            // Re-apply to ensure SoundManager.Instance is caught if it wasn't ready in Awake
            ApplySettings();
        }

        private void OnEnable()
        {
            SyncToggles();
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
