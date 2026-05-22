using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Core;
using Assets.Scripts.Lobby;
using System.Globalization;

namespace Assets.Scripts.LiveOps
{
    public class LiveOpManager : MonoBehaviour
    {
        private static LiveOpManager instance;
        public static LiveOpManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<LiveOpManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("LiveOpManager");
                        instance = go.AddComponent<LiveOpManager>();
                        instance.Init();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private bool isInitialized = false;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[LiveOpManager] Duplicate LiveOpManager on '{gameObject.name}' removed.");
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void Init()
        {
            if (isInitialized) return;
            isInitialized = true;

            // Automatically load all LiveOpSO assets from Resources/LiveOps
            LiveOpSO[] loadedSOs = Resources.LoadAll<LiveOpSO>("LiveOps");
            if (loadedSOs != null && loadedSOs.Length > 0)
            {
                AllLiveOps.Clear();
                AllLiveOps.AddRange(loadedSOs);
                Debug.Log($"[LiveOpManager] Loaded {AllLiveOps.Count} LiveOp definitions from Resources/LiveOps");
            }
            else
            {
                Debug.LogWarning("[LiveOpManager] No LiveOpSO assets found in Resources/LiveOps");
            }
        }

        [SerializeField] private List<LiveOpSO> AllLiveOps = new List<LiveOpSO>();
        private Dictionary<string, ALiveOpService> activeServices = new Dictionary<string, ALiveOpService>();

        private float checkInterval = 60f; // Check every minute
        private float timer = 0f;

        private void Start()
        {
            if (instance != this) return;

            Init();
            CheckLiveOps();
        }

        private void Update()
        {
            if (instance != this) return;

            timer += Time.deltaTime;
            if (timer >= checkInterval)
            {
                timer = 0f;
                CheckLiveOps();
            }
        }

        public void CheckLiveOps()
        {
            DateTime now = DateTime.Now;
            foreach (var so in AllLiveOps)
            {
                bool shouldBeActive = IsCurrentlyActive(so, now);
                string uniqueID = GetUniqueEventID(so, now);

                if (shouldBeActive)
                {
                    if (!activeServices.ContainsKey(so.EventID))
                    {
                        ActivateLiveOp(so, uniqueID);
                    }
                    else if (activeServices[so.EventID].UniqueID != uniqueID)
                    {
                        // New period detected (e.g. new day for Daily Missions, new week for others)
                        DeactivateLiveOp(so.EventID);
                        ActivateLiveOp(so, uniqueID);
                    }
                }
                else
                {
                    if (activeServices.ContainsKey(so.EventID))
                    {
                        DeactivateLiveOp(so.EventID);
                    }
                }
            }
        }

        private bool IsCurrentlyActive(LiveOpSO so, DateTime now)
        {
            if (so.ActiveDays == null || !so.ActiveDays.Contains(now.DayOfWeek))
                return false;

            // Check if current hour is within [ActivationHour, ActivationHour + DurationHours)
            int startHour = so.ActivationHour;
            int endHour = startHour + so.DurationHours;
            
            // Note: This logic assumes DurationHours doesn't cross midnight into a day not in ActiveDays.
            // If it can cross midnight, we'd need more complex logic. 
            // For now, let's keep it simple: it must be on one of the active days and within the hour range.
            
            return now.Hour >= startHour && now.Hour < endHour;
        }

        private string GetUniqueEventID(LiveOpSO so, DateTime now)
        {
            // Daily Missions reset each calendar day.
            if (so.EventID == DailyMissionsLiveOpService.EventId)
                return $"{so.EventID}_{now:yyyy-MM-dd}";

            // Other live ops use weekly buckets (ISO 8601 week).
            int week = GetIso8601WeekOfYear(now);
            return $"{so.EventID}_{now.Year}_W{week}";
        }

        private int GetIso8601WeekOfYear(DateTime time)
        {
            DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                time = time.AddDays(3);
            }
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private void ActivateLiveOp(LiveOpSO so, string uniqueID)
        {
            Debug.Log($"[LiveOpManager] Activating LiveOp: {uniqueID}");
            
            // Clear old data if exists for this event but with a different UniqueID
            UserDataManager.Instance.CleanupLiveOpData(so.EventID, uniqueID);

            Type serviceType = Type.GetType(so.ServiceClassName);
            if (serviceType == null)
            {
                // Try with namespace if not provided
                serviceType = Type.GetType($"Assets.Scripts.LiveOps.{so.ServiceClassName}");
            }

            if (serviceType != null)
            {
                ALiveOpService service = (ALiveOpService)Activator.CreateInstance(serviceType);
                service.Initialize(so, uniqueID);
                service.OnActivate();
                activeServices.Add(so.EventID, service);
                
                // UI instantiation (if lobby is active)
                InstantiateIcon(service);
            }
            else
            {
                Debug.LogError($"[LiveOpManager] Could not find service class: {so.ServiceClassName}");
            }
        }

        private void DeactivateLiveOp(string eventID)
        {
            if (activeServices.TryGetValue(eventID, out var service))
            {
                Debug.Log($"[LiveOpManager] Deactivating LiveOp: {service.UniqueID}");
                service.OnDeactivate();
                
                // Cleanup UI
                DestroyIconInstance(service);
                activeServices.Remove(eventID);
            }
        }

        public void InstantiateIcon(ALiveOpService service)
        {
            if (service == null || service.SO == null) return;
            if (service.IconInstance != null) return;

            HomeContoller lobby = FindFirstObjectByType<HomeContoller>();
            if (lobby == null || !lobby.gameObject.activeInHierarchy) return;

            Transform container = lobby.LiveOpIconsContainer;
            if (container == null) return;

            if (TryBindExistingIcon(service, container)) return;

            GameObject prefab = Resources.Load<GameObject>(service.SO.IconPrefabName);
            if (prefab == null) return;

            GameObject icon = Instantiate(prefab, container);
            icon.name = service.SO.IconPrefabName;
            service.IconInstance = icon;

            LiveOpIconView view = icon.GetComponent<LiveOpIconView>();
            if (view != null)
                view.Initialize(service);
        }

        /// <summary>
        /// Binds lobby icons when returning to home (does not create duplicates).
        /// </summary>
        public void SyncLobbyIcons()
        {
            if (instance != this) return;

            foreach (var service in activeServices.Values)
                InstantiateIcon(service);
        }

        private static bool TryBindExistingIcon(ALiveOpService service, Transform container)
        {
            string prefabName = service.SO.IconPrefabName;
            if (string.IsNullOrEmpty(prefabName)) return false;

            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (!IsMatchingIconObject(child.gameObject, prefabName)) continue;
                if (IsIconBoundToAnotherService(child.gameObject, service)) continue;

                service.IconInstance = child.gameObject;
                LiveOpIconView view = child.GetComponent<LiveOpIconView>();
                if (view != null)
                    view.Initialize(service);
                return true;
            }

            return false;
        }

        private static bool IsMatchingIconObject(GameObject obj, string prefabName)
        {
            if (obj == null) return false;
            string n = obj.name;
            return n == prefabName || n.StartsWith(prefabName + "(", StringComparison.Ordinal);
        }

        private static bool IsIconBoundToAnotherService(GameObject icon, ALiveOpService current)
        {
            if (instance == null) return false;
            foreach (var kvp in instance.activeServices)
            {
                if (kvp.Value == current) continue;
                if (kvp.Value?.IconInstance == icon) return true;
            }
            return false;
        }

        private static void DestroyIconInstance(ALiveOpService service)
        {
            if (service?.IconInstance == null) return;

            // Scene-placed icons (not runtime clones) are hidden, not destroyed.
            if (service.IconInstance.name.Contains("(Clone)", StringComparison.Ordinal))
                Destroy(service.IconInstance);
            else
                service.IconInstance.SetActive(false);

            service.IconInstance = null;
        }

        public ALiveOpService GetActiveService(string eventID)
        {
            activeServices.TryGetValue(eventID, out var service);
            return service;
        }
    }
}
