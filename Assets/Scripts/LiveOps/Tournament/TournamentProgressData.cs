using System;
using System.Collections.Generic;

namespace Assets.Scripts.LiveOps.Tournament
{
    public enum TournamentStatus
    {
        PendingJoin = 0,
        Joined = 1,
        Finished = 2
    }

    [Serializable]
    public class TournamentBotData
    {
        public string Name;
        public int Archetype;
        public int Seed;
        /// <summary>UTC ticks when this bot "joined" the simulated field.</summary>
        public long JoinUtcTicks;
        public List<TournamentScoreEvent> Events = new List<TournamentScoreEvent>();

        // Runtime-only cursor for GetBotScoreAt as time advances (not persisted).
        [NonSerialized] public long ScoreCacheTicks;
        [NonSerialized] public int ScoreCacheValue;
        [NonSerialized] public int ScoreCacheIndex;
    }

    [Serializable]
    public class TournamentScoreEvent
    {
        /// <summary>UTC ticks when this batch is applied.</summary>
        public long UtcTicks;
        public int Amount;
    }

    [Serializable]
    public class TournamentProgressData
    {
        public string UniqueId;
        public TournamentStatus Status = TournamentStatus.PendingJoin;
        public int PlayerScore;
        public string PlayerName;
        public long StartUtcTicks;
        public long EndUtcTicks;
        public long JoinedUtcTicks;
        public List<TournamentBotData> Bots = new List<TournamentBotData>();
        public int FinalPlace = -1;
        public bool ResultsClaimed;
    }

    [Serializable]
    public class TournamentPendingResultsData
    {
        public string UniqueId;
        public int FinalPlace;
        public int PlayerScore;
        public string PlayerName;
        public string RewardKey;
        public bool HasReward;
    }
}
