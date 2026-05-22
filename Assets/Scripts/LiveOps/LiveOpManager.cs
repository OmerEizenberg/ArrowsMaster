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
            Init();
            // Initial check
            CheckLiveOps();
        }

        private void Update()
        {
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
                        // Different week iteration detected
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
            // ID = EventID + Year + WeekNumber (using ISO 8601 week)
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
                if (service.IconInstance != null)
                {
                    Destroy(service.IconInstance);
                }
                
                activeServices.Remove(eventID);
            }
        }

        public void InstantiateIcon(ALiveOpService service)
        {
            // This would normally be triggered when entering the lobby
            // or if activation happens while in lobby
            HomeContoller lobby = FindFirstObjectByType<HomeContoller>();
            if (lobby != null && lobby.gameObject.activeInHierarchy)
            {
                Transform container = lobby.LiveOpIconsContainer;
                if (container != null)
                {
                    GameObject prefab = Resources.Load<GameObject>(service.SO.IconPrefabName);
                    if (prefab != null)
                    {
                        GameObject icon = Instantiate(prefab, container);
                        service.IconInstance = icon;
                        
                        LiveOpIconView view = icon.GetComponent<LiveOpIconView>();
                        if (view != null)
                        {
                            view.Initialize(service);
                        }
                    }
                }
            }
        }

        public ALiveOpService GetActiveService(string eventID)
        {
            activeServices.TryGetValue(eventID, out var service);
            return service;
        }
    }
}
