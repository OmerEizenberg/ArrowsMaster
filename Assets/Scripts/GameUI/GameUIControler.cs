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
    [SerializeField] private GameObject m_HintButton;
    
    private readonly Color activeColor = Color.white; // #FFFFFF
    private readonly Color inactiveColor = new Color(0.616f, 0.616f, 0.616f, 0.5f); // #9D9D9D with 128 alpha (0.5f)

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged += UpdateLivesUI;
            GameManager.Instance.OnHintVisibilityChanged += ToggleHintButton;
            UpdateLivesUI(GameManager.Instance.CurrentLives);
            ToggleHintButton(false); // Hide by default
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged -= UpdateLivesUI;
            GameManager.Instance.OnHintVisibilityChanged -= ToggleHintButton;
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
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayClick();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartCurrentLevel();
        }
    }

    public void BackToLobby()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowInterstitial();
        }
        
        m_LobbyUI.SetActive(true);
        m_GameUI.SetActive(false);
    }

    private void ToggleHintButton(bool visible)
    {
        if (m_HintButton != null)
        {
            m_HintButton.SetActive(visible);
        }
    }

    public void OnHintButtonClicked()
    {
        if (AdsManager.Instance != null)
        {
            AdsManager.Instance.ShowHintRewarded();
        }
    }
}
