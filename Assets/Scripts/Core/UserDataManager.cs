using System;
using UnityEngine;
using Assets.Scripts.Data;
using System.Collections.Generic;

namespace Assets.Scripts.Core
{
    [Serializable]
    public class LevelProgress
    {
        public string levelId;
        public bool isChallenge;
        public List<int> pickedArrowIds = new List<int>();
        public float remainingTime;
        public float levelDuration;
        public int remainingLives;
        public bool isTimerActive;
        public int challengeYear;
        public int challengeMonth;
        public int challengeDay;
        public int collectedCoins;
        public int playOnPurchaseCount;
        public bool hasProgress = false;
    }

    public class UserDataManager
    {
        private static UserDataManager instance;
        public static UserDataManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UserDataManager();
                }
                return instance;
            }
        }

        public event System.Action OnLevelChanged;
        public event System.Action<int> OnCurrencyChanged;

        private const string LevelKey = "CurrentLevel";
        private const string InstallDateKey = "InstallDate";
        private const string ArrowsCurrencyKey = "ArrowsCurrency";
        private const string CurrentLevelAttemptsKey = "CurrentLevelAttempts";
        private const string LastAttemptLevelIdKey = "LastAttemptLevelId";
        private const string MaxStreakKey = "MaxStreakRecord";
        private const string LevelProgressKey = "LevelProgress";
        private const string LastRateUsDateKey = "LastRateUsDate";
        private const string LevelStreakKey = "LevelStreak";
        private const string LiveOpDataPrefix = "LiveOpData_";
        private const string LiveOpCurrentIDPrefix = "LiveOpCurrentID_";
        private const string MagicBoosterKey = "MagicBoosterBalance";
        private const string HintBoosterKey = "HintBoosterBalance";
        private const string RefillBoosterKey = "RefillBoosterBalance";
        private const string ShuffleBoosterKey = "ShuffleBoosterBalance";
        private const string BoostersInitializedKey = "BoostersInitialized";
        private const string IsInterstitialActiveKey = "IsInterstitialActive";
        private const string IsDynamicMaxZoomKey = "IsDynamicMaxZoom";
        private const string SessionCountKey = "TotalSessionCount";
        private const string HasSentSession7Key = "HasSentSession7Event";



        public int CurrentLevel { get; private set; } = 1;
        public int ArrowsCurrency { get; private set; } = 0;
        private bool _isInterstitialActive = true;
        public bool IsInterstitialActive 
        { 
            get => _isInterstitialActive; 
            set 
            {
                _isInterstitialActive = value;
                PlayerPrefs.SetInt(IsInterstitialActiveKey, _isInterstitialActive ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private bool _isDynamicMaxZoom = true;
        public bool IsDynamicMaxZoom 
        { 
            get => _isDynamicMaxZoom; 
            set 
            {
                _isDynamicMaxZoom = value;
                PlayerPrefs.SetInt(IsDynamicMaxZoomKey, _isDynamicMaxZoom ? 1 : 0);
                PlayerPrefs.Save();
            }
        }



        private int _currentLevelAttempts = 0;
        public int CurrentLevelAttempts 
        { 
            get => _currentLevelAttempts; 
            private set 
            {
                _currentLevelAttempts = value;
                Debug.Log($"[UserDataManager] CurrentLevelAttempts changed to: {_currentLevelAttempts}");
            }
        }
        public string LastAttemptLevelId { get; private set; } = string.Empty;
        public int MaxStreak { get; private set; } = 0;
        public int LevelStreak { get; private set; } = 0;
        public System.DateTime InstallDate { get; private set; }
        public System.DateTime LastRateUsDate { get; private set; }
        public bool IsRateUsCheckPending { get; set; } = false;
        public int MagicBoosterCount { get; private set; } = 0;
        public int HintBoosterCount { get; private set; } = 0;
        public int RefillBoosterCount { get; private set; } = 0;
        public int ShuffleBoosterCount { get; private set; } = 0;
        public int SessionCount { get; private set; } = 0;
        public bool HasSentSession7 { get; private set; } = false;
        public event System.Action<int> OnMagicBoosterChanged;
        public event System.Action<int> OnHintBoosterChanged;
        public event System.Action<int> OnRefillBoosterChanged;
        public event System.Action<int> OnShuffleBoosterChanged;

        private const string LegendPassStepKey = "LegendPass_CurrentStep";
        private const string LegendPassPremiumKey = "LegendPass_PremiumUnlocked";
        private const string LegendPassClaimedFreeKey = "LegendPass_ClaimedFree";
        private const string LegendPassClaimedPremiumKey = "LegendPass_ClaimedPremium";
        private const string LegendPassStartDateKey = "LegendPass_StartDate";

        public int LegendPassStep { get; private set; } = 0;
        public bool IsLegendPassPremiumUnlocked { get; private set; } = false;
        public int LegendPassClaimedFreeMask { get; private set; } = 0;
        public int LegendPassClaimedPremiumMask { get; private set; } = 0;
        public string LegendPassStartDate { get; private set; } = string.Empty;

        public int LastViewedChallengeYear { get; private set; }
        public int LastViewedChallengeMonth { get; private set; }


        private Dictionary<string, int> m_MonthlyCache = new Dictionary<string, int>();

        // PlayerPrefs.Save() can hitch (sync disk flush), especially in Editor/Desktop.
        // LevelProgress is written frequently (periodic autosave), so throttle flushes.
        private const float LevelProgressPrefsFlushIntervalSeconds = 30f;
        private float m_LastLevelProgressPrefsFlushRealtime = -999f;
        private bool m_SuppressResourceAnalytics;

        private void MaybeFlushPrefsForLevelProgress(bool force)
        {
            if (force)
            {
                PlayerPrefs.Save();
                m_LastLevelProgressPrefsFlushRealtime = Time.realtimeSinceStartup;
                return;
            }

            if (Time.realtimeSinceStartup - m_LastLevelProgressPrefsFlushRealtime >= LevelProgressPrefsFlushIntervalSeconds)
            {
                PlayerPrefs.Save();
                m_LastLevelProgressPrefsFlushRealtime = Time.realtimeSinceStartup;
            }
        }

        private UserDataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            m_SuppressResourceAnalytics = true;
            CurrentLevel = PlayerPrefs.GetInt(LevelKey, 1);
            ArrowsCurrency = PlayerPrefs.GetInt(ArrowsCurrencyKey, 0);
            CurrentLevelAttempts = PlayerPrefs.GetInt(CurrentLevelAttemptsKey, 0);
            LastAttemptLevelId = PlayerPrefs.GetString(LastAttemptLevelIdKey, string.Empty);
            MaxStreak = PlayerPrefs.GetInt(MaxStreakKey, 0);
            LevelStreak = PlayerPrefs.GetInt(LevelStreakKey, 0);
            
            string installDateStr = PlayerPrefs.GetString(InstallDateKey, string.Empty);
            if (string.IsNullOrEmpty(installDateStr))
            {
                InstallDate = System.DateTime.Now;
                PlayerPrefs.SetString(InstallDateKey, InstallDate.ToBinary().ToString());
                PlayerPrefs.Save();
            }
            else
            {
                if (long.TryParse(installDateStr, out long binaryDate))
                {
                    InstallDate = System.DateTime.FromBinary(binaryDate);
                }
                else
                {
                    InstallDate = System.DateTime.Now;
                }
            }
            
            string lastRateUsDateStr = PlayerPrefs.GetString(LastRateUsDateKey, string.Empty);
            if (!string.IsNullOrEmpty(lastRateUsDateStr))
            {
                if (long.TryParse(lastRateUsDateStr, out long binaryDate))
                {
                    LastRateUsDate = System.DateTime.FromBinary(binaryDate);
                }
            }

            LastViewedChallengeYear = PlayerPrefs.GetInt("LastViewedChallengeYear", System.DateTime.Now.Year);
            LastViewedChallengeMonth = PlayerPrefs.GetInt("LastViewedChallengeMonth", System.DateTime.Now.Month);
            MagicBoosterCount = PlayerPrefs.GetInt(MagicBoosterKey, 0);
            HintBoosterCount = PlayerPrefs.GetInt(HintBoosterKey, 0);
            RefillBoosterCount = PlayerPrefs.GetInt(RefillBoosterKey, 0);
            ShuffleBoosterCount = PlayerPrefs.GetInt(ShuffleBoosterKey, 0);

            if (PlayerPrefs.GetInt(BoostersInitializedKey, 0) == 0)
            {
                MagicBoosterCount = 1;
                HintBoosterCount = 1;
                RefillBoosterCount = 1;
                ShuffleBoosterCount = 1;
                PlayerPrefs.SetInt(BoostersInitializedKey, 1);
                SaveMagicBooster();
                SaveHintBooster();
                SaveRefillBooster();
                SaveShuffleBooster();
            }

            LegendPassStep = PlayerPrefs.GetInt(LegendPassStepKey, 0);
            IsLegendPassPremiumUnlocked = PlayerPrefs.GetInt(LegendPassPremiumKey, 0) == 1;
            LegendPassClaimedFreeMask = PlayerPrefs.GetInt(LegendPassClaimedFreeKey, 0);
            LegendPassClaimedPremiumMask = PlayerPrefs.GetInt(LegendPassClaimedPremiumKey, 0);
            LegendPassStartDate = PlayerPrefs.GetString(LegendPassStartDateKey, string.Empty);
            _isInterstitialActive = PlayerPrefs.GetInt(IsInterstitialActiveKey, 1) == 1;
            _isDynamicMaxZoom = PlayerPrefs.GetInt(IsDynamicMaxZoomKey, 1) == 1;

            // Increment and save session count
            SessionCount = PlayerPrefs.GetInt(SessionCountKey, 0) + 1;
            PlayerPrefs.SetInt(SessionCountKey, SessionCount);
            HasSentSession7 = PlayerPrefs.GetInt(HasSentSession7Key, 0) == 1;
            PlayerPrefs.Save();
            m_SuppressResourceAnalytics = false;
        }

        private void LogEarn(string reason, int shuffle = 0, int hint = 0, int magicwand = 0, int refill = 0, int coins = 0)
        {
            if (m_SuppressResourceAnalytics || string.IsNullOrEmpty(reason) || AmountIsZero(shuffle, hint, magicwand, refill, coins)) return;
            FirebaseManager.Instance?.LogEarnEvent(reason, shuffle, hint, magicwand, refill, coins);
        }

        private void LogSpend(string reason, int shuffle = 0, int hint = 0, int magicwand = 0, int refill = 0, int coins = 0)
        {
            if (m_SuppressResourceAnalytics || string.IsNullOrEmpty(reason) || AmountIsZero(shuffle, hint, magicwand, refill, coins)) return;
            FirebaseManager.Instance?.LogSpendEvent(reason, shuffle, hint, magicwand, refill, coins);
        }

        private static bool AmountIsZero(int shuffle, int hint, int magicwand, int refill, int coins)
        {
            return shuffle == 0 && hint == 0 && magicwand == 0 && refill == 0 && coins == 0;
        }



        public void IncrementLevel()
        {
            CurrentLevel++;
            SaveData();
        }

        public void SetLevel(int level)
        {
            CurrentLevel = level;
            SaveData();
        }

        public void AddArrowsCurrency(int amount, string reason)
        {
            if (amount < 0) return; // Prevent negative addition
            ArrowsCurrency += amount;
            SaveCurrency();
            LogEarn(reason, coins: amount);
        }

        public bool ReduceArrowsCurrency(int amount, string reason)
        {
            if (amount < 0) return false;
            
            if (ArrowsCurrency >= amount)
            {
                ArrowsCurrency -= amount;
                SaveCurrency();
                LogSpend(reason, coins: amount);
                return true;
            }
            return false;
        }

        private void SaveCurrency()
        {
            PlayerPrefs.SetInt(ArrowsCurrencyKey, ArrowsCurrency);
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke(ArrowsCurrency);
        }

        public void AddMagicBooster(int amount, string reason)
        {
            if (amount < 0) return;
            MagicBoosterCount += amount;
            SaveMagicBooster();
            LogEarn(reason, magicwand: amount);
        }

        public bool UseMagicBooster(int amount, string reason)
        {
            if (amount < 0) return false;
            if (MagicBoosterCount >= amount)
            {
                MagicBoosterCount -= amount;
                SaveMagicBooster();
                LogSpend(reason, magicwand: amount);
                return true;
            }
            return false;
        }

        private void SaveMagicBooster()
        {
            PlayerPrefs.SetInt(MagicBoosterKey, MagicBoosterCount);
            PlayerPrefs.Save();
            OnMagicBoosterChanged?.Invoke(MagicBoosterCount);
        }

        public void AddHintBooster(int amount, string reason)
        {
            if (amount < 0) return;
            HintBoosterCount += amount;
            SaveHintBooster();
            LogEarn(reason, hint: amount);
        }

        public bool UseHintBooster(int amount, string reason)
        {
            if (amount < 0) return false;
            if (HintBoosterCount >= amount)
            {
                HintBoosterCount -= amount;
                SaveHintBooster();
                LogSpend(reason, hint: amount);
                return true;
            }
            return false;
        }

        private void SaveHintBooster()
        {
            PlayerPrefs.SetInt(HintBoosterKey, HintBoosterCount);
            PlayerPrefs.Save();
            OnHintBoosterChanged?.Invoke(HintBoosterCount);
        }

        public void AddRefillBooster(int amount, string reason)
        {
            if (amount < 0) return;
            RefillBoosterCount += amount;
            SaveRefillBooster();
            LogEarn(reason, refill: amount);
        }

        public bool UseRefillBooster(int amount, string reason)
        {
            if (amount < 0) return false;
            if (RefillBoosterCount >= amount)
            {
                RefillBoosterCount -= amount;
                SaveRefillBooster();
                LogSpend(reason, refill: amount);
                return true;
            }
            return false;
        }

        private void SaveRefillBooster()
        {
            PlayerPrefs.SetInt(RefillBoosterKey, RefillBoosterCount);
            PlayerPrefs.Save();
            OnRefillBoosterChanged?.Invoke(RefillBoosterCount);
        }

        public void AddShuffleBooster(int amount, string reason)
        {
            if (amount < 0) return;
            ShuffleBoosterCount += amount;
            SaveShuffleBooster();
            LogEarn(reason, shuffle: amount);
        }

        public bool UseShuffleBooster(int amount, string reason)
        {
            if (amount < 0) return false;
            if (ShuffleBoosterCount >= amount)
            {
                ShuffleBoosterCount -= amount;
                SaveShuffleBooster();
                LogSpend(reason, shuffle: amount);
                return true;
            }
            return false;
        }

        private void SaveShuffleBooster()
        {
            PlayerPrefs.SetInt(ShuffleBoosterKey, ShuffleBoosterCount);
            PlayerPrefs.Save();
            OnShuffleBoosterChanged?.Invoke(ShuffleBoosterCount);
        }

        public void MarkRateUsSeen()
        {
            LastRateUsDate = System.DateTime.Now;
            PlayerPrefs.SetString(LastRateUsDateKey, LastRateUsDate.ToBinary().ToString());
            PlayerPrefs.Save();
            IsRateUsCheckPending = false;
        }

        public void ResetProgress()
        {
            CurrentLevel = 1;
            
            // Reset Monthly Challenge data
            ClearAllMonthlyProgress();

            // Reset Install Date (so the challenge starts from now)
            InstallDate = System.DateTime.Now;
            PlayerPrefs.SetString(InstallDateKey, InstallDate.ToBinary().ToString());

            SaveData(); // Helpers call PlayerPrefs.Save() but SaveData does too
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayClick();
            }
            
            OnMonthlyProgressChanged?.Invoke();
        }

        private void ClearAllMonthlyProgress()
        {
            // Clear cache
            m_MonthlyCache.Clear();

            // Clear PlayerPrefs for monthly challenges
            // We'll clear a search range to ensure everything is removed
            for (int year = 2024; year <= 2030; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    string key = GetMonthlyKey(year, month);
                    PlayerPrefs.DeleteKey(key);
                }
            }
            PlayerPrefs.Save();
        }

        private void SaveData()
        {
            PlayerPrefs.SetInt(LevelKey, CurrentLevel);
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.SetString(LastAttemptLevelIdKey, LastAttemptLevelId);
            PlayerPrefs.Save();
            OnLevelChanged?.Invoke();
        }

        public void IncrementCurrentLevelAttempts()
        {
            CurrentLevelAttempts++;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.Save();
        }

        public void ResetCurrentLevelAttempts(string levelId)
        {
            CurrentLevelAttempts = 1;
            LastAttemptLevelId = levelId;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.SetString(LastAttemptLevelIdKey, LastAttemptLevelId);
            PlayerPrefs.Save();
        }

        public void ClearCurrentLevelAttempts()
        {
            CurrentLevelAttempts = 0;
            PlayerPrefs.SetInt(CurrentLevelAttemptsKey, CurrentLevelAttempts);
            PlayerPrefs.Save();
        }

        public void UpdateMaxStreak(int streak)
        {
            if (streak > MaxStreak)
            {
                MaxStreak = streak;
                PlayerPrefs.SetInt(MaxStreakKey, MaxStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] New Max Streak Record: {MaxStreak}");
            }
        }

        public void IncrementLevelStreak()
        {
            if (CurrentLevel >= 24)
            {
                LevelStreak++;
                NeedsLevelStreakAnimation = true;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak incremented to: {LevelStreak}");
            }
        }

        public bool NeedsLevelStreakAnimation { get; set; }

        public void ResetLevelStreak()
        {
            NeedsLevelStreakAnimation = false;
            if (LevelStreak > 0)
            {
                LevelStreak = 0;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak reset.");
            }
        }

        public void RestoreLevelStreak(int previousStreak)
        {
            NeedsLevelStreakAnimation = false;
            if (previousStreak > 0)
            {
                LevelStreak = previousStreak;
                PlayerPrefs.SetInt(LevelStreakKey, LevelStreak);
                PlayerPrefs.Save();
                Debug.Log($"[UserDataManager] Level Streak restored to: {LevelStreak}");
            }
        }

        private string GetMonthlyKey(int year, int month)
        {
            return $"MonthlyChallenge_{year}_{month}";
        }

        public event System.Action OnMonthlyProgressChanged;

        public void SaveMonthlyChallengeProgress(int year, int month, int day)
        {
            string key = GetMonthlyKey(year, month);
            int currentMask = GetMonthlyChallengeBitmask(year, month);
            
            // Set the bit corresponding to the day (day 1 is bit 0)
            int newMask = currentMask | (1 << (day - 1));
            
            if (newMask != currentMask)
            {
                m_MonthlyCache[key] = newMask;
                PlayerPrefs.SetInt(key, newMask);
                PlayerPrefs.Save();
                OnMonthlyProgressChanged?.Invoke();
            }
        }

        public int GetMonthlyChallengeBitmask(int year, int month)
        {
            string key = GetMonthlyKey(year, month);
            if (m_MonthlyCache.ContainsKey(key))
            {
                return m_MonthlyCache[key];
            }

            int mask = PlayerPrefs.GetInt(key, 0);
            m_MonthlyCache[key] = mask;
            return mask;
        }

        public bool IsDayCompleted(int year, int month, int day)
        {
            int mask = GetMonthlyChallengeBitmask(year, month);
            return (mask & (1 << (day - 1))) != 0;
        }

        public void SetLastViewedChallengeMonth(int year, int month)
        {
            LastViewedChallengeYear = year;
            LastViewedChallengeMonth = month;
            PlayerPrefs.SetInt("LastViewedChallengeYear", year);
            PlayerPrefs.SetInt("LastViewedChallengeMonth", month);
            PlayerPrefs.Save();
        }

        public void SaveLevelProgress(LevelProgress progress)
        {
            if (progress == null)
            {
                ClearLevelProgress();
                return;
            }
            string json = JsonUtility.ToJson(progress);
            PlayerPrefs.SetString(LevelProgressKey, json);
            MaybeFlushPrefsForLevelProgress(force: false);
        }

        public LevelProgress LoadLevelProgress()
        {
            string json = PlayerPrefs.GetString(LevelProgressKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new LevelProgress { hasProgress = false };
            }
            try
            {
                LevelProgress progress = JsonUtility.FromJson<LevelProgress>(json);
                return progress;
            }
            catch (Exception e)
            {
                Debug.LogError($"[UserDataManager] Error loading level progress: {e.Message}");
                return new LevelProgress { hasProgress = false };
            }
        }

        public void ClearLevelProgress()
        {
            PlayerPrefs.DeleteKey(LevelProgressKey);
            // Force flush so "no-progress" is durable immediately (e.g. on lobby return).
            MaybeFlushPrefsForLevelProgress(force: true);
        }

        #region LiveOps Persistence
        
        public string GetLiveOpData(string uniqueID)
        {
            return PlayerPrefs.GetString(LiveOpDataPrefix + uniqueID, string.Empty);
        }

        public void SaveLiveOpData(string uniqueID, string json)
        {
            PlayerPrefs.SetString(LiveOpDataPrefix + uniqueID, json);
            PlayerPrefs.Save();
        }

        public void CleanupLiveOpData(string eventID, string currentUniqueID)
        {
            string key = LiveOpCurrentIDPrefix + eventID;
            string lastID = PlayerPrefs.GetString(key, string.Empty);
            
            if (lastID != currentUniqueID)
            {
                if (!string.IsNullOrEmpty(lastID))
                {
                    Debug.Log($"[UserDataManager] Cleaning up old LiveOp data: {lastID}");
                    PlayerPrefs.DeleteKey(LiveOpDataPrefix + lastID);
                }
                
                PlayerPrefs.SetString(key, currentUniqueID);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region Legend Pass Persistence

        public void SetLegendPassStep(int step)
        {
            LegendPassStep = step;
            PlayerPrefs.SetInt(LegendPassStepKey, LegendPassStep);
            PlayerPrefs.Save();
        }

        public void SetLegendPassPremiumUnlocked(bool unlocked)
        {
            IsLegendPassPremiumUnlocked = unlocked;
            PlayerPrefs.SetInt(LegendPassPremiumKey, IsLegendPassPremiumUnlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetLegendPassClaimedMasks(int freeMask, int premiumMask)
        {
            LegendPassClaimedFreeMask = freeMask;
            LegendPassClaimedPremiumMask = premiumMask;
            PlayerPrefs.SetInt(LegendPassClaimedFreeKey, LegendPassClaimedFreeMask);
            PlayerPrefs.SetInt(LegendPassClaimedPremiumKey, LegendPassClaimedPremiumMask);
            PlayerPrefs.Save();
        }

        public void SetLegendPassStartDate(string dateStr)
        {
            LegendPassStartDate = dateStr;
            PlayerPrefs.SetString(LegendPassStartDateKey, LegendPassStartDate);
            PlayerPrefs.Save();
        }

        #endregion

        public void MarkSession7EventSent()
        {
            HasSentSession7 = true;
            PlayerPrefs.SetInt(HasSentSession7Key, 1);
            PlayerPrefs.Save();
            Debug.Log("[UserDataManager] Marked session7 event as sent.");
        }

        public int GetRetentionDay()
        {
            // Day 1 is the install day
            return (System.DateTime.Today - InstallDate.Date).Days + 1;
        }

        public bool HasSentRetentionEvent(int day)
        {
            return PlayerPrefs.GetInt("HasSentRet_" + day, 0) == 1;
        }

        public void MarkRetentionEventSent(int day)
        {
            PlayerPrefs.SetInt("HasSentRet_" + day, 1);
            PlayerPrefs.Save();
            Debug.Log($"[UserDataManager] Marked Ret_{day} event as sent.");
        }
    }
}
