using System;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Missions
{
    [Serializable]
    public class MissionDefinition
    {
        public MissionType Type;
        [Min(1)] public int TargetCount = 1;
        [Min(0)] public int CoinReward = 100;
        [Tooltip("Optional override. Leave empty to use the default description for the mission type.")]
        public string DescriptionOverride;

        [Tooltip("Optional reward label format, e.g. +{0} or {0} coins. Empty = use MissionSlotView default.")]
        public string RewardTextFormat;
    }
}
