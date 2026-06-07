using UnityEngine;
using TMPro;

public class ComboController : MonoBehaviour
{
    private const string ComboAnimationState = "ComboText";
    private const int ComboAnimationLayer = 0;

    [SerializeField] private TextMeshProUGUI m_comboNum;
    [SerializeField] private Animator m_Animator;

    private int m_upComingNum = 1;
    private int m_cachedHash;

    private void Awake()
    {
        if (m_Animator == null)
        {
            m_Animator = GetComponent<Animator>();
        }

        m_cachedHash = Animator.StringToHash(ComboAnimationState);
    }

    public void Show(int displayStreak, int upcomingStreak)
    {
        m_upComingNum = displayStreak;
        UpdateComboNumber();
        m_upComingNum = upcomingStreak;

        gameObject.SetActive(true);
        RestartAnimationFromBeginning();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateComboNumber()
    {
        if (m_comboNum != null)
        {
            m_comboNum.SetText("{0}", m_upComingNum);
        }
    }

    public void UpdateUpComingComboNumber(int num)
    {
        m_upComingNum = num;
    }

    public void DestroyME()
    {
        Hide();
    }

    private void RestartAnimationFromBeginning()
    {
        if (m_Animator == null)
        {
            return;
        }

        m_Animator.Rebind();
        m_Animator.Update(0f);
        m_Animator.Play(m_cachedHash, ComboAnimationLayer, 0f);
        m_Animator.Update(0f);
    }
}
