using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Assets.Scripts.Core;

/// <summary>
/// Main UI controller for the Legend's Pass. 
/// Populates and manages the scrollable list of steps.
/// </summary>
public class LegendPassUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private LegendPassConfig m_Config;
    [SerializeField] private LegendPassStepUI m_StepPrefab;
    [SerializeField] private ScrollRect m_ScrollRect;
    [SerializeField] private RectTransform m_ContentTransform;

    [Header("Reward Icons Mapping")]
    [SerializeField] private Sprite m_CoinSprite;
    [SerializeField] private Sprite m_HintSprite;
    [SerializeField] private Sprite m_WandSprite;
    [SerializeField] private Sprite m_LifeSprite;

    [Header("Purchase")]
    [SerializeField] private Button m_PurchaseButton;

    private void OnEnable()
    {
        RefreshUI();
        LegendPassManager.Instance.OnProgressChanged += RefreshUI;
    }

    private void OnDisable()
    {
        if (LegendPassManager.Instance != null)
        {
            LegendPassManager.Instance.OnProgressChanged -= RefreshUI;
        }
    }

    /// <summary>
    /// Closes the Legend's Pass UI. 
    /// </summary>
    public void ClosePass()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Populates/Updates the full scrollable rewards track.
    /// </summary>
    [ContextMenu("Refresh Pass UI")]
    public void RefreshUI()
    {
        if (m_Config == null || m_StepPrefab == null || m_ContentTransform == null)
        {
            Debug.LogError("[LegendPassUI] Missing references in LegendPassUI!");
            return;
        }

        // Simple cleanup - in a high performance scenario, could use pooling
        foreach (Transform child in m_ContentTransform)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < 30; i++)
        {
            LegendPassStepUI stepInstance = Instantiate(m_StepPrefab, m_ContentTransform);
            
            Reward freeReward = m_Config.ParseReward(m_Config.freeRewards[i]);
            Reward premiumReward = m_Config.ParseReward(m_Config.premiumRewards[i]);

            bool isReached = i <= LegendPassManager.Instance.currentStep;
            bool isPremiumUnlocked = LegendPassManager.Instance.isPremiumUnlocked;
            bool isFreeClaimed = LegendPassManager.Instance.IsStepClaimed(i, false);
            bool isPremiumClaimed = LegendPassManager.Instance.IsStepClaimed(i, true);

            stepInstance.Setup(
                i, 
                freeReward, 
                premiumReward, 
                isReached, 
                isPremiumUnlocked, 
                isFreeClaimed, 
                isPremiumClaimed
            );
            
            if (stepInstance.freeIcon != null) stepInstance.freeIcon.sprite = GetRewardSprite(freeReward.type);
            if (stepInstance.premiumIcon != null) stepInstance.premiumIcon.sprite = GetRewardSprite(premiumReward.type);
        }

        // Toggle Purchase Button
        if (m_PurchaseButton != null)
        {
            m_PurchaseButton.gameObject.SetActive(!LegendPassManager.Instance.isPremiumUnlocked);
        }

        // Force layout rebuild to ensure ScrollRect handles the new content size
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_ContentTransform);
        
        // Optional: Auto-scroll to the current unlocked step
        ScrollToCurrentStep();
    }

    /// <summary>
    /// Centers the ScrollRect on the user's current progress step.
    /// </summary>
    private void ScrollToCurrentStep()
    {
        if (m_ScrollRect == null || m_ContentTransform == null || m_ContentTransform.childCount == 0) return;

        int current = LegendPassManager.Instance.currentStep;
        float progress = (float)current / 29f; // 30 steps total (0 to 29)
        
        // Horizontal scroll normalization (0 = left, 1 = right)
        m_ScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(progress);
    }

    private Sprite GetRewardSprite(RewardType type)
    {
        switch (type)
        {
            case RewardType.Coin: return m_CoinSprite;
            case RewardType.Hint: return m_HintSprite;
            case RewardType.MagicWand: return m_WandSprite;
            case RewardType.RefillLife: return m_LifeSprite;
            default: return null;
        }
    }

    /// <summary>
    /// External entry point to advance progress and refresh UI.
    /// </summary>
    public void OnLevelCompleted()
    {
        LegendPassManager.Instance.OnLevelComplete();
        RefreshUI();
    }

    /// <summary>
    /// Invoked by the Activate/Purchase button.
    /// </summary>
    public void PurchasePass()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        LegendPassManager.Instance.PurchasePremiumPass();
    }
}
