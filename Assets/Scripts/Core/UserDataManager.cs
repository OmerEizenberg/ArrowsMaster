using UnityEngine;
using Assets.Scripts.Data;
using System.Collections.Generic;

namespace Assets.Scripts.Core
{
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

        private const string LevelKey = "CurrentLevel";
        private const string InstallDateKey = "InstallDate";

        public int CurrentLevel { get; private set; } = 1;
        public System.DateTime InstallDate { get; private set; }

        private UserDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            CurrentLevel = PlayerPrefs.GetInt(LevelKey, 1);
            
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

        public void ResetProgress()
        {
            CurrentLevel = 1;
            SaveData();
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }
        }

        private void SaveData()
        {
            PlayerPrefs.SetInt(LevelKey, CurrentLevel);
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke();
        }

        private string GetMonthlyKey(int year, int month)
        {
            return $"MonthlyChallenge_{year}_{month}";
        }

        public void SaveMonthlyChallengeProgress(int year, int month, int day)
        {
            string key = GetMonthlyKey(year, month);
            int currentMask = PlayerPrefs.GetInt(key, 0);
            
            // Set the bit corresponding to the day (day 1 is bit 0)
            int newMask = currentMask | (1 << (day - 1));
            
            if (newMask != currentMask)
            {
                PlayerPrefs.SetInt(key, newMask);
                PlayerPrefs.Save();
            }
        }

        public int GetMonthlyChallengeBitmask(int year, int month)
        {
            return PlayerPrefs.GetInt(GetMonthlyKey(year, month), 0);
        }

        public bool IsDayCompleted(int year, int month, int day)
        {
            int mask = GetMonthlyChallengeBitmask(year, month);
            return (mask & (1 << (day - 1))) != 0;
        }
    }
}
