using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Builds a tournament leaderboard row hierarchy used by both runtime fallback
    /// and the editor prefab generator.
    /// </summary>
    public static class TournamentLeaderboardRowFactory
    {
        public const float DefaultRowHeight = 216f;
        public const float PlaceWidth = 120f;
        public const float RewardWidth = 250f;
        public const float ScoreWidth = 250f;

        private static readonly Color PlaceColColor = new Color(0.95f, 0.75f, 0.2f, 0.45f);
        private static readonly Color NameColColor = new Color(0.2f, 0.55f, 0.75f, 0.35f);
        private static readonly Color RewardColColor = new Color(0.35f, 0.7f, 0.4f, 0.35f);
        private static readonly Color ScoreColColor = new Color(0.85f, 0.55f, 0.15f, 0.35f);
        private static readonly Color PlaceTextColor = new Color(1f, 0.88f, 0.35f, 1f);
        private static readonly Color NameTextColor = Color.white;
        private static readonly Color ScoreTextColor = new Color(1f, 0.9f, 0.45f, 1f);

        public static TournamentLeaderboardRowView Create(
            Transform parent,
            TMP_FontAsset font,
            Sprite backgroundSprite,
            float rowHeight = DefaultRowHeight)
        {
            var go = new GameObject("TournamentLeaderboardRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            if (parent != null)
                go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, rowHeight);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = rowHeight;
            le.preferredHeight = rowHeight;
            le.flexibleWidth = 1f;
            le.minWidth = 0f;

            var bg = go.GetComponent<Image>();
            bg.sprite = backgroundSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.35f, 0.35f, 0.4f, 0.9f);

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 12, 12);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var view = go.AddComponent<TournamentLeaderboardRowView>();
            view.Background = bg;
            view.Button = go.GetComponent<Button>();

            view.PlaceBg = CreateColumn(go.transform, "PlaceCol", PlaceWidth, 0f, PlaceColColor, backgroundSprite, out var placeRoot);
            view.PlaceText = CreateColumnText(placeRoot, "#1", 48f, PlaceTextColor, FontStyles.Bold, font);

            view.NameBg = CreateColumn(go.transform, "NameCol", 0f, 1f, NameColColor, backgroundSprite, out var nameRoot);
            view.NameLayout = nameRoot.GetComponent<LayoutElement>();
            view.NameText = CreateColumnText(nameRoot, "Name", 42f, NameTextColor, FontStyles.Normal, font);
            view.NameText.alignment = TextAlignmentOptions.MidlineLeft;
            view.NameText.margin = new Vector4(16f, 0f, 8f, 0f);

            view.RewardBg = CreateColumn(go.transform, "RewardCol", RewardWidth, 0f, RewardColColor, backgroundSprite, out var rewardRoot);
            view.RewardLayout = rewardRoot.GetComponent<LayoutElement>();
            view.RewardRoot = BuildRewardOverlay(rewardRoot, font, out var rewardIcon, out var rewardAmountText);
            view.RewardIcon = rewardIcon;
            view.RewardAmountText = rewardAmountText;

            view.ScoreBg = CreateColumn(go.transform, "ScoreCol", ScoreWidth, 0f, ScoreColColor, backgroundSprite, out var scoreRoot);
            view.ScoreText = CreateColumnText(scoreRoot, "0", 44f, ScoreTextColor, FontStyles.Bold, font);
            view.ScoreText.alignment = TextAlignmentOptions.Center;

            return view;
        }

        private static Image CreateColumn(Transform parent, string name, float preferredWidth, float flexibleWidth, Color tint, Sprite bgSprite, out RectTransform root)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            root = go.GetComponent<RectTransform>();

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.minWidth = preferredWidth > 0f ? preferredWidth * 0.6f : 100f;

            var img = go.GetComponent<Image>();
            img.sprite = bgSprite;
            img.type = Image.Type.Sliced;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateColumnText(Transform parent, string value, float size, Color color, FontStyles style, TMP_FontAsset font)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 28f;
            tmp.fontSizeMax = size;
            return tmp;
        }

        private static GameObject BuildRewardOverlay(Transform parent, TMP_FontAsset font, out Image icon, out TextMeshProUGUI amount)
        {
            var root = new GameObject("Reward", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.15f, 0.12f);
            iconRect.anchorMax = new Vector2(0.85f, 0.88f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.color = Color.white;

            var amountGo = new GameObject("Amount", typeof(RectTransform));
            amountGo.transform.SetParent(root.transform, false);
            Stretch(amountGo.GetComponent<RectTransform>());
            amount = amountGo.AddComponent<TextMeshProUGUI>();
            amount.font = font != null ? font : TMP_Settings.defaultFontAsset;
            amount.fontSize = 40f;
            amount.fontStyle = FontStyles.Bold;
            amount.color = Color.white;
            amount.alignment = TextAlignmentOptions.Center;
            amount.raycastTarget = false;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 28f;
            amount.fontSizeMax = 44f;
            amount.outlineWidth = 0.25f;
            amount.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            amount.text = "0";

            return root;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
