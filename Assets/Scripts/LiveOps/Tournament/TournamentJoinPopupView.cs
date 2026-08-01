using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven join popup (cloned from MissionsPopup art: NativeBG + ButtonGreen + Lilita).
    /// </summary>
    public class TournamentJoinPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_Description;
        [SerializeField] private TextMeshProUGUI m_TimerText;
        [SerializeField] private Button m_JoinButton;
        [SerializeField] private TextMeshProUGUI m_JoinLabel;

        private TournamentLiveOpService service;
        private float nextRefresh;

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
            ResolveRefs();
            SanitizeMissionLeftovers();
            ApplyCopy();
            WireButtons();
            WireDimToDismiss();
            RefreshTimer();
        }

        private void SanitizeMissionLeftovers()
        {
            var popup = FindDeep("Popup");
            if (popup == null) return;

            for (int i = popup.childCount - 1; i >= 0; i--)
            {
                Transform child = popup.GetChild(i);
                if (child == null) continue;
                string n = child.name;
                if (n == "Title" || n == "Description" || n == "GreenShadow" || n == "Title (1)")
                    continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void WireDimToDismiss()
        {
            // Tap dark overlay to close without joining.
            Image dimImage = null;
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null) continue;
                if (img.color.a < 0.8f || img.color.r > 0.15f || img.color.g > 0.15f || img.color.b > 0.15f)
                    continue;
                var rt = img.rectTransform;
                if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
                {
                    dimImage = img;
                    break;
                }
            }

            if (dimImage == null) return;
            var btn = dimImage.GetComponent<Button>();
            if (btn == null) btn = dimImage.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(Close);
        }

        private void Update()
        {
            if (service == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 1f;
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

            if (m_Description == null)
            {
                var t = FindDeep("Description");
                if (t != null) m_Description = t.GetComponent<TextMeshProUGUI>();
            }

            // Reuse a secondary title as timer if present.
            if (m_TimerText == null)
            {
                var t = FindDeep("Title (1)");
                if (t != null) m_TimerText = t.GetComponent<TextMeshProUGUI>();
            }

            if (m_JoinButton == null)
                m_JoinButton = m_CloseButton;

            if (m_JoinLabel == null && m_JoinButton != null)
                m_JoinLabel = m_JoinButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void ApplyCopy()
        {
            if (m_Title != null)
                m_Title.text = "Golden Tournament";
            if (m_Description != null)
            {
                m_Description.text =
                    "Compete with 24 players!\nCollect Golden Arrows from combos.\nJoin to start at 0 points.";
            }
            if (m_JoinLabel != null)
                m_JoinLabel.text = "JOIN";
        }

        private void WireButtons()
        {
            if (m_JoinButton != null)
            {
                m_JoinButton.onClick.RemoveAllListeners();
                m_JoinButton.onClick.AddListener(OnJoinOrClose);
            }
        }

        private void RefreshTimer()
        {
            if (service == null || m_TimerText == null) return;
            var rem = service.GetRemainingTime();
            m_TimerText.text = rem.TotalSeconds <= 0
                ? "Ending soon..."
                : $"Ends in {(int)rem.TotalHours}h {rem.Minutes}m";
        }

        private void OnJoinOrClose()
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
