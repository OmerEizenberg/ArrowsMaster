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

        private readonly List<TournamentLeaderboardRow> m_RowBuffer = new List<TournamentLeaderboardRow>(25);
        private bool m_ProgressDirty;
        private int m_CachedPlace = -1;
        private long m_CachedPlaceSecond = -1;
        private int m_CachedPlacePlayerScore = int.MinValue;

        private static string LastShownPlacePrefsKey(string uniqueId) => $"Tournament_LastShownPlace_{uniqueId}";
        private static string LastShownScorePrefsKey(string uniqueId) => $"Tournament_LastShownScore_{uniqueId}";
        private static string PlayerScorePrefsKey(string uniqueId) => $"Tournament_PlayerScore_{uniqueId}";

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
            else
            {
                RestorePlayerScoreFromPrefs();
                OverlayLastShownFromPrefs();
                if (Progress.Status == TournamentStatus.Joined && TrustedTimeService.UtcNow >= CurrentWindow.EndUtc)
                    FinalizeIfNeeded();

                TryRecoverClaimedFinishToPendingJoin();
            }

            NotifyStateChanged();
        }

        /// <summary>Flush any deferred tournament progress (call on app pause / quit).</summary>
        public void FlushPendingPersistence()
        {
            RestorePlayerScoreFromPrefs();
            FlushProgressIfDirty();
        }

        /// <summary>
        /// Force-finish + claim (same UniqueId) used to stamp Finished onto a fresh PendingJoin,
        /// which hid the lobby badge until the next window. Reopen join when safe.
        /// </summary>
        private bool TryRecoverClaimedFinishToPendingJoin()
        {
            if (Progress == null) return false;
            if (Progress.Status != TournamentStatus.Finished) return false;
            // Still have an unclaimed results popup — keep Finished until they claim.
            if (HasPendingResults()) return false;
            if (TrustedTimeService.UtcNow >= CurrentWindow.EndUtc) return false;

            Progress = CreateFreshProgress();
            SaveState();
            Debug.Log($"[TournamentLiveOpService] Reopened PendingJoin after finished window state for {UniqueID}");
            return true;
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
            if (!IsTournamentFeatureEnabled()) return false;
            if (SO == null || UserDataManager.Instance == null) return false;
            if (UserDataManager.Instance.CurrentLevel < SO.ShowLevel) return false;
            if (Progress == null) return false;
            if (!NetworkReconnectManager.IsOnline) return false;
            return Progress.Status == TournamentStatus.PendingJoin || Progress.Status == TournamentStatus.Joined;
        }

        /// <summary>
        /// Level/status eligibility only — used to keep the badge component alive while offline
        /// so it can reappear when connectivity returns.
        /// </summary>
        public bool IsBadgeEligible()
        {
            if (!IsTournamentFeatureEnabled()) return false;
            if (SO == null || UserDataManager.Instance == null) return false;
            if (UserDataManager.Instance.CurrentLevel < SO.ShowLevel) return false;
            if (Progress == null) return false;
            return Progress.Status == TournamentStatus.PendingJoin || Progress.Status == TournamentStatus.Joined;
        }

        public static bool IsTournamentFeatureEnabled()
        {
            // Default true when RemoteConfig is not ready yet (matches Firebase default).
            if (RemoteConfigManager.Instance == null)
                return true;
            return RemoteConfigManager.Instance.IsTournamentOn;
        }

        public int GetDisplayPlace()
        {
            if (Progress == null || Progress.Status == TournamentStatus.PendingJoin)
                return 25;

            return GetCurrentPlace();
        }

        public int GetCurrentPlace()
        {
            if (Progress == null || Progress.Status == TournamentStatus.PendingJoin)
                return 25;

            DateTime now = TrustedTimeService.UtcNow;
            long second = now.Ticks / TimeSpan.TicksPerSecond;
            int playerScore = Progress.PlayerScore;
            if (m_CachedPlace > 0 &&
                second == m_CachedPlaceSecond &&
                playerScore == m_CachedPlacePlayerScore)
            {
                return m_CachedPlace;
            }

            int betterBots = 0;
            if (Progress.Bots != null)
            {
                for (int i = 0; i < Progress.Bots.Count; i++)
                {
                    int botScore = TournamentBotSimulator.GetBotScoreAt(Progress.Bots[i], now);
                    if (botScore > playerScore)
                        betterBots++;
                }
            }

            m_CachedPlace = betterBots + 1;
            m_CachedPlaceSecond = second;
            m_CachedPlacePlayerScore = playerScore;
            return m_CachedPlace;
        }

        public bool TryJoin()
        {
            if (Progress == null || Config == null) return false;
            if (Progress.Status != TournamentStatus.PendingJoin) return false;
            if (!IsUnlocked()) return false;
            if (!IsTournamentFeatureEnabled()) return false;

            DateTime now = TrustedTimeService.UtcNow;
            DateTime end = new DateTime(Progress.EndUtcTicks, DateTimeKind.Utc);
            if (now >= end) return false;

            Progress.Status = TournamentStatus.Joined;
            Progress.JoinedUtcTicks = now.Ticks;
            Progress.PlayerScore = 0;
            Progress.LastShownPlace = -1;
            Progress.LastShownScore = -1;
            PlayerPrefs.DeleteKey(LastShownPlacePrefsKey(UniqueID));
            PlayerPrefs.DeleteKey(LastShownScorePrefsKey(UniqueID));
            PlayerPrefs.DeleteKey(PlayerScorePrefsKey(UniqueID));
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
            // Score changes once per level win — persist immediately so app kill before
            // lobby/tick flush does not lose golden arrows.
            PersistPlayerScoreToPrefs();
            SaveState();
            NotifyStateChanged();
        }

        public bool TryGetLastShownPlayerState(out int place, out int score)
        {
            place = -1;
            score = -1;
            if (Progress == null) return false;
            if (Progress.LastShownPlace < 1) return false;
            place = Progress.LastShownPlace;
            score = Mathf.Max(0, Progress.LastShownScore);
            return true;
        }

        public void MarkPlayerStateShown(int place, int score)
        {
            if (Progress == null) return;
            if (place < 1) return;
            Progress.LastShownPlace = place;
            Progress.LastShownScore = Mathf.Max(0, score);
            // Cheap prefs write instead of serializing the full bot schedule JSON.
            PlayerPrefs.SetInt(LastShownPlacePrefsKey(UniqueID), place);
            PlayerPrefs.SetInt(LastShownScorePrefsKey(UniqueID), Progress.LastShownScore);
            PlayerPrefs.Save();
            MarkProgressDirty();
        }

        /// <summary>
        /// Fills <paramref name="rows"/> without allocating a new list.
        /// Caller owns the list; do not retain the service internal buffer across frames.
        /// </summary>
        public void FillLeaderboardRows(DateTime utcNow, List<TournamentLeaderboardRow> rows)
        {
            if (rows == null) return;
            rows.Clear();
            if (Progress == null)
                return;

            if (Progress.Status == TournamentStatus.PendingJoin)
            {
                rows.Add(new TournamentLeaderboardRow
                {
                    Name = Progress.PlayerName ?? GetOrCreatePlayerDisplayName(),
                    Score = 0,
                    IsPlayer = true,
                    Place = 25
                });
                return;
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

            rows.Sort(CompareLeaderboardRows);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.Place = i + 1;
                rows[i] = row;
            }
        }

        public List<TournamentLeaderboardRow> BuildLeaderboardRows(DateTime utcNow)
        {
            FillLeaderboardRows(utcNow, m_RowBuffer);
            return m_RowBuffer;
        }

        private static int CompareLeaderboardRows(TournamentLeaderboardRow a, TournamentLeaderboardRow b)
        {
            int cmp = b.Score.CompareTo(a.Score);
            if (cmp != 0) return cmp;
            if (a.IsPlayer != b.IsPlayer)
                return a.IsPlayer ? -1 : 1;
            return string.CompareOrdinal(a.Name, b.Name);
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

            // Mark claimed on live progress if it still matches this tournament instance.
            // Do NOT overwrite a fresh PendingJoin (e.g. force-finish reopened same UniqueId,
            // or the next window already activated) — that permanently hides the lobby badge.
            if (Progress != null && Progress.UniqueId == pending.UniqueId)
            {
                if (Progress.Status == TournamentStatus.Joined ||
                    Progress.Status == TournamentStatus.Finished)
                {
                    Progress.ResultsClaimed = true;
                    Progress.Status = TournamentStatus.Finished;
                    SaveState();
                }
            }

            NotifyStateChanged();
            return true;
        }

        /// <summary>QA: reset current window to PendingJoin and refresh the lobby badge.</summary>
        public void DebugResetToPendingJoin()
        {
            CurrentWindow = TournamentSchedule.GetCurrentWindow(TrustedTimeService.UtcNow);
            Progress = CreateFreshProgress();
            SaveState();
            NotifyStateChanged();
            LiveOpManager.Instance?.SyncLobbyIcons();
            Debug.Log($"[TournamentLiveOpService] Reset to PendingJoin for {UniqueID}");
        }

        public void TickFinalize()
        {
            var before = Progress?.Status;
            FinalizeIfNeeded();
            if (TryRecoverClaimedFinishToPendingJoin())
                NotifyStateChanged();
            FlushProgressIfDirty();
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
            var serviceRows = new List<TournamentLeaderboardRow>(25);
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

            serviceRows.Sort(CompareLeaderboardRows);

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
            m_ProgressDirty = false;
            if (Progress.Status == TournamentStatus.Joined)
                PersistPlayerScoreToPrefs();
        }

        private void MarkProgressDirty() => m_ProgressDirty = true;

        private void FlushProgressIfDirty()
        {
            if (m_ProgressDirty)
                SaveState();
        }

        private void PersistPlayerScoreToPrefs()
        {
            if (Progress == null || string.IsNullOrEmpty(UniqueID)) return;
            if (Progress.Status != TournamentStatus.Joined) return;
            PlayerPrefs.SetInt(PlayerScorePrefsKey(UniqueID), Progress.PlayerScore);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Recover score written during gameplay if the full progress JSON was not flushed
        /// before the app was closed.
        /// </summary>
        private void RestorePlayerScoreFromPrefs()
        {
            if (Progress == null || string.IsNullOrEmpty(UniqueID)) return;
            if (Progress.Status != TournamentStatus.Joined) return;

            string key = PlayerScorePrefsKey(UniqueID);
            if (!PlayerPrefs.HasKey(key)) return;

            int savedScore = PlayerPrefs.GetInt(key, 0);
            if (savedScore <= Progress.PlayerScore) return;

            Progress.PlayerScore = savedScore;
            MarkProgressDirty();
            FlushProgressIfDirty();
            Debug.Log($"[TournamentLiveOpService] Restored player score {savedScore} from local cache for {UniqueID}");
        }

        private void InvalidatePlaceCache()
        {
            m_CachedPlace = -1;
            m_CachedPlaceSecond = -1;
            m_CachedPlacePlayerScore = int.MinValue;
        }

        private void OverlayLastShownFromPrefs()
        {
            if (Progress == null || string.IsNullOrEmpty(UniqueID)) return;
            string placeKey = LastShownPlacePrefsKey(UniqueID);
            if (!PlayerPrefs.HasKey(placeKey)) return;
            int place = PlayerPrefs.GetInt(placeKey, -1);
            int score = PlayerPrefs.GetInt(LastShownScorePrefsKey(UniqueID), -1);
            if (place < 1) return;
            // Prefs are authoritative for UI-shown state if newer than disk progress defaults.
            if (place != Progress.LastShownPlace || score != Progress.LastShownScore)
            {
                Progress.LastShownPlace = place;
                Progress.LastShownScore = Mathf.Max(0, score);
            }
        }

        private void NotifyStateChanged()
        {
            InvalidatePlaceCache();
            OnStateChanged?.Invoke();
        }

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
    public struct TournamentLeaderboardRow
    {
        public string Name;
        public int Score;
        public bool IsPlayer;
        public int Place;
    }
}
