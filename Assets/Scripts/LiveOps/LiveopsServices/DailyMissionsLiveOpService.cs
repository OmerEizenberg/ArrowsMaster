using System;
using UnityEngine;
using Assets.Scripts.LiveOps.Missions;

namespace Assets.Scripts.LiveOps
{
    public class DailyMissionsLiveOpService : ALiveOpService
    {
        public const string EventId = "DM";

        public event Action OnStateChanged;

        public DailyMissionsConfigSO Config { get; private set; }
        public DailyMissionsProgressData Progress { get; private set; }

        public override void OnActivate()
        {
            Config = Resources.Load<DailyMissionsConfigSO>("LiveOps/DailyMissionsConfig");
            if (Config == null)
            {
                Debug.LogError("[DailyMissionsLiveOpService] Missing Resources/LiveOps/DailyMissionsConfig.asset");
                return;
            }

            string todayId = GetTodayDayId();
            Progress = DeserializeProgress(LoadProgress());
            if (ShouldResetProgressForToday(todayId))
            {
                Progress = CreateFreshProgress(todayId);
                SaveState();
                Debug.Log($"[DailyMissionsLiveOpService] Started new daily progress for {todayId}");
            }
            else
            {
                Debug.Log($"[DailyMissionsLiveOpService] Loaded progress for {todayId}");
            }

            Debug.Log($"[DailyMissionsLiveOpService] Activated: {UniqueID}");
            NotifyStateChanged();
        }

        public override void OnDeactivate()
        {
            SaveState();
            Debug.Log($"[DailyMissionsLiveOpService] Deactivated: {UniqueID}");
        }

        public static void NotifyMainLevelWon()
        {
            TryGetActive(service => service.OnMainLevelWon());
        }

        public static void NotifyChallengeLevelWon()
        {
            TryGetActive(service => service.OnChallengeLevelWon());
        }

        public static void NotifyMainLevelFailed()
        {
            TryGetActive(service => service.OnMainLevelFailed());
        }

        public static void NotifyAdWatched()
        {
            TryGetActive(service => service.OnAdWatched());
        }

        public static void NotifyPurchaseMade()
        {
            TryGetActive(service => service.OnPurchaseMade());
        }

        private static void TryGetActive(Action<DailyMissionsLiveOpService> action)
        {
            if (LiveOpManager.Instance == null) return;
            var service = LiveOpManager.Instance.GetActiveService(EventId) as DailyMissionsLiveOpService;
            if (service != null && service.Config != null)
                action(service);
        }

        private void OnMainLevelWon()
        {
            IncrementMissionProgress(MissionType.CompleteLevels, 1);
            IncrementWinStreakMissions();
            NotifyStateChanged();
        }

        private void OnChallengeLevelWon()
        {
            IncrementMissionProgress(MissionType.CompleteChallengeLevels, 1);
            NotifyStateChanged();
        }

        private void OnMainLevelFailed()
        {
            ResetWinStreakMissions();
            NotifyStateChanged();
        }

        private void OnAdWatched()
        {
            IncrementMissionProgress(MissionType.WatchAds, 1);
            NotifyStateChanged();
        }

        private void OnPurchaseMade()
        {
            IncrementMissionProgress(MissionType.MakePurchase, 1);
            NotifyStateChanged();
        }

        public bool HasClaimableReward()
        {
            if (Config == null || Progress?.Missions == null) return false;

            for (int i = 0; i < Config.Missions.Count && i < Progress.Missions.Length; i++)
            {
                if (IsReadyToClaim(i)) return true;
            }
            return false;
        }

        public bool IsReadyToClaim(int index)
        {
            if (!TryGetMission(index, out var definition, out var entry)) return false;
            return !entry.Claimed && entry.Progress >= definition.TargetCount;
        }

        public bool IsClaimed(int index)
        {
            if (!TryGetMission(index, out _, out var entry)) return false;
            return entry.Claimed;
        }

        public bool TryClaimReward(int index, out int coinsGranted)
        {
            coinsGranted = 0;
            if (!IsReadyToClaim(index)) return false;

            var definition = Config.Missions[index];
            Progress.Missions[index].Claimed = true;
            coinsGranted = definition.CoinReward;
            SaveState();
            NotifyStateChanged();
            return true;
        }

        public int GetDisplayProgress(int index)
        {
            if (!TryGetMission(index, out var definition, out var entry)) return 0;

            if (definition.Type == MissionType.WinLevelsInARow)
                return Mathf.Min(entry.WinStreak, definition.TargetCount);

            return Mathf.Min(entry.Progress, definition.TargetCount);
        }

        public int GetTargetCount(int index)
        {
            return TryGetMission(index, out var definition, out _) ? definition.TargetCount : 0;
        }

        public int GetCoinReward(int index)
        {
            return TryGetMission(index, out var definition, out _) ? definition.CoinReward : 0;
        }

        private bool TryGetMission(int index, out MissionDefinition definition, out MissionProgressEntry entry)
        {
            definition = null;
            entry = null;
            if (Config == null || Progress?.Missions == null) return false;
            if (index < 0 || index >= Config.Missions.Count || index >= Progress.Missions.Length) return false;
            definition = Config.Missions[index];
            entry = Progress.Missions[index];
            return definition != null && entry != null;
        }

        private void IncrementMissionProgress(MissionType type, int amount)
        {
            for (int i = 0; i < Config.Missions.Count; i++)
            {
                if (Config.Missions[i].Type != type) continue;
                var entry = Progress.Missions[i];
                if (entry.Claimed) continue;
                entry.Progress = Mathf.Min(entry.Progress + amount, Config.Missions[i].TargetCount);
            }

            SaveState();
        }

        private void IncrementWinStreakMissions()
        {
            for (int i = 0; i < Config.Missions.Count; i++)
            {
                if (Config.Missions[i].Type != MissionType.WinLevelsInARow) continue;
                var entry = Progress.Missions[i];
                if (entry.Claimed) continue;

                entry.WinStreak++;
                entry.Progress = Mathf.Min(entry.WinStreak, Config.Missions[i].TargetCount);
            }

            SaveState();
        }

        private void ResetWinStreakMissions()
        {
            for (int i = 0; i < Config.Missions.Count; i++)
            {
                if (Config.Missions[i].Type != MissionType.WinLevelsInARow) continue;
                var entry = Progress.Missions[i];
                if (entry.Claimed) continue;
                entry.WinStreak = 0;
                entry.Progress = 0;
            }

            SaveState();
        }

        private static string GetTodayDayId()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        private bool ShouldResetProgressForToday(string todayId)
        {
            if (Progress == null || Progress.Missions == null)
                return true;

            if (Progress.Missions.Length != Config.Missions.Count)
                return true;

            if (string.IsNullOrEmpty(Progress.DayId) || Progress.DayId != todayId)
                return true;

            return false;
        }

        private DailyMissionsProgressData CreateFreshProgress(string todayId)
        {
            var data = new DailyMissionsProgressData
            {
                DayId = todayId,
                Missions = new MissionProgressEntry[Config.Missions.Count]
            };

            for (int i = 0; i < data.Missions.Length; i++)
            {
                data.Missions[i] = new MissionProgressEntry();
            }

            return data;
        }

        private void SaveState()
        {
            SaveProgress(JsonUtility.ToJson(Progress));
        }

        private static DailyMissionsProgressData DeserializeProgress(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<DailyMissionsProgressData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyMissionsLiveOpService] Failed to parse progress: {e.Message}");
                return null;
            }
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
