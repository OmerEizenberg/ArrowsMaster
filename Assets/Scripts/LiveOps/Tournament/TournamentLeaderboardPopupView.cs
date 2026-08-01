using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Tournament leaderboard popup.
    /// Strips leftover Daily-Missions chrome from the cloned prefab, then hosts a
    /// scrollable 25-row table that updates in place and focuses the player on open.
    /// </summary>
    public class TournamentLeaderboardPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private Transform m_RowsParent;
        [SerializeField] private ScrollRect m_ScrollRect;
        [SerializeField] private TournamentLeaderboardRowView m_RowPrefab;

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
        private static readonly Color PlaceColColor = new Color(0.95f, 0.75f, 0.2f, 0.45f);
        private static readonly Color NameColColor = new Color(0.2f, 0.55f, 0.75f, 0.35f);
        private static readonly Color RewardColColor = new Color(0.35f, 0.7f, 0.4f, 0.35f);
        private static readonly Color ScoreColColor = new Color(0.85f, 0.55f, 0.15f, 0.35f);
        private static readonly Color PlaceTextColor = new Color(1f, 0.88f, 0.35f, 1f);
        private static readonly Color NameTextColor = Color.white;
        private static readonly Color PlayerNameTextColor = new Color(0.65f, 0.9f, 1f, 1f);
        private static readonly Color RewardTextColor = new Color(0.85f, 1f, 0.85f, 1f);
        private static readonly Color ScoreTextColor = new Color(1f, 0.9f, 0.45f, 1f);

        private const float RowHeight = 216f;
        private const int ExpectedRows = 25;
        private const string RowPrefabResourcePath = "TournamentLeaderboardRow";

        private static readonly string[] TimerTaglines =
        {
            "Don't give up, {0} left!",
            "Hurry up, {0} left!",
            "Keep climbing, {0} left!",
            "Push harder, {0} remaining!",
            "The race is on — {0} left!",
            "Stay sharp, {0} left!",
            "Every arrow counts — {0} left!",
            "No time to rest, {0} left!",
            "Chase the lead, {0} remaining!",
            "Finish strong, {0} left!"
        };

        private static readonly HashSet<string> KeepUnderPopup = new HashSet<string>
        {
            "Title", "Description", "GreenShadow", "LeaderboardScroll", "ColumnHeaders", "PurpleBG", "Popup"
        };

        private TournamentLiveOpService service;
        private float nextRefresh;
        private TMP_FontAsset rowFont;
        private Sprite rowBgSprite;
        private readonly List<TournamentLeaderboardRowView> m_Rows = new List<TournamentLeaderboardRowView>(ExpectedRows);
        private bool m_RowsBuilt;
        private bool m_DidFocusPlayer;
        private RectTransform m_PopupRect;
        private Coroutine m_FocusCoroutine;
        private string m_SelectedTaglineFormat;

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
            ResolveCoreRefs();
            CacheVisuals();
            SanitizeMissionLeftovers();
            EnsurePurpleTitleBar();
            EnsureColumnHeaders();
            EnsureScrollArea();
            EnsureRewardSprites();
            WireButtons();
            EnsureNameEditUi();
            LayoutHeaderAndButton();

            if (m_Title != null)
                m_Title.text = "Golden Tournament";

            if (service != null)
            {
                service.OnStateChanged -= RefreshRows;
                service.OnStateChanged += RefreshRows;
            }

            EnsureRowsBuilt();
            RefreshRows();
            PickTagline();
            RefreshTimer();

            m_DidFocusPlayer = false;
            if (m_FocusCoroutine != null)
                StopCoroutine(m_FocusCoroutine);
            m_FocusCoroutine = StartCoroutine(FocusPlayerAfterLayout());
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

        private void ResolveCoreRefs()
        {
            if (m_CloseButton == null)
            {
                var t = FindDeep("GreenShadow");
                if (t != null) m_CloseButton = t.GetComponent<Button>();
            }
            if (m_Title == null)
            {
                var t = FindDeep("Title");
                // Prefer exact "Title" under Popup (not Title (1)/(2)/(3) from mission slots).
                if (t != null && t.name == "Title")
                    m_Title = t.GetComponent<TextMeshProUGUI>();
                else
                {
                    foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (tmp != null && tmp.gameObject.name == "Title")
                        {
                            m_Title = tmp;
                            break;
                        }
                    }
                }
            }
            if (m_TimerText == null)
            {
                var t = FindDeep("Description");
                if (t != null) m_TimerText = t.GetComponent<TextMeshProUGUI>();
            }

            var popup = FindDeep("Popup");
            if (popup != null)
                m_PopupRect = popup as RectTransform;
        }

        private void CacheVisuals()
        {
            if (m_Title != null)
                rowFont = m_Title.font;

            if (m_PopupRect != null)
            {
                var img = m_PopupRect.GetComponent<Image>();
                if (img != null) rowBgSprite = img.sprite;
            }
        }

        /// <summary>
        /// Removes Daily Missions children (Mission rows, PurpleBG, ReelBG, icons, etc.)
        /// so only tournament-relevant chrome remains.
        /// </summary>
        private void SanitizeMissionLeftovers()
        {
            if (m_PopupRect == null) return;

            for (int i = m_PopupRect.childCount - 1; i >= 0; i--)
            {
                Transform child = m_PopupRect.GetChild(i);
                if (child == null) continue;
                if (KeepUnderPopup.Contains(child.name))
                    continue;

                // Keep GreenShadow / Title / Description only.
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // Kill any leftover mission scripts on this popup.
            foreach (var slot in GetComponentsInChildren<Assets.Scripts.LiveOps.Missions.MissionSlotView>(true))
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(false);
                    Destroy(slot.gameObject);
                }
            }
            foreach (var holder in GetComponentsInChildren<Assets.Scripts.LiveOps.Missions.MissionsHolderView>(true))
            {
                if (holder != null)
                {
                    holder.gameObject.SetActive(false);
                    Destroy(holder.gameObject);
                }
            }
        }

        private void EnsureScrollArea()
        {
            if (m_PopupRect == null) return;

            Transform existing = m_PopupRect.Find("LeaderboardScroll");
            GameObject scrollGo;
            if (existing != null)
            {
                scrollGo = existing.gameObject;
            }
            else
            {
                scrollGo = new GameObject("LeaderboardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                scrollGo.transform.SetParent(m_PopupRect, false);
            }

            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            // Below column headers, above green button.
            scrollRectTransform.anchorMin = new Vector2(0.04f, 0.18f);
            scrollRectTransform.anchorMax = new Vector2(0.96f, 0.72f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var scrollImage = scrollGo.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0.15f);
            scrollImage.raycastTarget = true;

            m_ScrollRect = scrollGo.GetComponent<ScrollRect>();
            m_ScrollRect.horizontal = false;
            m_ScrollRect.vertical = true;
            m_ScrollRect.movementType = ScrollRect.MovementType.Clamped;
            m_ScrollRect.scrollSensitivity = 40f;

            // Viewport
            Transform vpTransform = scrollGo.transform.Find("Viewport");
            GameObject viewportGo;
            if (vpTransform == null)
            {
                viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewportGo.transform.SetParent(scrollGo.transform, false);
            }
            else
            {
                viewportGo = vpTransform.gameObject;
                if (viewportGo.GetComponent<RectMask2D>() == null)
                    viewportGo.AddComponent<RectMask2D>();
                if (viewportGo.GetComponent<Image>() == null)
                    viewportGo.AddComponent<Image>();
            }

            var vpRect = viewportGo.GetComponent<RectTransform>();
            Stretch(vpRect);
            var vpImage = viewportGo.GetComponent<Image>();
            vpImage.color = new Color(1f, 1f, 1f, 0.01f);
            vpImage.raycastTarget = true;

            // Content
            Transform contentTransform = viewportGo.transform.Find("Content");
            GameObject contentGo;
            if (contentTransform == null)
            {
                contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
            }
            else
            {
                contentGo = contentTransform.gameObject;
                if (contentGo.GetComponent<VerticalLayoutGroup>() == null)
                    contentGo.AddComponent<VerticalLayoutGroup>();
                if (contentGo.GetComponent<ContentSizeFitter>() == null)
                    contentGo.AddComponent<ContentSizeFitter>();
            }

            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
            contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);

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

            m_ScrollRect.viewport = vpRect;
            m_ScrollRect.content = contentRect;
            m_RowsParent = contentGo.transform;

            // If we previously pointed at MissionsHolder, drop it.
            var oldHolder = FindDeep("MissionsHolder");
            if (oldHolder != null)
                Destroy(oldHolder.gameObject);
        }

        private void EnsurePurpleTitleBar()
        {
            if (m_PopupRect == null) return;

            Transform existing = m_PopupRect.Find("PurpleBG");
            GameObject purpleGo;
            if (existing != null)
            {
                purpleGo = existing.gameObject;
                purpleGo.SetActive(true);
            }
            else
            {
                purpleGo = new GameObject("PurpleBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                purpleGo.transform.SetParent(m_PopupRect, false);
                purpleGo.transform.SetAsFirstSibling();
            }

            var rt = purpleGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 280f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

            var img = purpleGo.GetComponent<Image>();
            img.sprite = rowBgSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.49019608f, 0.37254903f, 1f, 1f);
            img.raycastTarget = false;
        }

        private void EnsureColumnHeaders()
        {
            if (m_PopupRect == null) return;

            Transform existing = m_PopupRect.Find("ColumnHeaders");
            GameObject headerGo;
            if (existing != null)
            {
                headerGo = existing.gameObject;
            }
            else
            {
                headerGo = new GameObject("ColumnHeaders", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                headerGo.transform.SetParent(m_PopupRect, false);
            }

            var rt = headerGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.72f);
            rt.anchorMax = new Vector2(0.96f, 0.78f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var hlg = headerGo.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 0, 0);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // Rebuild labels once.
            for (int i = headerGo.transform.childCount - 1; i >= 0; i--)
                Destroy(headerGo.transform.GetChild(i).gameObject);

            CreateHeaderLabel(headerGo.transform, "#", 120f, 0f, PlaceTextColor);
            CreateHeaderLabel(headerGo.transform, "Name", 0f, 1f, NameTextColor);
            CreateHeaderLabel(headerGo.transform, "Reward", 160f, 0f, RewardTextColor);
            CreateHeaderLabel(headerGo.transform, "Arrows", 160f, 0f, ScoreTextColor);
        }

        private void CreateHeaderLabel(Transform parent, string text, float preferredWidth, float flexibleWidth, Color color)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.minWidth = preferredWidth > 0f ? preferredWidth * 0.5f : 80f;

            var tmpGo = new GameObject("Text", typeof(RectTransform));
            tmpGo.transform.SetParent(go.transform, false);
            Stretch(tmpGo.GetComponent<RectTransform>());
            var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
            tmp.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = 32f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void LayoutHeaderAndButton()
        {
            // Title sits on purple banner.
            if (m_Title != null)
            {
                var rt = m_Title.rectTransform;
                rt.SetAsLastSibling();
                rt.anchorMin = new Vector2(0.05f, 0.90f);
                rt.anchorMax = new Vector2(0.95f, 0.98f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                m_Title.alignment = TextAlignmentOptions.Center;
                m_Title.fontSize = 64f;
                m_Title.color = Color.white;
            }

            if (m_TimerText != null)
            {
                var rt = m_TimerText.rectTransform;
                rt.SetAsLastSibling();
                rt.anchorMin = new Vector2(0.05f, 0.82f);
                rt.anchorMax = new Vector2(0.95f, 0.90f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                m_TimerText.alignment = TextAlignmentOptions.Center;
                m_TimerText.fontSize = 34f;
                m_TimerText.color = Color.white;
            }

            if (m_CloseButton != null)
            {
                var rt = m_CloseButton.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 48f);
                    rt.sizeDelta = new Vector2(512f, 144f);
                }
            }
        }

        private void EnsureRewardSprites()
        {
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

        private void WireButtons()
        {
            if (m_CloseButton != null)
            {
                m_CloseButton.onClick.RemoveAllListeners();
                m_CloseButton.onClick.AddListener(Close);
                var labels = m_CloseButton.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var label in labels)
                {
                    if (label != null)
                        label.text = "Let's Go!";
                }
            }
        }

        private void PickTagline()
        {
            m_SelectedTaglineFormat = TimerTaglines[Random.Range(0, TimerTaglines.Length)];
        }

        private void RefreshTimer()
        {
            if (service == null || m_TimerText == null) return;

            var rem = service.GetRemainingTime();
            if (rem.TotalSeconds <= 0)
            {
                m_TimerText.text = "Finished";
                return;
            }

            string timeLeft = FormatTimeLeft(rem);
            if (string.IsNullOrEmpty(m_SelectedTaglineFormat))
                PickTagline();

            m_TimerText.text = string.Format(m_SelectedTaglineFormat, timeLeft);
        }

        private static string FormatTimeLeft(System.TimeSpan rem)
        {
            if (rem.TotalDays >= 1)
                return $"{(int)rem.TotalDays}d {rem.Hours}h";
            if (rem.TotalHours >= 1)
                return $"{(int)rem.TotalHours}h {rem.Minutes}m";
            return $"{Mathf.Max(1, rem.Minutes)}m";
        }

        private void EnsureRowsBuilt()
        {
            if (m_RowsBuilt || m_RowsParent == null) return;
            m_RowsBuilt = true;

            if (m_RowPrefab == null)
            {
                var loadedGo = Resources.Load<GameObject>(RowPrefabResourcePath);
                if (loadedGo != null)
                    m_RowPrefab = loadedGo.GetComponent<TournamentLeaderboardRowView>();
            }

            for (int i = 0; i < ExpectedRows; i++)
                m_Rows.Add(CreateRowInstance(i));
        }

        private TournamentLeaderboardRowView CreateRowInstance(int index)
        {
            TournamentLeaderboardRowView view;
            if (m_RowPrefab != null)
            {
                view = Instantiate(m_RowPrefab, m_RowsParent);
                view.gameObject.name = $"Row_{index}";
            }
            else
            {
                // Fallback until prefab is built via LiveOps/Tournament/Build Leaderboard Row Prefab.
                view = TournamentLeaderboardRowFactory.Create(m_RowsParent, rowFont, rowBgSprite, RowHeight);
                view.gameObject.name = $"Row_{index}";
                Debug.LogWarning("[TournamentLeaderboardPopupView] Row prefab missing — using runtime factory. Run LiveOps/Tournament/Build Leaderboard Row Prefab.");
            }

            var le = view.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = RowHeight;
                le.preferredHeight = RowHeight;
                le.flexibleWidth = 1f;
            }

            return view;
        }

        private void RefreshRows()
        {
            if (service == null || m_RowsParent == null) return;
            EnsureRowsBuilt();

            List<TournamentLeaderboardRow> data = service.BuildLeaderboardRows(TrustedTimeService.UtcNow);
            int count = Mathf.Min(data.Count, m_Rows.Count);
            int playerIndex = -1;

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
                        PlayerRowColor,
                        BotRowColor);

                    if (view.NameText != null)
                        view.NameText.color = data[i].IsPlayer ? PlayerNameTextColor : NameTextColor;

                    if (view.Button != null)
                    {
                        view.Button.onClick.RemoveAllListeners();
                        if (data[i].IsPlayer)
                            view.Button.onClick.AddListener(OpenNameEditor);
                    }

                    if (data[i].IsPlayer)
                        playerIndex = i;
                }
                else if (view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }

            if (!m_DidFocusPlayer && playerIndex >= 0)
            {
                m_DidFocusPlayer = true;
                if (m_FocusCoroutine != null)
                    StopCoroutine(m_FocusCoroutine);
                m_FocusCoroutine = StartCoroutine(FocusPlayerAfterLayout(playerIndex));
            }
        }

        private IEnumerator FocusPlayerAfterLayout()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();

            int playerIndex = -1;
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].gameObject.activeSelf && m_Rows[i].IsPlayerRow)
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex >= 0)
                ScrollToPlayerIndex(playerIndex);
        }

        private IEnumerator FocusPlayerAfterLayout(int playerIndex)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            ScrollToPlayerIndex(playerIndex);
        }

        private void ScrollToPlayerIndex(int playerIndex)
        {
            if (m_ScrollRect == null || m_ScrollRect.content == null || m_Rows.Count == 0)
                return;

            int activeCount = 0;
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].gameObject.activeSelf)
                    activeCount++;
            }
            if (activeCount <= 1)
            {
                m_ScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            playerIndex = Mathf.Clamp(playerIndex, 0, activeCount - 1);

            // 1 = top, 0 = bottom. Center player when possible.
            float t = activeCount <= 1 ? 0f : (float)playerIndex / (activeCount - 1);
            float normalized = 1f - t;
            m_ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
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
