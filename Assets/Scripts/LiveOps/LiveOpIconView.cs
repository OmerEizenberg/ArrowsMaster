using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.LiveOps
{
    public class LiveOpIconView : MonoBehaviour
    {
        [SerializeField] private List<Image> m_ImagesToFade;
        [SerializeField] private List<TextMeshProUGUI> m_TimerTexts;
        [SerializeField] private Button m_IconButton;
        
        [Header("Lock Settings")]
        [SerializeField] private GameObject m_LockIcon;
        [SerializeField] private GameObject m_UnlockTooltip;
        [SerializeField] private TextMeshProUGUI m_UnlockTooltipText;

        [Header("Claim Notification")]
        [SerializeField] private GameObject m_ClaimNotification;

        private ALiveOpService service;
        private DailyMissionsLiveOpService dailyMissionsService;
        private bool isLocked = false;
        private bool m_LevelSubscribed;
        private Coroutine tooltipCoroutine;
        private float m_NextUiRefreshTime;
        private const float UiRefreshInterval = 1f;

        public void Initialize(ALiveOpService service)
        {
            UnsubscribeDailyMissions();

            this.service = service;
            dailyMissionsService = service as DailyMissionsLiveOpService;

            EnsureClaimNotification();

            if (m_UnlockTooltip != null) m_UnlockTooltip.SetActive(false);

            if (m_IconButton != null)
            {
                m_IconButton.onClick.RemoveAllListeners();
                m_IconButton.onClick.AddListener(OnIconClicked);
            }

            if (dailyMissionsService != null)
            {
                dailyMissionsService.OnStateChanged += RefreshClaimNotification;
                EnsureClaimNotification();
            }

            SubscribeLevelChanged();
            RefreshIconState();
            RefreshUI();
            RefreshClaimNotification();
        }

        private void OnDestroy()
        {
            UnsubscribeLevelChanged();
            UnsubscribeDailyMissions();
        }

        private void SubscribeLevelChanged()
        {
            if (m_LevelSubscribed || UserDataManager.Instance == null) return;
            UserDataManager.Instance.OnLevelChanged += RefreshIconState;
            m_LevelSubscribed = true;
        }

        private void UnsubscribeLevelChanged()
        {
            if (!m_LevelSubscribed || UserDataManager.Instance == null) return;
            UserDataManager.Instance.OnLevelChanged -= RefreshIconState;
            m_LevelSubscribed = false;
        }

        private void UnsubscribeDailyMissions()
        {
            if (dailyMissionsService != null)
            {
                dailyMissionsService.OnStateChanged -= RefreshClaimNotification;
                dailyMissionsService = null;
            }
        }

        private void Update()
        {
            if (service == null || Time.time < m_NextUiRefreshTime) return;

            m_NextUiRefreshTime = Time.time + UiRefreshInterval;
            RefreshIconState();
            RefreshClaimNotification();

            if (m_TimerTexts != null && m_TimerTexts.Count > 0)
            {
                UpdateTimers();
            }
        }

        private void RefreshIconState()
        {
            if (service == null || service.SO == null || UserDataManager.Instance == null) return;

            int currentLevel = UserDataManager.Instance.CurrentLevel;
            bool shouldShow = currentLevel >= service.SO.ShowLevel;
            if (gameObject.activeSelf != shouldShow)
                gameObject.SetActive(shouldShow);

            if (!shouldShow) return;

            bool newlyUnlocked = isLocked && currentLevel >= service.SO.UnlockLevel;
            isLocked = currentLevel < service.SO.UnlockLevel;

            if (m_LockIcon != null) m_LockIcon.SetActive(isLocked);

            float targetAlpha = isLocked ? 0.5f : 1.0f;
            ApplyAlpha(targetAlpha);

            if (newlyUnlocked)
                Debug.Log($"[LiveOpIconView] {service.SO.EventID} Unlocked!");
        }

        private void ApplyAlpha(float alpha)
        {
            if (m_ImagesToFade != null)
            {
                foreach (var img in m_ImagesToFade)
                {
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = alpha;
                        img.color = c;
                    }
                }
            }

            if (m_TimerTexts != null)
            {
                foreach (var text in m_TimerTexts)
                {
                    if (text != null)
                    {
                        Color c = text.color;
                        c.a = alpha;
                        text.color = c;
                    }
                }
            }
        }

        private void RefreshUI()
        {
            RefreshClaimNotification();
        }

        private void EnsureClaimNotification()
        {
            if (m_ClaimNotification == null)
                m_ClaimNotification = transform.Find("ClaimNotification")?.gameObject;

            if (m_ClaimNotification != null || dailyMissionsService == null) return;

            var badge = new GameObject("ClaimNotification", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(transform, false);
            var rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -8f);
            rect.sizeDelta = new Vector2(36f, 36f);

            var image = badge.GetComponent<Image>();
            image.color = new Color(1f, 0.2f, 0.2f, 1f);
            image.raycastTarget = false;
            badge.SetActive(false);
            m_ClaimNotification = badge;
        }

        private void RefreshClaimNotification()
        {
            if (dailyMissionsService == null) return;
            EnsureClaimNotification();
            if (m_ClaimNotification == null) return;
            bool show = !isLocked && dailyMissionsService.HasClaimableReward();
            m_ClaimNotification.SetActive(show);
        }

        private void UpdateTimers()
        {
            DateTime now = DateTime.Now;
            DateTime start = new DateTime(now.Year, now.Month, now.Day, service.SO.ActivationHour, 0, 0);
            DateTime end = start.AddHours(service.SO.DurationHours);
            
            TimeSpan remaining = end - now;
            string timeStr = "0h 0m";
            
            if (remaining.TotalSeconds > 0)
            {
                int hours = (int)remaining.TotalHours;
                int minutes = remaining.Minutes;
                timeStr = $"{hours}h {minutes}m";
            }

            foreach (var text in m_TimerTexts)
            {
                if (text != null) text.text = timeStr;
            }
        }

        private void OnIconClicked()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            if (service == null) return;
            
            if (isLocked)
            {
                ShowTooltip();
                return;
            }

            GameObject popupPrefab = Resources.Load<GameObject>(service.SO.PopupPrefabName);
            if (popupPrefab != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    GameObject popup = Instantiate(popupPrefab, null);
                    popup.SetActive(true);
                    popup.transform.SetAsLastSibling();

                    var dmService = dailyMissionsService;
                    if (dmService == null && LiveOpManager.Instance != null)
                        dmService = LiveOpManager.Instance.GetActiveService(DailyMissionsLiveOpService.EventId) as DailyMissionsLiveOpService;

                    if (dmService != null)
                    {
                        var popupView = popup.GetComponentInChildren<Missions.MissionsPopupView>(true);
                        if (popupView == null)
                            popupView = popup.AddComponent<Missions.MissionsPopupView>();
                        popupView.Initialize(dmService);
                    }
                    else
                    {
                        Debug.LogWarning("[LiveOpIconView] Daily Missions service is not active — popup UI will not update.");
                    }
                }
            }
        }

        private void ShowTooltip()
        {
            if (m_UnlockTooltip == null) return;
            
            if (m_UnlockTooltipText != null)
            {
                m_UnlockTooltipText.text = $"Unlocked at Level {service.SO.UnlockLevel}";
            }
            
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(TooltipCoroutine());
        }

        private IEnumerator TooltipCoroutine()
        {
            m_UnlockTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_UnlockTooltip.SetActive(false);
            tooltipCoroutine = null;
        }
    }
}
