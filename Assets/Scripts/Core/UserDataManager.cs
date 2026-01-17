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
        public int CurrentLevel { get; private set; } = 1;

        private UserDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            CurrentLevel = PlayerPrefs.GetInt(LevelKey, 1);
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
