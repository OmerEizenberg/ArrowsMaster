using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.GAE
{
    public class GAEView : MonoBehaviour
    {
        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI m_TimerText;

        [Header("Reward")]
        [SerializeField] private Image m_RewardIcon;
        [SerializeField] private TextMeshProUGUI m_RewardAmountText;
        [SerializeField] private TextMeshProUGUI m_RewardAmountTextSecondary;

        [Header("Progress")]
        [SerializeField] private TextMeshProUGUI m_ProgressText;
        [SerializeField] private TextMeshProUGUI m_ProgressTextSecondary;
        [SerializeField] private Slider m_ProgressSlider;

        [Header("Reward Icons")]
        [SerializeField] private Sprite m_CoinSprite;
        [SerializeField] private Sprite m_HintSprite;
        [SerializeField] private Sprite m_ShuffleSprite;

        [Header("Animation")]
        [SerializeField] private float m_ProgressAnimDuration = 0.75f;
        [SerializeField] private float m_TimerUpdateInterval = 1f;
        [SerializeField] private GameObject m_VisibilityRoot;

        private Coroutine m_ProgressAnimCoroutine;
        private float m_NextTimerRefreshTime;
        private bool m_IsSubscribed;

        private GameObject VisibilityTarget => m_VisibilityRoot != null ? m_VisibilityRoot : gameObject;

        private void Awake()
        {
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnEnable()
        {
            GAEManager.Instance.SyncEventState(forceResetOnMismatch: true);
            RefreshVisibility();
            RefreshStaticUI();
            TryPlayProgressAnimation(immediateIfNoChange: true);
            m_NextTimerRefreshTime = Time.time;
            UpdateTimerText();
        }

        private void OnDisable()
        {
            if (m_ProgressAnimCoroutine != null)
            {
                StopCoroutine(m_ProgressAnimCoroutine);
                m_ProgressAnimCoroutine = null;
            }
        }

        private void Update()
        {
            if (!VisibilityTarget.activeInHierarchy || !IsViewActive())
            {
                return;
            }

            if (Time.time >= m_NextTimerRefreshTime)
            {
                m_NextTimerRefreshTime = Time.time + m_TimerUpdateInterval;
                UpdateTimerText();
            }
        }

        private void Subscribe()
        {
            if (m_IsSubscribed)
            {
                return;
            }

            GAEManager.Instance.OnStateChanged += HandleStateChanged;
            m_IsSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_IsSubscribed)
            {
                return;
            }

            if (GAEManager.Instance != null)
            {
                GAEManager.Instance.OnStateChanged -= HandleStateChanged;
            }

            m_IsSubscribed = false;
        }

        private void HandleStateChanged()
        {
            RefreshVisibility();
            if (!IsViewActive())
            {
                return;
            }

            RefreshStaticUI();
            TryPlayProgressAnimation(immediateIfNoChange: false);
            UpdateTimerText();
        }

        private void RefreshVisibility()
        {
            bool shouldShow = IsViewActive();
            GameObject target = VisibilityTarget;
            if (target.activeSelf != shouldShow)
            {
                target.SetActive(shouldShow);
            }
        }

        private bool IsViewActive()
        {
            return GAEManager.Instance != null && GAEManager.Instance.IsEventActive;
        }

        private void RefreshStaticUI()
        {
            GAEStageDefinition stage = GAEManager.Instance.GetCurrentStageDefinition();
            if (stage == null)
            {
                return;
            }

            UpdateRewardVisuals(stage.RewardType, stage.RewardAmount);
        }

        private void UpdateRewardVisuals(GAERewardType type, int amount)
        {
            if (m_RewardIcon != null)
            {
                m_RewardIcon.sprite = GetRewardSprite(type);
                m_RewardIcon.enabled = m_RewardIcon.sprite != null;
            }

            string amountText = FormatRewardAmount(type, amount);
            SetText(m_RewardAmountText, amountText);
            SetText(m_RewardAmountTextSecondary, amountText);
        }

        private void TryPlayProgressAnimation(bool immediateIfNoChange)
        {
            GAEManager manager = GAEManager.Instance;
            manager.GetLastPresentedProgress(out int fromCollected, out int fromStageIndex);
            manager.GetStageProgress(out int targetCurrent, out int targetStageTotal, out int targetStageIndex);

            int targetCollected = GetAbsoluteCollectedForStageProgress(targetCurrent, targetStageIndex);
            int fromAbsoluteCollected = GetAbsoluteCollectedForStageProgress(
                GetStageRelativeCollected(fromCollected, fromStageIndex),
                fromStageIndex);

            bool hasProgressChange = targetCollected > fromAbsoluteCollected || targetStageIndex != fromStageIndex;
            if (!hasProgressChange)
            {
                ApplyProgressPresentation(targetCurrent, targetStageTotal, targetStageIndex);
                if (immediateIfNoChange)
                {
                    manager.SetLastPresentedProgress(targetCollected, targetStageIndex);
                }

                return;
            }

            if (m_ProgressAnimCoroutine != null)
            {
                StopCoroutine(m_ProgressAnimCoroutine);
            }

            m_ProgressAnimCoroutine = StartCoroutine(AnimateProgressRoutine(
                fromAbsoluteCollected,
                targetCollected,
                fromStageIndex,
                targetStageIndex));
        }

        private IEnumerator AnimateProgressRoutine(
            int fromCollected,
            int toCollected,
            int fromStageIndex,
            int toStageIndex)
        {
            GAEManager manager = GAEManager.Instance;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, m_ProgressAnimDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);

                int animatedCollected = Mathf.RoundToInt(Mathf.Lerp(fromCollected, toCollected, eased));
                int animatedStageIndex = eased >= 1f ? toStageIndex : fromStageIndex;
                manager.GetStageProgress(out _, out _, out int actualStageIndex);

                if (animatedCollected >= GetStageThreshold(toStageIndex) && toStageIndex > fromStageIndex && eased < 1f)
                {
                    animatedStageIndex = toStageIndex;
                }
                else
                {
                    animatedStageIndex = Mathf.Min(animatedStageIndex, actualStageIndex);
                }

                GetStageRelativeProgress(animatedCollected, animatedStageIndex, out int current, out int target);
                ApplyProgressPresentation(current, target, animatedStageIndex);
                yield return null;
            }

            manager.GetStageProgress(out int finalCurrent, out int finalTarget, out int finalStageIndex);
            ApplyProgressPresentation(finalCurrent, finalTarget, finalStageIndex);
            manager.SetLastPresentedProgress(toCollected, finalStageIndex);
            m_ProgressAnimCoroutine = null;
        }

        private void ApplyProgressPresentation(int current, int target, int stageIndex)
        {
            string progressText = FormatProgressPair(current, target);
            SetText(m_ProgressText, progressText);
            SetText(m_ProgressTextSecondary, progressText);

            if (m_ProgressSlider != null)
            {
                m_ProgressSlider.minValue = 0f;
                m_ProgressSlider.maxValue = 1f;
                m_ProgressSlider.value = target > 0 ? (float)current / target : 0f;
            }

            GAEStageDefinition stage = GetStageDefinition(stageIndex);
            if (stage != null)
            {
                UpdateRewardVisuals(stage.RewardType, stage.RewardAmount);
            }
        }

        private void UpdateTimerText()
        {
            SetText(m_TimerText, GAEManager.Instance.GetTimerString());
        }

        private GAEStageDefinition GetStageDefinition(int stageIndex)
        {
            GAEConfigSO config = GAEManager.Instance.Config;
            if (config == null || config.Stages == null || stageIndex < 0 || stageIndex >= config.Stages.Count)
            {
                return null;
            }

            return config.Stages[stageIndex];
        }

        private int GetStageThreshold(int stageIndex)
        {
            GAEStageDefinition stage = GetStageDefinition(stageIndex);
            return stage != null ? stage.ArrowTarget : 0;
        }

        private int GetPreviousStageThreshold(int stageIndex)
        {
            if (stageIndex <= 0)
            {
                return 0;
            }

            return GetStageThreshold(stageIndex - 1);
        }

        private int GetAbsoluteCollectedForStageProgress(int stageRelativeCollected, int stageIndex)
        {
            return GetPreviousStageThreshold(stageIndex) + stageRelativeCollected;
        }

        private int GetStageRelativeCollected(int absoluteCollected, int stageIndex)
        {
            GetStageRelativeProgress(absoluteCollected, stageIndex, out int current, out _);
            return current;
        }

        private void GetStageRelativeProgress(int absoluteCollected, int stageIndex, out int current, out int target)
        {
            int previousThreshold = GetPreviousStageThreshold(stageIndex);
            int stageThreshold = GetStageThreshold(stageIndex);
            target = Mathf.Max(1, stageThreshold - previousThreshold);
            current = Mathf.Clamp(absoluteCollected - previousThreshold, 0, target);
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private Sprite GetRewardSprite(GAERewardType type)
        {
            switch (type)
            {
                case GAERewardType.Coin: return m_CoinSprite;
                case GAERewardType.Hint: return m_HintSprite;
                case GAERewardType.Shuffle: return m_ShuffleSprite;
                default: return null;
            }
        }

        private static string FormatRewardAmount(GAERewardType type, int amount)
        {
            return type == GAERewardType.Coin ? FormatCompactNumber(amount) : amount.ToString();
        }

        public static string FormatProgressPair(int current, int target)
        {
            return $"{FormatCompactNumber(current)}/{FormatCompactNumber(target)}";
        }

        public static string FormatCompactNumber(int value)
        {
            if (value < 1000)
            {
                return value.ToString();
            }

            float thousands = value / 1000f;
            if (value % 1000 == 0)
            {
                return $"{(int)thousands}k";
            }

            return $"{thousands:0.#}k";
        }
    }
}
