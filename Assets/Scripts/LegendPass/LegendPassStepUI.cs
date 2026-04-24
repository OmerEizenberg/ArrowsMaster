using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Controls the UI representation of a single step in the Legend's Pass.
/// Now features separate claim buttons for free and premium reward tracks.
/// </summary>
public class LegendPassStepUI : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private TextMeshProUGUI m_StepIndexText;
    [SerializeField] private GameObject m_Highlight;

    [Header("Free Reward Track")]
    public Image freeIcon;
    [SerializeField] private TextMeshProUGUI m_FreeAmountText;
    [SerializeField] private GameObject m_FreeClaimedIndicator;
    [SerializeField] private Button m_FreeClaimButton;

    [Header("Premium Reward Track")]
    public Image premiumIcon;
    [SerializeField] private TextMeshProUGUI m_PremiumAmountText;
    [SerializeField] private GameObject m_PremiumClaimedIndicator;
    [SerializeField] private GameObject m_PremiumLockedOverlay;
    [SerializeField] private Button m_PremiumClaimButton;

    [Header("Generic Notifiers")]
    [SerializeField] private GameObject m_FreeClaimNotification;
    [SerializeField] private GameObject m_PremiumClaimNotification;

    private int _stepIndex;

    /// <summary>
    /// Populates the step UI with data and sets up separate button listeners.
    /// </summary>
    public void Setup(int index, Reward free, Reward premium, bool isReached, bool isPremiumUnlocked, bool isFreeClaimed, bool isPremiumClaimed)
    {
        _stepIndex = index;

        if (m_StepIndexText != null) m_StepIndexText.text = (index + 1).ToString();
        
        // Setup Free Reward UI
        if (m_FreeAmountText != null) m_FreeAmountText.text = free.amount.ToString();
        if (m_FreeClaimedIndicator != null) m_FreeClaimedIndicator.SetActive(isFreeClaimed);
        
        // Setup Premium Reward UI
        if (m_PremiumAmountText != null) m_PremiumAmountText.text = premium.amount.ToString();
        if (m_PremiumClaimedIndicator != null) m_PremiumClaimedIndicator.SetActive(isPremiumClaimed);
        
        // Current Step Highlight
        if (m_Highlight != null) m_Highlight.SetActive(isReached && index == LegendPassManager.Instance.currentStep);

        // Premium Selection Visuals
        if (premiumIcon != null)
        {
            premiumIcon.color = isPremiumUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);
        }
        if (m_PremiumLockedOverlay != null) m_PremiumLockedOverlay.SetActive(!isPremiumUnlocked);

        // Separate Button Logic
        SetupClaimButton(m_FreeClaimButton, m_FreeClaimNotification, isReached && !isFreeClaimed, false);
        SetupClaimButton(m_PremiumClaimButton, m_PremiumClaimNotification, isReached && isPremiumUnlocked && !isPremiumClaimed, true);
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
        // Manager handles validation and granting
        LegendPassManager.Instance.ClaimReward(_stepIndex, isPremium);
        
        // No need for explicit refresh here as LegendPassUI listens to OnProgressChanged event
    }
}
