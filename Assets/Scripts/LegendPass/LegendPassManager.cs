using UnityEngine;
using Assets.Scripts.Core;
using System;

/// <summary>
/// Manages the Legend's Pass logic, using UserDataManager for persistence.
/// Updated to follow calendar months and support IAP verification.
/// </summary>
public class LegendPassManager : MonoBehaviour
{
    private static LegendPassManager _instance;
    public static LegendPassManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LegendPassManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LegendPassManager");
                    _instance = go.AddComponent<LegendPassManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Configuration")]
    [SerializeField] private LegendPassConfig config;

    // Product ID for the pass
    public const string ProductID = "com.everybodygames.arrowsmaster.legendspass_999";

    // Direct access to UserDataManager properties for current state
    public int currentStep => UserDataManager.Instance.LegendPassStep;
    public bool isPremiumUnlocked => UserDataManager.Instance.IsLegendPassPremiumUnlocked;

    public System.Action OnProgressChanged;
    public System.Action<int> OnUnclaimedCountChanged;
    public System.Action<Reward> OnRewardClaimed;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadData();
    }

    private void Start()
    {
        // Listen for IAP successes to unlock the pass
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess += HandlePurchaseSuccess;
        }
    }

    private void OnDestroy()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess -= HandlePurchaseSuccess;
        }
    }

    /// <summary>
    /// Loads the pass progress and checks if it's a new calendar month.
    /// </summary>
    public void LoadData()
    {
        CheckRoundRotation();
        Debug.Log($"[LegendPassManager] Data Synced. Month: {DateTime.Now.Month}, Step: {currentStep}, Premium: {isPremiumUnlocked}");
        NotifyStateChanged();
    }

    private void CheckRoundRotation()
    {
        string startDateStr = UserDataManager.Instance.LegendPassStartDate;
        DateTime now = DateTime.Now;

        if (string.IsNullOrEmpty(startDateStr))
        {
            StartNewRound();
            return;
        }

        if (long.TryParse(startDateStr, out long binaryDate))
        {
            DateTime startDate = DateTime.FromBinary(binaryDate);
            
            // Calendar month rotation: if Year or Month has changed since last save
            if (now.Year != startDate.Year || now.Month != startDate.Month)
            {
                Debug.Log($"[LegendPassManager] New month detected ({now.Month}/{now.Year}). Rotating pass.");
                StartNewRound();
            }
        }
    }

    private void StartNewRound()
    {
        UserDataManager.Instance.SetLegendPassStep(0);
        UserDataManager.Instance.SetLegendPassPremiumUnlocked(false);
        UserDataManager.Instance.SetLegendPassClaimedMasks(0, 0);
        // Save the first day of the current month as the start date
        UserDataManager.Instance.SetLegendPassStartDate(DateTime.Now.ToBinary().ToString());
        
        NotifyStateChanged();
    }

    public bool IsStepClaimed(int step, bool isPremium)
    {
        if (step < 0 || step >= 30) return false;
        int mask = isPremium ? UserDataManager.Instance.LegendPassClaimedPremiumMask : UserDataManager.Instance.LegendPassClaimedFreeMask;
        return (mask & (1 << step)) != 0;
    }

    public void ClaimReward(int step, bool isPremium)
    {
        if (step < 0 || step >= 30) return;
        if (step > currentStep) return;
        if (isPremium && !isPremiumUnlocked) return;
        if (IsStepClaimed(step, isPremium)) return;

        int freeMask = UserDataManager.Instance.LegendPassClaimedFreeMask;
        int premMask = UserDataManager.Instance.LegendPassClaimedPremiumMask;

        if (isPremium) premMask |= (1 << step);
        else freeMask |= (1 << step);

        UserDataManager.Instance.SetLegendPassClaimedMasks(freeMask, premMask);

        if (config != null)
        {
            string rewardKey = isPremium ? config.premiumRewards[step] : config.freeRewards[step];
            Reward reward = config.ParseReward(rewardKey);
            GrantReward(reward);
            OnRewardClaimed?.Invoke(reward);
        }

        NotifyStateChanged();
    }

    private void GrantReward(Reward reward)
    {
        var userData = UserDataManager.Instance;
        switch (reward.type)
        {
            case RewardType.Coin: userData.AddArrowsCurrency(reward.amount); break;
            case RewardType.Hint: userData.AddHintBooster(reward.amount); break;
            case RewardType.MagicWand: userData.AddMagicBooster(reward.amount); break;
            case RewardType.RefillLife: userData.AddRefillBooster(reward.amount); break;
        }
    }

    public int GetUnclaimedRewardsCount()
    {
        int count = 0;
        for (int i = 0; i <= currentStep; i++)
        {
            // Only count if there IS a reward (amount > 0)
            if (!IsStepClaimed(i, false))
            {
                if (config != null && config.ParseReward(config.freeRewards[i]).amount > 0) count++;
            }

            if (isPremiumUnlocked && !IsStepClaimed(i, true))
            {
                if (config != null && config.ParseReward(config.premiumRewards[i]).amount > 0) count++;
            }
        }
        return count;
    }

    public bool HasUnclaimedRewards()
    {
        return GetUnclaimedRewardsCount() > 0;
    }

    public string GetTimerString()
    {
        DateTime now = DateTime.Now;
        // End of current month
        DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        TimeSpan remaining = nextMonth - now;

        if (remaining.TotalSeconds <= 0) return "0s";

        if (remaining.TotalDays >= 1)
        {
            return $"{remaining.Days}D {remaining.Hours}h";
        }
        else if (remaining.TotalHours >= 1)
        {
            return $"{remaining.Hours}h {remaining.Minutes}m";
        }
        else
        {
            return $"{remaining.Minutes}m";
        }
    }

    private void NotifyStateChanged()
    {
        OnProgressChanged?.Invoke();
        OnUnclaimedCountChanged?.Invoke(GetUnclaimedRewardsCount());
    }

    public void PurchasePremiumPass()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.BuyProduct(ProductID);
        }
    }

    private void HandlePurchaseSuccess(string id)
    {
        if (id == ProductID)
        {
            UnlockPremium();
        }
    }

    public void UnlockPremium()
    {
        UserDataManager.Instance.SetLegendPassPremiumUnlocked(true);
        NotifyStateChanged();
    }

    public void OnLevelComplete()
    {
        if (UserDataManager.Instance.CurrentLevel < 30) return;

        if (currentStep < 29)
        {
            // Skip empty steps in progression counter? 
            // Usually, steps always exist even if reward is empty. 
            // But user said "dont count it for notification OR INCREASE the number because of it"
            // This is ambiguous. If Step 2 is empty, does winning Level 2 skip to Step 3?
            // "dont increase the number because of it" likely means don't count it as a "Pending Claim" in notifications.
            // I'll keep the step progression normal (30 steps), but skip empty rewards in notification counts.
            
            UserDataManager.Instance.SetLegendPassStep(currentStep + 1);
            NotifyStateChanged();
        }
    }
}
