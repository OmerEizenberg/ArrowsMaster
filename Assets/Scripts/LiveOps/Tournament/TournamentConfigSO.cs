using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Tournament
{
    [Serializable]
    public class BotArchetypeGainRule
    {
        public BotArchetype Archetype;

        [Header("Batch cadence (hours between gains)")]
        public float IntervalHours = 3f;

        [Header("Golden Arrows per batch")]
        public int AmountMin = 5;
        public int AmountMax = 15;

        [Tooltip("0-1: portion of remaining time spent in the 'first phase' (quiet/early). Used by Sleeper/FrontRunner/Comeback.")]
        [Range(0f, 1f)]
        public float PhaseSplit = 0.5f;

        [Header("Second phase (optional, used by burst/comeback styles)")]
        public float Phase2IntervalHours = 2f;
        public int Phase2AmountMin = 10;
        public int Phase2AmountMax = 25;
    }

    [CreateAssetMenu(fileName = "TournamentConfig", menuName = "LiveOps/TournamentConfig")]
    public class TournamentConfigSO : ScriptableObject
    {
        [Tooltip("Rewards by final place. Index 0 = 1st place, 24 = last. Format: Type:Amount e.g. Coin:500")]
        public List<string> PlaceRewards = new List<string>(25);

        [Tooltip("At join time, every place that has a reward must already have a bot with at least this many golden arrows.")]
        public int MinArrowsForRewardedPlaces = 71;

        [Header("Late-join bucket feel")]
        [Tooltip("Bots are simulated as if they joined within this many minutes before the player.")]
        public float LateJoinLookbackMinutes = 30f;

        [Header("Bot Golden Arrow gain rules")]
        [Tooltip("Per-archetype cadence and batch sizes. If empty, built-in defaults are used.")]
        public List<BotArchetypeGainRule> BotGainRules = new List<BotArchetypeGainRule>();

        public BotArchetypeGainRule GetBotGainRule(BotArchetype archetype)
        {
            if (BotGainRules != null)
            {
                for (int i = 0; i < BotGainRules.Count; i++)
                {
                    if (BotGainRules[i] != null && BotGainRules[i].Archetype == archetype)
                        return BotGainRules[i];
                }
            }
            return CreateDefaultRule(archetype);
        }

        public static BotArchetypeGainRule CreateDefaultRule(BotArchetype archetype)
        {
            switch (archetype)
            {
                case BotArchetype.SteadyGrinder:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 2.5f, AmountMin = 8, AmountMax = 18
                    };
                case BotArchetype.Casual:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 8f, AmountMin = 3, AmountMax = 10
                    };
                case BotArchetype.SleeperBurst:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 10f, AmountMin = 0, AmountMax = 4,
                        PhaseSplit = 0.55f, Phase2IntervalHours = 1.8f, Phase2AmountMin = 15, Phase2AmountMax = 35
                    };
                case BotArchetype.FrontRunner:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 1.2f, AmountMin = 12, AmountMax = 28,
                        PhaseSplit = 0.35f, Phase2IntervalHours = 8f, Phase2AmountMin = 2, Phase2AmountMax = 8
                    };
                case BotArchetype.ComebackKid:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 7f, AmountMin = 1, AmountMax = 6,
                        PhaseSplit = 0.55f, Phase2IntervalHours = 1.5f, Phase2AmountMin = 14, Phase2AmountMax = 32
                    };
                case BotArchetype.DaySkipper:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 2.2f, AmountMin = 10, AmountMax = 24,
                        PhaseSplit = 0.4f
                    };
                case BotArchetype.Ghost:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 14f, AmountMin = 0, AmountMax = 5
                    };
                case BotArchetype.Spiky:
                    return new BotArchetypeGainRule
                    {
                        Archetype = archetype, IntervalHours = 0f, AmountMin = 5, AmountMax = 40
                    };
                default:
                    return new BotArchetypeGainRule { Archetype = archetype };
            }
        }

        public string GetRewardKey(int placeIndex)
        {
            if (PlaceRewards == null || placeIndex < 0 || placeIndex >= PlaceRewards.Count)
                return string.Empty;
            return PlaceRewards[placeIndex] ?? string.Empty;
        }

        public bool TryGetReward(int placeIndex, out Reward reward)
        {
            reward = ParseReward(GetRewardKey(placeIndex));
            return reward.amount > 0;
        }

        public int CountRewardedPlaces()
        {
            int count = 0;
            if (PlaceRewards == null) return 0;
            for (int i = 0; i < PlaceRewards.Count; i++)
            {
                if (ParseReward(PlaceRewards[i]).amount > 0)
                    count++;
            }
            return count;
        }

        public static Reward ParseReward(string key)
        {
            Reward reward = new Reward();
            if (string.IsNullOrEmpty(key)) return reward;

            var match = System.Text.RegularExpressions.Regex.Match(key, @"^([a-zA-Z]+)(\d+)$");
            if (match.Success)
            {
                string typeToken = match.Groups[1].Value.ToUpperInvariant();
                int.TryParse(match.Groups[2].Value, out reward.amount);
                switch (typeToken)
                {
                    case "C": reward.type = RewardType.Coin; return reward;
                    case "H": reward.type = RewardType.Hint; return reward;
                    case "MW": reward.type = RewardType.MagicWand; return reward;
                    case "L": reward.type = RewardType.RefillLife; return reward;
                }
            }

            string[] parts = key.Split(':');
            if (parts.Length < 2) return reward;

            string typeStr = parts[0].Trim().ToLowerInvariant();
            int.TryParse(parts[1].Trim(), out reward.amount);

            switch (typeStr)
            {
                case "coin":
                case "coins":
                case "c":
                    reward.type = RewardType.Coin;
                    break;
                case "hint":
                case "hints":
                case "h":
                    reward.type = RewardType.Hint;
                    break;
                case "magicwand":
                case "wand":
                case "mw":
                    reward.type = RewardType.MagicWand;
                    break;
                case "refilllife":
                case "life":
                case "l":
                    reward.type = RewardType.RefillLife;
                    break;
            }

            return reward;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureDefaultBotRules();
        }

        [ContextMenu("Fill Default Bot Gain Rules")]
        public void EnsureDefaultBotRules()
        {
            BotGainRules = new List<BotArchetypeGainRule>();
            foreach (BotArchetype archetype in Enum.GetValues(typeof(BotArchetype)))
                BotGainRules.Add(CreateDefaultRule(archetype));
        }
#endif
    }
}
