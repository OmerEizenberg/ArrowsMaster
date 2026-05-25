using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Missions
{
    public class MissionsPopupView : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private Transform m_MissionsHolder;
        [SerializeField] private MissionsHolderView m_MissionsHolderView;
        [SerializeField] private MissionSlotView[] m_MissionSlots;
        [SerializeField] private Button m_CloseButton;

        [Header("Fallback")]
        [Tooltip("Only used when no MissionSlotView is assigned. Off = prefab-only setup.")]
        [SerializeField] private bool m_AllowRuntimeSlotComponents;

        private readonly List<MissionSlotView> m_ActiveSlots = new List<MissionSlotView>();
        private DailyMissionsLiveOpService m_Service;
        private bool m_IsInitialized;

        private void Awake()
        {
            ResolveCloseButton();
            WireCloseButton();
            ResolveMissionSlots();
        }

        private void OnEnable()
        {
            SubscribeAdsEvents();

            if (m_IsInitialized && m_Service != null)
            {
                RefreshAll();
                return;
            }

            var service = ResolveDailyMissionsService();
            if (service != null)
                Initialize(service);
        }

        public void Initialize(DailyMissionsLiveOpService service)
        {
            if (service == null)
            {
                Debug.LogWarning("[MissionsPopupView] Initialize called with null service.");
                return;
            }

            m_Service = service;
            m_IsInitialized = true;

            ResolveMissionSlots();
            BindSlots();

            m_Service.OnStateChanged -= RefreshAll;
            m_Service.OnStateChanged += RefreshAll;

            RefreshAll();
        }

        private void OnDisable()
        {
            UnsubscribeAdsEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeAdsEvents();

            if (m_Service != null)
                m_Service.OnStateChanged -= RefreshAll;
            m_IsInitialized = false;
        }

        private void SubscribeAdsEvents()
        {
            if (AdsManager.Instance == null) return;
            AdsManager.Instance.OnAdReadinessChanged -= OnAdsReadinessChanged;
            AdsManager.Instance.OnAdReadinessChanged += OnAdsReadinessChanged;
            AdsManager.Instance.OnAdClosed -= OnAdsReadinessChanged;
            AdsManager.Instance.OnAdClosed += OnAdsReadinessChanged;
        }

        private void UnsubscribeAdsEvents()
        {
            if (AdsManager.Instance == null) return;
            AdsManager.Instance.OnAdReadinessChanged -= OnAdsReadinessChanged;
            AdsManager.Instance.OnAdClosed -= OnAdsReadinessChanged;
        }

        private void OnAdsReadinessChanged()
        {
            if (!m_IsInitialized) return;
            RefreshAll();
        }

        private static DailyMissionsLiveOpService ResolveDailyMissionsService()
        {
            if (LiveOpManager.Instance == null) return null;
            return LiveOpManager.Instance.GetActiveService(DailyMissionsLiveOpService.EventId) as DailyMissionsLiveOpService;
        }

        private void ResolveCloseButton()
        {
            if (m_CloseButton != null) return;

            var closeTransform = transform.Find("MissionsPopup/Popup/GreenShadow");
            if (closeTransform == null) closeTransform = transform.Find("Popup/GreenShadow");
            if (closeTransform != null)
                m_CloseButton = closeTransform.GetComponent<Button>();
        }

        private void WireCloseButton()
        {
            if (m_CloseButton == null) return;
            m_CloseButton.onClick.RemoveAllListeners();
            m_CloseButton.onClick.AddListener(ClosePopup);
        }

        private void ResolveMissionSlots()
        {
            if (m_MissionsHolder == null)
            {
                if (m_MissionsHolderView != null)
                    m_MissionsHolder = m_MissionsHolderView.transform;
                else
                {
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "MissionsHolder")
                        {
                            m_MissionsHolder = t;
                            break;
                        }
                    }
                }
            }

            if (m_MissionsHolderView == null && m_MissionsHolder != null)
                m_MissionsHolderView = m_MissionsHolder.GetComponent<MissionsHolderView>();
        }

        private void BindSlots()
        {
            m_ActiveSlots.Clear();
            if (m_Service == null) return;

            if (m_Service.Config == null)
            {
                Debug.LogWarning("[MissionsPopupView] DailyMissions config is null. Is the LiveOp active?");
                return;
            }

            var slots = CollectSlotViews();
            int missionCount = m_Service.Config.Missions.Count;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                int index = slot.MissionIndex;
                if (index < 0)
                    index = i;

                if (index >= missionCount)
                {
                    Debug.LogWarning($"[MissionsPopupView] Slot '{slot.name}' index {index} is out of config range.");
                    continue;
                }

                slot.Initialize(m_Service, index, OnClaimRequested);
                m_ActiveSlots.Add(slot);
            }

            if (m_ActiveSlots.Count == 0)
            {
                Debug.LogWarning("[MissionsPopupView] No mission slots bound. Assign MissionSlotView on each Mission (N) row under MissionsHolder.");
            }
        }

        private List<MissionSlotView> CollectSlotViews()
        {
            var result = new List<MissionSlotView>();

            if (m_MissionSlots != null && m_MissionSlots.Length > 0)
            {
                foreach (var slot in m_MissionSlots)
                    if (slot != null) result.Add(slot);
            }

            if (result.Count == 0 && m_MissionsHolderView != null && m_MissionsHolderView.MissionSlots != null)
            {
                foreach (var slot in m_MissionsHolderView.MissionSlots)
                    if (slot != null) result.Add(slot);
            }

            if (result.Count == 0 && m_MissionsHolder != null)
            {
                // Only direct children of MissionsHolder (Mission (1)..(5)), not nested UI.
                for (int i = 0; i < m_MissionsHolder.childCount; i++)
                {
                    var slot = m_MissionsHolder.GetChild(i).GetComponent<MissionSlotView>();
                    if (slot != null)
                        result.Add(slot);
                }

                if (result.Count == 0 && m_AllowRuntimeSlotComponents)
                {
                    int childCount = m_MissionsHolder.childCount;
                    int missionCount = m_Service.Config.Missions.Count;
                    int slotCount = Mathf.Min(childCount, missionCount);

                    for (int i = 0; i < slotCount; i++)
                    {
                        var slotRoot = m_MissionsHolder.GetChild(i);
                        var slot = slotRoot.GetComponent<MissionSlotView>();
                        if (slot == null)
                            slot = slotRoot.gameObject.AddComponent<MissionSlotView>();
                        result.Add(slot);
                    }
                }
            }

            result.Sort((a, b) => a.MissionIndex.CompareTo(b.MissionIndex));
            return result;
        }

        private void RefreshAll()
        {
            foreach (var slot in m_ActiveSlots)
                slot.Refresh();
        }

        private void OnClaimRequested(int index)
        {
            if (m_Service == null) return;
            if (!m_Service.TryClaimReward(index, out int coins)) return;

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayMediumCheer();

            if (UserDataManager.Instance != null)
                UserDataManager.Instance.AddArrowsCurrency(coins);

            if (AdsManager.Instance != null)
                AdsManager.Instance.SpawnCoinsSmallExplosion();

            RefreshAll();
        }

        private void ClosePopup()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            Destroy(gameObject);
        }
    }
}
