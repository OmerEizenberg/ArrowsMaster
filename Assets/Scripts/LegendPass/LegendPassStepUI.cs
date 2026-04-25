using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// Mounts the UI representation of a single step in the Legend's Pass.
/// Updated to hide empty rewards (Amount = 0).
public class LegendPassStepUI : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private TextMeshProUGUI m_StepIndexText;
    [SerializeField] private TextMeshProUGUI m_StepIndexText2;
    [SerializeField] private GameObject m_Highlight;

    [Header("Free Reward Track")]
    public Image freeIcon;
    [SerializeField] private TextMeshProUGUI m_FreeAmountText;
    [SerializeField] private TextMeshProUGUI m_FreeAmountText2;
    [SerializeField] private GameObject m_FreeClaimedIndicator;
    [SerializeField] private Button m_FreeClaimButton;
    [SerializeField] private GameObject m_FreeContentHolder; // Parent of icon/text

    [Header("Premium Reward Track")]
    public Image premiumIcon;
    [SerializeField] private TextMeshProUGUI m_PremiumAmountText;
    [SerializeField] private TextMeshProUGUI m_PremiumAmountText2;
    [SerializeField] private GameObject m_PremiumClaimedIndicator;
    [SerializeField] private GameObject m_PremiumLockedOverlay;
    [SerializeField] private Button m_PremiumClaimButton;
    [SerializeField] private GameObject m_PremiumContentHolder; // Parent of icon/text

    [Header("Generic Notifiers")]
    [SerializeField] private GameObject m_FreeClaimNotification;
    [SerializeField] private GameObject m_PremiumClaimNotification;

    private int _stepIndex;

    public void Setup(int index, Reward free, Reward premium, bool isReached, bool isPremiumUnlocked, bool isFreeClaimed, bool isPremiumClaimed)
    {
        _stepIndex = index;

        string indexStr = (index + 1).ToString();
        if (m_StepIndexText != null && m_StepIndexText.text != indexStr) m_StepIndexText.text = indexStr;
        if (m_StepIndexText2 != null && m_StepIndexText2.text != indexStr) m_StepIndexText2.text = indexStr;
        
        // Setup Free Reward UI
        bool hasFree = free.amount > 0;
        if (m_FreeContentHolder != null && m_FreeContentHolder.activeSelf != hasFree) m_FreeContentHolder.SetActive(hasFree);

        string freeAmountStr = free.amount.ToString();
        if (m_FreeAmountText != null && m_FreeAmountText.text != freeAmountStr) m_FreeAmountText.text = freeAmountStr;
        if (m_FreeAmountText2 != null && m_FreeAmountText2.text != freeAmountStr) m_FreeAmountText2.text = freeAmountStr;

        if (m_FreeClaimedIndicator != null && m_FreeClaimedIndicator.activeSelf != isFreeClaimed) m_FreeClaimedIndicator.SetActive(isFreeClaimed);
        
        if(isFreeClaimed){
           if (m_FreeAmountText != null && m_FreeAmountText.gameObject.activeSelf) m_FreeAmountText.gameObject.SetActive(false); 
           if (m_FreeAmountText2 != null && m_FreeAmountText2.gameObject.activeSelf) m_FreeAmountText2.gameObject.SetActive(false); 
        }

        // Setup Premium Reward UI
        bool hasPremium = premium.amount > 0;
        if (m_PremiumContentHolder != null && m_PremiumContentHolder.activeSelf != hasPremium) m_PremiumContentHolder.SetActive(hasPremium);

        string premAmountStr = premium.amount.ToString();
        if (m_PremiumAmountText != null && m_PremiumAmountText.text != premAmountStr) m_PremiumAmountText.text = premAmountStr;
        if (m_PremiumAmountText2 != null && m_PremiumAmountText2.text != premAmountStr) m_PremiumAmountText2.text = premAmountStr;

        if (m_PremiumClaimedIndicator != null && m_PremiumClaimedIndicator.activeSelf != isPremiumClaimed) m_PremiumClaimedIndicator.SetActive(isPremiumClaimed);

        if(isPremiumClaimed){
           if (m_PremiumAmountText != null && m_PremiumAmountText.gameObject.activeSelf) m_PremiumAmountText.gameObject.SetActive(false); 
           if (m_PremiumAmountText2 != null && m_PremiumAmountText2.gameObject.activeSelf) m_PremiumAmountText2.gameObject.SetActive(false); 
        }
        // Highlight logic
        if (m_Highlight != null) m_Highlight.SetActive(isReached && index == LegendPassManager.Instance.currentStep);

        // Premium Section Visuals
        if (m_PremiumLockedOverlay != null)
        {
            // Only show lock if there IS a reward
            m_PremiumLockedOverlay.SetActive(!isPremiumUnlocked && hasPremium);
        }

        // Button Logic (Only show if there IS a reward to claim)
        SetupClaimButton(m_FreeClaimButton, m_FreeClaimNotification, isReached && !isFreeClaimed && hasFree, false);
        SetupClaimButton(m_PremiumClaimButton, m_PremiumClaimNotification, isReached && isPremiumUnlocked && !isPremiumClaimed && hasPremium, true);
    }

    private void SetupClaimButton(Button button, GameObject notification, bool canClaim, bool isPremium)
    {
        if (button == null) return;

        button.gameObject.SetActive(canClaim);
        if (notification != null) notification.SetActive(canClaim);

        if (canClaim)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnClaimButtonClicked(isPremium));
        }
    }

    private void OnClaimButtonClicked(bool isPremium)
    {
        if (Assets.Scripts.Core.SoundManager.Instance != null)
        {
            Assets.Scripts.Core.SoundManager.Instance.PlayShop();
        }
        LegendPassManager.Instance.ClaimReward(_stepIndex, isPremium);
    }
}
