using UnityEngine;
using TMPro;
using Assets.Scripts.Core;

public class VoiceVibeController : MonoBehaviour
{
    private const string VoiceAnimationState = "ComboText";
    private const int VoiceAnimationLayer = 0;

    [SerializeField] private TextMeshProUGUI m_comboVibe;
    [SerializeField] private Animator m_Animator;

    private int m_cachedHash;

    private void Awake()
    {
        ResolveReferences();
        m_cachedHash = Animator.StringToHash(VoiceAnimationState);
    }

    public void Show()
    {
        ResolveReferences();
        if (m_comboVibe == null)
        {
            return;
        }

        gameObject.SetActive(true);
        m_comboVibe.gameObject.SetActive(true);

        ApplyRandomVibe();
        RestartAnimationFromBeginning();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void DestroyME()
    {
        Hide();
    }

    private void ResolveReferences()
    {
        if (m_Animator == null)
        {
            m_Animator = GetComponent<Animator>();
        }

        if (m_comboVibe == null)
        {
            m_comboVibe = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void RestartAnimationFromBeginning()
    {
        if (m_Animator == null)
        {
            return;
        }

        m_Animator.Rebind();
        m_Animator.Update(0f);
        m_Animator.Play(m_cachedHash, VoiceAnimationLayer, 0f);
        m_Animator.Update(0f);
    }

    private void ApplyRandomVibe()
    {
        float rand = Random.Range(0f, 4f);
        if (rand < 1.2f)
        {
            SetGood();
        }
        else if (rand < 2.4f)
        {
            SetNice();
        }
        else if (rand < 3.3f)
        {
            SetPerfect();
        }
        else if (rand < 3.8f)
        {
            SetAmazing();
        }
        else
        {
            SetExcellent();
        }
    }

    private void PlayVoiceClip(System.Action playClip)
    {
        if (SoundManager.Instance != null)
        {
            playClip();
        }
    }

    private void SetPerfect()
    {
        transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        m_comboVibe.text = "Perfect !";
        PlayVoiceClip(() => SoundManager.Instance.PlayPerfect());
    }

    private void SetAmazing()
    {
        transform.localScale = new Vector3(1.45f, 1.45f, 1.45f);
        m_comboVibe.text = "Amazing !";
        PlayVoiceClip(() => SoundManager.Instance.PlayAmazing());
    }

    private void SetGood()
    {
        transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        m_comboVibe.text = "Good !";
        PlayVoiceClip(() => SoundManager.Instance.PlayGood());
    }

    private void SetExcellent()
    {
        transform.localScale = new Vector3(1.7f, 1.7f, 1.7f);
        m_comboVibe.text = "Excellent !";
        PlayVoiceClip(() => SoundManager.Instance.PlayExcellent());
    }

    private void SetNice()
    {
        transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        m_comboVibe.text = "Nice !";
        PlayVoiceClip(() => SoundManager.Instance.PlayNice());
    }
}
