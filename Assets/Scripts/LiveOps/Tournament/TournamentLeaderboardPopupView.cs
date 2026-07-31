using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven leaderboard. Builds rows once, then updates cell texts/icons in place.
    /// </summary>
    public class TournamentLeaderboardPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private Transform m_RowsParent;

        [Header("Reward / score icons (Legend Pass style)")]
        [SerializeField] private Sprite m_CoinSprite;
        [SerializeField] private Sprite m_HintSprite;
        [SerializeField] private Sprite m_WandSprite;
        [SerializeField] private Sprite m_LifeSprite;
        [SerializeField] private Sprite m_GoldenArrowSprite;

        [SerializeField] private GameObject m_NameEditRoot;
        [SerializeField] private TMP_InputField m_NameInput;
        [SerializeField] private TextMeshProUGUI m_NameError;
        [SerializeField] private Button m_SaveNameButton;
        [SerializeField] private Button m_CancelNameButton;

        private static readonly Color PlayerRowColor = new Color(0.49f, 0.37f, 1f, 1f);
        private static readonly Color BotRowColor = new Color(0.35f, 0.35f, 0.4f, 0.9f);
        private static readonly Color PlaceColColor = new Color(0.95f, 0.75f, 0.2f, 0.35f);
        private static readonly Color NameColColor = new Color(0.2f, 0.55f, 0.75f, 0.28f);
        private static readonly Color RewardColColor = new Color(0.35f, 0.7f, 0.4f, 0.28f);
        private static readonly Color ScoreColColor = new Color(0.85f, 0.55f, 0.15f, 0.28f);
        private static readonly Color PlaceTextColor = new Color(1f, 0.88f, 0.35f, 1f);
        private static readonly Color NameTextColor = Color.white;
        private static readonly Color PlayerNameTextColor = new Color(0.65f, 0.9f, 1f, 1f);
        private static readonly Color RewardTextColor = new Color(0.75f, 1f, 0.8f, 1f);
        private static readonly Color ScoreTextColor = new Color(1f, 0.85f, 0.4f, 1f);

        private const float RowHeight = 108f;
        private const int ExpectedRows = 25;

        private TournamentLiveOpService service;
        private float nextRefresh;
        private TMP_FontAsset rowFont;
        private Sprite rowBgSprite;
        private readonly List<TournamentLeaderboardRowView> m_Rows = new List<TournamentLeaderboardRowView>(ExpectedRows);
        private bool m_RowsBuilt;

        public static void Show(TournamentLiveOpService service)
        {
            if (service == null) return;

            GameObject prefab = Resources.Load<GameObject>("TournamentLeaderboardPopup");
            if (prefab == null)
            {
                Debug.LogError("[TournamentLeaderboardPopupView] Missing Resources/TournamentLeaderboardPopup.prefab");
                return;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = "TournamentLeaderboardPopup";
            instance.SetActive(true);
            var view = instance.GetComponent<TournamentLeaderboardPopupView>();
            if (view == null)
                view = instance.AddComponent<TournamentLeaderboardPopupView>();
            view.Initialize(service);
        }

        public void Initialize(TournamentLiveOpService tournamentService)
        {
            service = tournamentService;
            ResolveRefs();
            CacheVisuals();
            EnsureRewardSprites();
            WireButtons();
            EnsureNameEditUi();
            EnsureParentLayout();

            if (m_Title != null)
                m_Title.text = "Golden Tournament";

            if (service != null)
            {
                service.OnStateChanged -= RefreshRows;
                service.OnStateChanged += RefreshRows;
            }

            EnsureRowsBuilt();
            RefreshRows();
            RefreshTimer();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnStateChanged -= RefreshRows;
        }

        private void Update()
        {
            if (service == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 2f;
            service.TickFinalize();
            if (service.Status == TournamentStatus.Finished || service.Status == TournamentStatus.PendingJoin)
            {
                Close();
                return;
            }
            RefreshRows();
            RefreshTimer();
        }

        private void ResolveRefs()
        {
            if (m_CloseButton == null)
            {
                var t = FindDeep("GreenShadow");
                if (t != null) m_CloseButton = t.GetComponent<Button>();
            }
            if (m_Title == null)
            {
                var t = FindDeep("Title");
                if (t != null) m_Title = t.GetComponent<TextMeshProUGUI>();
            }
            if (m_TimerText == null)
            {
                var t = FindDeep("Description");
                if (t != null) m_TimerText = t.GetComponent<TextMeshProUGUI>();
            }
            if (m_RowsParent == null)
            {
                var holder = FindDeep("MissionsHolder");
                if (holder != null)
                {
                    // Only clear original mission slots once, before we build tournament rows.
                    if (!m_RowsBuilt)
                    {
                        for (int i = holder.childCount - 1; i >= 0; i--)
                            Destroy(holder.GetChild(i).gameObject);
                    }
                    m_RowsParent = holder;
                }
            }
        }

        private void CacheVisuals()
        {
            if (m_Title != null)
                rowFont = m_Title.font;

            var native = FindDeep("Popup");
            if (native != null)
            {
                var img = native.GetComponent<Image>();
                if (img != null) rowBgSprite = img.sprite;
            }
        }

        private void EnsureRewardSprites()
        {
            // Prefab should have these assigned; keep null-safe fallbacks via Resources if present later.
            if (m_CoinSprite == null)
                m_CoinSprite = Resources.Load<Sprite>("Tournament/ArrowsCoin");
            if (m_HintSprite == null)
                m_HintSprite = Resources.Load<Sprite>("Tournament/Hint");
            if (m_WandSprite == null)
                m_WandSprite = Resources.Load<Sprite>("Tournament/Wand");
            if (m_LifeSprite == null)
                m_LifeSprite = Resources.Load<Sprite>("Tournament/Life");
            if (m_GoldenArrowSprite == null)
                m_GoldenArrowSprite = Resources.Load<Sprite>("Tournament/GoldenArrow");
        }

        private void EnsureParentLayout()
        {
            if (m_RowsParent == null) return;

            var vlg = m_RowsParent.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
                vlg = m_RowsParent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            var fitter = m_RowsParent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = m_RowsParent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var parentRect = m_RowsParent as RectTransform;
            if (parentRect != null)
            {
                // Stretch to holder width.
                parentRect.anchorMin = new Vector2(0f, 1f);
                parentRect.anchorMax = new Vector2(1f, 1f);
                parentRect.pivot = new Vector2(0.5f, 1f);
                parentRect.offsetMin = new Vector2(0f, parentRect.offsetMin.y);
                parentRect.offsetMax = new Vector2(0f, parentRect.offsetMax.y);
            }
        }

        private void WireButtons()
        {
            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveAllListeners();
                m_CloseButton.onClick.AddListener(Close);
                var label = m_CloseButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "Let's Go!";
            }
        }

        private void RefreshTimer()
        {
            if (service == null || m_TimerText == null) return;
            var rem = service.GetRemainingTime();
            m_TimerText.text = rem.TotalSeconds <= 0
                ? "Finished"
                : $"Ends in {(int)rem.TotalHours}h {rem.Minutes}m  •  Tap your name to edit";
        }

        private void EnsureRowsBuilt()
        {
            if (m_RowsBuilt || m_RowsParent == null) return;
            m_RowsBuilt = true;

            for (int i = 0; i < ExpectedRows; i++)
            {
                var row = CreateRowObject(i);
                m_Rows.Add(row);
            }
        }

        private void RefreshRows()
        {
            if (service == null || m_RowsParent == null) return;
            EnsureRowsBuilt();

            List<TournamentLeaderboardRow> data = service.BuildLeaderboardRows(TrustedTimeService.UtcNow);
            int count = Mathf.Min(data.Count, m_Rows.Count);

            for (int i = 0; i < m_Rows.Count; i++)
            {
                var view = m_Rows[i];
                if (view == null) continue;

                if (i < count)
                {
                    if (!view.gameObject.activeSelf)
                        view.gameObject.SetActive(true);

                    string rewardKey = service.GetRewardKeyForPlace(data[i].Place - 1);
                    Reward reward = TournamentConfigSO.ParseReward(rewardKey);
                    view.SetData(
                        data[i],
                        reward,
                        GetRewardSprite(reward.type, reward.amount),
                        m_GoldenArrowSprite,
                        PlayerRowColor,
                        BotRowColor);

                    if (view.NameText != null)
                        view.NameText.color = data[i].IsPlayer ? PlayerNameTextColor : NameTextColor;

                    // Rebind player click without rebuilding.
                    if (view.Button != null)
                    {
                        view.Button.onClick.RemoveAllListeners();
                        if (data[i].IsPlayer)
                            view.Button.onClick.AddListener(OpenNameEditor);
                    }
                }
                else if (view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        private TournamentLeaderboardRowView CreateRowObject(int index)
        {
            var go = new GameObject($"Row_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(m_RowsParent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;
            le.flexibleWidth = 1f;
            le.minWidth = 0f;

            var bg = go.GetComponent<Image>();
            bg.sprite = rowBgSprite;
            bg.type = Image.Type.Sliced;
            bg.color = BotRowColor;

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 8, 8);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var view = go.AddComponent<TournamentLeaderboardRowView>();
            view.Background = bg;
            view.Button = go.GetComponent<Button>();

            view.PlaceBg = CreateColumn(go.transform, "PlaceCol", 100f, 0f, PlaceColColor, out var placeRoot);
            view.PlaceText = CreateColumnText(placeRoot, "#1", 36f, PlaceTextColor, FontStyles.Bold);

            view.NameBg = CreateColumn(go.transform, "NameCol", 0f, 1f, NameColColor, out var nameRoot);
            view.NameText = CreateColumnText(nameRoot, "Name", 32f, NameTextColor, FontStyles.Normal);
            view.NameText.alignment = TextAlignmentOptions.MidlineLeft;
            view.NameText.margin = new Vector4(12f, 0f, 8f, 0f);

            view.RewardBg = CreateColumn(go.transform, "RewardCol", 150f, 0f, RewardColColor, out var rewardRoot);
            view.RewardRoot = BuildIconAmount(rewardRoot, "Reward", out view.RewardIcon, out view.RewardAmountText, RewardTextColor);

            view.ScoreBg = CreateColumn(go.transform, "ScoreCol", 150f, 0f, ScoreColColor, out var scoreRoot);
            BuildIconAmount(scoreRoot, "Score", out view.ScoreIcon, out view.ScoreText, ScoreTextColor);

            return view;
        }

        private Image CreateColumn(Transform parent, string name, float preferredWidth, float flexibleWidth, Color tint, out RectTransform root)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            root = go.GetComponent<RectTransform>();

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.minWidth = preferredWidth > 0f ? preferredWidth * 0.6f : 80f;

            var img = go.GetComponent<Image>();
            img.sprite = rowBgSprite;
            img.type = Image.Type.Sliced;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI CreateColumnText(Transform parent, string value, float size, Color color, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 22f;
            tmp.fontSizeMax = size;
            return tmp;
        }

        private GameObject BuildIconAmount(
            Transform parent,
            string name,
            out Image icon,
            out TextMeshProUGUI amount,
            Color amountColor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(root.transform, false);
            var iconLe = iconGo.GetComponent<LayoutElement>();
            iconLe.preferredWidth = 48f;
            iconLe.preferredHeight = 48f;
            icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            var amountGo = new GameObject("Amount", typeof(RectTransform), typeof(LayoutElement));
            amountGo.transform.SetParent(root.transform, false);
            var amountLe = amountGo.GetComponent<LayoutElement>();
            amountLe.preferredWidth = 70f;
            amountLe.flexibleWidth = 1f;
            amount = amountGo.AddComponent<TextMeshProUGUI>();
            amount.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            amount.fontSize = 32f;
            amount.color = amountColor;
            amount.alignment = TextAlignmentOptions.Center;
            amount.raycastTarget = false;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 22f;
            amount.fontSizeMax = 34f;

            return root;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Sprite GetRewardSprite(RewardType type, int amount)
        {
            if (amount <= 0) return null;
            switch (type)
            {
                case RewardType.Coin: return m_CoinSprite;
                case RewardType.Hint: return m_HintSprite;
                case RewardType.MagicWand: return m_WandSprite;
                case RewardType.RefillLife: return m_LifeSprite;
                default: return null;
            }
        }

        private void EnsureNameEditUi()
        {
            if (m_NameEditRoot != null) return;

            m_NameEditRoot = new GameObject("NameEditOverlay", typeof(RectTransform), typeof(Image));
            m_NameEditRoot.transform.SetParent(transform, false);
            var overlayRect = m_NameEditRoot.GetComponent<RectTransform>();
            Stretch(overlayRect);
            m_NameEditRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(m_NameEditRoot.transform, false);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(700f, 420f);
            var boxImg = box.GetComponent<Image>();
            boxImg.sprite = rowBgSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = Color.white;

            CreateTmp(box.transform, "Edit your name", 40f, new Vector2(0f, 140f), new Vector2(640f, 50f));

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(box.transform, false);
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.anchorMin = inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.sizeDelta = new Vector2(560f, 80f);
            inputRect.anchoredPosition = new Vector2(0f, 30f);
            inputGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);
            m_NameInput = inputGo.GetComponent<TMP_InputField>();
            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(inputGo.transform, false);
            var taRect = textArea.GetComponent<RectTransform>();
            Stretch(taRect);
            taRect.offsetMin = new Vector2(16f, 8f);
            taRect.offsetMax = new Vector2(-16f, -8f);
            var text = textArea.AddComponent<TextMeshProUGUI>();
            text.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            text.fontSize = 36f;
            text.color = Color.white;
            m_NameInput.textViewport = taRect;
            m_NameInput.textComponent = text;
            m_NameInput.characterLimit = TournamentNameFilter.MaxLength;

            m_NameError = CreateTmp(box.transform, "", 26f, new Vector2(0f, -50f), new Vector2(640f, 40f));
            m_NameError.color = new Color(1f, 0.45f, 0.45f, 1f);

            m_SaveNameButton = CreateGreenishButton(box.transform, "SAVE", new Vector2(-140f, -140f));
            m_SaveNameButton.onClick.AddListener(SaveName);
            m_CancelNameButton = CreateGreenishButton(box.transform, "CANCEL", new Vector2(140f, -140f));
            m_CancelNameButton.onClick.AddListener(() => m_NameEditRoot.SetActive(false));

            m_NameEditRoot.SetActive(false);
        }

        private TextMeshProUGUI CreateTmp(Transform parent, string value, float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Tmp", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateGreenishButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(220f, 80f);
            go.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.35f, 1f);
            CreateTmp(go.transform, label, 32f, Vector2.zero, new Vector2(200f, 70f));
            return go.GetComponent<Button>();
        }

        private void OpenNameEditor()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();
            EnsureNameEditUi();
            if (m_NameInput != null)
                m_NameInput.text = TournamentLiveOpService.GetOrCreatePlayerDisplayName();
            if (m_NameError != null)
                m_NameError.text = string.Empty;
            m_NameEditRoot.SetActive(true);
        }

        private void SaveName()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            string raw = m_NameInput != null ? m_NameInput.text : string.Empty;
            if (!TournamentLiveOpService.TrySetPlayerDisplayName(raw, out string error))
            {
                if (m_NameError != null) m_NameError.text = error;
                return;
            }

            m_NameEditRoot.SetActive(false);
            RefreshRows();
        }

        private void Close()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();
            Destroy(gameObject);
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
    }
}
