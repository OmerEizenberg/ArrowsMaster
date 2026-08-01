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
        private const float NameFlexibleWithReward = 1f;
        private const float NameFlexibleWithoutReward = 1f;
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

            if (m_Background != null)
                m_Background.color = row.IsPlayer ? playerColor : botColor;

            SetPairedText(m_PlaceText, m_PlaceTextBg, $"#{row.Place}");
            SetPairedText(m_NameText, m_NameTextBg, row.Name ?? string.Empty);

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
                SetPairedText(m_RewardAmountText, m_RewardAmountTextBg, reward.amount.ToString());
            }

            ApplyRewardColumnLayout(hasReward);

            SetPairedText(m_ScoreText, m_ScoreTextBg, Mathf.Max(0, row.Score).ToString());

            if (m_Button != null)
                m_Button.interactable = row.IsPlayer;

            var rowRect = transform as RectTransform;
            if (rowRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        }

        private void ApplyRewardColumnLayout(bool hasReward)
        {
            if (m_NameLayout != null)
            {
                m_NameLayout.minWidth = NameMinWidth;
                m_NameLayout.preferredWidth = -1f;
                m_NameLayout.flexibleWidth = hasReward ? NameFlexibleWithReward : NameFlexibleWithoutReward;
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
                // Keep column collapsed if it stays active for any reason.
                m_RewardLayout.ignoreLayout = true;
                m_RewardLayout.preferredWidth = 0f;
                m_RewardLayout.minWidth = 0f;
            }
        }

        private void EnsureColumnLayouts()
        {
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
                    m_NameLayout.flexibleWidth = NameFlexibleWithReward;
            }

            if (m_RewardLayout != null && m_RewardLayout.preferredWidth <= 0f)
                m_RewardLayout.preferredWidth = RewardPreferredWidth;

            EnsureFixedColumnLayout(m_PlaceBg, TournamentLeaderboardRowFactory.PlaceWidth);
            EnsureFixedColumnLayout(m_ScoreBg, 250f);
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
            if (m_NameTextBg == null)
                m_NameTextBg = FindChildTmp("NameBG");
            if (m_ScoreTextBg == null)
                m_ScoreTextBg = FindChildTmp("ScoreBG");
            if (m_RewardAmountTextBg == null)
                m_RewardAmountTextBg = FindChildTmp("AmountBG");
            if (m_PlaceTextBg == null)
            {
                m_PlaceTextBg = FindChildTmp("PlaceBG") ?? FindChildTmp("TextBG");
                // Sibling under PlaceCol (common when duplicating place text as BG).
                if (m_PlaceTextBg == null && m_PlaceText != null && m_PlaceText.transform.parent != null)
                {
                    m_PlaceTextBg = FindDirectChildTmp(m_PlaceText.transform.parent, "PlaceBG")
                                    ?? FindDirectChildTmp(m_PlaceText.transform.parent, "TextBG")
                                    ?? FindDirectChildTmp(m_PlaceText.transform.parent, "Text (1)");
                }
            }

            // Prefab may have had Score/Amount/Place wired to the BG by mistake — prefer the non-BG sibling.
            if (m_PlaceText != null && (m_PlaceText.gameObject.name == "PlaceBG" || m_PlaceText.gameObject.name == "TextBG"))
            {
                var place = FindChildTmp("Place") ?? FindChildTmp("Text");
                if (place != null && place != m_PlaceText)
                {
                    if (m_PlaceTextBg == null)
                        m_PlaceTextBg = m_PlaceText;
                    m_PlaceText = place;
                }
            }

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
