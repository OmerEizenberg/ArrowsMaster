using System;
using UnityEngine;

namespace Assets.Scripts.GAE
{
    [Serializable]
    public class GAEStageDefinition
    {
        [Tooltip("Total golden arrows required to complete this stage.")]
        public int ArrowTarget;

        public GAERewardType RewardType;
        public int RewardAmount;
    }
}
