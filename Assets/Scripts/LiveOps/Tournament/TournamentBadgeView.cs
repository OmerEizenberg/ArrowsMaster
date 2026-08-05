using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Binds to RaceIcon-style lobby badge hierarchy (Trophy / timer pill / place / lock).
    /// </summary>
    public class TournamentBadgeView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_PlaceText;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private Button m_Button;
        [SerializeField] private GameObject m_LockIcon;
        [SerializeField] private Image m_LogoImage;
        [SerializeField] private GameObject m_UnlockTooltip;
        [SerializeField] private TextMeshProUGUI m_UnlockTooltipText;
        [SerializeField] private List<TextMeshProUGUI> m_TimerTexts = new List<TextMeshProUGUI>();
        [SerializeField] private List<TextMeshProUGUI> m_PlaceTexts = new List<TextMeshProUGUI>();
        [SerializeField] private List<Image> m_ImagesToFade = new List<Image>();

        private TournamentLiveOpService service;
        private bool isLocked;
        private float nextRefresh;
        private Coroutine tooltipCoroutine;
        private bool resolved;
        private string m_LastPlaceText;
        private string m_LastTimerText;
        private int m_LastPlace = int.MinValue;
        private float m_LastAlpha = -1f;
        private int m_LastOnlineVisible = -1;
        private CanvasGroup m_CanvasGroup;

        public void Initialize(TournamentLiveOpService tournamentService)
        {
            service = tournamentService;
            ResolveRefs();
            // Reset UI caches so re-bind after lobby sync can't leave the badge stuck invisible.
            m_LastPlace = int.MinValue;
            m_LastPlaceText = null;
            m_LastTimerText = null;
            m_LastAlpha = -1f;
            m_LastOnlineVisible = -1;

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(OnClicked);
            }

            if (service != null)
            {
                service.OnStateChanged -= Refresh;
                service.OnStateChanged += Refresh;
            }

            if (RemoteConfigManager.Instance != null)
            {
                RemoteConfigManager.Instance.OnConfigValuesUpdated -= Refresh;
                RemoteConfigManager.Instance.OnConfigValuesUpdated += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnStateChanged -= Refresh;
            if (RemoteConfigManager.Instance != null)
                RemoteConfigManager.Instance.OnConfigValuesUpdated -= Refresh;
        }

        private void Update()
        {
            if (service == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 1f;
            Refresh();
        }

        private void Refresh()
        {
            if (service == null || service.SO == null || UserDataManager.Instance == null)
                return;

            // Keep this component active while the tournament window is eligible so Update
            // keeps running offline and the badge can reappear when back online.
            bool eligible = service.IsBadgeEligible();
            if (!eligible)
            {
                ApplyOnlineVisibility(false);
                if (gameObject.activeSelf)
                    gameObject.SetActive(false);
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            bool show = service.ShouldShowBadge();
            ApplyOnlineVisibility(show);
            if (!show) return;

            bool locked = !service.IsUnlocked();
            if (locked != isLocked)
            {
                isLocked = locked;
                if (m_LockIcon != null)
                    m_LockIcon.SetActive(isLocked);
            }

            int place = service.GetDisplayPlace();
            if (place != m_LastPlace)
            {
                m_LastPlace = place;
                string placeText = "#" + place.ToString();
                m_LastPlaceText = placeText;
                SetPlaceTexts(placeText);
            }

            string timeStr = FormatRemaining(service.GetRemainingTime());
            if (!string.Equals(m_LastTimerText, timeStr, StringComparison.Ordinal))
            {
                m_LastTimerText = timeStr;
                SetTimerTexts(timeStr);
            }

            float alpha = isLocked ? 0.5f : 1f;
            if (!Mathf.Approximately(alpha, m_LastAlpha))
            {
                m_LastAlpha = alpha;
                ApplyAlpha(alpha);
            }
        }

        private void ApplyOnlineVisibility(bool visible)
        {
            int flag = visible ? 1 : 0;
            if (m_CanvasGroup == null)
            {
                m_CanvasGroup = GetComponent<CanvasGroup>();
                if (m_CanvasGroup == null)
                    m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Always force alpha when becoming visible so a stale CanvasGroup(0) can't stick.
            if (flag == m_LastOnlineVisible && !(visible && m_CanvasGroup.alpha < 0.99f))
                return;
            m_LastOnlineVisible = flag;

            m_CanvasGroup.alpha = visible ? 1f : 0f;
            m_CanvasGroup.interactable = visible;
            m_CanvasGroup.blocksRaycasts = visible;
        }

        private void OnClicked()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            if (service == null) return;

            if (isLocked)
            {
                ShowTooltip();
                return;
            }

            if (service.Status == TournamentStatus.PendingJoin)
                TournamentJoinPopupView.Show(service);
            else if (service.Status == TournamentStatus.Joined)
                TournamentLeaderboardPopupView.Show(service);
        }

        private void ShowTooltip()
        {
            if (m_UnlockTooltip == null) return;
            if (m_UnlockTooltipText != null)
                m_UnlockTooltipText.text = $"Unlocked at Level {service.SO.UnlockLevel}";

            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(TooltipRoutine());
        }

        private IEnumerator TooltipRoutine()
        {
            m_UnlockTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_UnlockTooltip.SetActive(false);
            tooltipCoroutine = null;
        }

        private void ResolveRefs()
        {
            if (resolved) return;
            resolved = true;

            if (m_Button == null)
                m_Button = GetComponent<Button>();
            if (m_LogoImage == null)
                m_LogoImage = GetComponent<Image>();
            if (m_LockIcon == null)
            {
                var t = FindDeep("Lock");
                if (t != null) m_LockIcon = t.gameObject;
            }
            if (m_UnlockTooltip == null)
            {
                var t = FindDeep("LockedToolTip");
                if (t != null) m_UnlockTooltip = t.gameObject;
            }
            if (m_UnlockTooltipText == null && m_UnlockTooltip != null)
                m_UnlockTooltipText = m_UnlockTooltip.GetComponentInChildren<TextMeshProUGUI>(true);

            if (m_PlaceTexts == null || m_PlaceTexts.Count == 0)
            {
                m_PlaceTexts = new List<TextMeshProUGUI>();
                CollectNamedTmp("Place", m_PlaceTexts);
                CollectNamedTmp("PlaceS", m_PlaceTexts);
            }
            if (m_PlaceText == null && m_PlaceTexts.Count > 0)
                m_PlaceText = m_PlaceTexts[0];

            if (m_TimerTexts == null || m_TimerTexts.Count == 0)
            {
                m_TimerTexts = new List<TextMeshProUGUI>();
                CollectNamedTmp("Timer", m_TimerTexts);
            }
            if (m_TimerText == null && m_TimerTexts.Count > 0)
                m_TimerText = m_TimerTexts[0];

            if (m_ImagesToFade == null || m_ImagesToFade.Count == 0)
            {
                m_ImagesToFade = new List<Image>();
                foreach (var img in GetComponentsInChildren<Image>(true))
                {
                    if (img != null && img.gameObject != m_LockIcon)
                        m_ImagesToFade.Add(img);
                }
            }

            if (m_UnlockTooltip != null)
                m_UnlockTooltip.SetActive(false);
        }

        private void CollectNamedTmp(string objectName, List<TextMeshProUGUI> list)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName) continue;
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null && !list.Contains(tmp))
                    list.Add(tmp);
            }
        }

        private Transform FindDeep(string objectName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    return t;
            }
            return null;
        }

        private void SetPlaceTexts(string value)
        {
            if (m_PlaceTexts != null)
            {
                foreach (var t in m_PlaceTexts)
                    if (t != null) t.text = value;
            }
            else if (m_PlaceText != null)
            {
                m_PlaceText.text = value;
            }
        }

        private void SetTimerTexts(string value)
        {
            if (m_TimerTexts != null && m_TimerTexts.Count > 0)
            {
                foreach (var t in m_TimerTexts)
                    if (t != null) t.text = value;
            }
            else if (m_TimerText != null)
            {
                m_TimerText.text = value;
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (m_ImagesToFade == null) return;
            foreach (var img in m_ImagesToFade)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }

        private static string FormatRemaining(TimeSpan remaining)
        {
            return TournamentUiFormat.FormatTimeLeft(remaining);
        }
    }
}
