using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Assets.Scripts.Core;

public class HomeContoller : MonoBehaviour
{
    [SerializeField] private GameObject m_BG;

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
        m_BG.SetActive(false);
        GameManager.Instance.StartLevel("level1");
    }
}
