using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Assets.Scripts.Core;

public class VoiceVibeController : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI m_comboVibe;

    void OnEnable()
    {
       float rand = Random.Range(0f,4.0f);
        if (rand<1.3f)
        {
            SetGood();
        }
        else
        {
            if (rand<2.6f)
            {
                SetNice();
            }
            else
            {
                if (rand<3.5f)
                {
                    SetPerfect();
                }
                else
                {
                        SetExcellent();
                }
            }   
        }
    }
    public void SetPerfect()
    {
        m_comboVibe.text = "Perfect !";
        SoundManager.Instance.PlayPerfect();
    }
    public void SetGood()
    {
        SoundManager.Instance.PlayGood();
        m_comboVibe.text = "Good !";
    }
    public void SetExcellent()
    {
        m_comboVibe.text = "Excellent !";
        SoundManager.Instance.PlayExcellent();
    }
    public void SetNice()
    {
        m_comboVibe.text = "Nice !";
        SoundManager.Instance.PlayNice();

    }
}
