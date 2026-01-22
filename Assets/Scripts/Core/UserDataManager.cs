using UnityEngine;

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
    }
}
