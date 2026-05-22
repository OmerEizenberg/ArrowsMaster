using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Missions
{
    [CreateAssetMenu(fileName = "DailyMissionsConfig", menuName = "LiveOps/Daily Missions Config")]
    public class DailyMissionsConfigSO : ScriptableObject
    {
        public List<MissionDefinition> Missions = new List<MissionDefinition>
        {
            new MissionDefinition { Type = MissionType.CompleteLevels, TargetCount = 6, CoinReward = 500 },
            new MissionDefinition { Type = MissionType.WatchAds, TargetCount = 3, CoinReward = 300 },
            new MissionDefinition { Type = MissionType.WinLevelsInARow, TargetCount = 4, CoinReward = 800 },
            new MissionDefinition { Type = MissionType.MakePurchase, TargetCount = 1, CoinReward = 1000 },
            new MissionDefinition { Type = MissionType.CompleteChallengeLevels, TargetCount = 2, CoinReward = 600 }
        };
    }
}
