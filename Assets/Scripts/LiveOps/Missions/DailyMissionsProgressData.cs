using System;

namespace Assets.Scripts.LiveOps.Missions
{
    [Serializable]
    public class MissionProgressEntry
    {
        public int Progress;
        public bool Claimed;
        public int WinStreak;
    }

    [Serializable]
    public class DailyMissionsProgressData
    {
        /// <summary>Calendar day for this save (yyyy-MM-dd). Used to detect day rollover.</summary>
        public string DayId;
        public MissionProgressEntry[] Missions = Array.Empty<MissionProgressEntry>();
    }
}
