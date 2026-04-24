using System;
using System.Collections.Generic;
using UnityEngine;

public enum RewardType
{
    Coin,
    Hint,
    MagicWand,
    RefillLife
}

[Serializable]
public struct Reward
{
    public RewardType type;
    public int amount;
}

[CreateAssetMenu(fileName = "LegendPassConfig", menuName = "Configs/LegendPassConfig")]
public class LegendPassConfig : ScriptableObject
{
    [Tooltip("List of rewards for the free track. Use format 'Type:Amount', e.g., 'Coin:100'")]
    public List<string> freeRewards = new List<string>(new string[30]);
    
    [Tooltip("List of rewards for the premium track. Use format 'Type:Amount', e.g., 'Hint:5'")]
    public List<string> premiumRewards = new List<string>(new string[30]);

    /// <summary>
    /// Parses a reward string key into a Reward struct.
    /// Supports "Type:Amount" (e.g., "Coin:100") or shorthands (e.g., "C100", "MW1").
    /// </summary>
    public Reward ParseReward(string key)
    {
        Reward reward = new Reward();
        if (string.IsNullOrEmpty(key)) return reward;

        // 1. Try Shorthand parsing (e.g., MW1, C100, H5, L1)
        var match = System.Text.RegularExpressions.Regex.Match(key, @"^([a-zA-Z]+)(\d+)$");
        if (match.Success)
        {
            string typeToken = match.Groups[1].Value.ToUpper();
            int.TryParse(match.Groups[2].Value, out reward.amount);

            switch (typeToken)
            {
                case "C": reward.type = RewardType.Coin; return reward;
                case "H": reward.type = RewardType.Hint; return reward;
                case "MW": reward.type = RewardType.MagicWand; return reward;
                case "L": reward.type = RewardType.RefillLife; return reward;
            }
        }

        // 2. Try standard parsing (e.g., "Coin:100")
        string[] parts = key.Split(':');
        if (parts.Length < 2) return reward;

        string typeStr = parts[0].Trim().ToLower();
        int.TryParse(parts[1].Trim(), out reward.amount);

        switch (typeStr)
        {
            case "coin": case "coins": case "c":
                reward.type = RewardType.Coin;
                break;
            case "hint": case "hints": case "h":
                reward.type = RewardType.Hint;
                break;
            case "magicwand": case "wand": case "mw":
                reward.type = RewardType.MagicWand;
                break;
            case "refilllife": case "life": case "l":
                reward.type = RewardType.RefillLife;
                break;
            default:
                Debug.LogWarning($"[LegendPassConfig] Unknown reward type: {typeStr}");
                break;
        }

        return reward;
    }
}
