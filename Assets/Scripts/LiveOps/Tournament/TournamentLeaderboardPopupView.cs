using System;
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
    /// Layout/style lives entirely in Resources/TournamentLeaderboardPopup.prefab
    /// (+ TournamentLeaderboardRow.prefab). Runtime only updates text / row data.
    /// </summary>
    public class TournamentLeaderboardPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_CloseLabel;
        [SerializeField] private TextMeshProUGUI m_CloseLabelBg;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TitleBg;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private TextMeshProUGUI m_TimerTextBg;
        [SerializeField] private TournamentLeaderboardRowView m_RowPrefab;

        [Header("Table")]
        [SerializeField] private RectTransform m_TableRoot;
        [SerializeField] private ScrollRect m_ScrollRect;
        [SerializeField] private Image m_TableBackground;
        [SerializeField] private Transform m_RowsParent;

        [Header("Column Headers")]
        [SerializeField] private RectTransform m_ColumnHeadersRoot;
        [SerializeField] private TextMeshProUGUI m_PlaceHeaderText;
        [SerializeField] private TextMeshProUGUI m_PlaceHeaderTextBg;
        [SerializeField] private TextMeshProUGUI m_NameHeaderText;
        [SerializeField] private TextMeshProUGUI m_NameHeaderTextBg;
        [SerializeField] private TextMeshProUGUI m_RewardHeaderText;
        [SerializeField] private TextMeshProUGUI m_RewardHeaderTextBg;
        [SerializeField] private TextMeshProUGUI m_ScoreHeaderText;
        [SerializeField] private TextMeshProUGUI m_ScoreHeaderTextBg;

        [Header("Reward / score icons (Legend Pass style)")]
        [SerializeField] private Sprite m_CoinSprite;
        [SerializeField] private Sprite m_HintSprite;
        [SerializeField] private Sprite m_WandSprite;
        [SerializeField] private Sprite m_LifeSprite;
        [SerializeField] private Sprite m_GoldenArrowSprite;

        [Header("Name edit")]
        [SerializeField] private TournamentNameEditPopupView m_NameEditPrefab;
        private TournamentNameEditPopupView m_NameEdit;

        private static readonly Color PlayerRowColor = new Color(0.49f, 0.37f, 1f, 1f);
        private static readonly Color BotRowColor = Color.white;

        private const int ExpectedRows = 25;
        private const string RowPrefabResourcePath = "TournamentLeaderboardRow";
        private const string NameEditPrefabResourcePath = "TournamentNameEditPopup";

        private static readonly string[] TimerTaglines =
        {
            "DON'T GIVE UP",
            "HURRY UP",
            "KEEP CLIMBING",
            "PUSH HARDER",
            "THE RACE IS ON",
            "STAY SHARP",
            "EVERY ARROW COUNTS",
            "NO TIME TO REST",
            "CHASE THE LEAD",
            "FINISH STRONG"
        };

        private TournamentLiveOpService service;
        private float nextRefresh;
        private readonly List<TournamentLeaderboardRowView> m_Rows = new List<TournamentLeaderboardRowView>(ExpectedRows);
        private readonly List<TournamentLeaderboardRow> m_LeaderboardData = new List<TournamentLeaderboardRow>(ExpectedRows);
        private readonly List<TournamentLeaderboardRowView> m_OrderedRows = new List<TournamentLeaderboardRowView>(ExpectedRows);
        private readonly Dictionary<TournamentLeaderboardRowView, Vector2> m_StartPositions =
            new Dictionary<TournamentLeaderboardRowView, Vector2>(ExpectedRows);
        private readonly Dictionary<TournamentLeaderboardRowView, Vector2> m_EndPositions =
            new Dictionary<TournamentLeaderboardRowView, Vector2>(ExpectedRows);
        private bool m_RowsBuilt;
        private bool m_DidFocusPlayer;
        private RectTransform m_PopupRect;
        private Coroutine m_IntroCoroutine;
        private bool m_IntroPlaying;
        private bool m_RowsDirty = true;
        private string m_SelectedTagline;
        private string m_LastTimerText;
        private int m_LastBoardPlayerScore = int.MinValue;
        private long m_LastBoardSecond = -1;
        private float m_LastFollowContentY = float.NaN;

        private const float MaxRowsPerSecond = 8f;
        private const float MinIntroDuration = 0.4f;
        private const float ScoreOnlyDuration = 1.0f;

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
            WireTableRefsFromHierarchyIfNeeded();

            if (m_RowsParent == null || m_ScrollRect == null || m_ColumnHeadersRoot == null)
            {
                Debug.LogError(
                    "[TournamentLeaderboardPopupView] Table/headers missing on prefab. " +
                    "Run LiveOps/Tournament/Bake Table + Column Headers Into Leaderboard Popup.");
            }

            EnsureRewardSprites();
            WireCloseButton();
            EnsureNameEditUi();
            ApplyStaticCopy();

            if (service != null)
            {
                service.OnStateChanged -= OnServiceStateChanged;
                service.OnStateChanged += OnServiceStateChanged;
            }

            EnsureRowsBuilt();
            PickTagline();
            RefreshTimer();

            m_DidFocusPlayer = false;
            m_RowsDirty = true;
            if (m_IntroCoroutine != null)
                StopCoroutine(m_IntroCoroutine);

            m_IntroCoroutine = StartCoroutine(PlayIntroThenFocus());
        }

        private void OnServiceStateChanged()
        {
            m_RowsDirty = true;
            if (!m_IntroPlaying)
                RefreshRows();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnStateChanged -= OnServiceStateChanged;

            if (m_NameEdit != null)
            {
                m_NameEdit.OnSaveClicked -= SaveName;
                m_NameEdit.OnCancelClicked -= OnNameEditCancelled;
            }

            if (m_IntroCoroutine != null)
                StopCoroutine(m_IntroCoroutine);
            m_IntroPlaying = false;
        }

        private void Update()
        {
            if (service == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 2f;
            // Finalize is owned by LiveOpManager.
            if (service.Status == TournamentStatus.Finished || service.Status == TournamentStatus.PendingJoin)
            {
                Close();
                return;
            }
            if (!m_IntroPlaying)
                RefreshRows();
            RefreshTimer();
        }

        private void ResolveCoreRefs()
        {
            if (m_PopupRect == null)
            {
                var popup = FindDeep("Popup");
                if (popup != null)
                    m_PopupRect = popup as RectTransform;
            }

            if (m_CloseButton == null)
            {
                var t = FindDeep("GreenShadow");
                if (t != null) m_CloseButton = t.GetComponent<Button>();
            }

            if (m_CloseButton != null)
            {
                if (m_CloseLabel == null)
                    m_CloseLabel = FindDirectChildTmp(m_CloseButton.transform, "Text (TMP)")
                                   ?? FindDirectChildTmp(m_CloseButton.transform, "Text");
                if (m_CloseLabelBg == null)
                    m_CloseLabelBg = FindDirectChildTmp(m_CloseButton.transform, "TextBG");
            }

            if (m_Title == null || (m_Title != null && m_Title.gameObject.name == "TitleBG"))
            {
                var title = FindPopupTmp("Title");
                if (title != null)
                {
                    if (m_Title != null && m_Title.gameObject.name == "TitleBG" && m_TitleBg == null)
                        m_TitleBg = m_Title;
                    m_Title = title;
                }
            }

            if (m_TitleBg == null)
                m_TitleBg = FindPopupTmp("TitleBG");

            if (m_TimerText == null || (m_TimerText != null && m_TimerText.gameObject.name.StartsWith("Description") && m_TimerText.gameObject.name != "Description"))
            {
                var desc = FindPopupTmp("Description");
                if (desc != null)
                    m_TimerText = desc;
            }

            if (m_TimerTextBg == null)
            {
                m_TimerTextBg = FindPopupTmp("DescriptionBG")
                                ?? FindPopupTmp("Description (1)")
                                ?? FindPopupTmp("TimerBG");
            }
        }

        private TextMeshProUGUI FindPopupTmp(string objectName)
        {
            foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || tmp.gameObject.name != objectName)
                    continue;
                if (m_PopupRect != null && !tmp.transform.IsChildOf(m_PopupRect) && tmp.transform != m_PopupRect)
                    continue;
                return tmp;
            }
            return null;
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

        private void WireTableRefsFromHierarchyIfNeeded()
        {
            if (m_PopupRect == null) return;

            if (m_ColumnHeadersRoot == null)
            {
                var headers = m_PopupRect.Find("ColumnHeaders");
                if (headers != null)
                    WireHeadersFromHierarchy(headers);
            }

            if (m_ScrollRect == null || m_RowsParent == null)
            {
                var scroll = m_PopupRect.Find("LeaderboardScroll");
                if (scroll != null)
                    WireScrollFromHierarchy(scroll);
            }
        }

        private void WireScrollFromHierarchy(Transform scrollRoot)
        {
            m_TableRoot = scrollRoot as RectTransform;
            m_ScrollRect = scrollRoot.GetComponent<ScrollRect>();
            m_TableBackground = scrollRoot.GetComponent<Image>();

            Transform viewport = scrollRoot.Find("Viewport");
            Transform content = viewport != null ? viewport.Find("Content") : null;
            if (content != null)
                m_RowsParent = content;

            if (m_ScrollRect != null)
            {
                if (viewport != null)
                    m_ScrollRect.viewport = viewport as RectTransform;
                if (content != null)
                    m_ScrollRect.content = content as RectTransform;
            }
        }

        private void WireHeadersFromHierarchy(Transform headerRoot)
        {
            m_ColumnHeadersRoot = headerRoot as RectTransform;
            TryWireHeaderPair(headerRoot, "PlaceHeader", "#", ref m_PlaceHeaderText, ref m_PlaceHeaderTextBg);
            TryWireHeaderPair(headerRoot, "NameHeader", "Name", ref m_NameHeaderText, ref m_NameHeaderTextBg);
            TryWireHeaderPair(headerRoot, "RewardHeader", "Reward", ref m_RewardHeaderText, ref m_RewardHeaderTextBg);
            TryWireHeaderPair(headerRoot, "ScoreHeader", "Arrows", ref m_ScoreHeaderText, ref m_ScoreHeaderTextBg);
            if (m_ScoreHeaderText == null)
                TryWireHeaderPair(headerRoot, "ScoreHeader", "Score", ref m_ScoreHeaderText, ref m_ScoreHeaderTextBg);
        }

        private static void TryWireHeaderPair(
            Transform headerRoot,
            string columnName,
            string legacyName,
            ref TextMeshProUGUI main,
            ref TextMeshProUGUI bg)
        {
            Transform col = headerRoot.Find(columnName) ?? headerRoot.Find(legacyName);
            if (col == null) return;

            if (main == null)
            {
                var text = col.Find("Text");
                if (text != null)
                    main = text.GetComponent<TextMeshProUGUI>();
                if (main == null)
                    main = col.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (bg == null)
            {
                var textBg = col.Find("TextBG");
                if (textBg != null)
                    bg = textBg.GetComponent<TextMeshProUGUI>();
            }
        }

        private void ApplyStaticCopy()
        {
            SetPairedText(m_Title, m_TitleBg, "Golden Tournament");
            SetPairedText(m_CloseLabel, m_CloseLabelBg, "LET'S GO!");

            // Keep header TextBG shadows matching whatever is authored on the main header labels.
            SyncPairedFromMain(m_PlaceHeaderText, m_PlaceHeaderTextBg);
            SyncPairedFromMain(m_NameHeaderText, m_NameHeaderTextBg);
            SyncPairedFromMain(m_RewardHeaderText, m_RewardHeaderTextBg);
            SyncPairedFromMain(m_ScoreHeaderText, m_ScoreHeaderTextBg);
        }

        private static void SyncPairedFromMain(TextMeshProUGUI main, TextMeshProUGUI bg)
        {
            if (main == null || bg == null) return;
            bg.text = main.text;
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

        private void WireCloseButton()
        {
            if (m_CloseButton == null) return;
            m_CloseButton.onClick.RemoveAllListeners();
            m_CloseButton.onClick.AddListener(Close);
        }

        private void PickTagline()
        {
            m_SelectedTagline = TimerTaglines[UnityEngine.Random.Range(0, TimerTaglines.Length)];
        }

        private void RefreshTimer()
        {
            if (service == null || m_TimerText == null) return;

            var rem = service.GetRemainingTime();
            string text;
            if (rem.TotalSeconds <= 0)
            {
                text = "Finished";
            }
            else
            {
                string timeLeft = FormatTimeLeft(rem);
                if (string.IsNullOrEmpty(m_SelectedTagline))
                    PickTagline();
                text = $"<size=125%>{m_SelectedTagline}</size>\n<size=90%>{timeLeft} left</size>";
            }

            if (string.Equals(m_LastTimerText, text, System.StringComparison.Ordinal))
                return;

            m_LastTimerText = text;
            SetPairedText(m_TimerText, m_TimerTextBg, text);
        }

        private static void SetPairedText(TextMeshProUGUI main, TextMeshProUGUI bg, string value)
        {
            if (main != null)
                main.text = value;
            if (bg != null)
                bg.text = value;
        }

        private static string FormatTimeLeft(System.TimeSpan rem)
        {
            return TournamentUiFormat.FormatTimeLeft(rem);
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

            if (m_RowPrefab == null)
            {
                Debug.LogError(
                    "[TournamentLeaderboardPopupView] Missing TournamentLeaderboardRow prefab. " +
                    "Run LiveOps/Tournament/Build Leaderboard Row Prefab.");
                return;
            }

            for (int i = 0; i < ExpectedRows; i++)
            {
                var view = Instantiate(m_RowPrefab, m_RowsParent);
                view.gameObject.name = $"Row_{i}";
                if (view.Button != null)
                {
                    view.Button.onClick.RemoveAllListeners();
                    view.Button.onClick.AddListener(OpenNameEditor);
                }
                m_Rows.Add(view);
            }
        }

        private void RefreshRows()
        {
            if (service == null || m_RowsParent == null || m_IntroPlaying) return;
            EnsureRowsBuilt();

            DateTime now = TrustedTimeService.UtcNow;
            long second = now.Ticks / TimeSpan.TicksPerSecond;
            int playerScore = service.Progress != null ? service.Progress.PlayerScore : 0;
            if (!m_RowsDirty &&
                second == m_LastBoardSecond &&
                playerScore == m_LastBoardPlayerScore)
            {
                return;
            }

            m_RowsDirty = false;
            m_LastBoardSecond = second;
            m_LastBoardPlayerScore = playerScore;

            service.FillLeaderboardRows(now, m_LeaderboardData);
            ApplyRowData(m_LeaderboardData);

            if (!m_DidFocusPlayer)
            {
                int playerIndex = FindPlayerRowIndex();
                if (playerIndex >= 0)
                {
                    m_DidFocusPlayer = true;
                    FocusRow(playerIndex);
                }
            }
        }

        private IEnumerator PlayIntroThenFocus()
        {
            m_IntroPlaying = true;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (m_ScrollRect != null && m_ScrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_ScrollRect.content);

            List<TournamentLeaderboardRow> data = m_LeaderboardData;
            if (service != null)
                service.FillLeaderboardRows(TrustedTimeService.UtcNow, data);

            if (data == null || data.Count == 0 || service == null)
            {
                m_IntroPlaying = false;
                yield break;
            }

            int currentPlace = 1;
            int currentScore = 0;
            for (int i = 0; i < data.Count; i++)
            {
                if (!data[i].IsPlayer) continue;
                currentPlace = data[i].Place;
                currentScore = Mathf.Max(0, data[i].Score);
                break;
            }

            bool hasLast = service.TryGetLastShownPlayerState(out int lastPlace, out int lastScore);
            bool placeChanged = hasLast && lastPlace != currentPlace;
            bool scoreChanged = hasLast && lastScore != currentScore;

            ApplyRowData(data);
            int playerIndex = FindPlayerRowIndex();

            if (!hasLast || (!placeChanged && !scoreChanged))
            {
                service.MarkPlayerStateShown(currentPlace, currentScore);
                m_IntroPlaying = false;
                if (playerIndex >= 0)
                {
                    m_DidFocusPlayer = true;
                    FocusRow(playerIndex);
                }
                yield break;
            }

            TournamentLeaderboardRowView playerRow = playerIndex >= 0 ? m_Rows[playerIndex] : null;
            if (playerRow != null)
            {
                playerRow.SetPlaceDisplay(lastPlace);
                playerRow.SetScoreDisplay(lastScore);
            }

            if (placeChanged && playerIndex >= 0)
            {
                int fromIndex = Mathf.Clamp(lastPlace - 1, 0, m_Rows.Count - 1);
                int toIndex = playerIndex;
                yield return AnimatePlayerMoveAndScore(
                    playerRow,
                    fromIndex,
                    toIndex,
                    lastPlace,
                    currentPlace,
                    lastScore,
                    currentScore,
                    scoreChanged);
                m_DidFocusPlayer = true;
            }
            else if (scoreChanged && playerRow != null)
            {
                yield return AnimateScore(playerRow, lastScore, currentScore, ScoreOnlyDuration);
                playerRow.SetPlaceDisplay(currentPlace);
                if (playerIndex >= 0)
                {
                    m_DidFocusPlayer = true;
                    FocusRow(playerIndex);
                }
            }

            service.MarkPlayerStateShown(currentPlace, currentScore);
            m_IntroPlaying = false;

            if (!m_DidFocusPlayer)
            {
                playerIndex = FindPlayerRowIndex();
                if (playerIndex >= 0)
                {
                    m_DidFocusPlayer = true;
                    FocusRow(playerIndex);
                }
            }
            m_IntroCoroutine = null;
        }

        private IEnumerator AnimatePlayerMoveAndScore(
            TournamentLeaderboardRowView playerRow,
            int fromIndex,
            int toIndex,
            int fromPlace,
            int toPlace,
            int fromScore,
            int toScore,
            bool animateScore)
        {
            if (playerRow == null || m_RowsParent == null || fromIndex == toIndex)
            {
                if (animateScore && playerRow != null)
                    yield return AnimateScore(playerRow, fromScore, toScore, ScoreOnlyDuration);
                if (playerRow != null)
                    playerRow.SetPlaceDisplay(toPlace);
                yield break;
            }

            var layout = m_RowsParent.GetComponent<VerticalLayoutGroup>();
            bool layoutWasEnabled = layout != null && layout.enabled;

            // Capture final (current) positions while layout is on.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_RowsParent as RectTransform);
            CaptureRowPositions(m_EndPositions);

            // Build start sibling order: player appears at last shown place.
            m_OrderedRows.Clear();
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].gameObject.activeSelf)
                    m_OrderedRows.Add(m_Rows[i]);
            }

            if (toIndex < 0 || toIndex >= m_OrderedRows.Count)
            {
                if (animateScore)
                    yield return AnimateScore(playerRow, fromScore, toScore, ScoreOnlyDuration);
                playerRow.SetPlaceDisplay(toPlace);
                yield break;
            }

            fromIndex = Mathf.Clamp(fromIndex, 0, m_OrderedRows.Count - 1);
            int rowsMoved = Mathf.Abs(toIndex - fromIndex);
            float duration = Mathf.Max(MinIntroDuration, rowsMoved / MaxRowsPerSecond);

            var moving = m_OrderedRows[toIndex];
            m_OrderedRows.RemoveAt(toIndex);
            m_OrderedRows.Insert(fromIndex, moving);

            for (int i = 0; i < m_OrderedRows.Count; i++)
                m_OrderedRows[i].transform.SetSiblingIndex(i);

            for (int i = 0; i < m_OrderedRows.Count; i++)
                m_OrderedRows[i].SetPlaceDisplay(i + 1);
            playerRow.SetScoreDisplay(fromScore);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_RowsParent as RectTransform);
            CaptureRowPositions(m_StartPositions);

            // Restore final sibling order, then free-move from start → end.
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].gameObject.activeSelf)
                    m_Rows[i].transform.SetSiblingIndex(i);
            }

            if (layout != null)
                layout.enabled = false;

            ApplyCapturedPositions(m_StartPositions);
            // Draw player above every other row for the whole travel (once — not per frame).
            if (playerRow != null)
                playerRow.transform.SetAsLastSibling();

            RectTransform contentRt = ResolveScrollContent();
            RectTransform viewportRt = m_ScrollRect != null ? m_ScrollRect.viewport : null;
            float contentHeight = contentRt != null ? contentRt.rect.height : 0f;
            float viewportHeight = viewportRt != null ? viewportRt.rect.height : 0f;
            float maxScrollY = Mathf.Max(0f, contentHeight - viewportHeight);
            m_LastFollowContentY = float.NaN;

            bool scrollWasEnabled = m_ScrollRect != null && m_ScrollRect.enabled;
            if (m_ScrollRect != null)
            {
                m_ScrollRect.StopMovement();
                m_ScrollRect.enabled = false; // prevent ScrollRect LateUpdate from fighting Content.y
            }

            ScrollContentToFollowPlayer(playerRow, contentRt, maxScrollY, viewportHeight);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = EaseInOutCubic(u);

                for (int i = 0; i < m_Rows.Count; i++)
                {
                    var row = m_Rows[i];
                    if (row == null || !row.gameObject.activeSelf) continue;
                    if (!m_StartPositions.TryGetValue(row, out var start)) continue;
                    if (!m_EndPositions.TryGetValue(row, out var end)) continue;
                    var rt = row.transform as RectTransform;
                    if (rt != null)
                        rt.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
                }

                if (playerRow != null)
                {
                    if (animateScore)
                    {
                        int score = Mathf.RoundToInt(Mathf.Lerp(fromScore, toScore, eased));
                        playerRow.SetScoreDisplay(score);
                    }

                    int place = fromPlace;
                    if (u >= 0.35f)
                    {
                        float placeU = Mathf.Clamp01((u - 0.35f) / 0.65f);
                        place = Mathf.RoundToInt(Mathf.Lerp(fromPlace, toPlace, EaseInOutCubic(placeU)));
                    }
                    playerRow.SetPlaceDisplay(place);
                }

                ScrollContentToFollowPlayer(playerRow, contentRt, maxScrollY, viewportHeight);
                yield return null;
            }

            ApplyCapturedPositions(m_EndPositions);
            ScrollContentToFollowPlayer(playerRow, contentRt, maxScrollY, viewportHeight);

            // Restore place order before layout turns back on.
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].gameObject.activeSelf)
                    m_Rows[i].transform.SetSiblingIndex(i);
            }

            if (layout != null)
                layout.enabled = layoutWasEnabled;

            if (m_ScrollRect != null)
            {
                SyncScrollRectFromContentY(contentRt, maxScrollY);
                m_ScrollRect.enabled = scrollWasEnabled;
            }

            if (service != null)
            {
                service.FillLeaderboardRows(TrustedTimeService.UtcNow, m_LeaderboardData);
                ApplyRowData(m_LeaderboardData);
            }
        }

        private IEnumerator AnimateScore(
            TournamentLeaderboardRowView playerRow,
            int fromScore,
            int toScore,
            float duration)
        {
            if (playerRow == null || fromScore == toScore)
            {
                if (playerRow != null)
                    playerRow.SetScoreDisplay(toScore);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = EaseInOutCubic(Mathf.Clamp01(t / duration));
                int score = Mathf.RoundToInt(Mathf.Lerp(fromScore, toScore, u));
                playerRow.SetScoreDisplay(score);
                yield return null;
            }

            playerRow.SetScoreDisplay(toScore);
        }

        private void CaptureRowPositions(Dictionary<TournamentLeaderboardRowView, Vector2> map)
        {
            map.Clear();
            for (int i = 0; i < m_Rows.Count; i++)
            {
                var row = m_Rows[i];
                if (row == null || !row.gameObject.activeSelf) continue;
                var rt = row.transform as RectTransform;
                if (rt != null)
                    map[row] = rt.anchoredPosition;
            }
        }

        private static void ApplyCapturedPositions(Dictionary<TournamentLeaderboardRowView, Vector2> positions)
        {
            if (positions == null) return;
            foreach (var kv in positions)
            {
                if (kv.Key == null) continue;
                var rt = kv.Key.transform as RectTransform;
                if (rt != null)
                    rt.anchoredPosition = kv.Value;
            }
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private void ApplyRowData(List<TournamentLeaderboardRow> data)
        {
            int count = data != null ? data.Count : 0;

            for (int i = 0; i < m_Rows.Count; i++)
            {
                var view = m_Rows[i];
                if (view == null) continue;

                if (i < count)
                {
                    if (!view.gameObject.activeSelf)
                        view.gameObject.SetActive(true);

                    Reward reward = default;
                    if (service != null && service.Config != null)
                        service.Config.TryGetReward(data[i].Place - 1, out reward);

                    view.SetData(
                        data[i],
                        reward,
                        GetRewardSprite(reward.type, reward.amount),
                        PlayerRowColor,
                        BotRowColor);
                }
                else if (view.gameObject.activeSelf)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }

        private int FindPlayerRowIndex()
        {
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] != null && m_Rows[i].IsPlayerRow)
                    return i;
            }
            return -1;
        }

        private void FocusRow(int index)
        {
            if (index < 0 || index >= m_Rows.Count || m_Rows[index] == null)
                return;
            FocusRowView(m_Rows[index]);
        }

        private void FocusRowView(TournamentLeaderboardRowView rowView)
        {
            RectTransform contentRt = ResolveScrollContent();
            if (contentRt == null) return;
            float viewportHeight = m_ScrollRect != null && m_ScrollRect.viewport != null
                ? m_ScrollRect.viewport.rect.height
                : 0f;
            float maxScrollY = Mathf.Max(0f, contentRt.rect.height - viewportHeight);
            ScrollContentToFollowPlayer(rowView, contentRt, maxScrollY, viewportHeight);
            SyncScrollRectFromContentY(contentRt, maxScrollY);
        }

        private RectTransform ResolveScrollContent()
        {
            if (m_ScrollRect != null && m_ScrollRect.content != null)
                return m_ScrollRect.content;
            return m_RowsParent as RectTransform;
        }

        /// <summary>
        /// Drives LeaderboardScroll Content.anchoredPosition.y so the player stays in the
        /// middle of the viewport when possible. Near the top/bottom of the list the content
        /// clamps and the row continues alone (sticky middle follow).
        /// </summary>
        private void ScrollContentToFollowPlayer(
            TournamentLeaderboardRowView rowView,
            RectTransform content,
            float maxScrollY,
            float viewportHeight)
        {
            if (rowView == null || content == null || viewportHeight <= 0.01f)
                return;

            var row = rowView.transform as RectTransform;
            if (row == null) return;

            // Row center in content-local space (content pivot is top).
            Vector3 rowCenterWorld = row.TransformPoint(row.rect.center);
            Vector3 rowCenterInContent = content.InverseTransformPoint(rowCenterWorld);
            float rowCenterFromTop = -rowCenterInContent.y;

            // Desired content Y that would put the row in the vertical middle.
            // Clamp: stay at top until the row reaches mid, then follow, then pin at bottom.
            float desiredY = rowCenterFromTop - viewportHeight * 0.5f;
            float targetY = Mathf.Clamp(desiredY, 0f, maxScrollY);
            if (!float.IsNaN(m_LastFollowContentY) && Mathf.Abs(m_LastFollowContentY - targetY) < 0.05f)
                return;

            Vector2 pos = content.anchoredPosition;
            pos.y = targetY;
            content.anchoredPosition = pos;
            m_LastFollowContentY = targetY;
        }

        private void SyncScrollRectFromContentY(RectTransform content, float maxScrollY)
        {
            if (m_ScrollRect == null || content == null) return;
            m_ScrollRect.StopMovement();
            if (maxScrollY <= 0.01f)
            {
                m_ScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            float y = content.anchoredPosition.y;
            m_ScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(y / maxScrollY);
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
            if (m_NameEdit != null) return;

            if (m_NameEditPrefab == null)
            {
                var loadedGo = Resources.Load<GameObject>(NameEditPrefabResourcePath);
                if (loadedGo != null)
                    m_NameEditPrefab = loadedGo.GetComponent<TournamentNameEditPopupView>();
            }

            if (m_NameEditPrefab != null)
            {
                m_NameEdit = Instantiate(m_NameEditPrefab, transform);
                m_NameEdit.gameObject.name = "TournamentNameEditPopup";
            }
            else
            {
                Debug.LogWarning("[TournamentLeaderboardPopupView] Name-edit prefab missing.");
                return;
            }

            m_NameEdit.OnSaveClicked -= SaveName;
            m_NameEdit.OnSaveClicked += SaveName;
            m_NameEdit.OnCancelClicked -= OnNameEditCancelled;
            m_NameEdit.OnCancelClicked += OnNameEditCancelled;
            m_NameEdit.WireButtons();
            m_NameEdit.Hide();
        }

        private void OpenNameEditor()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();
            EnsureNameEditUi();
            if (m_NameEdit != null)
                m_NameEdit.Show(TournamentLiveOpService.GetOrCreatePlayerDisplayName());
        }

        private void OnNameEditCancelled()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();
        }

        private void SaveName()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            if (m_NameEdit == null) return;

            string raw = m_NameEdit.InputText;
            if (!TournamentLiveOpService.TrySetPlayerDisplayName(raw, out string error))
            {
                m_NameEdit.SetError(error);
                return;
            }

            m_NameEdit.Hide();
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
