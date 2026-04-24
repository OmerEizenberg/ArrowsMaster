using UnityEngine;
using Assets.Scripts.Core;

/// <summary>
/// Manages the Legend's Pass logic, using UserDataManager for persistence.
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

    // Direct access to UserDataManager properties for current state
    public int currentStep => UserDataManager.Instance.LegendPassStep;
    public bool isPremiumUnlocked => UserDataManager.Instance.IsLegendPassPremiumUnlocked;

    public System.Action OnProgressChanged;
    public System.Action<Reward> OnRewardClaimed;

    private const int PASS_DURATION_DAYS = 28;

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

    /// <summary>
    /// Loads the pass progress and checks if a new 28-day round should start.
    /// </summary>
    public void LoadData()
    {
        CheckRoundRotation();
        Debug.Log($"[LegendPassManager] Data Synced from UserDataManager. Step: {currentStep}, Premium: {isPremiumUnlocked}");
    }

    private void CheckRoundRotation()
    {
        string startDateStr = UserDataManager.Instance.LegendPassStartDate;
        
        if (string.IsNullOrEmpty(startDateStr))
        {
            StartNewRound();
            return;
        }

        if (long.TryParse(startDateStr, out long binaryDate))
        {
            System.DateTime startDate = System.DateTime.FromBinary(binaryDate);
            System.TimeSpan elapsed = System.DateTime.Now - startDate;

            if (elapsed.TotalDays >= PASS_DURATION_DAYS)
            {
                Debug.Log($"[LegendPassManager] Pass expired after {elapsed.TotalDays:F1} days. Rotating.");
                StartNewRound();
            }
        }
    }

    private void StartNewRound()
    {
        UserDataManager.Instance.SetLegendPassStep(0);
        UserDataManager.Instance.SetLegendPassPremiumUnlocked(false);
        UserDataManager.Instance.SetLegendPassClaimedMasks(0, 0);
        UserDataManager.Instance.SetLegendPassStartDate(System.DateTime.Now.ToBinary().ToString());
        
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Checks if a specific step has been claimed.
    /// </summary>
    public bool IsStepClaimed(int step, bool isPremium)
    {
        if (step < 0 || step >= 30) return false;
        int mask = isPremium ? UserDataManager.Instance.LegendPassClaimedPremiumMask : UserDataManager.Instance.LegendPassClaimedFreeMask;
        return (mask & (1 << step)) != 0;
    }

    /// <summary>
    /// Claims the reward for a specific track (free or premium) and step.
    /// </summary>
    public void ClaimReward(int step, bool isPremium)
    {
        if (step < 0 || step >= 30) return;
        if (step > currentStep) return;
        if (isPremium && !isPremiumUnlocked) return;
        if (IsStepClaimed(step, isPremium)) return;

        // 1. Update Bitmasks in UserDataManager
        int freeMask = UserDataManager.Instance.LegendPassClaimedFreeMask;
        int premMask = UserDataManager.Instance.LegendPassClaimedPremiumMask;

        if (isPremium)
            premMask |= (1 << step);
        else
            freeMask |= (1 << step);

        UserDataManager.Instance.SetLegendPassClaimedMasks(freeMask, premMask);

        // 2. Parse and Grant Reward
        if (config != null)
        {
            string rewardKey = isPremium ? config.premiumRewards[step] : config.freeRewards[step];
            Reward reward = config.ParseReward(rewardKey);
            GrantReward(reward);
            
            OnRewardClaimed?.Invoke(reward);
            Debug.Log($"[LegendPassManager] Claimed {(isPremium ? "Premium" : "Free")} reward step {step}");
        }

        OnProgressChanged?.Invoke();
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

    public void UnlockPremium()
    {
        UserDataManager.Instance.SetLegendPassPremiumUnlocked(true);
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Returns the total number of rewards currently available to claim.
    /// Used for the lobby notification badge count.
    /// </summary>
    public int GetUnclaimedRewardsCount()
    {
        int count = 0;
        for (int i = 0; i <= currentStep; i++)
        {
            if (!IsStepClaimed(i, false)) count++;
            if (isPremiumUnlocked && !IsStepClaimed(i, true)) count++;
        }
        return count;
    }

    public bool HasUnclaimedRewards()
    {
        return GetUnclaimedRewardsCount() > 0;
    }

    public string GetTimerString()
    {
        string startDateStr = UserDataManager.Instance.LegendPassStartDate;
        if (string.IsNullOrEmpty(startDateStr)) return "0s";

        if (long.TryParse(startDateStr, out long binaryDate))
        {
            System.DateTime startDate = System.DateTime.FromBinary(binaryDate);
            System.DateTime endDate = startDate.AddDays(28);
            System.TimeSpan remaining = endDate - System.DateTime.Now;

            if (remaining.TotalSeconds <= 0) return "0s";

            if (remaining.TotalDays >= 1)
                return $"{Mathf.FloorToInt((float)remaining.TotalDays)}D";
            else if (remaining.TotalHours >= 1)
                return $"{Mathf.FloorToInt((float)remaining.TotalHours)}h";
            else
                return $"{Mathf.FloorToInt((float)remaining.TotalMinutes)}M";
        }

        return "0s";
    }

    public void OnLevelComplete()
    {
        if (currentStep < 29)
        {
            UserDataManager.Instance.SetLegendPassStep(currentStep + 1);
            OnProgressChanged?.Invoke();
        }
    }
}
