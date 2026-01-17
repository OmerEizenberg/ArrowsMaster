using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Core;

public class GameUIContoleer : MonoBehaviour
{
    [SerializeField] private GameObject m_LobbyUI;
    [SerializeField] private GameObject m_GameUI;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private Image[] m_Hearts;
    
    private readonly Color activeColor = Color.white; // #FFFFFF
    private readonly Color inactiveColor = new Color(0.616f, 0.616f, 0.616f, 0.5f); // #9D9D9D with 128 alpha (0.5f)

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged += UpdateLivesUI;
            UpdateLivesUI(GameManager.Instance.CurrentLives);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged -= UpdateLivesUI;
        }
    }

    private void UpdateLivesUI(int currentLives)
    {
        if (m_Hearts == null) return;

        for (int i = 0; i < m_Hearts.Length; i++)
        {
            if (m_Hearts[i] != null)
            {
                m_Hearts[i].color = (i < currentLives) ? activeColor : inactiveColor;
            }
        }
    }

    public void restartLevel()
    {
        SoundManager.Instance.PlayClick();
        if (m_LevelManager != null)
        {
            m_LevelManager.RestartLevel();
        }
    }

    public void BackToLobby()
    {
        m_LobbyUI.SetActive(true);
        m_GameUI.SetActive(false);
    }
}
