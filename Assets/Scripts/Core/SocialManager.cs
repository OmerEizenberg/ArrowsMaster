#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using UnityEngine;
using UnityEngine.SocialPlatforms;
using System;

namespace Assets.Scripts.Core
{
    public class SocialManager : MonoBehaviour
    {
        public static SocialManager Instance { get; private set; }
        private bool isConnecting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("SocialManager");
                go.AddComponent<SocialManager>();
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
            InitializeSocial();
        }

        private void InitializeSocial()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[SocialManager] Initializing Google Play Games...");
            PlayGamesPlatform.Activate();
            SignIn();
#elif UNITY_IOS && !UNITY_EDITOR
            Debug.Log("[SocialManager] Initializing iOS Game Center...");
            SignIn();
#else
            Debug.Log("[SocialManager] Social features are only supported on Android or iOS devices.");
#endif
        }

        public void SignIn()
        {
            if (isConnecting) return;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            isConnecting = true;
            Debug.Log("[SocialManager] Starting Authentication...");

            UnityEngine.Social.localUser.Authenticate((bool success) =>
            {
                isConnecting = false;
                if (success)
                {
                    Debug.Log("[SocialManager] Sign-in successful!");
                }
                else
                {
                    Debug.LogWarning("[SocialManager] Sign-in failed.");
                }
            });
#endif
        }

        public bool IsAuthenticated()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return UnityEngine.Social.localUser.authenticated;
#else
            return false;
#endif
        }

        public void ShowLeaderboardUI()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                UnityEngine.Social.ShowLeaderboardUI();
            }
            else
            {
                Debug.Log("[SocialManager] Not authenticated, trying to sign in...");
                SignIn();
            }
#endif
        }

        public void ReportScore(long score, string leaderboardId)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                UnityEngine.Social.ReportScore(score, leaderboardId, (bool success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[SocialManager] Successfully reported score {score} to {leaderboardId}");
                    }
                    else
                    {
                        Debug.LogError($"[SocialManager] Failed to report score to {leaderboardId}");
                    }
                });
            }
#endif
        }

        public void UnlockAchievement(string milestone)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (IsAuthenticated())
            {
                string achievementId = GetPlatformAchievementId(milestone);
                if (string.IsNullOrEmpty(achievementId)) return;

                UnityEngine.Social.ReportProgress(achievementId, 100.0, (bool success) =>
                {
                    if (success)
                    {
                        Debug.Log($"[SocialManager] Successfully unlocked achievement {achievementId}");
                    }
                    else
                    {
                        Debug.LogError($"[SocialManager] Failed to unlock achievement {achievementId}");
                    }
                });
            }
#endif
        }

        private string GetPlatformAchievementId(string milestone)
        {
#if UNITY_ANDROID
            switch (milestone)
            {
                case "tutorial": return ACHIEVEMENT_ANDROID_TUTORIAL;
                case "lvl25": return ACHIEVEMENT_ANDROID_25;
                case "lvl50": return ACHIEVEMENT_ANDROID_50;
                case "lvl75": return ACHIEVEMENT_ANDROID_75;
                case "lvl100": return ACHIEVEMENT_ANDROID_100;
                case "lvl150": return ACHIEVEMENT_ANDROID_150;
                case "lvl200": return ACHIEVEMENT_ANDROID_200;
                case "lvl250": return ACHIEVEMENT_ANDROID_250;
                case "lvl300": return ACHIEVEMENT_ANDROID_300;
                case "lvl400": return ACHIEVEMENT_ANDROID_400;
                case "lvl500": return ACHIEVEMENT_ANDROID_500;
                case "lvl600": return ACHIEVEMENT_ANDROID_600;
                case "lvl750": return ACHIEVEMENT_ANDROID_750;
                case "lvl1000": return ACHIEVEMENT_ANDROID_1000;
            }
#elif UNITY_IOS
            switch (milestone)
            {
                // TODO: Replace these with your actual App Store Connect achievement IDs
                case "tutorial": return "com.everybodygames.arrowsmaster.tutorial";
                case "lvl25": return "com.everybodygames.arrowsmaster.lvl25";
                case "lvl50": return "com.everybodygames.arrowsmaster.lvl50";
                case "lvl75": return "com.everybodygames.arrowsmaster.lvl75";
                case "lvl100": return "com.everybodygames.arrowsmaster.lvl100";
                case "lvl150": return "com.everybodygames.arrowsmaster.lvl150";
                case "lvl200": return "com.everybodygames.arrowsmaster.lvl200";
                case "lvl250": return "com.everybodygames.arrowsmaster.lvl250";
                case "lvl300": return "com.everybodygames.arrowsmaster.lvl300";
                case "lvl400": return "com.everybodygames.arrowsmaster.lvl400";
                case "lvl500": return "com.everybodygames.arrowsmaster.lvl500";
                case "lvl600": return "com.everybodygames.arrowsmaster.lvl600";
                case "lvl750": return "com.everybodygames.arrowsmaster.lvl750";
                case "lvl1000": return "com.everybodygames.arrowsmaster.lvl1000";
            }
#endif
            return string.Empty;
        }

        #region Achievement IDs - Android
        public const string ACHIEVEMENT_ANDROID_TUTORIAL = "CgkIkrOzieYREAIQAQ";
        public const string ACHIEVEMENT_ANDROID_25 = "CgkIkrOzieYREAIQAw";
        public const string ACHIEVEMENT_ANDROID_50 = "CgkIkrOzieYREAIQAg";
        public const string ACHIEVEMENT_ANDROID_75 = "CgkIkrOzieYREAIQBA";
        public const string ACHIEVEMENT_ANDROID_100 = "CgkIkrOzieYREAIQBQ";
        public const string ACHIEVEMENT_ANDROID_150 = "CgkIkrOzieYREAIQBg";
        public const string ACHIEVEMENT_ANDROID_200 = "CgkIkrOzieYREAIQBw";
        public const string ACHIEVEMENT_ANDROID_250 = "CgkIkrOzieYREAIQCA";
        public const string ACHIEVEMENT_ANDROID_300 = "CgkIkrOzieYREAIQCQ";
        public const string ACHIEVEMENT_ANDROID_400 = "CgkIkrOzieYREAIQCg";
        public const string ACHIEVEMENT_ANDROID_500 = "CgkIkrOzieYREAIQCw";
        public const string ACHIEVEMENT_ANDROID_600 = "CgkIkrOzieYREAIQDA";
        public const string ACHIEVEMENT_ANDROID_750 = "CgkIkrOzieYREAIQDQ";
        public const string ACHIEVEMENT_ANDROID_1000 = "CgkIkrOzieYREAIQDg";
        #endregion
    }
}
