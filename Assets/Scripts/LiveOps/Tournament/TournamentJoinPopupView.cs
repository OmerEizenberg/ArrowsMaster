using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven join popup (Resources/TournamentJoinPopup).
    /// Runtime only fills text + wires the Join button. Layout/style stay in the prefab.
    /// </summary>
    public class TournamentJoinPopupView : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button m_JoinButton;
        [SerializeField] private Button m_DimCloseButton;

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TitleBg;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private TextMeshProUGUI m_TimerTextBg;

        [Header("Description")]
        [SerializeField] private TextMeshProUGUI m_Description;
        [SerializeField] private TextMeshProUGUI m_DescriptionBg;

        [Header("Join label")]
        [SerializeField] private TextMeshProUGUI m_JoinLabel;
        [SerializeField] private TextMeshProUGUI m_JoinLabelBg;

        private TournamentLiveOpService service;
        private float nextRefresh;
        private string m_LastTimerText;

        public static void Show(TournamentLiveOpService service)
        {
            if (service == null) return;

            GameObject prefab = Resources.Load<GameObject>("TournamentJoinPopup");
            if (prefab == null)
            {
                Debug.LogError("[TournamentJoinPopupView] Missing Resources/TournamentJoinPopup.prefab");
                return;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = "TournamentJoinPopup";
            instance.SetActive(true);

            var view = instance.GetComponent<TournamentJoinPopupView>();
            if (view == null)
                view = instance.AddComponent<TournamentJoinPopupView>();
            view.Initialize(service);
        }

        public void Initialize(TournamentLiveOpService tournamentService)
        {
            service = tournamentService;
            ResolveRefsIfNeeded();
            ApplyStaticCopy();
            WireButtons();
            RefreshTimer();
        }

        private void Update()
        {
            if (service == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 1f;
            RefreshTimer();
        }

        private void ResolveRefsIfNeeded()
        {
            // Prefer inspector wiring on the prefab. Only fill missing refs by name.
            if (m_JoinButton == null)
            {
                var green = FindDeep("GreenShadow");
                if (green != null)
                    m_JoinButton = green.GetComponent<Button>();
            }

            if (m_Title == null)
                m_Title = FindTmp("Title");
            if (m_TitleBg == null)
                m_TitleBg = FindTmp("TitleBG");

            if (m_TimerText == null)
                m_TimerText = FindTmp("Timer") ?? FindTmp("Title (1)");
            if (m_TimerTextBg == null)
                m_TimerTextBg = FindTmp("TimerBG") ?? FindTmp("TitleBG (1)");

            if (m_Description == null)
                m_Description = FindTmp("Description");
            if (m_DescriptionBg == null)
                m_DescriptionBg = FindTmp("DescriptionBG") ?? FindTmp("Description (1)");

            if (m_JoinButton != null)
            {
                if (m_JoinLabel == null)
                    m_JoinLabel = FindDirectChildTmp(m_JoinButton.transform, "Text (TMP)")
                                  ?? FindDirectChildTmp(m_JoinButton.transform, "Text")
                                  ?? m_JoinButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (m_JoinLabelBg == null)
                    m_JoinLabelBg = FindDirectChildTmp(m_JoinButton.transform, "TextBG")
                                    ?? FindDirectChildTmp(m_JoinButton.transform, "Text (TMP) (1)");
            }

            if (m_DimCloseButton == null)
            {
                // Optional: dark full-screen overlay under Popup (tap outside to dismiss).
                var overlay = FindDeep("TournamentJoinPopup");
                if (overlay != null && overlay != transform)
                    m_DimCloseButton = overlay.GetComponent<Button>();
            }
        }

        private void ApplyStaticCopy()
        {
            SetPairedText(m_Title, m_TitleBg, "Golden Tournament");
            SetPairedText(
                m_Description,
                m_DescriptionBg,
                "Compete with 24 players!\nCollect Golden Arrows from combos.\n<size=140%>JOIN TO START!</size>");
            SetPairedText(m_JoinLabel, m_JoinLabelBg, "JOIN");
        }

        private void WireButtons()
        {
            if (m_JoinButton != null)
            {
                m_JoinButton.onClick.RemoveAllListeners();
                m_JoinButton.onClick.AddListener(OnJoinClicked);
            }

            if (m_DimCloseButton != null)
            {
                m_DimCloseButton.onClick.RemoveAllListeners();
                m_DimCloseButton.onClick.AddListener(Close);
            }
        }

        private void RefreshTimer()
        {
            if (service == null || m_TimerText == null) return;

            var rem = service.GetRemainingTime();
            string text = rem.TotalSeconds <= 0
                ? "Ending soon..."
                : $"Ends in {FormatTimeLeft(rem)}";

            if (string.Equals(m_LastTimerText, text, System.StringComparison.Ordinal))
                return;

            m_LastTimerText = text;
            SetPairedText(m_TimerText, m_TimerTextBg, text);
        }

        private static string FormatTimeLeft(System.TimeSpan rem)
        {
            return TournamentUiFormat.FormatTimeLeft(rem);
        }

        private void OnJoinClicked()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            if (service != null && service.Status == TournamentStatus.PendingJoin && service.TryJoin())
            {
                var svc = service;
                Close();
                TournamentLeaderboardPopupView.Show(svc);
                return;
            }

            Close();
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private static void SetPairedText(TextMeshProUGUI main, TextMeshProUGUI bg, string value)
        {
            if (main != null)
                main.text = value;
            if (bg != null)
                bg.text = value;
        }

        private static TextMeshProUGUI FindDirectChildTmp(Transform parent, string objectName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == objectName)
                    return child.GetComponent<TextMeshProUGUI>();
            }
            return null;
        }

        private TextMeshProUGUI FindTmp(string objectName)
        {
            var t = FindDeep(objectName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private Transform FindDeep(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                    return transforms[i];
            }
            return null;
        }
    }
}
