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
        public GameObject m_GameUI;
        public GameObject m_LobbyUI;

        [Header("Settings")]
        public int maxLives = 3;

        public int CurrentLives { get; private set; }
        private int activeArrowsCount = 0;

        // Events
        public event Action<int> OnLivesChanged;
        public event Action OnLevelStarted;
        public event Action OnGameOver;
        public event Action OnLevelWon;
        public event Action<bool> OnHintVisibilityChanged;
        public bool p_isLevelProgression = true;

        public int currentChallengeYear;
        public int currentChallengeMonth;
        public int currentChallengeDay;

        private float hintTimer = 0f;
        private bool isEntranceFinished = false;
        private bool isHintVisible = false;
        private bool isWinning = false;

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
        }

        private void Start()
        {
            CurrentLives = maxLives;
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnPlayOnRewardGranted += HandlePlayOnRewardGranted;
                AdsManager.Instance.OnHintRewardGranted += HandleHintRewardGranted;
            }

            if (levelManager != null)
            {
                levelManager.OnEntranceAnimationFinished += () => {
                    isEntranceFinished = true;
                    ResetHintTimer();
                };
            }
        }

        private void OnDestroy()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnPlayOnRewardGranted -= HandlePlayOnRewardGranted;
                AdsManager.Instance.OnHintRewardGranted -= HandleHintRewardGranted;
            }
        }

        private void HandlePlayOnRewardGranted()
        {
            // Give 3 lives back and hide failure screen
            ResetLives();
            HideFailureScreen();
        }

        private void HandleHintRewardGranted()
        {
            Debug.Log("Hint Reward Granted! Showing hint...");
            // Trigger actual hint mechanism here
            ShowHint();
        }

        private void ShowHint()
        {
            // Logic to show a hint: find an arrow that can actually be picked
            ArrowController[] arrows = GameObject.FindObjectsOfType<ArrowController>();
            ArrowController bestArrow = null;

            foreach (var arrow in arrows)
            {
                if (arrow != null && arrow.gameObject.activeInHierarchy && arrow.CanMoveForward())
                {
                    bestArrow = arrow;
                    break;
                }
            }

            // Fallback to any arrow if none are "clear" (though there should be one)
            if (bestArrow == null && arrows.Length > 0) bestArrow = arrows[0];

            if (bestArrow != null)
            {
                // 1. Pan Camera to the head of the pickable arrow
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.FocusOn(bestArrow.GetHeadPosition(), 0.5f);
                }

                // 2. Flash trajectory preview
                bestArrow.ShowPreview();
                bestArrow.Invoke("HidePreview", 3.0f);
            }

            ResetHintTimer();
        }

        public void PlayOn()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowPlayOnRewarded();
            }
            else
            {
                // Fallback if no AdsManager
                HandlePlayOnRewardGranted();
            }
        }

        public void RestartCurrentLevel()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial();
            }

            if (levelManager != null && !string.IsNullOrEmpty(levelManager.CurrentLevelId))
            {
                if (p_isLevelProgression)
                {
                    StartLevel(levelManager.CurrentLevelId);
                }
                else
                {
                    StartChallengeLevel(levelManager.CurrentLevelId, currentChallengeYear, currentChallengeMonth, currentChallengeDay);
                }
            }
        }

        public void StartLevel(string levelId)
        {
            ResetLives();
            HideScreens();
            
            p_isLevelProgression = true;
            // Reset arrow count before loading new level
            activeArrowsCount = 0; 

            if (levelManager != null)
            {
                levelManager.LoadLevelFromResources(levelId);
            }

            OnLevelStarted?.Invoke();
            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
        }

        public void StartChallengeLevel(string levelId, int year, int month, int day)
        {
            ResetLives();
            HideScreens();
            
            p_isLevelProgression = false;
            currentChallengeYear = year;
            currentChallengeMonth = month;
            currentChallengeDay = day;

            // Reset arrow count before loading new level
            activeArrowsCount = 0; 

            if (levelManager != null)
            {
                levelManager.LoadChallengeLevelFromResources(levelId);
            }

            OnLevelStarted?.Invoke();
            isEntranceFinished = false;
            isWinning = false;
            ResetHintTimer();
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
                    isWinning = true;
                    SetHintVisibility(false);
                    StartCoroutine(WinSequence());
                }
            }
        }

        private System.Collections.IEnumerator WinSequence()
        {
            Debug.Log("Level Complete! Waiting for win screen...");
            if(p_isLevelProgression)
            {
                UserDataManager.Instance.IncrementLevel();
            }
            else
            {
                UserDataManager.Instance.SaveMonthlyChallengeProgress(currentChallengeYear, currentChallengeMonth, currentChallengeDay);
            }

            if (levelManager != null)
            {
                levelManager.PlayWinAnimation();
            }

            yield return new WaitForSeconds(1.5f);
            
            if (m_GameUI != null)
            {
                m_GameUI.SetActive(false);
            }
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayWin();
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowInterstitial();
            }

            m_LobbyUI.SetActive(true);

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
        }

        public void HideFailureScreen()
        {
            if (failureScreen != null)
            {
                failureScreen.SetActive(false);
            }
        }

        private void Update()
        {
            if (isEntranceFinished && !isWinning && !isHintVisible)
            {
                hintTimer += Time.deltaTime;
                if (hintTimer >= 5.0f)
                {
                    SetHintVisibility(true);
                }
            }
        }

        public void ResetHintTimer()
        {
            hintTimer = 0f;
            SetHintVisibility(false);
        }

        private void SetHintVisibility(bool visible)
        {
            isHintVisible = visible;
            OnHintVisibilityChanged?.Invoke(visible);
        }
    }
}
