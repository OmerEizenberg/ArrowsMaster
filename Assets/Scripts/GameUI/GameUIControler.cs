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
    [SerializeField] private Button m_RestartButton;
    [SerializeField] private Button m_QuitButton;
    // Start is called before the first frame update
    public void restartLevel()
    {
        SoundManager.Instance.PlayClick();
        if (m_LevelManager != null)
        {
            m_LevelManager.RestartLevel();
        }
    }

    // Update is called once per frame
    public void BackToLobby()
    {
        m_LobbyUI.SetActive(true);
        m_GameUI.SetActive(false);
    }
}
