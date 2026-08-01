using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Single steady leaderboard row. Prefab-friendly: assign refs in the inspector.
    /// Columns: Place | Name | Reward (icon with amount overlay) | Golden Arrows score.
    /// Optional *TextBg fields mirror the main text (shadow/outline style).
    /// </summary>
    public class TournamentLeaderboardRowView : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private Image m_Background;
        [SerializeField] private Button m_Button;

        [Header("Place")]
        [SerializeField] private Image m_PlaceBg;
        [SerializeField] private TextMeshProUGUI m_PlaceText;
        [SerializeField] private TextMeshProUGUI m_PlaceTextBg;

        [Header("Name")]
        [SerializeField] private Image m_NameBg;
        [SerializeField] private TextMeshProUGUI m_NameText;
        [SerializeField] private TextMeshProUGUI m_NameTextBg;
        [SerializeField] private LayoutElement m_NameLayout;

        [Header("Reward")]
        [SerializeField] private Image m_RewardBg;
        [SerializeField] private GameObject m_RewardRoot;
        [SerializeField] private Image m_RewardIcon;
        [SerializeField] private TextMeshProUGUI m_RewardAmountText;
        [SerializeField] private TextMeshProUGUI m_RewardAmountTextBg;
        [SerializeField] private LayoutElement m_RewardLayout;

        [Header("Score")]
        [SerializeField] private Image m_ScoreBg;
        [SerializeField] private TextMeshProUGUI m_ScoreText;
        [SerializeField] private TextMeshProUGUI m_ScoreTextBg;

        [Header("Colors")]
        [SerializeField] private Color m_PlayerRowColor = new Color(0.49f, 0.37f, 1f, 1f);
        [SerializeField] private Color m_BotRowColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);

        public Image Background { get => m_Background; set => m_Background = value; }
        public Button Button { get => m_Button; set => m_Button = value; }
        public TextMeshProUGUI PlaceText { get => m_PlaceText; set => m_PlaceText = value; }
        public TextMeshProUGUI PlaceTextBg { get => m_PlaceTextBg; set => m_PlaceTextBg = value; }
        public TextMeshProUGUI NameText { get => m_NameText; set => m_NameText = value; }
        public TextMeshProUGUI NameTextBg { get => m_NameTextBg; set => m_NameTextBg = value; }
        public Image RewardIcon { get => m_RewardIcon; set => m_RewardIcon = value; }
        public TextMeshProUGUI RewardAmountText { get => m_RewardAmountText; set => m_RewardAmountText = value; }
        public TextMeshProUGUI RewardAmountTextBg { get => m_RewardAmountTextBg; set => m_RewardAmountTextBg = value; }
        public GameObject RewardRoot { get => m_RewardRoot; set => m_RewardRoot = value; }
        public TextMeshProUGUI ScoreText { get => m_ScoreText; set => m_ScoreText = value; }
        public TextMeshProUGUI ScoreTextBg { get => m_ScoreTextBg; set => m_ScoreTextBg = value; }
        public Image PlaceBg { get => m_PlaceBg; set => m_PlaceBg = value; }
        public Image NameBg { get => m_NameBg; set => m_NameBg = value; }
        public Image RewardBg { get => m_RewardBg; set => m_RewardBg = value; }
        public Image ScoreBg { get => m_ScoreBg; set => m_ScoreBg = value; }
        public LayoutElement NameLayout { get => m_NameLayout; set => m_NameLayout = value; }
        public LayoutElement RewardLayout { get => m_RewardLayout; set => m_RewardLayout = value; }

        private bool isPlayerRow;
        private bool m_LayoutsReady;
        private bool m_HasAppliedRewardState;
        private bool m_LastHasReward;
        private int m_LastPlace = int.MinValue;
        private int m_LastScore = int.MinValue;
        private int m_LastRewardAmount = int.MinValue;
        private string m_LastName;
        private Sprite m_LastRewardSprite;
        private bool m_LastIsPlayer;
        private Color m_LastBgColor;

        private const float NameFlexibleWidth = 1f;
        private const float RewardPreferredWidth = 250f;
        private const float NameMinWidth = 100f;

        public bool IsPlayerRow => isPlayerRow;

        private void Awake()
        {
            TryAutoWireTextBgs();
            EnsureColumnLayouts();
        }

        public void SetData(
            TournamentLeaderboardRow row,
            Reward reward,
            Sprite rewardSprite,
            Color playerRowColor,
            Color botRowColor)
        {
            isPlayerRow = row.IsPlayer;
            EnsureColumnLayouts();

            Color playerColor = playerRowColor.a > 0f ? playerRowColor : m_PlayerRowColor;
            Color botColor = botRowColor.a > 0f ? botRowColor : m_BotRowColor;
            Color bgColor = row.IsPlayer ? playerColor : botColor;

            if (m_Background != null && (m_LastIsPlayer != row.IsPlayer || m_LastBgColor != bgColor))
            {
                m_Background.color = bgColor;
                m_LastBgColor = bgColor;
            }
            m_LastIsPlayer = row.IsPlayer;

            if (m_LastPlace != row.Place)
            {
                SetPairedText(m_PlaceText, m_PlaceTextBg, $"#{row.Place}");
                m_LastPlace = row.Place;
            }

            string name = row.Name ?? string.Empty;
            if (!string.Equals(m_LastName, name, System.StringComparison.Ordinal))
            {
                SetPairedText(m_NameText, m_NameTextBg, name);
                m_LastName = name;
            }

            bool hasReward = reward.amount > 0;
            bool rewardVisibilityChanged = !m_HasAppliedRewardState || m_LastHasReward != hasReward;

            if (rewardVisibilityChanged)
            {
                if (m_RewardBg != null && m_RewardBg.gameObject.activeSelf != hasReward)
                    m_RewardBg.gameObject.SetActive(hasReward);
                if (m_RewardRoot != null && m_RewardRoot.activeSelf != hasReward)
                    m_RewardRoot.SetActive(hasReward);

                ApplyRewardColumnLayout(hasReward);
                m_LastHasReward = hasReward;
                m_HasAppliedRewardState = true;
            }

            if (hasReward)
            {
                if (m_RewardIcon != null && !ReferenceEquals(m_LastRewardSprite, rewardSprite))
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
                    m_LastRewardSprite = rewardSprite;
                }

                if (m_LastRewardAmount != reward.amount)
                {
                    SetPairedText(m_RewardAmountText, m_RewardAmountTextBg, reward.amount.ToString());
                    m_LastRewardAmount = reward.amount;
                }
            }
            else
            {
                m_LastRewardAmount = int.MinValue;
                m_LastRewardSprite = null;
            }

            int score = Mathf.Max(0, row.Score);
            if (m_LastScore != score)
            {
                SetPairedText(m_ScoreText, m_ScoreTextBg, score.ToString());
                m_LastScore = score;
            }

            if (m_Button != null)
                m_Button.interactable = row.IsPlayer;

            // Layout rebuild only when reward column visibility changes (expensive).
            if (rewardVisibilityChanged)
            {
                var rowRect = transform as RectTransform;
                if (rowRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
            }
        }

        private void ApplyRewardColumnLayout(bool hasReward)
        {
            if (m_NameLayout != null)
            {
                m_NameLayout.minWidth = NameMinWidth;
                m_NameLayout.preferredWidth = -1f;
                m_NameLayout.flexibleWidth = NameFlexibleWidth;
            }

            if (m_RewardLayout == null)
                return;

            m_RewardLayout.flexibleWidth = 0f;
            if (hasReward)
            {
                m_RewardLayout.ignoreLayout = false;
                m_RewardLayout.preferredWidth = RewardPreferredWidth;
                m_RewardLayout.minWidth = RewardPreferredWidth * 0.6f;
            }
            else
            {
                m_RewardLayout.ignoreLayout = true;
                m_RewardLayout.preferredWidth = 0f;
                m_RewardLayout.minWidth = 0f;
            }
        }

        private void EnsureColumnLayouts()
        {
            if (m_LayoutsReady)
                return;

            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childControlWidth = true;
                hlg.childForceExpandWidth = false;
            }

            if (m_NameLayout == null && m_NameBg != null)
                m_NameLayout = m_NameBg.GetComponent<LayoutElement>() ?? m_NameBg.gameObject.AddComponent<LayoutElement>();

            if (m_RewardLayout == null && m_RewardBg != null)
                m_RewardLayout = m_RewardBg.GetComponent<LayoutElement>() ?? m_RewardBg.gameObject.AddComponent<LayoutElement>();

            if (m_NameLayout != null)
            {
                m_NameLayout.minWidth = NameMinWidth;
                if (m_NameLayout.flexibleWidth < 0f)
                    m_NameLayout.flexibleWidth = NameFlexibleWidth;
            }

            if (m_RewardLayout != null && m_RewardLayout.preferredWidth <= 0f)
                m_RewardLayout.preferredWidth = RewardPreferredWidth;

            EnsureFixedColumnLayout(m_PlaceBg, TournamentLeaderboardRowFactory.PlaceWidth);
            EnsureFixedColumnLayout(m_ScoreBg, 250f);
            m_LayoutsReady = true;
        }

        private static void EnsureFixedColumnLayout(Image columnBg, float fallbackPreferredWidth)
        {
            if (columnBg == null)
                return;

            var le = columnBg.GetComponent<LayoutElement>() ?? columnBg.gameObject.AddComponent<LayoutElement>();
            float width = fallbackPreferredWidth;
            var rt = columnBg.rectTransform;
            if (rt != null && rt.sizeDelta.x > 0f)
                width = rt.sizeDelta.x;

            if (le.preferredWidth <= 0f)
                le.preferredWidth = width;
            le.flexibleWidth = 0f;
            if (le.minWidth <= 0f)
                le.minWidth = le.preferredWidth * 0.6f;
        }

        private static void SetPairedText(TextMeshProUGUI main, TextMeshProUGUI bg, string value)
        {
            if (main != null)
                main.text = value;
            if (bg != null)
                bg.text = value;
        }

        private void TryAutoWireTextBgs()
        {
            // Prefab should already wire these; only scan hierarchy for missing refs.
            bool needsScan =
                m_NameTextBg == null ||
                m_ScoreTextBg == null ||
                m_RewardAmountTextBg == null ||
                m_PlaceText == null ||
                m_PlaceTextBg == null ||
                (m_PlaceText != null && m_PlaceText.gameObject.name.EndsWith("BG", System.StringComparison.Ordinal)) ||
                (m_ScoreText != null && m_ScoreText.gameObject.name == "ScoreBG") ||
                (m_RewardAmountText != null && m_RewardAmountText.gameObject.name == "AmountBG") ||
                (m_NameText != null && m_NameText.gameObject.name == "NameBG");

            if (!needsScan)
                return;

            if (m_NameTextBg == null)
                m_NameTextBg = FindChildTmp("NameBG");
            if (m_ScoreTextBg == null)
                m_ScoreTextBg = FindChildTmp("ScoreBG");
            if (m_RewardAmountTextBg == null)
                m_RewardAmountTextBg = FindChildTmp("AmountBG");
            if (m_PlaceTextBg == null)
            {
                m_PlaceTextBg = FindChildTmp("PositionBG")
                                ?? FindChildTmp("PlaceBG")
                                ?? FindChildTmp("TextBG");
                if (m_PlaceTextBg == null && m_PlaceText != null && m_PlaceText.transform.parent != null)
                {
                    m_PlaceTextBg = FindDirectChildTmp(m_PlaceText.transform.parent, "PositionBG")
                                    ?? FindDirectChildTmp(m_PlaceText.transform.parent, "PlaceBG")
                                    ?? FindDirectChildTmp(m_PlaceText.transform.parent, "TextBG")
                                    ?? FindDirectChildTmp(m_PlaceText.transform.parent, "Text (1)");
                }
            }

            if (m_PlaceText != null &&
                (m_PlaceText.gameObject.name == "PositionBG"
                 || m_PlaceText.gameObject.name == "PlaceBG"
                 || m_PlaceText.gameObject.name == "TextBG"))
            {
                var place = FindChildTmp("Position") ?? FindChildTmp("Place") ?? FindChildTmp("Text");
                if (place != null && place != m_PlaceText)
                {
                    if (m_PlaceTextBg == null)
                        m_PlaceTextBg = m_PlaceText;
                    m_PlaceText = place;
                }
            }

            if (m_PlaceText == null)
                m_PlaceText = FindChildTmp("Position") ?? FindChildTmp("Place");
            if (m_PlaceTextBg == null)
                m_PlaceTextBg = FindChildTmp("PositionBG") ?? FindChildTmp("PlaceBG");

            if (m_ScoreText != null && m_ScoreText.gameObject.name == "ScoreBG")
            {
                var score = FindChildTmp("Score");
                if (score != null)
                {
                    if (m_ScoreTextBg == null)
                        m_ScoreTextBg = m_ScoreText;
                    m_ScoreText = score;
                }
            }

            if (m_RewardAmountText != null && m_RewardAmountText.gameObject.name == "AmountBG")
            {
                var amount = FindChildTmp("Amount");
                if (amount != null)
                {
                    if (m_RewardAmountTextBg == null)
                        m_RewardAmountTextBg = m_RewardAmountText;
                    m_RewardAmountText = amount;
                }
            }

            if (m_NameText != null && m_NameText.gameObject.name == "NameBG")
            {
                var name = FindChildTmp("Name");
                if (name != null)
                {
                    if (m_NameTextBg == null)
                        m_NameTextBg = m_NameText;
                    m_NameText = name;
                }
            }
        }

        private static TextMeshProUGUI FindDirectChildTmp(Transform parent, string objectName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name != objectName)
                    continue;
                return child.GetComponent<TextMeshProUGUI>();
            }
            return null;
        }

        private TextMeshProUGUI FindChildTmp(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != objectName)
                    continue;
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    return tmp;
            }
            return null;
        }
    }
}
