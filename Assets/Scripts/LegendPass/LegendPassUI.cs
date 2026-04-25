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
    [SerializeField] private GameObject m_PassCrown;

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
    /// Reuses existing children to avoid memory allocation and layout leaks.
    /// </summary>
    [ContextMenu("Refresh Pass UI")]
    public void RefreshUI()
    {
        if (m_Config == null || m_StepPrefab == null || m_ContentTransform == null)
        {
            Debug.LogError("[LegendPassUI] Missing references in LegendPassUI!");
            return;
        }

        int targetCount = 30;
        int currentChildCount = m_ContentTransform.childCount;

        // 1. Ensure we have exactly 30 children
        if (currentChildCount < targetCount)
        {
            for (int i = currentChildCount; i < targetCount; i++)
            {
                Instantiate(m_StepPrefab, m_ContentTransform);
            }
        }
        else if (currentChildCount > targetCount)
        {
            for (int i = currentChildCount - 1; i >= targetCount; i--)
            {
                DestroyImmediate(m_ContentTransform.GetChild(i).gameObject);
            }
        }

        // 2. Update each child
        for (int i = 0; i < targetCount; i++)
        {
            LegendPassStepUI stepInstance = m_ContentTransform.GetChild(i).GetComponent<LegendPassStepUI>();
            if (stepInstance == null) continue;

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

        // Toggle Purchase Button & Crown
        bool isUnlocked = LegendPassManager.Instance.isPremiumUnlocked;
        if (m_PurchaseButton != null)
        {
            m_PurchaseButton.gameObject.SetActive(!isUnlocked);
        }
        if (m_PassCrown != null)
        {
            m_PassCrown.SetActive(isUnlocked);
        }

        // Force layout rebuild to ensure ScrollRect handles the new content size
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_ContentTransform);
        
        // Auto-scroll to the current unlocked step with a slight delay to ensure layout is ready
        if (m_ScrollCoroutine != null) StopCoroutine(m_ScrollCoroutine);
        m_ScrollCoroutine = StartCoroutine(ScrollToCurrentStepRoutine());
    }

    private Coroutine m_ScrollCoroutine;

    private System.Collections.IEnumerator ScrollToCurrentStepRoutine()
    {
        // Wait a few frames to let the UI system settle and LayoutGroups finish their first pass
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        if (m_ScrollRect != null && m_ContentTransform != null && LegendPassManager.Instance != null)
        {
            int current = LegendPassManager.Instance.currentStep;
            if (current < 0) current = 0;
            if (current >= m_ContentTransform.childCount) current = m_ContentTransform.childCount - 1;

            if (m_ContentTransform.childCount > 0)
            {
                RectTransform targetChild = m_ContentTransform.GetChild(current) as RectTransform;
                if (targetChild != null)
                {
                    // Calculate Y based on the child's anchored position
                    float targetY = Mathf.Abs(targetChild.anchoredPosition.y);

                    float contentHeight = m_ContentTransform.rect.height;
                    float viewportHeight = m_ScrollRect.viewport != null ? m_ScrollRect.viewport.rect.height : m_ScrollRect.GetComponent<RectTransform>().rect.height;
                    float maxScroll = Mathf.Max(0, contentHeight - viewportHeight);

                    Vector2 newPos = m_ContentTransform.anchoredPosition;
                    newPos.y = Mathf.Clamp(targetY, 0, maxScroll);
                    
                    m_ContentTransform.anchoredPosition = newPos;
                    // Debug.Log eliminated to reduce console noise during high-churn operations
                }
            }
        }
        m_ScrollCoroutine = null;
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
