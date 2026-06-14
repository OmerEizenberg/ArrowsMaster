using System;

namespace Assets.Scripts.GAE
{
    [Serializable]
    public class GAEProgressData
    {
        public string EventInstanceId;
        public int CollectedArrows;
        public int ClaimedStageMask;
        public int LastPresentedCollected;
        public int LastPresentedStageIndex;
    }
}
