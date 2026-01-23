using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro; 

using Assets.Scripts.Core;
using Assets.Scripts.Data;

namespace Assets.Scripts.Lobby
{
    public class HomeContoller : MonoBehaviour
    {
        [SerializeField] private GameObject m_LobbyUI;
        [SerializeField] private GameObject m_GameUI;


        [SerializeField] private GameObject m_CalanderLayer;
        [SerializeField] private GameObject m_SettingsLayer;

        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_DifficultyText;

        [SerializeField] private Color m_CircleColor;
        [SerializeField] private Color m_SuperHardColor;
        [SerializeField] private Color m_NightmareColor;
        [SerializeField] private Color m_HardColor;
        [SerializeField] private Color m_EasyColor;
        
        [SerializeField] private Color m_LevelColor;
        [SerializeField] private MonthlyChallengeController m_MonthlyChallengeController;

        private void OnEnable()
        {
            RefreshLobbyUI();
            UserDataManager.Instance.OnLevelChanged += RefreshLobbyUI;
            if(GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                OnCalanderButtonClicked();
            }
        }

        private void OnDisable()
        {
            UserDataManager.Instance.OnLevelChanged -= RefreshLobbyUI;
        }

        public void RefreshLobbyUI()
        {
            m_TitleText.text = "Arrows Master";
            
            string levelId;
            string folder;
            
            if (GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                m_LevelText.text = $"Challenge {m_MonthlyChallengeController.p_CurrentMonth}/{m_MonthlyChallengeController.p_CurrentDay}/{m_MonthlyChallengeController.p_CurrentYear}";
                int month = m_MonthlyChallengeController.p_CurrentMonth;
                int day = m_MonthlyChallengeController.p_CurrentDay;
                int year = m_MonthlyChallengeController.p_CurrentYear;
                levelId = $"level{month + day + (year % 10)}";
                folder = "ChallengeLevels";
            }
            else
            {
                m_LevelText.text = $"Level {UserDataManager.Instance.CurrentLevel}";
                levelId = $"level{UserDataManager.Instance.CurrentLevel}";
                folder = "Levels";
            }

            TextAsset jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId}");
            
            if (jsonFile != null)
            {
                LevelData data = JsonUtility.FromJson<LevelData>(jsonFile.text);
                int totalPoints = 0;
                if (data != null && data.arrows != null)
                {
                    foreach (var arrow in data.arrows)
                    {
                        if (arrow.path != null) totalPoints += arrow.path.Count;
                    }
                }

                if (totalPoints < 120)
                {
                    m_DifficultyText.text = "Easy Level";
                    Color c = m_EasyColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (totalPoints < 400)
                {
                    m_DifficultyText.text = "Hard Level";
                    Color c = m_HardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (totalPoints < 900)
                {
                    m_DifficultyText.text = "Super Hard Level";
                    Color c = m_SuperHardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else
                {
                    m_DifficultyText.text = "Nightmare Level";
                    Color c = m_NightmareColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
            }
            else
            {
                m_DifficultyText.text = "Level Info Unavailable";
                Color c = m_EasyColor; c.a = 1f;
                m_DifficultyText.color = c;
            }

            m_LevelText.color = m_LevelColor;
        }
        
        public void OnSettingsButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_SettingsLayer.activeInHierarchy)
            {
                m_SettingsLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(true);
                m_CalanderLayer.SetActive(false);
            }
        }
        public void OnCalanderButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_CalanderLayer.activeInHierarchy)
            {
                m_CalanderLayer.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(true);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = false;
            }
            RefreshLobbyUI();
        }
        public void OnHomeButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            m_SettingsLayer.SetActive(false);
            m_CalanderLayer.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
            RefreshLobbyUI();
        }

        public void OnShopButtonClicked()
        {
            SoundManager.Instance.PlayClick();
           
            if(m_CalanderLayer.activeInHierarchy)
            { //TODO change to shop
                m_CalanderLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                //TODO Add shop layer
            }
        }

        public void OnPlayButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            m_LobbyUI.SetActive(false);
            m_GameUI.SetActive(true);
            
            string levelName = $"level{UserDataManager.Instance.CurrentLevel}";
            GameManager.Instance.StartLevel(levelName);
        }

        public void OnCalenderPlayButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            m_LobbyUI.SetActive(false);
            m_GameUI.SetActive(true);
            
            int month = m_MonthlyChallengeController.p_CurrentMonth;
            int day = m_MonthlyChallengeController.p_CurrentDay;
            int year = m_MonthlyChallengeController.p_CurrentYear;

            string levelName = $"level{month + day + (year % 10)}";
            GameManager.Instance.StartChallengeLevel(levelName, year, month, day);
        }
    }
}
