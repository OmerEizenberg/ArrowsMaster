using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven leaderboard (MissionsPopup shell + NativeBG rows).
    /// </summary>
    public class TournamentLeaderboardPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private Transform m_RowsParent;
        [SerializeField] private GameObject m_NameEditRoot;
        [SerializeField] private TMP_InputField m_NameInput;
        [SerializeField] private TextMeshProUGUI m_NameError;
        [SerializeField] private Button m_SaveNameButton;
        [SerializeField] private Button m_CancelNameButton;

        private TournamentLiveOpService service;
        private float nextRefresh;
        private TMP_FontAsset rowFont;
        private Sprite rowBgSprite;

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
            WireButtons();
            EnsureNameEditUi();

            if (m_Title != null)
                m_Title.text = "Golden Tournament";

            if (service != null)
            {
                service.OnStateChanged -= RebuildRows;
                service.OnStateChanged += RebuildRows;
            }

            RebuildRows();
            RefreshTimer();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnStateChanged -= RebuildRows;
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
            RebuildRows();
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
                    // Clear mission slot children — we rebuild tournament rows.
                    for (int i = holder.childCount - 1; i >= 0; i--)
                        Destroy(holder.GetChild(i).gameObject);
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
                : $"Ends in {(int)rem.TotalHours}h {rem.Minutes}m  •  Tap your row to edit name";
        }

        private void RebuildRows()
        {
            if (service == null || m_RowsParent == null) return;

            for (int i = m_RowsParent.childCount - 1; i >= 0; i--)
                Destroy(m_RowsParent.GetChild(i).gameObject);

            List<TournamentLeaderboardRow> rows = service.BuildLeaderboardRows(TrustedTimeService.UtcNow);
            for (int i = 0; i < rows.Count; i++)
                CreateRow(rows[i], service.GetRewardKeyForPlace(rows[i].Place - 1));
        }

        private void CreateRow(TournamentLeaderboardRow row, string rewardKey)
        {
            var go = new GameObject($"Row_{row.Place}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(m_RowsParent, false);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 110f;
            le.preferredHeight = 110f;

            var img = go.GetComponent<Image>();
            img.sprite = rowBgSprite;
            img.type = Image.Type.Sliced;
            img.color = row.IsPlayer
                ? new Color(0.49f, 0.37f, 1f, 1f)
                : new Color(0.35f, 0.35f, 0.4f, 0.85f);

            string rewardLabel = FormatRewardShort(rewardKey);
            string line = $"{rewardLabel}   #{row.Place}   {Truncate(row.Name, 14)}   {row.Score}";
            var tmpGo = new GameObject("Text", typeof(RectTransform));
            tmpGo.transform.SetParent(go.transform, false);
            var rect = tmpGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 8f);
            rect.offsetMax = new Vector2(-24f, -8f);
            var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
            tmp.font = rowFont != null ? rowFont : TMP_Settings.defaultFontAsset;
            tmp.text = line;
            tmp.fontSize = 34f;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            if (row.IsPlayer)
                btn.onClick.AddListener(OpenNameEditor);
            else
                btn.interactable = false;
        }

        private void EnsureNameEditUi()
        {
            if (m_NameEditRoot != null) return;

            m_NameEditRoot = new GameObject("NameEditOverlay", typeof(RectTransform), typeof(Image));
            m_NameEditRoot.transform.SetParent(transform, false);
            var overlayRect = m_NameEditRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
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

            var title = CreateTmp(box.transform, "Edit your name", 40f, new Vector2(0f, 140f), new Vector2(640f, 50f));

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
            taRect.anchorMin = Vector2.zero;
            taRect.anchorMax = Vector2.one;
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
            var tmp = CreateTmp(go.transform, label, 32f, Vector2.zero, new Vector2(200f, 70f));
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
            RebuildRows();
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

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private static string FormatRewardShort(string key)
        {
            var reward = TournamentConfigSO.ParseReward(key);
            if (reward.amount <= 0) return "-";
            switch (reward.type)
            {
                case RewardType.Coin: return $"C{reward.amount}";
                case RewardType.Hint: return $"H{reward.amount}";
                case RewardType.MagicWand: return $"MW{reward.amount}";
                case RewardType.RefillLife: return $"L{reward.amount}";
                default: return "-";
            }
        }
    }
}
