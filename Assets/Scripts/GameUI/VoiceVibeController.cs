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
        if (rand<1.2f)
        {
            SetGood();
        }
        else
        {
            if (rand<2.4f)
            {
                SetNice();
            }
            else
            {
                if (rand<3.3f)
                {
                    SetPerfect();
                }
                else
                {
                        if (rand<3.8f)
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
    }
    public void SetPerfect()
    {
        transform.localScale = new Vector3(1.2f,1.2f,1.2f);
        m_comboVibe.text = "Perfect !";
        SoundManager.Instance.PlayPerfect();
    }
    public void SetAmazing()
    {
        transform.localScale = new Vector3(1.45f,1.45f,1.45f);

        m_comboVibe.text = "Amazing !";
        SoundManager.Instance.PlayAmazing();
    }
    public void SetGood()
    {
        transform.localScale = new Vector3(1.3f,1.3f,1.3f);

        SoundManager.Instance.PlayGood();
        m_comboVibe.text = "Good !";
    }
    public void SetExcellent()
    {
        transform.localScale = new Vector3(1.7f,1.7f,1.7f);

        m_comboVibe.text = "Excellent !";
        SoundManager.Instance.PlayExcellent();
    }
    public void SetNice()
    {
        transform.localScale = new Vector3(1.3f,1.3f,1.3f);

        m_comboVibe.text = "Nice !";
        SoundManager.Instance.PlayNice();

    }

    public void DestroyME()
    {
        // OPTIMIZATION #6: Return to pool instead of destroying
        if (Assets.Scripts.Core.GameManager.Instance != null)
        {
            Assets.Scripts.Core.GameManager.Instance.ReturnEffect(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
