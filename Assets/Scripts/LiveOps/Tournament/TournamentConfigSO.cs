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

        [Tooltip("At join time, every place that has a reward must already have a bot with at least this many golden arrows. Unused by the pace-based simulator (kept for compatibility).")]
        public int MinArrowsForRewardedPlaces = 0;

        [Header("Late-join bucket feel")]
        [Tooltip("Legacy field. Bot schedules now use the player's remaining tournament window.")]
        public float LateJoinLookbackMinutes = 30f;

        [Header("Score calibration (Golden Arrows)")]
        [Tooltip("Average golden arrows earned per completed level.")]
        public int AvgGoldenArrowsPerLevel = 150;

        [Tooltip("Top-player daily level pace used for #1-style bots (design target: 10).")]
        public float TopLevelsPerDayMin = 10f;

        [Tooltip("Upper end of top-player daily level pace (design: 10-12).")]
        public float TopLevelsPerDayMax = 12f;

        [Tooltip("Average player daily level pace for the mid pack.")]
        public float AvgLevelsPerDay = 5f;

        [Header("Per-tournament / bot variance")]
        [Tooltip("Each tournament's overall intensity drifts by up to this fraction (0.12 = ±12%).")]
        [Range(0f, 0.35f)]
        public float TournamentIntensityVariance = 0.12f;

        [Tooltip("Extra random % applied to each bot's final target on top of pace (0.08 = ±8%).")]
        [Range(0f, 0.35f)]
        public float BotTargetVariance = 0.10f;

        [Tooltip("Minimum relative pace gap between neighboring competitive bots (0.04 = 4%). Keeps ranks from clustering.")]
        [Range(0f, 0.2f)]
        public float MinPaceGapPercent = 0.04f;

        [Tooltip("How much arrows-per-level can drift per bot (0.15 = ±15% around AvgGoldenArrowsPerLevel).")]
        [Range(0f, 0.4f)]
        public float ArrowsPerLevelVariance = 0.15f;

        [Header("Bot Golden Arrow gain rules")]
        [Tooltip("Legacy per-archetype batch rules. Pace-based simulator uses Score calibration above; archetypes only shape timing.")]
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

        private static readonly Dictionary<string, Reward> s_RewardCache =
            new Dictionary<string, Reward>(StringComparer.Ordinal);

        public static Reward ParseReward(string key)
        {
            if (string.IsNullOrEmpty(key))
                return default;

            if (s_RewardCache.TryGetValue(key, out Reward cached))
                return cached;

            Reward reward = ParseRewardUncached(key);
            s_RewardCache[key] = reward;
            return reward;
        }

        private static Reward ParseRewardUncached(string key)
        {
            Reward reward = new Reward();
            int colon = key.IndexOf(':');
            if (colon > 0 && colon < key.Length - 1)
            {
                ParseRewardType(key.Substring(0, colon).Trim(), out reward.type);
                int.TryParse(key.Substring(colon + 1).Trim(), out reward.amount);
                return reward;
            }

            // Compact tokens e.g. C500 / H3 / MW1 / L1
            int split = 0;
            while (split < key.Length && char.IsLetter(key[split]))
                split++;
            if (split > 0 && split < key.Length && int.TryParse(key.Substring(split), out reward.amount))
            {
                string typeToken = key.Substring(0, split);
                if (typeToken.Equals("C", StringComparison.OrdinalIgnoreCase) ||
                    typeToken.Equals("Coin", StringComparison.OrdinalIgnoreCase) ||
                    typeToken.Equals("Coins", StringComparison.OrdinalIgnoreCase))
                    reward.type = RewardType.Coin;
                else if (typeToken.Equals("H", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("Hint", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("Hints", StringComparison.OrdinalIgnoreCase))
                    reward.type = RewardType.Hint;
                else if (typeToken.Equals("MW", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("Wand", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("MagicWand", StringComparison.OrdinalIgnoreCase))
                    reward.type = RewardType.MagicWand;
                else if (typeToken.Equals("L", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("Life", StringComparison.OrdinalIgnoreCase) ||
                         typeToken.Equals("RefillLife", StringComparison.OrdinalIgnoreCase))
                    reward.type = RewardType.RefillLife;
            }

            return reward;
        }

        private static void ParseRewardType(string typeStr, out RewardType type)
        {
            type = default;
            if (string.IsNullOrEmpty(typeStr))
                return;

            switch (typeStr.ToLowerInvariant())
            {
                case "coin":
                case "coins":
                case "c":
                    type = RewardType.Coin;
                    break;
                case "hint":
                case "hints":
                case "h":
                    type = RewardType.Hint;
                    break;
                case "magicwand":
                case "wand":
                case "mw":
                    type = RewardType.MagicWand;
                    break;
                case "refilllife":
                case "life":
                case "l":
                    type = RewardType.RefillLife;
                    break;
            }
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
