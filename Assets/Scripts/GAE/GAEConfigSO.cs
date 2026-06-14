using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GAE
{
    [CreateAssetMenu(fileName = "GAEConfig", menuName = "Configs/GAEConfig")]
    public class GAEConfigSO : ScriptableObject
    {
        public List<GAEStageDefinition> Stages = new List<GAEStageDefinition>();

        public void EnsureDefaultStages()
        {
            if (Stages != null && Stages.Count > 0)
            {
                return;
            }

            Stages = new List<GAEStageDefinition>
            {
                new GAEStageDefinition { ArrowTarget = 200, RewardType = GAERewardType.Coin, RewardAmount = 200 },
                new GAEStageDefinition { ArrowTarget = 400, RewardType = GAERewardType.Hint, RewardAmount = 1 },
                new GAEStageDefinition { ArrowTarget = 800, RewardType = GAERewardType.Coin, RewardAmount = 1000 },
                new GAEStageDefinition { ArrowTarget = 1200, RewardType = GAERewardType.Shuffle, RewardAmount = 1 },
                new GAEStageDefinition { ArrowTarget = 2000, RewardType = GAERewardType.Coin, RewardAmount = 2600 },
                new GAEStageDefinition { ArrowTarget = 3500, RewardType = GAERewardType.Coin, RewardAmount = 5000 }
            };
        }
    }
}
