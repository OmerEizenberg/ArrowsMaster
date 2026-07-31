using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Cheat-resistant UTC clock: syncs HTTP Date, then extrapolates with realtimeSinceStartup.
    /// Supports a debug time offset for simulating LiveOps / bot progression in the editor.
    /// </summary>
    public class TrustedTimeService : MonoBehaviour
    {
        private static TrustedTimeService instance;
        public static TrustedTimeService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<TrustedTimeService>();
                    if (instance == null)
                    {
                        var go = new GameObject("TrustedTimeService");
                        instance = go.AddComponent<TrustedTimeService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private const string SyncUrl = "https://www.google.com";
        private const float ResyncIntervalSeconds = 15 * 60f;
        private const string DebugOffsetPrefsKey = "TrustedTime_DebugOffsetSeconds";

        private bool hasSync;
        private DateTime syncedServerUtc;
        private float syncedRealtime;
        private float nextSyncAt;
        private bool syncInFlight;
        private double debugOffsetSeconds;

        public bool HasSync => hasSync;
        public double DebugOffsetSeconds => debugOffsetSeconds;

        public static DateTime UtcNow => Instance.GetUtcNow();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            debugOffsetSeconds = PlayerPrefs.GetFloat(DebugOffsetPrefsKey, 0f);
        }

        private void Start()
        {
            StartCoroutine(SyncLoop());
        }

        public DateTime GetUtcNow()
        {
            DateTime baseUtc;
            if (!hasSync)
            {
                baseUtc = DateTime.UtcNow;
            }
            else
            {
                double elapsed = Time.realtimeSinceStartup - syncedRealtime;
                if (elapsed < 0) elapsed = 0;
                baseUtc = syncedServerUtc.AddSeconds(elapsed);
            }

            if (Math.Abs(debugOffsetSeconds) > 0.01)
                return baseUtc.AddSeconds(debugOffsetSeconds);
            return baseUtc;
        }

        /// <summary>Advance (or rewind) trusted time. Persists across sessions for QA.</summary>
        public void AddDebugOffset(TimeSpan delta)
        {
            debugOffsetSeconds += delta.TotalSeconds;
            PlayerPrefs.SetFloat(DebugOffsetPrefsKey, (float)debugOffsetSeconds);
            PlayerPrefs.Save();
            Debug.Log($"[TrustedTimeService] Debug offset now {FormatOffset(debugOffsetSeconds)} → UtcNow={GetUtcNow():u}");
        }

        public void SetDebugOffset(TimeSpan offset)
        {
            debugOffsetSeconds = offset.TotalSeconds;
            PlayerPrefs.SetFloat(DebugOffsetPrefsKey, (float)debugOffsetSeconds);
            PlayerPrefs.Save();
            Debug.Log($"[TrustedTimeService] Debug offset set to {FormatOffset(debugOffsetSeconds)} → UtcNow={GetUtcNow():u}");
        }

        public void ClearDebugOffset()
        {
            debugOffsetSeconds = 0;
            PlayerPrefs.DeleteKey(DebugOffsetPrefsKey);
            PlayerPrefs.Save();
            Debug.Log($"[TrustedTimeService] Debug offset cleared → UtcNow={GetUtcNow():u}");
        }

        public void RequestSync()
        {
            if (!syncInFlight)
                StartCoroutine(SyncOnce());
        }

        private IEnumerator SyncLoop()
        {
            yield return SyncOnce();
            while (true)
            {
                float wait = Mathf.Max(5f, nextSyncAt - Time.realtimeSinceStartup);
                yield return new WaitForSecondsRealtime(wait);
                yield return SyncOnce();
            }
        }

        private IEnumerator SyncOnce()
        {
            if (syncInFlight)
                yield break;

            syncInFlight = true;
            using (UnityWebRequest request = UnityWebRequest.Head(SyncUrl))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string dateHeader = request.GetResponseHeader("Date");
                    if (TryParseHttpDate(dateHeader, out DateTime serverUtc))
                    {
                        syncedServerUtc = DateTime.SpecifyKind(serverUtc, DateTimeKind.Utc);
                        syncedRealtime = Time.realtimeSinceStartup;
                        hasSync = true;
                        nextSyncAt = Time.realtimeSinceStartup + ResyncIntervalSeconds;
                        Debug.Log($"[TrustedTimeService] Synced UTC: {syncedServerUtc:o}");
                    }
                    else
                    {
                        ScheduleRetry();
                        Debug.LogWarning($"[TrustedTimeService] Could not parse Date header: '{dateHeader}'");
                    }
                }
                else
                {
                    ScheduleRetry();
                    Debug.LogWarning($"[TrustedTimeService] Sync failed: {request.error}");
                }
            }

            syncInFlight = false;
        }

        private void ScheduleRetry()
        {
            nextSyncAt = Time.realtimeSinceStartup + 30f;
        }

        private static bool TryParseHttpDate(string value, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(value))
                return false;

            return DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out utc);
        }

        private static string FormatOffset(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            string sign = seconds < 0 ? "-" : "+";
            ts = ts.Duration();
            return $"{sign}{(int)ts.TotalHours}h {ts.Minutes}m";
        }
    }
}
