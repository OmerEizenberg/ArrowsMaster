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
        [SerializeField] private GameObject m_DonateLayer;
        [SerializeField] private GameObject m_NoAdsLayer;

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
            // Ensure GameUI is hidden when refreshing lobby (returning to lobby)
            if (m_GameUI != null) m_GameUI.SetActive(false);
            else if (GameManager.Instance != null && GameManager.Instance.m_GameUI != null)
                GameManager.Instance.m_GameUI.gameObject.SetActive(false);

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

            TextAsset jsonFile = null;
            
            if (folder == "Levels" && GameManager.Instance != null && GameManager.Instance.levelManager != null)
            {
                // Use the LevelManager to get the correct level file, handling looping if max level is reached
                jsonFile = GameManager.Instance.levelManager.GetLevelTextAsset(levelId);
            }
            else
            {
                // Fallback for Challenge levels or if LevelManager is not available
                jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId}");
            }
            
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
        public void OnDonateButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_DonateLayer.activeInHierarchy)
            {
                m_DonateLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_DonateLayer.SetActive(true);
            }
        }

        public void OnNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_NoAdsLayer.activeInHierarchy)
            {
                m_NoAdsLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_NoAdsLayer.SetActive(true);
            }
        }

        public void OnBuyDonationButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.Donate199);
            m_DonateLayer.SetActive(false);
        }

        public void OnBuyNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.NoAds999);
            m_NoAdsLayer.SetActive(false);
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
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            string levelName = $"level{UserDataManager.Instance.CurrentLevel}";
            Debug.Log($"[HomeContoller] Play clicked. Starting Level: {levelName}");
            
            SwitchToGameUI();
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartLevel(levelName);
            }
        }

        public void OnCalenderPlayButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            if (m_MonthlyChallengeController == null)
            {
                Debug.LogError("[HomeContoller] MonthlyChallengeController reference missing!");
                return;
            }

            int month = m_MonthlyChallengeController.p_CurrentMonth;
            int day = m_MonthlyChallengeController.p_CurrentDay;
            int year = m_MonthlyChallengeController.p_CurrentYear;

            string levelName = $"level{month + day + (year % 10)}";
            Debug.Log($"[HomeContoller] Calendar Play clicked. Starting Challenge: {levelName}");

            SwitchToGameUI();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartChallengeLevel(levelName, year, month, day);
            }
        }

        private void SwitchToGameUI()
        {
            // Use local references if assigned, otherwise fallback to GameManager
            GameObject lobby = m_LobbyUI;
            GameObject game = m_GameUI;

            if (lobby == null && GameManager.Instance != null) lobby = GameManager.Instance.m_LobbyUI;
            if (game == null && GameManager.Instance != null) game = GameManager.Instance.m_GameUI.gameObject;

            if (lobby != null)
            {
                Debug.Log($"[HomeContoller] Hiding Lobby UI: {lobby.name}");
                lobby.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[HomeContoller] Could not find Lobby UI reference to hide!");
            }

            if (game != null)
            {
                Debug.Log($"[HomeContoller] Showing Game UI: {game.name}");
                game.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[HomeContoller] Could not find Game UI reference to show!");
            }
        }
    }
}
