using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;
using System;

namespace Assets.Scripts.Core
{
    public class PlayGamesManager : MonoBehaviour
    {
        public static PlayGamesManager Instance { get; private set; }
        private bool isConnecting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("PlayGamesManager");
                go.AddComponent<PlayGamesManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeGPG();
        }

        private void InitializeGPG()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[PlayGamesManager] Initializing Google Play Games...");
            
            // Activate the Google Play Games platform
            PlayGamesPlatform.Activate();

            // Perform Silent Sign-In
            SignIn();
#else
            Debug.Log("[PlayGamesManager] Google Play Games is only supported on Android devices.");
#endif
        }

        public void SignIn()
        {
            if (isConnecting) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            isConnecting = true;
            Debug.Log("[PlayGamesManager] Starting Authentication...");

            PlayGamesPlatform.Instance.Authenticate((SignInStatus status) =>
            {
                isConnecting = false;
                if (status == SignInStatus.Success)
                {
                    Debug.Log("[PlayGamesManager] Sign-in successful!");
                    // Handle success (e.g., sync cloud saves or update leaderboards)
                }
                else
                {
                    Debug.LogWarning($"[PlayGamesManager] Sign-in failed with status: {status}");
                }
            });
#endif
        }

        public bool IsAuthenticated()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return PlayGamesPlatform.Instance.IsAuthenticated();
#else
            return false;
#endif
        }

        public void ShowLeaderboardUI()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                PlayGamesPlatform.Instance.ShowLeaderboardUI();
            }
            else
            {
                Debug.Log("[PlayGamesManager] Not authenticated, trying to sign in...");
                SignIn();
            }
#endif
        }

        public void ReportScore(long score, string leaderboardId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                Social.ReportScore(score, leaderboardId, (bool success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[PlayGamesManager] Successfully reported score {score} to {leaderboardId}");
                    }
                    else
                    {
                        Debug.LogError($"[PlayGamesManager] Failed to report score to {leaderboardId}");
                    }
                });
            }
#endif
        }

        public void UnlockAchievement(string achievementId)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                Social.ReportProgress(achievementId, 100.0f, (bool success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[PlayGamesManager] Successfully unlocked achievement {achievementId}");
                    }
                    else
                    {
                        Debug.LogError($"[PlayGamesManager] Failed to unlock achievement {achievementId}");
                    }
                });
            }
#endif
        }

        #region Achievement IDs
        public const string ACHIEVEMENT_FINISH_TUTORIAL = "CgkIkrOzieYREAIQAQ";
        public const string ACHIEVEMENT_COMPLETED_25_LEVELS = "CgkIkrOzieYREAIQAw";
        public const string ACHIEVEMENT_COMPLETED_50_LEVELS = "CgkIkrOzieYREAIQAg";
        public const string ACHIEVEMENT_COMPLETED_75_LEVELS = "CgkIkrOzieYREAIQBA";
        public const string ACHIEVEMENT_COMPLETED_100_LEVELS = "CgkIkrOzieYREAIQBQ";
        public const string ACHIEVEMENT_COMPLETED_150_LEVELS = "CgkIkrOzieYREAIQBg";
        public const string ACHIEVEMENT_COMPLETED_200_LEVELS = "CgkIkrOzieYREAIQBw";
        public const string ACHIEVEMENT_COMPLETED_250_LEVELS = "CgkIkrOzieYREAIQCA";
        public const string ACHIEVEMENT_COMPLETED_300_LEVELS = "CgkIkrOzieYREAIQCQ";
        public const string ACHIEVEMENT_COMPLETED_400_LEVELS = "CgkIkrOzieYREAIQCg";
        public const string ACHIEVEMENT_COMPLETED_500_LEVELS = "CgkIkrOzieYREAIQCw";
        public const string ACHIEVEMENT_COMPLETED_600_LEVELS = "CgkIkrOzieYREAIQDA";
        public const string ACHIEVEMENT_COMPLETED_750_LEVELS = "CgkIkrOzieYREAIQDQ";
        public const string ACHIEVEMENT_COMPLETED_1000_LEVELS = "CgkIkrOzieYREAIQDg";
        #endregion
    }
}
