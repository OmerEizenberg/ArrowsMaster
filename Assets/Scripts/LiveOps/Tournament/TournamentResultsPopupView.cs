using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven results popup (MissionsPopup shell).
    /// </summary>
    public class TournamentResultsPopupView : MonoBehaviour
    {
        [SerializeField] private Button m_ActionButton;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_Body;
        [SerializeField] private TextMeshProUGUI m_ActionLabel;

        private TournamentPendingResultsData pending;

        public static bool TryShowPending()
        {
            if (!TournamentLiveOpService.HasPendingResults())
                return false;

            var pending = TournamentLiveOpService.GetPendingResults();
            if (pending == null)
                return false;

            // Avoid stacking duplicates.
            if (Object.FindFirstObjectByType<TournamentResultsPopupView>() != null)
                return true;

            GameObject prefab = Resources.Load<GameObject>("TournamentResultsPopup");
            if (prefab == null)
            {
                Debug.LogError("[TournamentResultsPopupView] Missing Resources/TournamentResultsPopup.prefab");
                return false;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = "TournamentResultsPopup";
            instance.SetActive(true);
            var view = instance.GetComponent<TournamentResultsPopupView>();
            if (view == null)
                view = instance.AddComponent<TournamentResultsPopupView>();
            view.Initialize(pending);
            return true;
        }

        public void Initialize(TournamentPendingResultsData data)
        {
            pending = data;
            ResolveRefs();
            SanitizeMissionLeftovers();
            ApplyCopy();
            WireButton();
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
                if (n == "Title" || n == "Description" || n == "GreenShadow")
                    continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void ResolveRefs()
        {
            if (m_ActionButton == null)
            {
                var t = FindDeep("GreenShadow");
                if (t != null) m_ActionButton = t.GetComponent<Button>();
            }
            if (m_Title == null)
            {
                var t = FindDeep("Title");
                if (t != null) m_Title = t.GetComponent<TextMeshProUGUI>();
            }
            if (m_Body == null)
            {
                var t = FindDeep("Description");
                if (t != null) m_Body = t.GetComponent<TextMeshProUGUI>();
            }
            if (m_ActionLabel == null && m_ActionButton != null)
                m_ActionLabel = m_ActionButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void ApplyCopy()
        {
            if (pending == null) return;

            if (m_Title != null)
                m_Title.text = "Tournament Finished";

            if (m_Body != null)
            {
                if (pending.HasReward)
                {
                    var reward = TournamentConfigSO.ParseReward(pending.RewardKey);
                    m_Body.text =
                        $"You finished in position #{pending.FinalPlace}\n" +
                        $"Reward: {FormatReward(reward)}\n" +
                        $"Golden Arrows: {pending.PlayerScore}";
                }
                else
                {
                    m_Body.text =
                        $"You finished in position #{pending.FinalPlace}\n" +
                        "Better luck next time!\n" +
                        $"Golden Arrows: {pending.PlayerScore}";
                }
            }

            if (m_ActionLabel != null)
                m_ActionLabel.text = pending.HasReward ? "CLAIM" : "OK";
        }

        private void WireButton()
        {
            if (m_ActionButton == null) return;
            m_ActionButton.onClick.RemoveAllListeners();
            m_ActionButton.onClick.AddListener(OnAction);
        }

        private void OnAction()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayClick();

            var service = LiveOpManager.Instance != null
                ? LiveOpManager.Instance.GetActiveService(TournamentLiveOpService.EventId) as TournamentLiveOpService
                : null;

            if (service != null)
            {
                service.ClaimPendingResultsAndGrantRewards(out _);
            }
            else
            {
                var p = TournamentLiveOpService.GetPendingResults();
                if (p != null && p.HasReward)
                    Grant(TournamentConfigSO.ParseReward(p.RewardKey));
                TournamentLiveOpService.ClearPendingResults();
            }

            Destroy(gameObject);
        }

        private static void Grant(Reward reward)
        {
            if (reward.amount <= 0 || UserDataManager.Instance == null) return;
            string reason = ResourceAnalyticsReasons.TournamentClaim;
            switch (reward.type)
            {
                case RewardType.Coin: UserDataManager.Instance.AddArrowsCurrency(reward.amount, reason); break;
                case RewardType.Hint: UserDataManager.Instance.AddHintBooster(reward.amount, reason); break;
                case RewardType.MagicWand: UserDataManager.Instance.AddMagicBooster(reward.amount, reason); break;
                case RewardType.RefillLife: UserDataManager.Instance.AddRefillBooster(reward.amount, reason); break;
            }
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

        private static string FormatReward(Reward reward)
        {
            switch (reward.type)
            {
                case RewardType.Coin: return $"{reward.amount} Coins";
                case RewardType.Hint: return $"{reward.amount} Hint(s)";
                case RewardType.MagicWand: return $"{reward.amount} Magic Wand(s)";
                case RewardType.RefillLife: return $"{reward.amount} Life Refill(s)";
                default: return $"{reward.amount}";
            }
        }
    }
}
