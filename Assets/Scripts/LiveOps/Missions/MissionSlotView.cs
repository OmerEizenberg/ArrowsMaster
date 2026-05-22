using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Missions
{
    public class MissionSlotView : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("0 = first row / first entry in DailyMissionsConfig.")]
        [SerializeField] private int m_MissionIndex;

        [Header("Task Description")]
        [FormerlySerializedAs("m_TaskText")]
        [Tooltip("Primary task title (e.g. TODOTask).")]
        [SerializeField] private TextMeshProUGUI m_TaskText1;
        [Tooltip("Secondary task title duplicate (e.g. Title (2)).")]
        [SerializeField] private TextMeshProUGUI m_TaskText2;

        [Header("Progress UI")]
        [SerializeField] private Slider m_ProgressSlider;
        [Tooltip("First progress label (e.g. TODOTaskS on the bar).")]
        [SerializeField] private TextMeshProUGUI m_ProgressText1;
        [Tooltip("Second progress label (e.g. duplicate TODOTaskS).")]
        [SerializeField] private TextMeshProUGUI m_ProgressText2;
        [SerializeField] private string m_ProgressTextFormat = "{0}/{1}";

        [Header("Reward")]
        [SerializeField] private TextMeshProUGUI m_RewardText;
        [SerializeField] private string m_RewardTextFormat = "+{0}";

        [Header("Row State")]
        [SerializeField] private Image m_RowBackground;
        [SerializeField] private Button m_ClaimButton;
        [SerializeField] private TextMeshProUGUI m_ClaimButtonText;
        [SerializeField] private GameObject m_CompletedOverlay;
        [SerializeField] private TextMeshProUGUI m_CompletedText;

        [Header("Watch Ads (optional — wire on Watch Ads row only)")]
        [SerializeField] private Button m_WatchAdButton;
        [SerializeField] private TextMeshProUGUI m_WatchAdButtonText;

        [Header("Colors")]
        [SerializeField] private Color m_ActiveRowColor = new Color(0.49f, 0.37f, 1f, 1f);
        [SerializeField] private Color m_CompletedRowColor = new Color(0.35f, 0.35f, 0.4f, 0.85f);

        private DailyMissionsLiveOpService m_Service;
        private Action<int> m_OnClaimRequested;
        private bool m_IsBound;

        public int MissionIndex => m_MissionIndex;

        public void Initialize(DailyMissionsLiveOpService service, Action<int> onClaimRequested)
        {
            Initialize(service, -1, onClaimRequested);
        }

        /// <param name="missionIndexOverride">Use when index is not set on prefab (-1 keeps serialized index).</param>
        public void Initialize(DailyMissionsLiveOpService service, int missionIndexOverride, Action<int> onClaimRequested)
        {
            if (missionIndexOverride >= 0)
                m_MissionIndex = missionIndexOverride;

            m_Service = service;
            m_OnClaimRequested = onClaimRequested;
            m_IsBound = true;

            if (m_ClaimButton != null)
            {
                m_ClaimButton.onClick.RemoveAllListeners();
                m_ClaimButton.onClick.AddListener(OnClaimClicked);
            }

            if (m_WatchAdButton != null)
            {
                m_WatchAdButton.onClick.RemoveAllListeners();
                m_WatchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            AutoWireTexts();
            Refresh();
        }

        public void Refresh()
        {
            if (!m_IsBound)
                return;

            if (m_Service == null || m_Service.Config == null)
            {
                Debug.LogWarning($"[MissionSlotView] Cannot refresh slot '{name}' — service or config is null.");
                return;
            }

            if (m_MissionIndex < 0 || m_MissionIndex >= m_Service.Config.Missions.Count)
            {
                Debug.LogWarning($"[MissionSlotView] Slot '{name}' has invalid Mission Index {m_MissionIndex}.");
                return;
            }

            var definition = m_Service.Config.Missions[m_MissionIndex];
            bool claimed = m_Service.IsClaimed(m_MissionIndex);
            bool readyToClaim = m_Service.IsReadyToClaim(m_MissionIndex);
            int progress = m_Service.GetDisplayProgress(m_MissionIndex);
            int target = m_Service.GetTargetCount(m_MissionIndex);
            int coinReward = m_Service.GetCoinReward(m_MissionIndex);

            SetTaskTexts(MissionDescriptions.GetDescription(definition));
            SetProgressTexts(progress, target);

            if (m_RewardText != null)
            {
                string format = string.IsNullOrEmpty(definition.RewardTextFormat)
                    ? m_RewardTextFormat
                    : definition.RewardTextFormat;
                m_RewardText.text = string.Format(format, coinReward);
                m_RewardText.gameObject.SetActive(!claimed);
            }

            if (m_ProgressSlider != null)
            {
                m_ProgressSlider.minValue = 0f;
                m_ProgressSlider.maxValue = target;
                m_ProgressSlider.value = progress;
                m_ProgressSlider.gameObject.SetActive(!claimed);
            }

            if (m_RowBackground != null)
                m_RowBackground.color = claimed ? m_CompletedRowColor : m_ActiveRowColor;

            if (m_ClaimButton != null)
            {
                m_ClaimButton.gameObject.SetActive(readyToClaim);
                m_ClaimButton.interactable = readyToClaim;
            }

            if (m_ClaimButtonText != null && readyToClaim)
                m_ClaimButtonText.text = "Claim";

            if (m_CompletedOverlay != null)
                m_CompletedOverlay.SetActive(claimed);

            if (m_CompletedText != null && claimed)
                m_CompletedText.text = "Completed";

            RefreshWatchAdButton(definition.Type, claimed, readyToClaim, progress, target);
        }

        private void SetTaskTexts(string description)
        {
            if (m_TaskText1 != null)
                m_TaskText1.text = description;

            if (m_TaskText2 != null)
                m_TaskText2.text = description;
        }

        private void SetProgressTexts(int current, int max)
        {
            string label = string.Format(m_ProgressTextFormat, current, max);

            if (m_ProgressText1 != null)
                m_ProgressText1.text = label;

            if (m_ProgressText2 != null)
                m_ProgressText2.text = label;
        }

        private void RefreshWatchAdButton(MissionType type, bool claimed, bool readyToClaim, int progress, int target)
        {
            if (m_WatchAdButton == null) return;

            bool isWatchAdsMission = type == MissionType.WatchAds;
            bool inProgress = !claimed && !readyToClaim && progress < target;
            bool show = isWatchAdsMission && inProgress;

            m_WatchAdButton.gameObject.SetActive(show);
            if (!show) return;

            bool adReady = false;
            if (AdsManager.Instance != null)
            {
                adReady = AdsManager.Instance.IsCoinsRewardedReady || AdsManager.Instance.IsInterstitialReady;
                if (!adReady)
                    AdsManager.Instance.LoadCoinsRewarded();
            }

            m_WatchAdButton.interactable = adReady;

            if (m_WatchAdButtonText != null)
                m_WatchAdButtonText.text = adReady ? "Watch Ad" : "Ad Loading...";
        }

        private void OnClaimClicked()
        {
            m_OnClaimRequested?.Invoke(m_MissionIndex);
        }

        private void OnWatchAdClicked()
        {
            if (AdsManager.Instance == null) return;

            if (!AdsManager.Instance.IsCoinsRewardedReady && !AdsManager.Instance.IsInterstitialReady)
            {
                AdsManager.Instance.LoadCoinsRewarded();
                RefreshWatchAdButtonForCurrentMission();
                return;
            }

            AdsManager.Instance.ShowRewardedForCoins();
        }

        private void RefreshWatchAdButtonForCurrentMission()
        {
            if (m_Service == null || m_Service.Config == null || m_MissionIndex < 0) return;

            var definition = m_Service.Config.Missions[m_MissionIndex];
            bool claimed = m_Service.IsClaimed(m_MissionIndex);
            bool readyToClaim = m_Service.IsReadyToClaim(m_MissionIndex);
            int progress = m_Service.GetDisplayProgress(m_MissionIndex);
            int target = m_Service.GetTargetCount(m_MissionIndex);
            RefreshWatchAdButton(definition.Type, claimed, readyToClaim, progress, target);
        }

        private void AutoWireTexts()
        {
            var progressRoot = transform.Find("MissionProgress");
            if (progressRoot == null) return;

            var taskLabels = new List<TextMeshProUGUI>();
            var progressLabels = new List<TextMeshProUGUI>();

            foreach (var tmp in progressRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == m_RewardText || tmp == m_ClaimButtonText || tmp == m_CompletedText ||
                    tmp == m_WatchAdButtonText)
                    continue;

                switch (tmp.name)
                {
                    case "TODOTask":
                        taskLabels.Add(tmp);
                        break;
                    case "TODOTaskS":
                        progressLabels.Add(tmp);
                        break;
                    case "Title (2)":
                    case "Title (3)":
                        if (taskLabels.Count < 2)
                            taskLabels.Add(tmp);
                        break;
                }
            }

            if (m_TaskText1 == null && taskLabels.Count > 0)
                m_TaskText1 = taskLabels[0];

            if (m_TaskText2 == null && taskLabels.Count > 1)
                m_TaskText2 = taskLabels[1];

            if (m_ProgressText1 == null && progressLabels.Count > 0)
                m_ProgressText1 = progressLabels[0];

            if (m_ProgressText2 == null && progressLabels.Count > 1)
                m_ProgressText2 = progressLabels[1];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_ProgressSlider == null)
            {
                var progress = transform.Find("MissionProgress");
                if (progress != null) m_ProgressSlider = progress.GetComponent<Slider>();
            }
            AutoWireTexts();
            if (m_RewardText == null)
            {
                var t = transform.Find("MissionProgress/RewardText");
                if (t == null) t = transform.Find("RewardText");
                if (t != null) m_RewardText = t.GetComponent<TextMeshProUGUI>();
            }
            if (m_RowBackground == null)
                m_RowBackground = GetComponent<Image>();
            if (m_ClaimButton == null)
            {
                var t = transform.Find("ClaimButton");
                if (t != null) m_ClaimButton = t.GetComponent<Button>();
            }
            if (m_WatchAdButton == null)
            {
                var t = transform.Find("WatchAdButton");
                if (t != null) m_WatchAdButton = t.GetComponent<Button>();
            }
        }
#endif
    }
}
