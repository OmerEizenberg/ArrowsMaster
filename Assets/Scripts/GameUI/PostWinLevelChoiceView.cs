using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.GameUI
{
    /// <summary>
    /// Post-win overlay: lets the player continue to the next level or return home.
    /// Driven by <see cref="Assets.Scripts.Core.GameManager"/> after the win sequence.
    /// </summary>
    public class PostWinLevelChoiceView : MonoBehaviour
    {
        public enum Choice
        {
            None,
            NextLevel,
            Home
        }

        private const string NextLevelLabel = "Next Level";
        private const float NetflixInitialDelaySeconds = 1.6f;
        private const float NetflixCountdownStepSeconds = 1.25f;
        private const int NetflixCountdownStart = 3;
        private const float NetflixFinalDelaySeconds = 0f;

        [SerializeField] private Button m_NextLevelButton;
        [SerializeField] private Button m_HomeButton;
        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_DescriptionText;
        [SerializeField] private string[] m_TitleOptions;
        [SerializeField] private TextMeshProUGUI[] m_NextLevelButtonTexts;
        [SerializeField] private Image m_NextLevelFillImage;

        public Choice SelectedChoice { get; private set; } = Choice.None;

        private bool m_IsInitialized;
        private Coroutine m_NetflixEffectCoroutine;
        private bool m_IsFullscreenAdOpen;
        private bool m_IsSubscribedToAdPauseEvents;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (m_IsInitialized) return;
            m_IsInitialized = true;

            CacheNextLevelButtonTexts();
            EnsureNetflixFillBar();

            if (m_NextLevelButton != null)
            {
                m_NextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
                m_NextLevelButton.onClick.AddListener(OnNextLevelClicked);
            }

            if (m_HomeButton != null)
            {
                m_HomeButton.onClick.RemoveListener(OnHomeClicked);
                m_HomeButton.onClick.AddListener(OnHomeClicked);
            }
        }

        /// <summary>
        /// Keep this GameObject disabled in the Inspector by default.
        /// Do not call SetActive(false) from Awake — that runs on first Show() and would hide the popup immediately.
        /// </summary>
        public void Show(int completedLevel)
        {
            EnsureInitialized();

            SelectedChoice = Choice.None;
            SetButtonsInteractable(true);
            RefreshTexts(completedLevel);
            ResetNextLevelButtonPresentation();
            gameObject.SetActive(true);
            StartNetflixEffectIfEnabled();
        }

        private void OnEnable()
        {
            ResetNextLevelButtonPresentation();
            ShowBanner();
        }

        private void OnDisable()
        {
            StopNetflixEffect();
            HideBanner();
        }

        private void RefreshTexts(int completedLevel)
        {
            if (m_DescriptionText != null)
            {
                m_DescriptionText.text = $"Level {completedLevel} Completed!";
            }

            if (m_TitleText != null && m_TitleOptions != null && m_TitleOptions.Length > 0)
            {
                m_TitleText.text = m_TitleOptions[Random.Range(0, m_TitleOptions.Length)];
            }
        }

        public void Hide()
        {
            StopNetflixEffect();
            gameObject.SetActive(false);
            SelectedChoice = Choice.None;
        }

        private void OnNextLevelClicked()
        {
            if (SelectedChoice != Choice.None) return;
            StopNetflixEffect();
            HideBanner();
            SelectedChoice = Choice.NextLevel;
            SetButtonsInteractable(false);
        }

        private void OnHomeClicked()
        {
            if (SelectedChoice != Choice.None) return;
            StopNetflixEffect();
            HideBanner();
            SelectedChoice = Choice.Home;
            SetButtonsInteractable(false);
        }

        private void ShowBanner()
        {
            if (AdsManager.Instance == null) return;
            AdsManager.Instance.LoadSettingsBanner();
            AdsManager.Instance.ShowSettingsBanner();
        }

        private void HideBanner()
        {
            if (AdsManager.Instance == null) return;
            AdsManager.Instance.HideSettingsBanner();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (m_NextLevelButton != null) m_NextLevelButton.interactable = interactable;
            if (m_HomeButton != null) m_HomeButton.interactable = interactable;
        }

        private bool IsNetflixEffectEnabled()
        {
            return RemoteConfigManager.Instance != null && RemoteConfigManager.Instance.IsNetflixEffectEnabled;
        }

        private void StartNetflixEffectIfEnabled()
        {
            StopNetflixEffect();
            if (!IsNetflixEffectEnabled()) return;

            SubscribeNetflixAdPauseListeners();
            m_NetflixEffectCoroutine = StartCoroutine(NetflixEffectRoutine());
        }

        private void StopNetflixEffect()
        {
            if (m_NetflixEffectCoroutine != null)
            {
                StopCoroutine(m_NetflixEffectCoroutine);
                m_NetflixEffectCoroutine = null;
            }

            UnsubscribeNetflixAdPauseListeners();
            ResetNextLevelFillBar();
        }

        private void SubscribeNetflixAdPauseListeners()
        {
            if (m_IsSubscribedToAdPauseEvents || AdsManager.Instance == null) return;

            m_IsSubscribedToAdPauseEvents = true;
            m_IsFullscreenAdOpen = false;
            AdsManager.Instance.OnAdOpened += HandleNetflixAdOpened;
            AdsManager.Instance.OnAdClosed += HandleNetflixAdClosed;
        }

        private void UnsubscribeNetflixAdPauseListeners()
        {
            if (!m_IsSubscribedToAdPauseEvents || AdsManager.Instance == null) return;

            AdsManager.Instance.OnAdOpened -= HandleNetflixAdOpened;
            AdsManager.Instance.OnAdClosed -= HandleNetflixAdClosed;
            m_IsSubscribedToAdPauseEvents = false;
            m_IsFullscreenAdOpen = false;
        }

        private void HandleNetflixAdOpened()
        {
            m_IsFullscreenAdOpen = true;
        }

        private void HandleNetflixAdClosed()
        {
            m_IsFullscreenAdOpen = false;
        }

        private bool ShouldCancelNetflixEffect()
        {
            return SelectedChoice != Choice.None || !gameObject.activeInHierarchy;
        }

        private void ResetNextLevelButtonPresentation()
        {
            SetNextLevelButtonLabel(NextLevelLabel);
            ResetNextLevelFillBar();
        }

        private void SetNextLevelButtonLabel(string label)
        {
            if (m_NextLevelButtonTexts == null) return;

            for (int i = 0; i < m_NextLevelButtonTexts.Length; i++)
            {
                if (m_NextLevelButtonTexts[i] != null)
                {
                    m_NextLevelButtonTexts[i].text = label;
                }
            }
        }

        private void ResetNextLevelFillBar()
        {
            if (m_NextLevelFillImage == null) return;

            m_NextLevelFillImage.fillAmount = 0f;
            m_NextLevelFillImage.gameObject.SetActive(false);
        }

        private void SetNextLevelFillProgress(float normalizedProgress)
        {
            if (m_NextLevelFillImage == null) return;

            if (!m_NextLevelFillImage.gameObject.activeSelf)
            {
                m_NextLevelFillImage.gameObject.SetActive(true);
            }

            m_NextLevelFillImage.fillAmount = Mathf.Clamp01(normalizedProgress);
        }

        private IEnumerator NetflixEffectRoutine()
        {
            try
            {
                float elapsed = 0f;
                float fillStartTime = NetflixInitialDelaySeconds;
                float fillDuration = NetflixCountdownStart * NetflixCountdownStepSeconds + NetflixFinalDelaySeconds;
                float totalDuration = fillStartTime + fillDuration;

                while (elapsed < totalDuration)
                {
                    if (ShouldCancelNetflixEffect())
                    {
                        yield break;
                    }

                    if (!m_IsFullscreenAdOpen)
                    {
                        elapsed += Time.unscaledDeltaTime;
                    }

                    UpdateNetflixPresentation(elapsed, fillStartTime, fillDuration);
                    yield return null;
                }

                SetNextLevelFillProgress(1f);

                if (!ShouldCancelNetflixEffect())
                {
                    OnNextLevelClicked();
                }
            }
            finally
            {
                m_NetflixEffectCoroutine = null;
            }
        }

        private void UpdateNetflixPresentation(float elapsed, float fillStartTime, float fillDuration)
        {
            if (elapsed < fillStartTime)
            {
                SetNextLevelButtonLabel(NextLevelLabel);
                ResetNextLevelFillBar();
                return;
            }

            float fillElapsed = elapsed - fillStartTime;
            SetNextLevelFillProgress(fillElapsed / fillDuration);

            if (fillElapsed < NetflixCountdownStepSeconds)
            {
                SetNextLevelButtonLabel($"Next Level in {NetflixCountdownStart}");
            }
            else if (fillElapsed < NetflixCountdownStepSeconds * 2f)
            {
                SetNextLevelButtonLabel("Next Level in 2");
            }
            else
            {
                SetNextLevelButtonLabel("Next Level in 1");
            }
        }

        private void CacheNextLevelButtonTexts()
        {
            if (m_NextLevelButtonTexts != null && m_NextLevelButtonTexts.Length > 0) return;
            if (m_NextLevelButton == null) return;

            m_NextLevelButtonTexts = m_NextLevelButton.GetComponentsInChildren<TextMeshProUGUI>(true);
        }

        private void EnsureNetflixFillBar()
        {
            if (m_NextLevelFillImage != null || m_NextLevelButton == null) return;

            Transform existing = m_NextLevelButton.transform.Find("NetflixFill");
            if (existing != null)
            {
                m_NextLevelFillImage = existing.GetComponent<Image>();
                ConfigureNetflixFillImage(m_NextLevelFillImage);
                return;
            }

            var fillObject = new GameObject("NetflixFill", typeof(RectTransform), typeof(Image));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(m_NextLevelButton.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.SetAsFirstSibling();

            m_NextLevelFillImage = fillObject.GetComponent<Image>();
            ConfigureNetflixFillImage(m_NextLevelFillImage);
        }

        private void ConfigureNetflixFillImage(Image fillImage)
        {
            if (fillImage == null) return;

            if (m_NextLevelButton != null && m_NextLevelButton.targetGraphic is Image buttonImage && buttonImage.sprite != null)
            {
                fillImage.sprite = buttonImage.sprite;
            }

            fillImage.color = new Color(1f, 1f, 1f, 0.35f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            fillImage.gameObject.SetActive(false);
        }
    }
}
