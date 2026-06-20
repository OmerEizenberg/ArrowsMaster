using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core;
using Assets.Scripts.GAE;

namespace Assets.Scripts.GameUI
{
    public class MultiplyCoinsPopup : MonoBehaviour
    {
        private static int s_ActivePopupCount;

        public static bool IsAnyVisible => s_ActivePopupCount > 0;

        [Header("Slot Machine UI")]
        [SerializeField] private RectTransform[] m_ReelParents; // Array of containers for the 3 reels
        [SerializeField] private TextMeshProUGUI m_SymbolTemplate; // Template for the multipliers
        [SerializeField] private float m_SymbolSpacing = 120f; // Vertical distance between symbols
        
        [Header("3D Effect Parameters")]
        [SerializeField] private float m_MinAlpha = 0.2f;
        [SerializeField] private float m_MaxAlpha = 1.0f;
        [SerializeField] private float m_MinScale = 0.7f;
        [SerializeField] private float m_MaxScale = 1.3f;
        
        [Header("General UI")]
        [SerializeField] private TextMeshProUGUI m_CoinsWonText;
        [SerializeField] private TextMeshProUGUI m_CoinsWonTextShadow;
        [SerializeField] private TextMeshProUGUI m_AnimatedCoinsText; // The one that counts up from original to multiplied
        [SerializeField] private TextMeshProUGUI m_AnimatedCoinsTextShadow;
        [SerializeField] private Button m_MultiplyButton;
        [SerializeField] private Button m_NoThanksButton;
        [SerializeField] private Canvas m_Canvas;

        [Header("Config")]
        [SerializeField] private MultiplyRewardConfig m_Config;

        [Header("GAE Fly Animation")]
        [SerializeField] private Sprite m_GaeFlyIconSprite;
        [SerializeField] private int m_GaeFlyIconCount = 10;
        [SerializeField] private float m_GaeFlyIconScale = 0.38f;
        [SerializeField] private float m_GaeFlyStaggerDelay = 0.09f;
        [SerializeField] private float m_GaeFlySpawnGap = 26f;
        [SerializeField] private float m_GaeFlyDuration = 0.65f;
        [SerializeField] private float m_GaeFlyScatterRadius = 8f;
        [SerializeField] private RectTransform m_GaeFlySource;

        private List<TextMeshProUGUI>[] m_AllReelSymbols;
        private int[] m_AvailableMultipliers;
        private int m_InitialCoins;
        private bool m_IsSpinning = false;
        private bool m_IsAdShowing = false;
        private int m_CurrentMultiplier = 1;
        private bool m_RewardClaimed = false;
        
        private float[] m_ReelOffsets; // Array of scroll offsets for each reel

        public void Setup(int coinsWon)
        {
            m_InitialCoins = coinsWon;
            Debug.Log($"[SlotMachine] Setup called. Coins: {m_InitialCoins}");
            if (m_CoinsWonText != null) m_CoinsWonText.text = coinsWon.ToString("N0");
            if (m_CoinsWonTextShadow != null) m_CoinsWonTextShadow.text = coinsWon.ToString("N0");
            
            if (m_AnimatedCoinsText != null) m_AnimatedCoinsText.gameObject.SetActive(false);
            if (m_AnimatedCoinsTextShadow != null) m_AnimatedCoinsTextShadow.gameObject.SetActive(false);
            
            m_IsSpinning = false;
            m_IsAdShowing = false;
            m_RewardClaimed = false;
            
            if (m_MultiplyButton != null)
            {
                m_MultiplyButton.onClick.RemoveAllListeners();
                m_MultiplyButton.onClick.AddListener(OnMultiplyClicked);
            }
                
            if (m_NoThanksButton != null)
            {
                m_NoThanksButton.onClick.RemoveAllListeners();
                m_NoThanksButton.onClick.AddListener(OnNoThanksClicked);
            }
        }

        private void OnEnable()
        {
            s_ActivePopupCount++;
            // Ensure the Canvas has a camera reference for ScreenSpaceCamera mode
            m_Canvas.worldCamera = Camera.main;

            // Pull win amount from GameManager as the primary source/fallback
            if (GameManager.Instance != null)
            {
                m_InitialCoins = GameManager.Instance.p_lastWinAmount;
                Debug.Log($"[SlotMachine] OnEnable: Retrieved win amount from GameManager: {m_InitialCoins}");
                if (m_CoinsWonText != null) m_CoinsWonText.text = m_InitialCoins.ToString("N0");
                if (m_CoinsWonTextShadow != null) m_CoinsWonTextShadow.text = m_InitialCoins.ToString("N0");
            }

            InitializeSlotMachine();
            
            // Ensure ad events are hooked up
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnMultiplyRewardReceived -= HandleRewardReceived;
                AdsManager.Instance.OnMultiplyRewardReceived += HandleRewardReceived;
                AdsManager.Instance.OnAdClosed -= HandleAdClosed;
                AdsManager.Instance.OnAdClosed += HandleAdClosed;
                
                AdsManager.Instance.LoadMultiplyRewarded();
            }
        }

        private MultiplierZone GetWeightedMultiplierFromConfig()
        {
            if (m_Config == null || m_Config.zones == null || m_Config.zones.Length == 0)
                return new MultiplierZone() { multiplier = 2 };

            float totalWeight = 0;
            foreach (var z in m_Config.zones) totalWeight += z.weight;
            
            float r = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0;
            foreach (var z in m_Config.zones)
            {
                cumulative += z.weight;
                if (r <= cumulative) return z;
            }
            return m_Config.zones[0];
        }

        private void InitializeSlotMachine()
        {
            Debug.Log("[SlotMachine] Initializing 3 Reels...");
            
            if (m_SymbolTemplate == null)
            {
                Debug.LogError("[SlotMachine] ERROR: Symbol Template is not assigned in the Inspector!");
                return;
            }
            if (m_ReelParents == null || m_ReelParents.Length == 0)
            {
                Debug.LogError("[SlotMachine] ERROR: Reel Parents are not assigned in the Inspector!");
                return;
            }

            int reelCount = m_ReelParents.Length;
            m_AllReelSymbols = new List<TextMeshProUGUI>[reelCount];
            m_ReelOffsets = new float[reelCount];

            // Get multipliers from config zones for symbol generation
            if (m_Config != null && m_Config.zones != null && m_Config.zones.Length > 0)
            {
                m_AvailableMultipliers = new int[m_Config.zones.Length];
                for (int i = 0; i < m_Config.zones.Length; i++)
                    m_AvailableMultipliers[i] = m_Config.zones[i].multiplier;
            }
            else
            {
                m_AvailableMultipliers = new int[] { 2, 3, 4, 3, 2 };
            }

            m_SymbolTemplate.gameObject.SetActive(false);

            for (int r = 0; r < reelCount; r++)
            {
                m_AllReelSymbols[r] = new List<TextMeshProUGUI>();
                m_ReelOffsets[r] = 0f;

                // Clear existing children in this reel parent (if any)
                foreach (Transform child in m_ReelParents[r])
                {
                    if (child.gameObject != m_SymbolTemplate.gameObject)
                        Destroy(child.gameObject);
                }

                // Create 5 symbols per reel
                for (int i = 0; i < 5; i++)
                {
                    MultiplierZone zone = GetWeightedMultiplierFromConfig();
                    TextMeshProUGUI sym = Instantiate(m_SymbolTemplate, m_ReelParents[r]);
                    sym.gameObject.SetActive(true);
                    sym.text = "X" + zone.multiplier;
                    m_AllReelSymbols[r].Add(sym);
                }
                UpdateReelPositions(r);
            }

            Debug.Log($"[SlotMachine] Successfully initialized {reelCount} reels.");
        }

        private int ForceMultiplierInReel(int reelIndex, int value)
        {
            // Ensure the target multiplier value exists in the specified reel.
            // Check if it already exists among the 5 symbols.
            for (int i = 0; i < m_AllReelSymbols[reelIndex].Count; i++)
            {
                if (m_AllReelSymbols[reelIndex][i].text == "X" + value) return i;
            }
            // If not found, replace the middle one (index 2 is a safe bet) or a random one.
            int randomIndex = UnityEngine.Random.Range(0, 5);
            m_AllReelSymbols[reelIndex][randomIndex].text = "X" + value;
            return randomIndex;
        }


        private void OnDestroy()
        {
            s_ActivePopupCount = Mathf.Max(0, s_ActivePopupCount - 1);
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnMultiplyRewardReceived -= HandleRewardReceived;
                AdsManager.Instance.OnAdClosed -= HandleAdClosed;
            }
        }

        private void UpdateReelPositions(int r)
        {
            if (m_AllReelSymbols == null || r >= m_AllReelSymbols.Length) return;
            int count = m_AllReelSymbols[r].Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                // Calculate position relative to container center
                float posIndex = (i + m_ReelOffsets[r]) % count;
                if (posIndex < 0) posIndex += count;

                float distanceFromCenter = posIndex - 2f;
                m_AllReelSymbols[r][i].rectTransform.anchoredPosition = new Vector2(0, -distanceFromCenter * m_SymbolSpacing);

                float absDist = Mathf.Abs(distanceFromCenter);
                float normalizedDist = Mathf.Clamp01(absDist / 2f); 

                float alpha = Mathf.Lerp(m_MaxAlpha, m_MinAlpha, normalizedDist);
                float scale = Mathf.Lerp(m_MaxScale, m_MinScale, normalizedDist);

                Color c = m_AllReelSymbols[r][i].color;
                c.a = alpha;
                m_AllReelSymbols[r][i].color = c;
                m_AllReelSymbols[r][i].transform.localScale = Vector3.one * scale;
            }
        }

        public void OnMultiplyClicked()
        {
            if (m_IsSpinning || m_IsAdShowing || m_RewardClaimed) return;
            if (m_MultiplyButton != null) m_MultiplyButton.interactable = false;
            if (m_NoThanksButton != null) m_NoThanksButton.interactable = false;
            
            Debug.Log("[SlotMachine] MultiplyClicked! Starting Spin...");
            StartCoroutine(SpinReelRoutine());
        }

        private IEnumerator SpinReelRoutine()
        {
            m_IsSpinning = true;
            
            // PRE-DETERMINE THE WINNER globally for all 3 reels
            MultiplierZone winnerZone = GetWeightedMultiplierFromConfig();
            m_CurrentMultiplier = winnerZone.multiplier;
            
            int reelCount = m_ReelParents.Length;
            int[] winnerIndices = new int[reelCount];
            for (int r = 0; r < reelCount; r++)
            {
                winnerIndices[r] = ForceMultiplierInReel(r, m_CurrentMultiplier);
            }

            // Start all reels with staggered delays and durations to stop in reverse order (2 -> 1 -> 0)
            List<Coroutine> spinningCoroutines = new List<Coroutine>();
            for (int r = 0; r < reelCount; r++)
            {
                // Each reel spins longer or shorter to control stop order
                // For 2 -> 1 -> 0, reel 2 stays first (shortest duration), then 1, then 0
                int reverseIndex = (reelCount - 1 - r); // 0 -> 2, 1 -> 1, 2 -> 0
                
                float startDelay = reverseIndex * 0.15f;
                float spinDuration = 0.8f + reverseIndex * 0.6f; 
                // Add minor randomization to duration for unique look
                spinDuration += UnityEngine.Random.Range(-0.1f, 0.1f);

                spinningCoroutines.Add(StartCoroutine(AnimateSingleReel(r, winnerIndices[r], spinDuration, startDelay)));
            }

            // Wait for all reels to stop
            foreach (var cor in spinningCoroutines) yield return cor;

            m_IsSpinning = false;
            
            Debug.Log($"[SlotMachine] All 3 Reels stopped on X{m_CurrentMultiplier}. Waiting small delay...");

            yield return new WaitForSeconds(0.6f);

            Debug.Log("[SlotMachine] Proceeding to Ad sequence.");
            if (AdsManager.Instance != null && (AdsManager.Instance.IsMultiplyRewardedReady || AdsManager.Instance.IsInterstitialReady))
            {
                m_IsAdShowing = true;
                AdsManager.Instance.ShowRewardedForMultiply();
            }
            else if (AdsManager.Instance == null)
            {
                StartCoroutine(RewardAnimationRoutine(ResourceAnalyticsReasons.CoinsMultiplyFallback));
            }
            else
            {
                Debug.LogWarning("[SlotMachine] Ad not ready.");
            }
        }

        private IEnumerator AnimateSingleReel(int r, int targetIndex, float duration, float startDelay)
        {
            if (startDelay > 0) yield return new WaitForSeconds(startDelay);

            // Wider peak speed range for more varied mechanical "power"
            float peakSpeed = UnityEngine.Random.Range(85f, 115f);
            float elapsed = 0f;

            // --- Physical Spin Phase (Acceleration -> Peak -> Deceleration) ---
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                float speedCurve;
                if (t < 0.3f)
                {
                    // Quadratic acceleration for a smooth start
                    float localT = t / 0.3f;
                    speedCurve = localT * localT;
                }
                else if (t < 0.65f)
                {
                    // Constant Peak Speed
                    speedCurve = 1.0f;
                }
                else
                {
                    // Cubic deceleration for a "heavy" mechanical ramp down
                    float localT = (t - 0.65f) / 0.35f;
                    speedCurve = 1f - (localT * localT * localT);
                }
                
                speedCurve = Mathf.Clamp(speedCurve, 0.08f, 1.0f);
                m_ReelOffsets[r] += peakSpeed * speedCurve * Time.deltaTime;
                UpdateReelPositions(r);
                yield return null;
            }

            // --- Physical Snap (Spring-like Overshoot and Return) ---
            float symbolsCount = m_AllReelSymbols[r].Count;
            float currentVal = m_ReelOffsets[r];
            float desiredOffsetBase = 2f - targetIndex;
            float targetOffsetEnd = desiredOffsetBase + (Mathf.Round((currentVal - desiredOffsetBase) / symbolsCount) * symbolsCount);

            float startSnapOffset = m_ReelOffsets[r];
            float snapTotalTime = 0.45f;
            float snapElapsed = 0f;
            
            // This curve simulates the reel hitting the brake, jumping past, and falling back
            while (snapElapsed < snapTotalTime)
            {
                snapElapsed += Time.deltaTime;
                float t = snapElapsed / snapTotalTime;
                
                // Back-Ease-Out Approximation
                // It goes from 0 to ~1.2 before settling exactly at 1.0
                float overshootAmount = 0.35f;
                float easedT;
                if (t < 0.6f)
                {
                    float localT = t / 0.6f;
                    float invT = 1f - localT;
                    easedT = (1f + overshootAmount) * (1f - invT * invT); // Overshoot
                }
                else
                {
                    float localT = (t - 0.6f) / 0.4f;
                    easedT = Mathf.Lerp(1f + overshootAmount, 1.0f, localT * localT); // Settle back
                }

                // Manual Lerp to ensure overshoot beyond 1.0 works as intended
                m_ReelOffsets[r] = startSnapOffset + (targetOffsetEnd - startSnapOffset) * easedT;
                UpdateReelPositions(r);
                yield return null;
            }

            m_ReelOffsets[r] = targetOffsetEnd;
            UpdateReelPositions(r);

            // Arrival feedback
            StartCoroutine(PunchSymbolRoutine(m_AllReelSymbols[r][targetIndex].transform));
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        }

        private IEnumerator PunchSymbolRoutine(Transform target, float punchFactor = 1.3f)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            Vector3 startScale = target.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Simple punch curve
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * (punchFactor - 1f);
                target.localScale = startScale * scale;
                yield return null;
            }
            target.localScale = startScale;
        }

        private void HandleRewardReceived()
        {
            Debug.Log("[SlotMachine] Ad Reward Received! Starting count-up...");
            m_IsAdShowing = false;
            StartCoroutine(RewardAnimationRoutine(ResourceAnalyticsReasons.CoinsMultiplyAd));
        }

        private void HandleAdClosed()
        {
            if (m_IsAdShowing && !m_RewardClaimed)
            {
                Debug.Log("[SlotMachine] Ad Closed without reward.");
                m_IsAdShowing = false;
            }
        }

        private IEnumerator RewardAnimationRoutine(string earnReason)
        {
            m_RewardClaimed = true;
            m_IsAdShowing = false;
            
            if (m_AnimatedCoinsText != null)
            {
                if (m_CoinsWonText != null) m_CoinsWonText.gameObject.SetActive(false);
                if (m_CoinsWonTextShadow != null) m_CoinsWonTextShadow.gameObject.SetActive(false);
                
                m_AnimatedCoinsText.gameObject.SetActive(true);
                m_AnimatedCoinsText.text = m_InitialCoins.ToString();
                
                
                m_AnimatedCoinsTextShadow.gameObject.SetActive(true);
                m_AnimatedCoinsTextShadow.text = m_AnimatedCoinsText.text;
            }
            
            int targetCoins = m_InitialCoins * m_CurrentMultiplier;
            int additionalCoins = targetCoins - m_InitialCoins;
            
            Debug.Log($"[SlotMachine] Starting Reward Animation: {m_InitialCoins} x {m_CurrentMultiplier} = {targetCoins}");
            
            float duration = 1.6f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                
                int current = Mathf.RoundToInt(Mathf.Lerp(m_InitialCoins, targetCoins, t));
                m_AnimatedCoinsText.text = current.ToString();
                m_AnimatedCoinsTextShadow.text = m_AnimatedCoinsText.text;
                yield return null;
            }
            
            if (m_AnimatedCoinsText != null)
            {
                m_AnimatedCoinsText.text = targetCoins.ToString();
                m_AnimatedCoinsTextShadow.text = targetCoins.ToString();
                
                // Final punch animation on the reward text
                StartCoroutine(PunchSymbolRoutine(m_AnimatedCoinsText.transform, 1.2f));
                StartCoroutine(PunchSymbolRoutine(m_AnimatedCoinsTextShadow.transform, 1.2f));
            }
            
            if (UserDataManager.Instance != null && additionalCoins > 0)
            {
                if (IsGaeMultiplyMode())
                {
                    GAEManager.Instance.AddPendingLevelArrows(additionalCoins);
                }
                else
                {
                    UserDataManager.Instance.AddArrowsCurrency(additionalCoins, earnReason);
                }
            }

            if (IsGaeMultiplyMode())
            {
                yield return PlayGaeFlyAnimationRoutine();
            }
            else if (AdsManager.Instance != null)
            {
                AdsManager.Instance.SpawnCoinsSmallExplosion();
            }
            if (SoundManager.Instance != null) SoundManager.Instance.PlayMediumCheer();

            yield return new WaitForSeconds(2.5f);
            Close();
        }

        private bool IsGaeMultiplyMode()
        {
            return GAEManager.Instance != null && GAEManager.Instance.IsGameplayGaeCurrencyActive;
        }

        private IEnumerator PlayGaeFlyAnimationRoutine()
        {
            RectTransform source = m_GaeFlySource;
            if (source == null && m_AnimatedCoinsText != null)
            {
                source = m_AnimatedCoinsText.rectTransform;
            }

            RectTransform target = GAEManager.Instance != null ? GAEManager.Instance.GetBarAnimationTarget() : null;
            if (source == null || target == null || m_Canvas == null || m_GaeFlyIconSprite == null)
            {
                yield break;
            }

            GAEFlyEffectRunner runner = GAEFlyEffectRunner.Create(
                m_Canvas.transform,
                GAECurrencyFlyAnimation.ComputeStaggeredFlyDuration(
                    m_GaeFlyIconCount,
                    m_GaeFlyStaggerDelay,
                    m_GaeFlyDuration));

            yield return GAECurrencyFlyAnimation.PlayStaggered(
                source,
                target,
                m_Canvas,
                m_GaeFlyIconSprite,
                m_GaeFlyIconCount,
                m_GaeFlyIconScale,
                m_GaeFlyStaggerDelay,
                m_GaeFlySpawnGap,
                m_GaeFlyDuration,
                m_GaeFlyScatterRadius,
                runner);
        }

        public void OnNoThanksClicked()
        {
            if (m_IsSpinning || m_IsAdShowing || m_RewardClaimed) return;
            
            if (m_MultiplyButton != null) m_MultiplyButton.interactable = false;
            if (m_NoThanksButton != null) m_NoThanksButton.interactable = false;
            
            Close();
        }

        private void Close()
        {
            Debug.Log("[SlotMachine] Closing Multiply Coins Popup.");
            if(transform.parent != null)
            {
                Destroy(transform.parent.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
