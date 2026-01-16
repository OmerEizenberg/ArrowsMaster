using System;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public LevelManager levelManager;
        public GameObject failureScreen;
        public GameObject winScreen;

        [Header("Settings")]
        public int maxLives = 3;

        public int CurrentLives { get; private set; }
        private int activeArrowsCount = 0;

        // Events
        public event Action<int> OnLivesChanged;
        public event Action OnLevelStarted;
        public event Action OnGameOver;
        public event Action OnLevelWon;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (failureScreen != null) failureScreen.SetActive(false);
            if (winScreen != null) winScreen.SetActive(false);
        }

        private void Start()
        {
            CurrentLives = maxLives;
        }

        public void StartLevel(string levelId)
        {
            ResetLives();
            HideScreens();
            
            // Reset arrow count before loading new level
            activeArrowsCount = 0; 

            if (levelManager != null)
            {
                levelManager.LoadLevelFromResources(levelId);
            }

            OnLevelStarted?.Invoke();
        }

        private void ResetLives()
        {
            CurrentLives = maxLives;
            OnLivesChanged?.Invoke(CurrentLives);
        }

        public void RegisterArrow()
        {
            activeArrowsCount++;
        }

        public void NotifyArrowSuccess()
        {
            if (activeArrowsCount > 0)
            {
                activeArrowsCount--;
                if (activeArrowsCount == 0)
                {
                    StartCoroutine(WinSequence());
                }
            }
        }

        private System.Collections.IEnumerator WinSequence()
        {
            Debug.Log("Level Complete! Waiting for win screen...");
            yield return new WaitForSeconds(1.5f);
            
            if (winScreen != null)
            {
                winScreen.SetActive(true);
            }
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayWin();
            }

            OnLevelWon?.Invoke();
        }

        public void LoseLife()
        {
            if (CurrentLives > 0)
            {
                CurrentLives--;
                OnLivesChanged?.Invoke(CurrentLives);

                if (CurrentLives <= 0)
                {
                    HandleGameOver();
                }
            }
        }

        public void GainLife()
        {
            if (CurrentLives < maxLives) 
            {
                CurrentLives++;
                OnLivesChanged?.Invoke(CurrentLives);
            }
        }

        private void HandleGameOver()
        {
            Debug.Log("Game Over!");
            if (failureScreen != null)
            {
                failureScreen.SetActive(true);
            }
            OnGameOver?.Invoke();
        }

        public void HideScreens()
        {
            if (failureScreen != null) failureScreen.SetActive(false);
            if (winScreen != null) winScreen.SetActive(false);
        }

        public void HideFailureScreen()
        {
            if (failureScreen != null)
            {
                failureScreen.SetActive(false);
            }
        }
    }
}
