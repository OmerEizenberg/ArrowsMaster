using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro; 

using Assets.Scripts.Core;

public class HomeContoller : MonoBehaviour
{
    [SerializeField] private GameObject m_LobbyUI;
    [SerializeField] private GameObject m_GameUI;


    [SerializeField] private GameObject m_SettingsPopup;

    [SerializeField] private TextMeshProUGUI m_TitleText;
    [SerializeField] private TextMeshProUGUI m_LevelText;
    [SerializeField] private TextMeshProUGUI m_DifficultyText;

    [SerializeField] private Color m_CircleColor;
    [SerializeField] private Color m_SuperHardColor;
    [SerializeField] private Color m_HardColor;
    [SerializeField] private Color m_EasyColor;
    [SerializeField] private Color m_LevelColor;
    [SerializeField] private MonthlyChallengeController m_MonthlyChallengeController;

    private void OnEnable()
    {
        RefreshLobbyUI();
        UserDataManager.Instance.OnLevelChanged += RefreshLobbyUI;
    }

    private void OnDisable()
    {
        UserDataManager.Instance.OnLevelChanged -= RefreshLobbyUI;
    }

    private void RefreshLobbyUI()
    {
        m_TitleText.text = "Arrows Master";
        m_LevelText.text = $"Level {UserDataManager.Instance.CurrentLevel}";
        m_DifficultyText.text = "Easy";
        m_LevelText.color = m_LevelColor;
        //TODO : Color by difficulty
        m_DifficultyText.color = m_EasyColor;
    }
    
    public void OnSettingsButtonClicked()
    {
        SoundManager.Instance.PlayClick();

        if(m_SettingsPopup.activeInHierarchy)
        {
            m_SettingsPopup.SetActive(false);
        }else{
            m_SettingsPopup.SetActive(true);
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
        
        string levelName = $"level{m_MonthlyChallengeController.p_CurrentMonth+m_MonthlyChallengeController.p_CurrentDay+(m_MonthlyChallengeController.p_CurrentYear%10)}";
        GameManager.Instance.StartChallengeLevel(levelName);
    }
}
