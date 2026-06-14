using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Singular;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Persists Singular device attribution safely, applies Firebase user properties when ready,
    /// and links a stable analytics user id to Singular/Firebase without blocking SDK callbacks.
    /// </summary>
    public sealed class SingularAttributionBridge : MonoBehaviour
    {
        private const string PrefsSnapshotKey = "singular_attribution_snapshot_v1";
        private const string PrefsAnalyticsUserIdKey = "analytics_user_id_v1";
        private const int FirebaseUserPropertyMaxLength = 36;

        private static SingularAttributionBridge _instance;
        private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();

        private FirebaseManager _firebaseManager;
        private bool _firebaseReady;
        private bool _singularReady;
        private string _pendingAnalyticsUserId;
        private SingularAttributionSnapshot _cachedSnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null)
                return;

            var go = new GameObject("SingularAttributionBridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SingularAttributionBridge>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSnapshotFromPrefs();
        }

        private void Update()
        {
            while (MainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SingularAttributionBridge] Main-thread action failed: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public static void HandleDeviceAttributionCallback(Dictionary<string, object> attributionInfo)
        {
            if (attributionInfo == null || attributionInfo.Count == 0)
                return;

            EnqueueOnMainThread(() =>
            {
                if (!EnsureInstance())
                    return;

                _instance.IngestAttributionDictionary(attributionInfo);
            });
        }

        public static void NotifyFirebaseReady(FirebaseManager firebaseManager)
        {
            EnqueueOnMainThread(() =>
            {
                if (!EnsureInstance())
                    return;

                _instance._firebaseManager = firebaseManager;
                _instance._firebaseReady = firebaseManager != null;
                _instance.ApplyPendingAnalyticsUserId(fireLoginEvent: false);
                _instance.ApplySnapshotToFirebase();
                _instance.ApplySnapshotToSingularGlobals();
            });
        }

        public static void NotifySingularInitialized()
        {
            EnqueueOnMainThread(() =>
            {
                if (!EnsureInstance())
                    return;

                _instance._singularReady = true;
                _instance.ApplyPendingAnalyticsUserId(fireLoginEvent: false);
                _instance.ApplySnapshotToSingularGlobals();
            });
        }

        /// <summary>Stable id for Firebase + Singular (created on first use, stored locally).</summary>
        public static void EnsureAnalyticsUserIdLinked()
        {
            EnqueueOnMainThread(() =>
            {
                if (!EnsureInstance())
                    return;

                _instance.ApplyPendingAnalyticsUserId(fireLoginEvent: false);
            });
        }

        public static bool TryGetCachedSnapshot(out SingularAttributionSnapshot snapshot)
        {
            snapshot = _instance?._cachedSnapshot;
            return snapshot != null && snapshot.HasAnyData;
        }

        private static bool EnsureInstance()
        {
            if (_instance != null)
                return true;

            Create();
            return _instance != null;
        }

        private static void EnqueueOnMainThread(Action action)
        {
            if (action == null)
                return;

            if (_instance != null)
            {
                MainThreadQueue.Enqueue(action);
                return;
            }

            // Bootstrap may not exist yet; create and queue.
            Create();
            MainThreadQueue.Enqueue(action);
        }

        private void IngestAttributionDictionary(Dictionary<string, object> attributionInfo)
        {
            var snapshot = SingularAttributionSnapshot.FromDictionary(attributionInfo);
            if (!snapshot.HasAnyData)
            {
                Debug.LogWarning("[SingularAttributionBridge] Attribution callback had no usable fields.");
                return;
            }

            _cachedSnapshot = snapshot;
            SaveSnapshotToPrefs(snapshot);

            Debug.Log(
                $"[SingularAttributionBridge] Attribution stored: network={snapshot.Network}, " +
                $"campaign={snapshot.Campaign}, type={snapshot.AcquisitionType}");

            ApplySnapshotToFirebase();
            ApplySnapshotToSingularGlobals();
            AdsManager.NotifyAttributionUpdated(snapshot);
        }

        private void LoadSnapshotFromPrefs()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsSnapshotKey, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return;

                var snapshot = JsonUtility.FromJson<SingularAttributionSnapshot>(json);
                if (snapshot != null && snapshot.HasAnyData)
                    _cachedSnapshot = snapshot;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SingularAttributionBridge] Could not load cached attribution: {ex.Message}");
            }
        }

        private void SaveSnapshotToPrefs(SingularAttributionSnapshot snapshot)
        {
            try
            {
                string json = JsonUtility.ToJson(snapshot);
                PlayerPrefs.SetString(PrefsSnapshotKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SingularAttributionBridge] Could not persist attribution: {ex.Message}");
            }
        }

        private string GetOrCreateAnalyticsUserId()
        {
            string existing = PlayerPrefs.GetString(PrefsAnalyticsUserIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing.Trim();

            string generated = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PrefsAnalyticsUserIdKey, generated);
            PlayerPrefs.Save();
            return generated;
        }

        private void ApplyPendingAnalyticsUserId(bool fireLoginEvent)
        {
            if (string.IsNullOrWhiteSpace(_pendingAnalyticsUserId))
                _pendingAnalyticsUserId = GetOrCreateAnalyticsUserId();

            if (string.IsNullOrWhiteSpace(_pendingAnalyticsUserId))
                return;

#if !UNITY_EDITOR
            if (_singularReady && SingularSDK.Initialized)
                SingularSDK.SetCustomUserId(_pendingAnalyticsUserId);
#endif

            if (_firebaseReady && _firebaseManager != null)
                _firebaseManager.ApplyAnalyticsUserId(_pendingAnalyticsUserId, fireLoginEvent);
        }

        private void ApplySnapshotToFirebase()
        {
            if (!_firebaseReady || _firebaseManager == null || _cachedSnapshot == null || !_cachedSnapshot.HasAnyData)
                return;

            _firebaseManager.ApplyAttributionUserProperties(_cachedSnapshot);
        }

        private void ApplySnapshotToSingularGlobals()
        {
#if UNITY_EDITOR
            return;
#else
            if (!_singularReady || !SingularSDK.Initialized || _cachedSnapshot == null || !_cachedSnapshot.HasAnyData)
                return;

            TrySetGlobalProperty("acq_network", _cachedSnapshot.Network);
            TrySetGlobalProperty("acq_campaign", _cachedSnapshot.Campaign);
            TrySetGlobalProperty("acq_sub_campaign", _cachedSnapshot.SubCampaign);
            TrySetGlobalProperty("acq_type", _cachedSnapshot.AcquisitionType);
            TrySetGlobalProperty("acq_campaign_id", _cachedSnapshot.CampaignId);
#endif
        }

#if !UNITY_EDITOR
        private static void TrySetGlobalProperty(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                SingularSDK.SetGlobalProperty(key, Truncate(value, FirebaseUserPropertyMaxLength), true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SingularAttributionBridge] SetGlobalProperty({key}) failed: {ex.Message}");
            }
        }
#endif

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
                return string.Empty;

            value = value.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    [Serializable]
    public class SingularAttributionSnapshot
    {
        public string Network;
        public string Campaign;
        public string CampaignId;
        public string SubCampaign;
        public string SubAdNetwork;
        public string Creative;
        public string TrackerName;
        public string AcquisitionType;
        public string IsViewThrough;
        public string BidType;

        public bool HasAnyData =>
            !string.IsNullOrWhiteSpace(Network) ||
            !string.IsNullOrWhiteSpace(Campaign) ||
            !string.IsNullOrWhiteSpace(CampaignId) ||
            !string.IsNullOrWhiteSpace(SubCampaign) ||
            !string.IsNullOrWhiteSpace(SubAdNetwork) ||
            !string.IsNullOrWhiteSpace(AcquisitionType);

        public static SingularAttributionSnapshot FromDictionary(Dictionary<string, object> attributionInfo)
        {
            var snapshot = new SingularAttributionSnapshot
            {
                Network = GetField(attributionInfo, "network", "partner", "media_source"),
                Campaign = GetField(attributionInfo, "campaign", "campaign_name", "pcn"),
                CampaignId = GetField(attributionInfo, "campaign_id", "adn_campaign_id"),
                SubCampaign = GetField(attributionInfo, "sub_campaign", "sub_campaign_name", "pscn", "adgroup", "ad_group"),
                SubAdNetwork = GetField(attributionInfo, "sub_adnetwork", "psn", "publisher"),
                Creative = GetField(attributionInfo, "creative", "creative_name", "pcrn"),
                TrackerName = GetField(attributionInfo, "tracker_name", "tracker"),
                BidType = GetField(attributionInfo, "bid_type", "campaign_attribution_type", "campaign_type"),
                IsViewThrough = GetField(attributionInfo, "is_view_through", "viewthrough")
            };

            snapshot.AcquisitionType = DeriveAcquisitionType(attributionInfo, snapshot.BidType);
            return snapshot;
        }

        private static string DeriveAcquisitionType(Dictionary<string, object> attributionInfo, string bidType)
        {
            if (!string.IsNullOrWhiteSpace(bidType))
                return bidType.Trim();

            string reengagement = GetField(attributionInfo, "is_reengagement", "is_re_engagement");
            if (IsTruthy(reengagement))
                return "reengagement";

            return "install";
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            return value == "1" ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetField(Dictionary<string, object> dict, params string[] keys)
        {
            if (dict == null || keys == null)
                return null;

            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key) || !dict.TryGetValue(key, out object raw) || raw == null)
                    continue;

                string value = raw.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }
    }
}
