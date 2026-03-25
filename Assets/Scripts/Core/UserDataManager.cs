using System;
using UnityEngine;
using Assets.Scripts.Data;
using System.Collections.Generic;

namespace Assets.Scripts.Core
{
    [Serializable]
    public class LevelProgress
    {
        public string levelId;
        public bool isChallenge;
        public List<int> pickedArrowIds = new List<int>();
        public float remainingTime;
        public float levelDuration;
        public int remainingLives;
        public bool isTimerActive;
        public int challengeYear;
        public int challengeMonth;
        public int challengeDay;
        public int collectedCoins;
        public bool hasProgress = false;
    }

    public class UserDataManager
    {
        private static UserDataManager instance;
        public static UserDataManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UserDataManager();
                }
                return instance;
            }
        }

        public event System.Action OnLevelChanged;
        public event System.Action<int> OnCurrencyChanged;

        private const string LevelKey = "CurrentLevel";
        private const string InstallDateKey = "InstallDate";
        private const string ArrowsCurrencyKey = "ArrowsCurrency";
        private const string CurrentLevelAttemptsKey = "CurrentLevelAttempts";
        private const string LastAttemptLevelIdKey = "LastAttemptLevelId";
        private const string MaxStreakKey = "MaxStreakRecord";
        private const string LevelProgressKey = "LevelProgress";
        private const string LastRateUsDateKey = "LastRateUsDate";
        private const string LevelStreakKey = "LevelStreak";

        public int CurrentLevel { get; private set; } = 1;
        public int ArrowsCurrency { get; private set; } = 0;
        private int _currentLevelAttempts = 0;
        public int CurrentLevelAttempts 
        { 
            get => _currentLevelAttempts; 
            private set 
            {
                _currentLevelAttempts = value;
                Debug.Log($"[UserDataManager] CurrentLevelAttempts changed to: {_currentLevelAttempts}");
            }
        }
        public string LastAttemptLevelId { get; private set; } = string.Empty;
        public int MaxStreak { get; private set; } = 0;
        public int LevelStreak { get; private set; } = 0;
        public System.DateTime InstallDate { get; private set; }
        public System.DateTime LastRateUsDate { get; private set; }
        public bool IsRateUsCheckPending { get; set; } = false;


        private Dictionary<string, int> m_MonthlyCache = new Dictionary<string, int>();

        private UserDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            CurrentLevel = PlayerPrefs.GetInt(LevelKey, 1);
            ArrowsCurrency = PlayerPrefs.GetInt(ArrowsCurrencyKey, 0);
            CurrentLevelAttempts = PlayerPrefs.GetInt(CurrentLevelAttemptsKey, 0);
            LastAttemptLevelId = PlayerPrefs.GetString(LastAttemptLevelIdKey, string.Empty);
            MaxStreak = PlayerPrefs.GetInt(MaxStreakKey, 0);
            LevelStreak = PlayerPrefs.GetInt(LevelStreakKey, 0);
            
            string installDateStr = PlayerPrefs.GetString(InstallDateKey, string.Empty);
            if (string.IsNullOrEmpty(installDateStr))
            {
                InstallDate = System.DateTime.Now;
                PlayerPrefs.SetString(InstallDateKey, InstallDate.ToBinary().ToString());
                PlayerPrefs.Save();
            }
            else
            {
                if (long.TryParse(installDateStr, out long binaryDate))
                {
                    InstallDate = System.DateTime.FromBinary(binaryDate);
                }
                else
                {
                    InstallDate = System.DateTime.Now;
                }
            }
            
            string lastRateUsDateStr = PlayerPrefs.GetString(LastRateUsDateKey, string.Empty);
            if (!string.IsNullOrEmpty(lastRateUsDateStr))
            {
                if (long.TryParse(lastRateUsDateStr, out long binaryDate))
                {
                    LastRateUsDate = System.DateTime.FromBinary(binaryDate);
                }
            }
        }

        public void IncrementLevel()
        {
            CurrentLevel++;
            SaveData();
        }

        public void SetLevel(int level)
        {
            CurrentLevel = level;
            SaveData();
        }

        public void AddArrowsCurrency(int amount)
        {
            if (amount < 0) return; // Prevent negative addition
            ArrowsCurrency += amount;
            SaveCurrency();
        }

        public bool ReduceArrowsCurrency(int amount)
        {
            if (amount < 0) return false;
            
            if (ArrowsCurrency >= amount)
            {
                ArrowsCurrency -= amount;
                SaveCurrency();
                return true;
            }
            return false;
        }

        private void SaveCurrency()
        {
            PlayerPrefs.SetInt(ArrowsCurrencyKey, ArrowsCurrency);
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke(ArrowsCurrency);
        }

        public void MarkRateUsSeen()
        {
            LastRateUsDate = System.DateTime.Now;
            PlayerPrefs.SetString(LastRateUsDateKey, LastRateUsDate.ToBinary().ToString());
            PlayerPrefs.Save();
            IsRateUsCheckPending = false;
        }

        public void ResetProgress()
        {
            CurrentLevel = 1;
            
            // Reset Monthly Challenge data
            ClearAllMonthlyProgress();

            // Reset Install Date (so the challenge starts from now)
            InstallDate = System.DateTime.Now;
            PlayerPrefs.SetString(InstallDateKey, InstallDate.ToBinary().ToString());

            SaveData(); // Helpers call PlayerPrefs.Save() but SaveData does too
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }
            
            OnMonthlyProgressChanged?.Invoke();
        }

        private void ClearAllMonthlyProgress()
        {
            // Clear cache
            m_MonthlyCache.Clear();

            // Clear PlayerPrefs for monthly challenges
            // We'll clear a search range to ensure everything is removed
            for (int year = 2024; year <= 2030; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    string key = GetMonthlyKey(year, month);
                    PlayerPrefs.DeleteKey(key);
                }
            }
            PlayerPrefs.Save();
        }

        private void SaveData()
        {
            PlayerPrefs.SetInt(LevelKey, CurrentLevel);
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.SetString(LastAttemptLevelIdKey, LastAttemptLevelId);
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke();
        }

        public void IncrementCurrentLevelAttempts()
        {
            CurrentLevelAttempts++;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.Save();
        }

        public void ResetCurrentLevelAttempts(string levelId)
        {
            CurrentLevelAttempts = 1;
            LastAttemptLevelId = levelId;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.SetString(LastAttemptLevelIdKey, LastAttemptLevelId);
            PlayerPrefs.Save();
        }

        public void ClearCurrentLevelAttempts()
        {
            CurrentLevelAttempts = 0;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.Save();
        }

        public void UpdateMaxStreak(int streak)
        {
            if (streak > MaxStreak)
            {
                MaxStreak = streak;
                PlayerPrefs.SetInt(MaxStreakKey, MaxStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] New Max Streak Record: {MaxStreak}");
            }
        }

        public void IncrementLevelStreak()
        {
            if (CurrentLevel >= 25)
            {
                LevelStreak++;
                NeedsLevelStreakAnimation = true;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak incremented to: {LevelStreak}");
            }
        }

        public bool NeedsLevelStreakAnimation { get; set; } = false;

        public void ResetLevelStreak()
        {
            if (LevelStreak > 0)
            {
                LevelStreak = 0;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak reset.");
            }
        }

        public void RestoreLevelStreak(int previousStreak)
        {
            if (previousStreak > 0)
            {
                LevelStreak = previousStreak;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak restored to: {LevelStreak}");
            }
        }

        private string GetMonthlyKey(int year, int month)
        {
            return $"MonthlyChallenge_{year}_{month}";
        }

        public event System.Action OnMonthlyProgressChanged;

        public void SaveMonthlyChallengeProgress(int year, int month, int day)
        {
            string key = GetMonthlyKey(year, month);
            int currentMask = GetMonthlyChallengeBitmask(year, month);
            
            // Set the bit corresponding to the day (day 1 is bit 0)
            int newMask = currentMask | (1 << (day - 1));
            
            if (newMask != currentMask)
            {
                m_MonthlyCache[key] = newMask;
                PlayerPrefs.SetInt(key, newMask);
                PlayerPrefs.Save();
                OnMonthlyProgressChanged?.Invoke();
            }
        }

        public int GetMonthlyChallengeBitmask(int year, int month)
        {
            string key = GetMonthlyKey(year, month);
            if (m_MonthlyCache.ContainsKey(key))
            {
                return m_MonthlyCache[key];
            }

            int mask = PlayerPrefs.GetInt(key, 0);
            m_MonthlyCache[key] = mask;
            return mask;
        }

        public bool IsDayCompleted(int year, int month, int day)
        {
            int mask = GetMonthlyChallengeBitmask(year, month);
            return (mask & (1 << (day - 1))) != 0;
        }

        public void SaveLevelProgress(LevelProgress progress)
        {
            if (progress == null)
            {
                ClearLevelProgress();
                return;
            }
            string json = JsonUtility.ToJson(progress);
            PlayerPrefs.SetString(LevelProgressKey, json);
            PlayerPrefs.Save();
        }

        public LevelProgress LoadLevelProgress()
        {
            string json = PlayerPrefs.GetString(LevelProgressKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new LevelProgress { hasProgress = false };
            }
            try
            {
                LevelProgress progress = JsonUtility.FromJson<LevelProgress>(json);
                return progress;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UserDataManager] Error loading level progress: {e.Message}");
                return new LevelProgress { hasProgress = false };
            }
        }

        public void ClearLevelProgress()
        {
            PlayerPrefs.DeleteKey(LevelProgressKey);
            PlayerPrefs.Save();
        }
    }
}
