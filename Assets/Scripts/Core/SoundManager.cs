using UnityEngine;

namespace Assets.Scripts.Core
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Source")]
        [SerializeField] private AudioSource audioSource;

        [Header("Clips")]
        public AudioClip ClickSound;
        public AudioClip ArrowSelectSound;
        public AudioClip ArrowBlockedSound;

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
        }

        public void PlayClick()
        {
            PlaySound(ClickSound);
        }

        public void PlayArrowSelect()
        {
            PlaySound(ArrowSelectSound);
        }

        public void PlayArrowBlocked()
        {
            PlaySound(ArrowBlockedSound);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
