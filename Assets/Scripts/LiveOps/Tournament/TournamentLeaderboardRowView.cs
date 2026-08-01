using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Single steady leaderboard row. Prefab-friendly: assign refs in the inspector.
    /// Columns: Place | Name | Reward (icon with amount overlay) | Golden Arrows score.
    /// </summary>
    public class TournamentLeaderboardRowView : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Image m_Background;
        [SerializeField] private Button m_Button;

        [Header("Place")]
        [SerializeField] private Image m_PlaceBg;
        [SerializeField] private TextMeshProUGUI m_PlaceText;

        [Header("Name")]
        [SerializeField] private Image m_NameBg;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private LayoutElement m_NameLayout;

        [Header("Reward")]
        [SerializeField] private Image m_RewardBg;
        [SerializeField] private GameObject m_RewardRoot;
        [SerializeField] private Image m_RewardIcon;
        [SerializeField] private TextMeshProUGUI m_RewardAmountText;
        [SerializeField] private LayoutElement m_RewardLayout;

        [Header("Score")]
        [SerializeField] private Image m_ScoreBg;
        [SerializeField] private TextMeshProUGUI m_ScoreText;

        [Header("Colors")]
        [SerializeField] private Color m_PlayerRowColor = new Color(0.49f, 0.37f, 1f, 1f);
        [SerializeField] private Color m_BotRowColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);

        public Image Background { get => m_Background; set => m_Background = value; }
        public Button Button { get => m_Button; set => m_Button = value; }
        public TextMeshProUGUI PlaceText { get => m_PlaceText; set => m_PlaceText = value; }
        public TextMeshProUGUI NameText { get => m_NameText; set => m_NameText = value; }
        public Image RewardIcon { get => m_RewardIcon; set => m_RewardIcon = value; }
        public TextMeshProUGUI RewardAmountText { get => m_RewardAmountText; set => m_RewardAmountText = value; }
        public GameObject RewardRoot { get => m_RewardRoot; set => m_RewardRoot = value; }
        public TextMeshProUGUI ScoreText { get => m_ScoreText; set => m_ScoreText = value; }
        public Image PlaceBg { get => m_PlaceBg; set => m_PlaceBg = value; }
        public Image NameBg { get => m_NameBg; set => m_NameBg = value; }
        public Image RewardBg { get => m_RewardBg; set => m_RewardBg = value; }
        public Image ScoreBg { get => m_ScoreBg; set => m_ScoreBg = value; }
        public LayoutElement NameLayout { get => m_NameLayout; set => m_NameLayout = value; }
        public LayoutElement RewardLayout { get => m_RewardLayout; set => m_RewardLayout = value; }

        private bool isPlayerRow;
        private const float NameFlexibleWithReward = 1f;
        private const float NameFlexibleWithoutReward = 1.55f;
        private const float RewardPreferredWidth = 160f;

        public bool IsPlayerRow => isPlayerRow;

        public void SetData(
            TournamentLeaderboardRow row,
            Reward reward,
            Sprite rewardSprite,
            Color playerRowColor,
            Color botRowColor)
        {
            isPlayerRow = row.IsPlayer;

            Color playerColor = playerRowColor.a > 0f ? playerRowColor : m_PlayerRowColor;
            Color botColor = botRowColor.a > 0f ? botRowColor : m_BotRowColor;

            if (m_Background != null)
                m_Background.color = row.IsPlayer ? playerColor : botColor;

            if (m_PlaceText != null)
                m_PlaceText.text = $"#{row.Place}";

            if (m_NameText != null)
                m_NameText.text = row.Name ?? string.Empty;

            bool hasReward = reward.amount > 0;

            if (m_RewardBg != null)
                m_RewardBg.gameObject.SetActive(hasReward);
            if (m_RewardRoot != null)
                m_RewardRoot.SetActive(hasReward);

            if (hasReward)
            {
                if (m_RewardIcon != null)
                {
                    if (rewardSprite != null)
                    {
                        m_RewardIcon.sprite = rewardSprite;
                        m_RewardIcon.enabled = true;
                        m_RewardIcon.color = Color.white;
                        m_RewardIcon.preserveAspect = true;
                    }
                    else
                    {
                        m_RewardIcon.enabled = false;
                    }
                }
                if (m_RewardAmountText != null)
                    m_RewardAmountText.text = reward.amount.ToString();
            }

            if (m_NameLayout != null)
                m_NameLayout.flexibleWidth = hasReward ? NameFlexibleWithReward : NameFlexibleWithoutReward;
            if (m_RewardLayout != null)
            {
                m_RewardLayout.preferredWidth = hasReward ? RewardPreferredWidth : 0f;
                m_RewardLayout.minWidth = hasReward ? RewardPreferredWidth * 0.6f : 0f;
                m_RewardLayout.flexibleWidth = 0f;
            }

            if (m_ScoreText != null)
                m_ScoreText.text = Mathf.Max(0, row.Score).ToString();

            if (m_Button != null)
                m_Button.interactable = row.IsPlayer;

            var rowRect = transform as RectTransform;
            if (rowRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        }
    }
}
