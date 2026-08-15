using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Builds the leaderboard scroll table + column header row for the popup prefab
    /// (and as a runtime fallback when missing from the prefab).
    /// </summary>
    public static class TournamentLeaderboardTableFactory
    {
        public const float PlaceWidth = 120f;
        public const float RewardWidth = 160f;
        public const float ScoreWidth = 160f;

        public struct BuiltTable
        {
            public RectTransform ColumnHeadersRoot;
            public TextMeshProUGUI PlaceHeader;
            public TextMeshProUGUI PlaceHeaderBg;
            public TextMeshProUGUI NameHeader;
            public TextMeshProUGUI NameHeaderBg;
            public TextMeshProUGUI RewardHeader;
            public TextMeshProUGUI RewardHeaderBg;
            public TextMeshProUGUI ScoreHeader;
            public TextMeshProUGUI ScoreHeaderBg;
            public ScrollRect ScrollRect;
            public Image TableBackground;
            public Transform RowsParent;
        }

        public static BuiltTable Create(
            Transform popupParent,
            TMP_FontAsset font,
            Sprite backgroundSprite = null)
        {
            var result = new BuiltTable();
            CreateHeaders(popupParent, font, ref result);
            CreateScroll(popupParent, backgroundSprite, ref result);
            return result;
        }

        public static BuiltTable CreateHeadersOnly(Transform popupParent, TMP_FontAsset font)
        {
            var result = new BuiltTable();
            CreateHeaders(popupParent, font, ref result);
            return result;
        }

        public static BuiltTable CreateScrollOnly(Transform popupParent, Sprite backgroundSprite = null)
        {
            var result = new BuiltTable();
            CreateScroll(popupParent, backgroundSprite, ref result);
            return result;
        }

        private static void CreateHeaders(Transform popupParent, TMP_FontAsset font, ref BuiltTable result)
        {
            var headerGo = new GameObject("ColumnHeaders", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            headerGo.transform.SetParent(popupParent, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.04f, 0.72f);
            headerRt.anchorMax = new Vector2(0.96f, 0.78f);
            headerRt.offsetMin = Vector2.zero;
            headerRt.offsetMax = Vector2.zero;

            var hlg = headerGo.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 0, 0);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            result.ColumnHeadersRoot = headerRt;
            CreateHeaderColumn(headerGo.transform, "PlaceHeader", "#", PlaceWidth, 0f,
                new Color(1f, 0.88f, 0.35f, 1f), font, out result.PlaceHeader, out result.PlaceHeaderBg);
            CreateHeaderColumn(headerGo.transform, "NameHeader", "Name", 0f, 1f,
                Color.white, font, out result.NameHeader, out result.NameHeaderBg);
            CreateHeaderColumn(headerGo.transform, "RewardHeader", "Reward", RewardWidth, 0f,
                new Color(0.85f, 1f, 0.85f, 1f), font, out result.RewardHeader, out result.RewardHeaderBg);
            CreateHeaderColumn(headerGo.transform, "ScoreHeader", "Arrows", ScoreWidth, 0f,
                new Color(1f, 0.9f, 0.45f, 1f), font, out result.ScoreHeader, out result.ScoreHeaderBg);
        }

        private static void CreateScroll(Transform popupParent, Sprite backgroundSprite, ref BuiltTable result)
        {
            var scrollGo = new GameObject("LeaderboardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(popupParent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.11f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.72f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var scrollImage = scrollGo.GetComponent<Image>();
            if (backgroundSprite != null)
            {
                scrollImage.sprite = backgroundSprite;
                scrollImage.type = Image.Type.Sliced;
            }
            scrollImage.color = new Color(0f, 0f, 0f, 0.15f);
            scrollImage.raycastTarget = true;
            result.TableBackground = scrollImage;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            result.ScrollRect = scroll;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewportGo.GetComponent<RectTransform>();
            Stretch(vpRect);
            var vpImage = viewportGo.GetComponent<Image>();
            vpImage.color = new Color(1f, 1f, 1f, 0.01f);
            vpImage.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRect;
            scroll.content = contentRect;
            result.RowsParent = contentGo.transform;
        }

        private static void CreateHeaderColumn(
            Transform parent,
            string objectName,
            string label,
            float preferredWidth,
            float flexibleWidth,
            Color color,
            TMP_FontAsset font,
            out TextMeshProUGUI text,
            out TextMeshProUGUI textBg)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.minWidth = preferredWidth > 0f ? preferredWidth * 0.5f : 80f;

            textBg = CreateHeaderTmp(go.transform, "TextBG", label, font, new Color(0f, 0f, 0f, 0.55f));
            textBg.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            text = CreateHeaderTmp(go.transform, "Text", label, font, color);
        }

        private static TextMeshProUGUI CreateHeaderTmp(
            Transform parent,
            string objectName,
            string label,
            TMP_FontAsset font,
            Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
            tmp.text = label;
            tmp.fontSize = 32f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
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
