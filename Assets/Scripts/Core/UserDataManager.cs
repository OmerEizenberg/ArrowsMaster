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
        public event System.Action<int> OnCurrencyChanged;

        private const string LevelKey = "CurrentLevel";
        private const string InstallDateKey = "InstallDate";
        private const string ArrowsCurrencyKey = "ArrowsCurrency";

        public int CurrentLevel { get; private set; } = 1;
        public int ArrowsCurrency { get; private set; } = 0;
        public System.DateTime InstallDate { get; private set; }

        private Dictionary<string, int> m_MonthlyCache = new Dictionary<string, int>();

        private UserDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            CurrentLevel = PlayerPrefs.GetInt(LevelKey, 1);
            ArrowsCurrency = PlayerPrefs.GetInt(ArrowsCurrencyKey, 0);
            
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

        public void ResetProgress()
        {
            CurrentLevel = 1;
            ArrowsCurrency = 0;
            
            // Reset Monthly Challenge data
            ClearAllMonthlyProgress();

            // Reset Install Date (so the challenge starts from now)
            InstallDate = System.DateTime.Now;
            PlayerPrefs.SetString(InstallDateKey, InstallDate.ToBinary().ToString());

            SaveData(); // Helpers call PlayerPrefs.Save() but SaveData does too
            SaveCurrency(); 
            
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
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke();
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
    }
}
