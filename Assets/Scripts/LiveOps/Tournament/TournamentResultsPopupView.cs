using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-driven results popup (Resources/TournamentResultsPopup), styled like the join popup.
    /// Win cases add reward flair + light celebration FX.
    /// </summary>
    public class TournamentResultsPopupView : MonoBehaviour
    {
        private static readonly Color PlaceYellow = new Color(1f, 0.88f, 0.35f, 1f);
        private static readonly Color PlaceOutline = new Color(0.45f, 0.25f, 0.05f, 0.85f);
        private static readonly Color RewardGold = new Color(1f, 0.85f, 0.25f, 1f);

        [Header("Buttons")]
        [SerializeField] private Button m_ActionButton;

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TitleBg;

        [Header("Subtitle (place)")]
        [SerializeField] private TextMeshProUGUI m_Subtitle;
        [SerializeField] private TextMeshProUGUI m_SubtitleBg;

        [Header("Body")]
        [SerializeField] private TextMeshProUGUI m_Body;
        [SerializeField] private TextMeshProUGUI m_BodyBg;

        [Header("Action label")]
        [SerializeField] private TextMeshProUGUI m_ActionLabel;
        [SerializeField] private TextMeshProUGUI m_ActionLabelBg;
        [SerializeField] private TextMeshProUGUI m_ClaimAmountLabel;
        [SerializeField] private TextMeshProUGUI m_ClaimAmountLabelBg;

        [Header("Win flair (optional)")]
        [SerializeField] private Image m_RewardIcon;
        [SerializeField] private RectTransform m_RewardRoot;
        [SerializeField] private Image m_ClaimRewardIcon;
        [SerializeField] private Sprite m_CoinSprite;
        [SerializeField] private Sprite m_HintSprite;
        [SerializeField] private Sprite m_WandSprite;
        [SerializeField] private Sprite m_LifeSprite;

        private TournamentPendingResultsData pending;
        private Coroutine m_FxRoutine;
        private ParticleSystem m_Confetti;

        public static bool TryShowPending()
        {
            if (!NetworkReconnectManager.IsOnline)
                return false;

            if (!TournamentLiveOpService.HasPendingResults())
                return false;

            var pendingData = TournamentLiveOpService.GetPendingResults();
            if (pendingData == null)
                return false;

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
            view.Initialize(pendingData);
            return true;
        }

        public void Initialize(TournamentPendingResultsData data)
        {
            pending = data;
            ResolveRefsIfNeeded();
            ApplyCopy();
            WireButton();
            PlayIntroFx();
        }

        private void OnDestroy()
        {
            if (m_FxRoutine != null)
                StopCoroutine(m_FxRoutine);
        }

        private void ResolveRefsIfNeeded()
        {
            if (m_ActionButton == null)
            {
                var green = FindDeep("GreenShadow");
                if (green != null)
                    m_ActionButton = green.GetComponent<Button>();
            }

            if (m_Title == null)
                m_Title = FindTmp("Title");
            if (m_TitleBg == null)
                m_TitleBg = FindTmp("TitleBG");

            if (m_Subtitle == null)
                m_Subtitle = FindTmp("Subtitle") ?? FindTmp("Title (1)");
            if (m_SubtitleBg == null)
                m_SubtitleBg = FindTmp("SubtitleBG") ?? FindTmp("TitleBG (1)");

            if (m_Body == null)
                m_Body = FindTmp("Description") ?? FindTmp("Body");
            if (m_BodyBg == null)
                m_BodyBg = FindTmp("DescriptionBG") ?? FindTmp("BodyBG") ?? FindTmp("Description (1)");

            if (m_ActionButton != null)
            {
                if (m_ActionLabel == null)
                    m_ActionLabel = FindDirectChildTmp(m_ActionButton.transform, "Text (TMP)")
                                    ?? FindDirectChildTmp(m_ActionButton.transform, "Text")
                                    ?? m_ActionButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (m_ActionLabelBg == null)
                    m_ActionLabelBg = FindDirectChildTmp(m_ActionButton.transform, "TextBG")
                                      ?? FindDirectChildTmp(m_ActionButton.transform, "Text (TMP) (1)");
                if (m_ClaimAmountLabel == null)
                    m_ClaimAmountLabel = FindDirectChildTmp(m_ActionButton.transform, "ClaimAmount")
                                         ?? FindTmp("ClaimAmount");
                if (m_ClaimAmountLabelBg == null)
                    m_ClaimAmountLabelBg = FindDirectChildTmp(m_ActionButton.transform, "ClaimAmountBG")
                                           ?? FindTmp("ClaimAmountBG");
                if (m_ClaimRewardIcon == null)
                {
                    var iconTf = m_ActionButton.transform.Find("ClaimRewardIcon");
                    if (iconTf != null)
                        m_ClaimRewardIcon = iconTf.GetComponent<Image>();
                }
            }

            // Legacy center reward slot (ReelBG) — always hide; reward lives on the claim button.
            if (m_RewardRoot == null)
            {
                var reel = FindDeep("ReelBG");
                if (reel != null)
                {
                    m_RewardRoot = reel as RectTransform;
                    m_RewardIcon = reel.GetComponent<Image>();
                }
            }
            HideRewardVisual();
        }

        private void ApplyCopy()
        {
            if (pending == null) return;

            SetPairedText(m_Title, m_TitleBg, "GOLDEN TOURNAMENT");
            ApplyPlaceStyle();

            string placeLine = pending.FinalPlace > 0 ? $"#{pending.FinalPlace}" : "#—";
            SetPairedText(m_Subtitle, m_SubtitleBg, placeLine);

            bool topFive = pending.FinalPlace > 0 && pending.FinalPlace <= 5;
            string body;
            if (pending.HasReward)
            {
                var reward = TournamentConfigSO.ParseReward(pending.RewardKey);
                SetupClaimButtonReward(reward);
                body =
                    "<color=#2B3640>You finished in position " +
                    $"<color=#FFE159><b>#{pending.FinalPlace}</b></color></color>\n" +
                    $"<color=#FFE159><size=145%><b>YOU WON!</b></size></color>\n" +
                    $"<color=#2B3640><b>Golden Arrows: {Mathf.Max(0, pending.PlayerScore)}</b></color>";
            }
            else
            {
                SetupClaimButtonOk();
                if (topFive)
                {
                    body =
                        $"You finished in position <color=#FFE159><b>#{pending.FinalPlace}</b></color>\n" +
                        "<size=120%>Great run!</size>\n" +
                        $"<b>Golden Arrows: {Mathf.Max(0, pending.PlayerScore)}</b>";
                }
                else
                {
                    body =
                        $"You finished in position <color=#FFE159><b>#{pending.FinalPlace}</b></color>\n" +
                        "Better luck next time!\n" +
                        $"<b>Golden Arrows: {Mathf.Max(0, pending.PlayerScore)}</b>";
                }
            }

            SetPairedText(m_Body, m_BodyBg, body);
        }

        private void ApplyPlaceStyle()
        {
            ApplyYellowPlace(m_Subtitle);
            ApplyYellowPlace(m_SubtitleBg);
            if (m_SubtitleBg != null)
            {
                var c = PlaceYellow;
                c.a = 0.55f;
                m_SubtitleBg.color = c;
            }
        }

        private static void ApplyYellowPlace(TextMeshProUGUI tmp)
        {
            if (tmp == null) return;
            tmp.color = PlaceYellow;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 72f;
            tmp.fontSizeMax = 180f;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = PlaceOutline;
        }

        private void SetupClaimButtonReward(Reward reward)
        {
            Sprite sprite = GetRewardSprite(reward.type);

            if (m_ClaimRewardIcon != null)
            {
                m_ClaimRewardIcon.gameObject.SetActive(true);
                m_ClaimRewardIcon.enabled = sprite != null;
                if (sprite != null)
                {
                    m_ClaimRewardIcon.sprite = sprite;
                    m_ClaimRewardIcon.color = Color.white;
                    m_ClaimRewardIcon.preserveAspect = true;
                    m_ClaimRewardIcon.type = Image.Type.Simple;
                }
            }

            ApplyClaimLabelStyle(m_ActionLabel, rightAlign: true);
            ApplyClaimLabelStyle(m_ActionLabelBg, rightAlign: true);
            ApplyClaimLabelStyle(m_ClaimAmountLabel, rightAlign: false);
            ApplyClaimLabelStyle(m_ClaimAmountLabelBg, rightAlign: false);
            ApplyActionLabelRects(rewardMode: true);

            SetPairedText(m_ActionLabel, m_ActionLabelBg, "CLAIM");
            SetPairedText(m_ClaimAmountLabel, m_ClaimAmountLabelBg, reward.amount.ToString());
            SetClaimAmountVisible(true);
        }

        private void SetupClaimButtonOk()
        {
            if (m_ClaimRewardIcon != null)
                m_ClaimRewardIcon.gameObject.SetActive(false);

            SetClaimAmountVisible(false);
            ApplyClaimLabelStyle(m_ActionLabel, rightAlign: false, center: true);
            ApplyClaimLabelStyle(m_ActionLabelBg, rightAlign: false, center: true);
            ApplyActionLabelRects(rewardMode: false);
            SetPairedText(m_ActionLabel, m_ActionLabelBg, "OK");
        }

        private void SetClaimAmountVisible(bool visible)
        {
            if (m_ClaimAmountLabel != null)
                m_ClaimAmountLabel.gameObject.SetActive(visible);
            if (m_ClaimAmountLabelBg != null)
                m_ClaimAmountLabelBg.gameObject.SetActive(visible);
        }

        private void ApplyActionLabelRects(bool rewardMode)
        {
            const float sidePad = 20f;
            const float iconSize = 72f;
            const float gap = 10f; // space between text and icon — reads as one phrase
            float halfIcon = iconSize * 0.5f;

            if (m_ClaimRewardIcon != null)
            {
                var iconRt = m_ClaimRewardIcon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(0f, 16f);
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            }

            if (rewardMode)
            {
                // Pack CLAIM | icon | amount tightly around the button center.
                SetPackedLabel(m_ActionLabel, pivotX: 1f, x: -(halfIcon + gap), width: 200f, y: 16f);
                SetPackedLabel(m_ActionLabelBg, pivotX: 1f, x: -(halfIcon + gap), width: 200f, y: 2f);
                SetPackedLabel(m_ClaimAmountLabel, pivotX: 0f, x: halfIcon + gap, width: 200f, y: 16f);
                SetPackedLabel(m_ClaimAmountLabelBg, pivotX: 0f, x: halfIcon + gap, width: 200f, y: 2f);
            }
            else
            {
                SetPaddedFullLabel(m_ActionLabel, sidePad, 16f);
                SetPaddedFullLabel(m_ActionLabelBg, sidePad, 2f);
            }
        }

        private static void SetPackedLabel(TextMeshProUGUI tmp, float pivotX, float x, float width, float y)
        {
            if (tmp == null) return;
            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, 120f);
        }

        private static void SetPaddedFullLabel(TextMeshProUGUI tmp, float sidePad, float y)
        {
            if (tmp == null) return;
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(sidePad, 10f);
            rt.offsetMax = new Vector2(-sidePad, -10f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        private static void ApplyClaimLabelStyle(TextMeshProUGUI tmp, bool rightAlign, bool center = false)
        {
            if (tmp == null) return;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 36f;
            tmp.fontSizeMax = 72f;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontStyle = FontStyles.Bold;
            tmp.margin = Vector4.zero;
            if (center)
                tmp.alignment = TextAlignmentOptions.Center;
            else if (rightAlign)
                tmp.alignment = TextAlignmentOptions.MidlineRight;
            else
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private void HideRewardVisual()
        {
            if (m_RewardRoot != null)
                m_RewardRoot.gameObject.SetActive(false);
        }

        private void PlayIntroFx()
        {
            if (m_FxRoutine != null)
                StopCoroutine(m_FxRoutine);
            m_FxRoutine = StartCoroutine(IntroFxRoutine());
        }

        private IEnumerator IntroFxRoutine()
        {
            // Punch the place number.
            if (m_Subtitle != null)
                yield return PunchScale(m_Subtitle.rectTransform, 1.35f, 0.12f, 0.1f);

            if (pending != null && pending.HasReward)
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayWin();
                    SoundManager.Instance.PlaySmallCheer();
                }

                // Reuse the game's existing coin celebration FX when possible.
                if (AdsManager.Instance != null)
                    AdsManager.Instance.SpawnCoinsSmallExplosion();
                else
                    SpawnConfettiBurst();

                RectTransform claimFxTarget = m_ClaimRewardIcon != null
                    ? m_ClaimRewardIcon.rectTransform
                    : (m_ActionButton != null ? m_ActionButton.transform as RectTransform : null);

                if (claimFxTarget != null && claimFxTarget.gameObject.activeInHierarchy)
                {
                    claimFxTarget.localScale = Vector3.zero;
                    yield return PunchScale(claimFxTarget, 1.25f, 0.18f, 0.12f);
                    m_FxRoutine = StartCoroutine(IdlePulse(claimFxTarget));
                    yield break;
                }
            }

            m_FxRoutine = null;
        }

        private IEnumerator IdlePulse(RectTransform target)
        {
            if (target == null) yield break;
            Vector3 baseScale = Vector3.one;
            float t = 0f;
            while (target != null)
            {
                t += Time.unscaledDeltaTime;
                float s = 1f + Mathf.Sin(t * 3.2f) * 0.06f;
                target.localScale = baseScale * s;
                if (m_ClaimRewardIcon != null && target == m_ClaimRewardIcon.rectTransform)
                {
                    float glow = 0.85f + Mathf.Sin(t * 4f) * 0.15f;
                    m_ClaimRewardIcon.color = Color.Lerp(Color.white, RewardGold, (glow - 0.85f) / 0.15f * 0.35f);
                }
                yield return null;
            }
        }

        private static IEnumerator PunchScale(RectTransform target, float punch, float up, float down)
        {
            if (target == null) yield break;
            Vector3 start = Vector3.one * 0.01f;
            Vector3 peak = Vector3.one * punch;
            Vector3 end = Vector3.one;
            target.localScale = start;

            float t = 0f;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.LerpUnclamped(start, peak, Mathf.SmoothStep(0f, 1f, t / up));
                yield return null;
            }

            t = 0f;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                target.localScale = Vector3.LerpUnclamped(peak, end, Mathf.SmoothStep(0f, 1f, t / down));
                yield return null;
            }

            target.localScale = end;
        }

        private void SpawnConfettiBurst()
        {
            if (m_Confetti != null)
            {
                m_Confetti.Play(true);
                return;
            }

            var host = m_Subtitle != null ? m_Subtitle.transform.parent : transform;
            var go = new GameObject("TournamentConfetti", typeof(RectTransform));
            go.transform.SetParent(host, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.75f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            m_Confetti = go.AddComponent<ParticleSystem>();
            var main = m_Confetti.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.2f;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(180f, 420f);
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 22f);
            main.startColor = new ParticleSystem.MinMaxGradient(PlaceYellow, RewardGold);
            main.gravityModifier = 0.8f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = m_Confetti.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

            var shape = m_Confetti.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 10f;

            var colorOverLifetime = m_Confetti.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(PlaceYellow, 0f),
                    new GradientColorKey(Color.white, 0.4f),
                    new GradientColorKey(RewardGold, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // UI-friendly sorting so confetti draws above the popup sheet.
            renderer.sortingOrder = 250;

            // Make ParticleSystem work under a Screen Space Overlay canvas.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Overlay canvases don't always show world particles well; keep burst subtle via UI punch only.
                // Still play — many projects use Overlay with default particle shader successfully.
            }

            m_Confetti.Play(true);
        }

        private Sprite GetRewardSprite(RewardType type)
        {
            switch (type)
            {
                case RewardType.Coin:
                    return m_CoinSprite != null ? m_CoinSprite : Resources.Load<Sprite>("Tournament/ArrowsCoin");
                case RewardType.Hint:
                    return m_HintSprite != null ? m_HintSprite : Resources.Load<Sprite>("Tournament/Hint");
                case RewardType.MagicWand:
                    return m_WandSprite != null ? m_WandSprite : Resources.Load<Sprite>("Tournament/Wand");
                case RewardType.RefillLife:
                    return m_LifeSprite != null ? m_LifeSprite : Resources.Load<Sprite>("Tournament/Life");
                default:
                    return null;
            }
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
