using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps.Tournament;

namespace Assets.Scripts.LiveOps
{
    public class TournamentLiveOpService : ALiveOpService
    {
        public const string EventId = "Tournament";
        private const string ConfigResourcePath = "LiveOps/TournamentConfig";
        private const string PendingResultsKey = "Tournament_PendingResults";
        private const string PlayerNameKey = "Tournament_PlayerDisplayName";

        public event Action OnStateChanged;

        public TournamentConfigSO Config { get; private set; }
        public TournamentProgressData Progress { get; private set; }
        public TournamentSchedule.Window CurrentWindow { get; private set; }

        public override void OnActivate()
        {
            Config = Resources.Load<TournamentConfigSO>(ConfigResourcePath);
            if (Config == null)
                Debug.LogError($"[TournamentLiveOpService] Missing Resources/{ConfigResourcePath}.asset");

            CurrentWindow = TournamentSchedule.GetCurrentWindow(TrustedTimeService.UtcNow);
            Progress = Deserialize(LoadProgress());

            if (Progress == null || Progress.UniqueId != UniqueID)
            {
                Progress = CreateFreshProgress();
                SaveState();
                Debug.Log($"[TournamentLiveOpService] New tournament instance {UniqueID}");
            }
            else if (Progress.Status == TournamentStatus.Joined && TrustedTimeService.UtcNow >= CurrentWindow.EndUtc)
            {
                FinalizeIfNeeded();
            }

            NotifyStateChanged();
        }

        public override void OnDeactivate()
        {
            FinalizeIfNeeded();
            SaveState();
            Debug.Log($"[TournamentLiveOpService] Deactivated: {UniqueID}");
        }

        public override TimeSpan GetRemainingTime()
        {
            DateTime now = TrustedTimeService.UtcNow;
            DateTime end = Progress != null && Progress.EndUtcTicks > 0
                ? new DateTime(Progress.EndUtcTicks, DateTimeKind.Utc)
                : CurrentWindow.EndUtc;
            return now >= end ? TimeSpan.Zero : end - now;
        }

        public static void NotifyGoldenArrowsEarned(int amount)
        {
            if (amount <= 0) return;
            TryGetActive(service => service.AddPlayerScore(amount));
        }

        public static void PreserveFinishedResultsBeforeCleanup(string oldUniqueId)
        {
            if (string.IsNullOrEmpty(oldUniqueId)) return;

            string json = UserDataManager.Instance.GetLiveOpData(oldUniqueId);
            var progress = Deserialize(json);
            if (progress == null) return;

            if (progress.Status == TournamentStatus.Joined || progress.Status == TournamentStatus.Finished)
            {
                if (progress.Status != TournamentStatus.Finished)
                {
                    progress = FinalizeProgress(progress, LoadConfigStatic());
                }

                if (!progress.ResultsClaimed)
                    SavePendingResults(BuildPendingResults(progress, LoadConfigStatic()));
            }

            // Persist finalized snapshot back in case cleanup is delayed.
            if (progress != null)
                UserDataManager.Instance.SaveLiveOpData(oldUniqueId, JsonUtility.ToJson(progress));
        }

        public static bool HasPendingResults()
        {
            return !string.IsNullOrEmpty(PlayerPrefs.GetString(PendingResultsKey, string.Empty));
        }

        public static TournamentPendingResultsData GetPendingResults()
        {
            string json = PlayerPrefs.GetString(PendingResultsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<TournamentPendingResultsData>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void ClearPendingResults()
        {
            PlayerPrefs.DeleteKey(PendingResultsKey);
            PlayerPrefs.Save();
        }

        public static string GetOrCreatePlayerDisplayName()
        {
            string name = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            if (!string.IsNullOrEmpty(name))
            {
                if (TournamentNameFilter.TryValidate(name, out string cleaned, out _))
                    return cleaned;

                PlayerPrefs.DeleteKey(PlayerNameKey);
            }

            name = "Player" + UnityEngine.Random.Range(0, 1001);
            PlayerPrefs.SetString(PlayerNameKey, name);
            PlayerPrefs.Save();
            return name;
        }

        public static void SetPlayerDisplayName(string name)
        {
            if (!TournamentNameFilter.TryValidate(name, out string cleaned, out string error))
            {
                Debug.LogWarning($"[TournamentLiveOpService] Rejected display name: {error}");
                return;
            }

            PlayerPrefs.SetString(PlayerNameKey, cleaned);
            PlayerPrefs.Save();

            TryGetActive(service =>
            {
                if (service.Progress != null)
                {
                    service.Progress.PlayerName = cleaned;
                    service.SaveState();
                    service.NotifyStateChanged();
                }
            });
        }

        /// <summary>Returns false if invalid; used by UI to show errors.</summary>
        public static bool TrySetPlayerDisplayName(string name, out string error)
        {
            if (!TournamentNameFilter.TryValidate(name, out string cleaned, out error))
                return false;

            PlayerPrefs.SetString(PlayerNameKey, cleaned);
            PlayerPrefs.Save();

            TryGetActive(service =>
            {
                if (service.Progress != null)
                {
                    service.Progress.PlayerName = cleaned;
                    service.SaveState();
                    service.NotifyStateChanged();
                }
            });
            return true;
        }

        public TournamentStatus Status => Progress?.Status ?? TournamentStatus.PendingJoin;

        public bool IsUnlocked()
        {
            if (SO == null || UserDataManager.Instance == null) return false;
            return UserDataManager.Instance.CurrentLevel >= SO.UnlockLevel;
        }

        public bool ShouldShowBadge()
        {
            if (SO == null || UserDataManager.Instance == null) return false;
            if (UserDataManager.Instance.CurrentLevel < SO.ShowLevel) return false;
            if (Progress == null) return false;
            return Progress.Status == TournamentStatus.PendingJoin || Progress.Status == TournamentStatus.Joined;
        }

        public int GetDisplayPlace()
        {
            if (Progress == null || Progress.Status == TournamentStatus.PendingJoin)
                return 25;

            return GetCurrentPlace();
        }

        public int GetCurrentPlace()
        {
            var rows = BuildLeaderboardRows(TrustedTimeService.UtcNow);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsPlayer)
                    return i + 1;
            }
            return 25;
        }

        public bool TryJoin()
        {
            if (Progress == null || Config == null) return false;
            if (Progress.Status != TournamentStatus.PendingJoin) return false;
            if (!IsUnlocked()) return false;

            DateTime now = TrustedTimeService.UtcNow;
            DateTime end = new DateTime(Progress.EndUtcTicks, DateTimeKind.Utc);
            if (now >= end) return false;

            Progress.Status = TournamentStatus.Joined;
            Progress.JoinedUtcTicks = now.Ticks;
            Progress.PlayerScore = 0;
            Progress.PlayerName = GetOrCreatePlayerDisplayName();
            Progress.Bots = TournamentBotSimulator.CreateBotsOnJoin(
                Config,
                CurrentWindow.StartUtc,
                CurrentWindow.EndUtc,
                now,
                UniqueID);

            SaveState();
            NotifyStateChanged();
            Debug.Log($"[TournamentLiveOpService] Player joined {UniqueID} with {Progress.Bots.Count} bots");
            return true;
        }

        public void AddPlayerScore(int amount)
        {
            if (Progress == null || amount <= 0) return;
            if (Progress.Status != TournamentStatus.Joined) return;

            DateTime now = TrustedTimeService.UtcNow;
            DateTime end = new DateTime(Progress.EndUtcTicks, DateTimeKind.Utc);
            if (now >= end)
            {
                FinalizeIfNeeded();
                return;
            }

            Progress.PlayerScore += amount;
            SaveState();
            NotifyStateChanged();
        }

        public List<TournamentLeaderboardRow> BuildLeaderboardRows(DateTime utcNow)
        {
            var rows = new List<TournamentLeaderboardRow>(25);
            if (Progress == null)
                return rows;

            if (Progress.Status == TournamentStatus.PendingJoin)
            {
                rows.Add(new TournamentLeaderboardRow
                {
                    Name = Progress.PlayerName ?? GetOrCreatePlayerDisplayName(),
                    Score = 0,
                    IsPlayer = true,
                    Place = 25
                });
                return rows;
            }

            rows.Add(new TournamentLeaderboardRow
            {
                Name = Progress.PlayerName ?? GetOrCreatePlayerDisplayName(),
                Score = Progress.PlayerScore,
                IsPlayer = true
            });

            if (Progress.Bots != null)
            {
                for (int i = 0; i < Progress.Bots.Count; i++)
                {
                    var bot = Progress.Bots[i];
                    rows.Add(new TournamentLeaderboardRow
                    {
                        Name = bot.Name,
                        Score = TournamentBotSimulator.GetBotScoreAt(bot, utcNow),
                        IsPlayer = false
                    });
                }
            }

            rows.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                // Player wins ties for friendlier ranking.
                if (a.IsPlayer != b.IsPlayer)
                    return a.IsPlayer ? -1 : 1;
                return string.CompareOrdinal(a.Name, b.Name);
            });

            for (int i = 0; i < rows.Count; i++)
                rows[i].Place = i + 1;

            return rows;
        }

        public string GetRewardKeyForPlace(int zeroBasedPlace)
        {
            return Config != null ? Config.GetRewardKey(zeroBasedPlace) : string.Empty;
        }

        public bool ClaimPendingResultsAndGrantRewards(out Reward granted)
        {
            granted = default;
            var pending = GetPendingResults();
            if (pending == null) return false;

            if (pending.HasReward)
            {
                granted = TournamentConfigSO.ParseReward(pending.RewardKey);
                GrantReward(granted);
            }

            ClearPendingResults();

            // Mark claimed on live progress if it still matches.
            if (Progress != null && Progress.UniqueId == pending.UniqueId)
            {
                Progress.ResultsClaimed = true;
                Progress.Status = TournamentStatus.Finished;
                SaveState();
            }

            NotifyStateChanged();
            return true;
        }

        public void TickFinalize()
        {
            var before = Progress?.Status;
            FinalizeIfNeeded();
            if (before == TournamentStatus.Joined &&
                Progress != null &&
                Progress.Status == TournamentStatus.Finished &&
                LiveOpManager.Instance != null)
            {
                LiveOpManager.Instance.CheckLiveOps();
            }
        }

        /// <summary>QA: advance trusted time so bots/schedules progress without waiting.</summary>
        public void DebugSimulateTime(TimeSpan delta)
        {
            TrustedTimeService.Instance.AddDebugOffset(delta);
            CurrentWindow = TournamentSchedule.GetCurrentWindow(TrustedTimeService.UtcNow);
            TickFinalize();
            NotifyStateChanged();
            Debug.Log($"[TournamentLiveOpService] Simulated +{delta}. UtcNow={TrustedTimeService.UtcNow:u}, place=#{GetDisplayPlace()}");
        }

        /// <summary>Dev helper: end the current joined tournament immediately and queue results.</summary>
        public void DebugForceFinishNow()
        {
            if (Progress == null) return;
            if (Progress.Status == TournamentStatus.PendingJoin)
            {
                Debug.LogWarning("[TournamentLiveOpService] Join first before forcing finish.");
                return;
            }

            Progress.EndUtcTicks = TrustedTimeService.UtcNow.AddSeconds(-1).Ticks;
            FinalizeIfNeeded();

            CurrentWindow = TournamentSchedule.GetCurrentWindow(TrustedTimeService.UtcNow);
            Progress = CreateFreshProgress();
            SaveState();
            NotifyStateChanged();
            Debug.Log("[TournamentLiveOpService] Forced finish. Pending results queued.");
        }

        private void FinalizeIfNeeded()
        {
            if (Progress == null) return;
            if (Progress.Status != TournamentStatus.Joined) return;

            DateTime now = TrustedTimeService.UtcNow;
            DateTime end = new DateTime(Progress.EndUtcTicks, DateTimeKind.Utc);
            if (now < end)
                return;

            Progress = FinalizeProgress(Progress, Config);
            SavePendingResults(BuildPendingResults(Progress, Config));
            SaveState();
            NotifyStateChanged();
        }

        private static TournamentProgressData FinalizeProgress(TournamentProgressData progress, TournamentConfigSO config)
        {
            if (progress == null) return null;
            if (progress.Status == TournamentStatus.Finished && progress.FinalPlace >= 0)
                return progress;

            DateTime endUtc = new DateTime(progress.EndUtcTicks, DateTimeKind.Utc);
            var serviceRows = new List<TournamentLeaderboardRow>();
            serviceRows.Add(new TournamentLeaderboardRow
            {
                Name = progress.PlayerName,
                Score = progress.PlayerScore,
                IsPlayer = true
            });
            if (progress.Bots != null)
            {
                for (int i = 0; i < progress.Bots.Count; i++)
                {
                    serviceRows.Add(new TournamentLeaderboardRow
                    {
                        Name = progress.Bots[i].Name,
                        Score = TournamentBotSimulator.GetBotScoreAt(progress.Bots[i], endUtc),
                        IsPlayer = false
                    });
                }
            }

            serviceRows.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                if (a.IsPlayer != b.IsPlayer)
                    return a.IsPlayer ? -1 : 1;
                return string.CompareOrdinal(a.Name, b.Name);
            });

            int place = 25;
            for (int i = 0; i < serviceRows.Count; i++)
            {
                if (serviceRows[i].IsPlayer)
                {
                    place = i + 1;
                    break;
                }
            }

            progress.FinalPlace = place;
            progress.Status = TournamentStatus.Finished;
            return progress;
        }

        private static TournamentPendingResultsData BuildPendingResults(TournamentProgressData progress, TournamentConfigSO config)
        {
            int zeroBased = Math.Max(0, progress.FinalPlace - 1);
            string rewardKey = config != null ? config.GetRewardKey(zeroBased) : string.Empty;
            var reward = TournamentConfigSO.ParseReward(rewardKey);
            return new TournamentPendingResultsData
            {
                UniqueId = progress.UniqueId,
                FinalPlace = progress.FinalPlace,
                PlayerScore = progress.PlayerScore,
                PlayerName = progress.PlayerName,
                RewardKey = rewardKey,
                HasReward = reward.amount > 0
            };
        }

        private static void SavePendingResults(TournamentPendingResultsData data)
        {
            if (data == null) return;

            // Don't clobber an unclaimed older result with a different tournament id.
            var existing = GetPendingResults();
            if (existing != null &&
                !string.IsNullOrEmpty(existing.UniqueId) &&
                existing.UniqueId != data.UniqueId)
            {
                Debug.LogWarning(
                    $"[TournamentLiveOpService] Keeping unclaimed results {existing.UniqueId}; " +
                    $"not overwriting with {data.UniqueId}");
                return;
            }

            PlayerPrefs.SetString(PendingResultsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private static void GrantReward(Reward reward)
        {
            if (reward.amount <= 0 || UserDataManager.Instance == null) return;
            string reason = ResourceAnalyticsReasons.TournamentClaim;
            switch (reward.type)
            {
                case RewardType.Coin:
                    UserDataManager.Instance.AddArrowsCurrency(reward.amount, reason);
                    break;
                case RewardType.Hint:
                    UserDataManager.Instance.AddHintBooster(reward.amount, reason);
                    break;
                case RewardType.MagicWand:
                    UserDataManager.Instance.AddMagicBooster(reward.amount, reason);
                    break;
                case RewardType.RefillLife:
                    UserDataManager.Instance.AddRefillBooster(reward.amount, reason);
                    break;
            }
        }

        private TournamentProgressData CreateFreshProgress()
        {
            return new TournamentProgressData
            {
                UniqueId = UniqueID,
                Status = TournamentStatus.PendingJoin,
                PlayerScore = 0,
                PlayerName = GetOrCreatePlayerDisplayName(),
                StartUtcTicks = CurrentWindow.StartUtc.Ticks,
                EndUtcTicks = CurrentWindow.EndUtc.Ticks,
                Bots = new List<TournamentBotData>(),
                FinalPlace = -1,
                ResultsClaimed = false
            };
        }

        private void SaveState()
        {
            if (Progress == null) return;
            SaveProgress(JsonUtility.ToJson(Progress));
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();

        private static TournamentProgressData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<TournamentProgressData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TournamentLiveOpService] Failed to parse progress: {e.Message}");
                return null;
            }
        }

        private static TournamentConfigSO LoadConfigStatic()
        {
            return Resources.Load<TournamentConfigSO>(ConfigResourcePath);
        }

        private static void TryGetActive(Action<TournamentLiveOpService> action)
        {
            if (LiveOpManager.Instance == null) return;
            var service = LiveOpManager.Instance.GetActiveService(EventId) as TournamentLiveOpService;
            if (service != null)
                action(service);
        }
    }

    [Serializable]
    public class TournamentLeaderboardRow
    {
        public string Name;
        public int Score;
        public bool IsPlayer;
        public int Place;
    }
}
