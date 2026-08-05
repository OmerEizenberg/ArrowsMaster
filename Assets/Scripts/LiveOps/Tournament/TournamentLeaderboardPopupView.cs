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
        private bool m_RowsBuilt;
        private bool m_DidFocusPlayer;
        private RectTransform m_PopupRect;
        private Coroutine m_FocusCoroutine;
        private string m_SelectedTagline;
        private string m_LastTimerText;

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

            if (m_NameEdit != null)
            {
                m_NameEdit.OnSaveClicked -= SaveName;
                m_NameEdit.OnCancelClicked -= OnNameEditCancelled;
            }
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
            m_SelectedTagline = TimerTaglines[Random.Range(0, TimerTaglines.Length)];
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
            if (service == null || m_RowsParent == null) return;
            EnsureRowsBuilt();

            List<TournamentLeaderboardRow> data = service.BuildLeaderboardRows(TrustedTimeService.UtcNow);
            int count = data != null ? data.Count : 0;
            int playerIndex = -1;

            for (int i = 0; i < m_Rows.Count; i++)
            {
                var view = m_Rows[i];
                if (view == null) continue;

                if (i < count)
                {
                    if (!view.gameObject.activeSelf)
                        view.gameObject.SetActive(true);

                    Reward reward = default;
                    if (service.Config != null)
                        service.Config.TryGetReward(data[i].Place - 1, out reward);

                    view.SetData(
                        data[i],
                        reward,
                        GetRewardSprite(reward.type, reward.amount),
                        PlayerRowColor,
                        BotRowColor);

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
                FocusRow(playerIndex);
            }
        }

        private IEnumerator FocusPlayerAfterLayout()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (m_ScrollRect != null && m_ScrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_ScrollRect.content);

            // Rows already populated in Initialize; only scroll to the player.
            if (!m_DidFocusPlayer)
            {
                for (int i = 0; i < m_Rows.Count; i++)
                {
                    if (m_Rows[i] != null && m_Rows[i].IsPlayerRow)
                    {
                        m_DidFocusPlayer = true;
                        FocusRow(i);
                        break;
                    }
                }
            }
        }

        private void FocusRow(int index)
        {
            if (m_ScrollRect == null || m_ScrollRect.content == null || m_Rows.Count == 0)
                return;
            if (index < 0 || index >= m_Rows.Count || m_Rows[index] == null)
                return;

            Canvas.ForceUpdateCanvases();
            var content = m_ScrollRect.content;
            var row = m_Rows[index].transform as RectTransform;
            if (row == null) return;

            float contentHeight = content.rect.height;
            float viewportHeight = m_ScrollRect.viewport != null ? m_ScrollRect.viewport.rect.height : 0f;
            if (contentHeight <= viewportHeight || contentHeight <= 0.01f)
            {
                m_ScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            float rowCenter = Mathf.Abs(row.anchoredPosition.y) + row.rect.height * 0.5f;
            float normalized = 1f - Mathf.Clamp01((rowCenter - viewportHeight * 0.5f) / (contentHeight - viewportHeight));
            m_ScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
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
