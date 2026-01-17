using UnityEngine;

namespace Assets.Scripts.Core
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;
        private bool isMuted = false;

        [Header("Clips")]
        public AudioClip ClickSound;
        public AudioClip ArrowSelectSound;
        public AudioClip ArrowBlockedSound;
        public AudioClip LevelInitializedSound;
        public AudioClip WinSound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Load initial state from PlayerPrefs to ensure synchronization
            isMuted = PlayerPrefs.GetInt("SoundEnabled", 1) == 0;
            if (audioSource != null)
            {
                audioSource.mute = isMuted;
            }
        }

        public void PlayClick()
        {
            PlaySound(ClickSound);
            VibrationManager.VibrateSelection();
        }

        public void PlayArrowSelect()
        {
            PlaySound(ArrowSelectSound);
        }

        public void PlayArrowBlocked()
        {
            PlaySound(ArrowBlockedSound);
        }

        public void PlayLevelInitialized()
        {
            PlaySound(LevelInitializedSound);
        }

        public void PlayWin()
        {
            PlaySound(WinSound);
        }

        public void SetMute(bool mute)
        {
            isMuted = mute;
            if (audioSource != null)
            {
                audioSource.mute = mute;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (isMuted) return;
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
