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

    [SerializeField] private TextMeshProUGUI m_TitleText;
    [SerializeField] private TextMeshProUGUI m_LevelText;
    [SerializeField] private TextMeshProUGUI m_DifficultyText;

    [SerializeField] private Color m_CircleColor;
    [SerializeField] private Color m_SuperHardColor;
    [SerializeField] private Color m_HardColor;
    [SerializeField] private Color m_EasyColor;
    [SerializeField] private Color m_LevelColor;

    private void enabled()
    {
        m_TitleText.text = "Arrows Master";
        m_LevelText.text = "Level 1";
        m_DifficultyText.text = "Easy";
        m_LevelText.color = m_LevelColor;
        //TODO : COlor by difficulty
        m_DifficultyText.color = m_EasyColor;
    }
    
    public void OnPlayButtonClicked()
    {
        SoundManager.Instance.PlayClick();
        m_LobbyUI.SetActive(false);
        m_GameUI.SetActive(true);
        GameManager.Instance.StartLevel("level1");
    }
}
