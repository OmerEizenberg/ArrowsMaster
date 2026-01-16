using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Assets.Scripts.Core;

public class HomeContoller : MonoBehaviour
{
    [SerializeField] private GameObject m_LobbyUI;
    [SerializeField] private GameObject m_GameUI;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPlayButtonClicked()
    {
        SoundManager.Instance.PlayClick();
        m_LobbyUI.SetActive(false);
        m_GameUI.SetActive(true);
        GameManager.Instance.StartLevel("level1");
    }
}
