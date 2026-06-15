using System;
using UnityEngine;
using Assets.Scripts.Core;
using Assets.Scripts.GameUI;
using Assets.Scripts.Lobby;

namespace Assets.Scripts.GAE
{
    public class GAEManager : MonoBehaviour
    {
        private static GAEManager _instance;
        public static GAEManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GAEManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GAEManager");
                        _instance = go.AddComponent<GAEManager>();
                    }
                }

                return _instance;
            }
        }

        [SerializeField] private GAEConfigSO m_Config;
        [SerializeField] private int m_UnlockLevel = 5;

        public GAEConfigSO Config => m_Config;
        public int UnlockLevel => m_UnlockLevel;
        public GAEProgressData Progress { get; private set; }

        public event Action OnStateChanged;
        public event Action<int, GAERewardType, int> OnStageRewardGranted;

        private string m_ActiveEventInstanceId;
        private float m_NextScheduleCheckTime;
        private const float ScheduleCheckInterval = 1f;

        public bool IsFeatureEnabled =>
            RemoteConfigManager.Instance == null || RemoteConfigManager.Instance.IsGAEEnabled;

        public bool IsUnlocked =>
            UserDataManager.Instance != null &&
            UserDataManager.Instance.CurrentLevel >= m_UnlockLevel;

        public bool IsEventActive => IsFeatureEnabled && !string.IsNullOrEmpty(m_ActiveEventInstanceId);

        public bool ShouldShowUI => IsEventActive && IsUnlocked;

        /// <summary>
        /// When true, level picks award GAE arrows instead of coins.
        /// </summary>
        public bool IsGameplayGaeCurrencyActive => ShouldShowUI;

        private int m_PendingLevelArrows;
        private RectTransform m_BarAnimationTarget;

        public bool HasPendingLevelArrows => m_PendingLevelArrows > 0;
        public int PendingLevelArrows => m_PendingLevelArrows;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
            SyncEventState(forceResetOnMismatch: true);
        }

        private void Update()
        {
            if (Time.time < m_NextScheduleCheckTime)
            {
                return;
            }

            m_NextScheduleCheckTime = Time.time + ScheduleCheckInterval;
            SyncEventState(forceResetOnMismatch: false);
            TryCommitPendingArrows();
        }

        private void LoadConfig()
        {
            if (m_Config == null)
            {
                m_Config = Resources.Load<GAEConfigSO>("GAE/GAEConfig");
            }

            if (m_Config == null)
            {
                m_Config = ScriptableObject.CreateInstance<GAEConfigSO>();
            }

            m_Config.EnsureDefaultStages();
        }

        public void SyncEventState(bool forceResetOnMismatch)
        {
            if (!IsFeatureEnabled)
            {
                if (Progress != null || !string.IsNullOrEmpty(m_ActiveEventInstanceId))
                {
                    ResetAllProgress("Feature disabled by remote config.");
                }

                NotifyStateChanged();
                return;
            }

            string currentEventId = GAESchedule.GetCurrentEventInstanceId(DateTime.UtcNow);
            if (!forceResetOnMismatch &&
                string.Equals(m_ActiveEventInstanceId, currentEventId, StringComparison.Ordinal) &&
                Progress != null &&
                string.Equals(Progress.EventInstanceId, currentEventId, StringComparison.Ordinal))
            {
                return;
            }

            if (Progress == null)
            {
                Progress = DeserializeProgress(UserDataManager.Instance.GetGAEProgressJson());
            }

            if (Progress == null ||
                !string.Equals(Progress.EventInstanceId, currentEventId, StringComparison.Ordinal))
            {
                ResetAllProgress($"New GAE event started: {currentEventId}");
                Progress = CreateFreshProgress(currentEventId);
                SaveProgress();
            }

            m_ActiveEventInstanceId = currentEventId;
            ProcessStageRewards();
            NotifyStateChanged();
        }

        public void AddGoldenArrows(int amount)
        {
            if (!IsEventActive || !IsUnlocked || amount <= 0 || m_Config == null || m_Config.Stages == null || m_Config.Stages.Count == 0)
            {
                return;
            }

            int maxTarget = m_Config.Stages[m_Config.Stages.Count - 1].ArrowTarget;
            Progress.CollectedArrows = Mathf.Min(Progress.CollectedArrows + amount, maxTarget);
            SaveProgress();
            ProcessStageRewards();
            NotifyStateChanged();
        }

        public void RegisterBarAnimationTarget(RectTransform target)
        {
            m_BarAnimationTarget = target;
        }

        public RectTransform GetBarAnimationTarget()
        {
            return m_BarAnimationTarget;
        }

        public void QueueLevelWinArrows(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            m_PendingLevelArrows += amount;
        }

        public void AddPendingLevelArrows(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            m_PendingLevelArrows += amount;
        }

        public void ClearPendingLevelArrows()
        {
            m_PendingLevelArrows = 0;
        }

        public bool IsProgressAnimationBlocked()
        {
            if (MultiplyCoinsPopup.IsAnyVisible)
            {
                return true;
            }

            if (HomeContoller.IsNoAdsOfferVisible)
            {
                return true;
            }

            return false;
        }

        public void TryCommitPendingArrows()
        {
            if (m_PendingLevelArrows <= 0 || IsProgressAnimationBlocked())
            {
                return;
            }

            int amount = m_PendingLevelArrows;
            m_PendingLevelArrows = 0;
            AddGoldenArrows(amount);
        }

        public int GetCurrentStageIndex()
        {
            if (m_Config == null || m_Config.Stages == null || m_Config.Stages.Count == 0 || Progress == null)
            {
                return 0;
            }

            for (int i = 0; i < m_Config.Stages.Count; i++)
            {
                if (!IsStageClaimed(i))
                {
                    return i;
                }
            }

            return m_Config.Stages.Count - 1;
        }

        public bool IsStageClaimed(int stageIndex)
        {
            if (Progress == null || stageIndex < 0)
            {
                return false;
            }

            return (Progress.ClaimedStageMask & (1 << stageIndex)) != 0;
        }

        public bool AreAllStagesComplete()
        {
            if (m_Config == null || m_Config.Stages == null || Progress == null)
            {
                return false;
            }

            for (int i = 0; i < m_Config.Stages.Count; i++)
            {
                if (!IsStageClaimed(i))
                {
                    return false;
                }
            }

            return true;
        }

        public void GetStageProgress(out int current, out int target, out int stageIndex)
        {
            current = 0;
            target = 1;
            stageIndex = 0;

            if (m_Config == null || m_Config.Stages == null || m_Config.Stages.Count == 0 || Progress == null)
            {
                return;
            }

            stageIndex = GetCurrentStageIndex();
            int previousThreshold = stageIndex > 0 ? m_Config.Stages[stageIndex - 1].ArrowTarget : 0;
            int stageThreshold = m_Config.Stages[stageIndex].ArrowTarget;
            target = Mathf.Max(1, stageThreshold - previousThreshold);
            current = Mathf.Clamp(Progress.CollectedArrows - previousThreshold, 0, target);

            if (AreAllStagesComplete())
            {
                current = target;
            }
        }

        public GAEStageDefinition GetCurrentStageDefinition()
        {
            if (m_Config == null || m_Config.Stages == null || m_Config.Stages.Count == 0)
            {
                return null;
            }

            return m_Config.Stages[GetCurrentStageIndex()];
        }

        public string GetTimerString()
        {
            return GAESchedule.FormatRemainingTime(GAESchedule.GetRemainingTime(DateTime.UtcNow));
        }

        public TimeSpan GetRemainingTime()
        {
            return GAESchedule.GetRemainingTime(DateTime.UtcNow);
        }

        public void SetLastPresentedProgress(int collected, int stageIndex)
        {
            if (Progress == null)
            {
                return;
            }

            Progress.LastPresentedCollected = collected;
            Progress.LastPresentedStageIndex = stageIndex;
            SaveProgress();
        }

        public void GetLastPresentedProgress(out int collected, out int stageIndex)
        {
            collected = Progress?.LastPresentedCollected ?? 0;
            stageIndex = Progress?.LastPresentedStageIndex ?? 0;
        }

        private void ProcessStageRewards()
        {
            if (m_Config == null || m_Config.Stages == null || Progress == null)
            {
                return;
            }

            for (int i = 0; i < m_Config.Stages.Count; i++)
            {
                if (IsStageClaimed(i))
                {
                    continue;
                }

                GAEStageDefinition stage = m_Config.Stages[i];
                if (Progress.CollectedArrows < stage.ArrowTarget)
                {
                    break;
                }

                Progress.ClaimedStageMask |= 1 << i;
                GrantReward(stage.RewardType, stage.RewardAmount);
                OnStageRewardGranted?.Invoke(i, stage.RewardType, stage.RewardAmount);
            }

            SaveProgress();
        }

        private void GrantReward(GAERewardType type, int amount)
        {
            UserDataManager userData = UserDataManager.Instance;
            switch (type)
            {
                case GAERewardType.Coin:
                    userData.AddArrowsCurrency(amount, ResourceAnalyticsReasons.GaeStageReward);
                    break;
                case GAERewardType.Hint:
                    userData.AddHintBooster(amount, ResourceAnalyticsReasons.GaeStageReward);
                    break;
                case GAERewardType.Shuffle:
                    userData.AddShuffleBooster(amount, ResourceAnalyticsReasons.GaeStageReward);
                    break;
                case GAERewardType.MagicWand:
                    userData.AddMagicBooster(amount, ResourceAnalyticsReasons.GaeStageReward);
                    break;
                case GAERewardType.RefillLife:
                    userData.AddRefillBooster(amount, ResourceAnalyticsReasons.GaeStageReward);
                    break;
            }
        }

        private void ResetAllProgress(string reason)
        {
            Debug.Log($"[GAEManager] {reason}");
            Progress = null;
            m_ActiveEventInstanceId = null;
            m_PendingLevelArrows = 0;
            UserDataManager.Instance.ClearGAEProgress();
        }

        private GAEProgressData CreateFreshProgress(string eventInstanceId)
        {
            return new GAEProgressData
            {
                EventInstanceId = eventInstanceId,
                CollectedArrows = 0,
                ClaimedStageMask = 0,
                LastPresentedCollected = 0,
                LastPresentedStageIndex = 0
            };
        }

        private void SaveProgress()
        {
            if (Progress == null)
            {
                return;
            }

            UserDataManager.Instance.SaveGAEProgressJson(JsonUtility.ToJson(Progress));
        }

        private static GAEProgressData DeserializeProgress(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<GAEProgressData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GAEManager] Failed to parse progress: {e.Message}");
                return null;
            }
        }

        private void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
