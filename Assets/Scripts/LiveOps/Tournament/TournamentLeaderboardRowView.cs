using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Single steady leaderboard row. Created once, then updated via SetData.
    /// Columns: Place | Name | Reward (icon+amount) | Score (GA icon+amount).
    /// </summary>
    public class TournamentLeaderboardRowView : MonoBehaviour
    {
        public Image Background;
        public Button Button;
        public TextMeshProUGUI PlaceText;
        public TextMeshProUGUI NameText;
        public Image RewardIcon;
        public TextMeshProUGUI RewardAmountText;
        public GameObject RewardRoot;
        public Image ScoreIcon;
        public TextMeshProUGUI ScoreText;
        public Image PlaceBg;
        public Image NameBg;
        public Image RewardBg;
        public Image ScoreBg;

        private bool isPlayerRow;

        public bool IsPlayerRow => isPlayerRow;

        public void SetData(
            TournamentLeaderboardRow row,
            Reward reward,
            Sprite rewardSprite,
            Sprite scoreSprite,
            Color playerRowColor,
            Color botRowColor)
        {
            isPlayerRow = row.IsPlayer;

            if (Background != null)
                Background.color = row.IsPlayer ? playerRowColor : botRowColor;

            if (PlaceText != null)
                PlaceText.text = $"#{row.Place}";

            if (NameText != null)
                NameText.text = row.Name ?? string.Empty;

            bool hasReward = reward.amount > 0 && rewardSprite != null;
            if (RewardRoot != null && RewardRoot.activeSelf != hasReward)
                RewardRoot.SetActive(hasReward);

            if (hasReward)
            {
                if (RewardIcon != null)
                {
                    RewardIcon.sprite = rewardSprite;
                    RewardIcon.enabled = true;
                    RewardIcon.preserveAspect = true;
                }
                if (RewardAmountText != null)
                    RewardAmountText.text = reward.amount.ToString();
            }
            else if (RewardAmountText != null)
            {
                RewardAmountText.text = "-";
            }

            if (ScoreIcon != null && scoreSprite != null)
            {
                ScoreIcon.sprite = scoreSprite;
                ScoreIcon.enabled = true;
                ScoreIcon.preserveAspect = true;
            }

            if (ScoreText != null)
                ScoreText.text = row.Score.ToString();

            if (Button != null)
                Button.interactable = row.IsPlayer;
        }
    }
}
