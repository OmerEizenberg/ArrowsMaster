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
        public AudioClip SmallCheer;
        public AudioClip MediumCheer;
        public AudioClip BigCheer;
        public AudioClip ThumbUp;
        public AudioClip HintIn;
        public AudioClip BackgroundMusic;
        public AudioClip PerfectVoice;
        public AudioClip GoodVoice;
        public AudioClip ExcellentVoice;
        public AudioClip NiceVoice;
        public AudioClip AmazingVoice;
        public AudioClip FireOnSound;
        public AudioClip[] streakSounds;
        public AudioClip ShopSound;
        public AudioClip NewRecordSound;
        public AudioClip MagicBoosterSound;
        public AudioClip HintBoosterSound;
        public AudioClip RefillBoosterSound;
        public AudioClip HeartNudgeSound;

        private float m_LastNewRecordSoundTime = -30f;

        [SerializeField] private AudioSource musicSource;

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

            if (musicSource == null)
            {
                // Create a secondary source for looping music
                GameObject musicObj = new GameObject("MusicSource");
                musicObj.transform.SetParent(this.transform);
                musicSource = musicObj.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            // Load initial state from PlayerPrefs to ensure synchronization
            isMuted = PlayerPrefs.GetInt("SoundEnabled", 1) == 0;
            ApplyMuteState();
            
            StartBackgroundMusic();
        }

        private void StartBackgroundMusic()
        {
            if (BackgroundMusic != null && musicSource != null)
            {
                musicSource.clip = BackgroundMusic;
                musicSource.Play();
            }
        }
        public void PlayStreak(int index=0)
        {
            if (index > streakSounds.Length-1)
            {
                index = streakSounds.Length-1;
            }

            PlaySound(streakSounds[index]);
        }

        public void PlayClick()
        {
            PlaySound(ClickSound);
            VibrationManager.VibrateSelection();
        }
        public void PlayHint()
        {
            PlaySound(HintIn);
        }
        public void PlayGood()
        {
            PlaySound(GoodVoice);
        }
        public void PlayNice()
        {
            PlaySound(NiceVoice);
        }
        public void PlayAmazing()
        {
            PlaySound(AmazingVoice);
        }
        public void PlayExcellent()
        {
            PlaySound(ExcellentVoice);
        }
        public void PlayPerfect()
        {
            PlaySound(PerfectVoice);
        }

        public void PlayLike()
        {
            PlaySound(ThumbUp);
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

        public void PlayFireOn()
        {
            PlaySound(FireOnSound);
        }

        public void PlayShop()
        {
            PlaySound(ShopSound);
        }

        public void PlayNewRecord()
        {
            if (Time.time - m_LastNewRecordSoundTime >= 30f)
            {
                PlaySound(NewRecordSound);
                m_LastNewRecordSoundTime = Time.time;
            }
        }

        public void PlaySmallCheer()
        {
            PlaySound(SmallCheer);
        }

        public void PlayMediumCheer()
        {
            PlaySound(MediumCheer);
        }

        public void PlayBigCheer()
        {
            PlaySound(BigCheer);
        }

        public void PlayMagicBooster()
        {
            PlaySound(MagicBoosterSound);
        }

        public void PlayHintBooster()
        {
            PlaySound(HintBoosterSound);
        }

        public void PlayRefillBooster()
        {
            PlaySound(RefillBoosterSound);
        }

        public void PlayShuffleBooster()
        {
            PlaySound(HintBoosterSound != null ? HintBoosterSound : MagicBoosterSound);
        }

        public void PlayHeartNudge()
        {
            PlaySound(HeartNudgeSound != null ? HeartNudgeSound : RefillBoosterSound);
        }

        public void SetMute(bool mute)
        {
            isMuted = mute;
            ApplyMuteState();
            Debug.Log($"[SoundManager] Mute state set to: {mute}");
        }

        private void ApplyMuteState()
        {
            if (audioSource != null)
            {
                audioSource.mute = isMuted;
            }
            if (musicSource != null)
            {
                musicSource.mute = isMuted;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            // Double check both the boolean and the source property
            bool effectivelyMuted = isMuted || (audioSource != null && audioSource.mute);
            if (effectivelyMuted) return;

            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
