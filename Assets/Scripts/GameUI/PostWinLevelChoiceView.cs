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

        [SerializeField] private Button m_NextLevelButton;
        [SerializeField] private Button m_HomeButton;
        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_DescriptionText;
        [SerializeField] private string[] m_TitleOptions;

        public Choice SelectedChoice { get; private set; } = Choice.None;

        private bool m_IsInitialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (m_IsInitialized) return;
            m_IsInitialized = true;

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
            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            ShowBanner();
        }

        private void OnDisable()
        {
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
            gameObject.SetActive(false);
            SelectedChoice = Choice.None;
        }

        private void OnNextLevelClicked()
        {
            if (SelectedChoice != Choice.None) return;
            HideBanner();
            SelectedChoice = Choice.NextLevel;
            SetButtonsInteractable(false);
        }

        private void OnHomeClicked()
        {
            if (SelectedChoice != Choice.None) return;
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
    }
}
