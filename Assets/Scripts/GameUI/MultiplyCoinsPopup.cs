using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core;

namespace Assets.Scripts.GameUI
{
    public class MultiplyCoinsPopup : MonoBehaviour
    {
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
        [SerializeField] private TextMeshProUGUI m_AnimatedCoinsText; // The one that counts up from original to multiplied
        [SerializeField] private Button m_MultiplyButton;
        [SerializeField] private Button m_NoThanksButton;
        [SerializeField] private Canvas m_Canvas;

        [Header("Config")]
        [SerializeField] private MultiplyRewardConfig m_Config;

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
            if (m_AnimatedCoinsText != null) m_AnimatedCoinsText.gameObject.SetActive(false);
            
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
            // Ensure the Canvas has a camera reference for ScreenSpaceCamera mode
            m_Canvas.worldCamera = Camera.main;

            // Pull win amount from GameManager as the primary source/fallback
            if (GameManager.Instance != null)
            {
                m_InitialCoins = GameManager.Instance.p_lastWinAmount;
                Debug.Log($"[SlotMachine] OnEnable: Retrieved win amount from GameManager: {m_InitialCoins}");
                if (m_CoinsWonText != null) m_CoinsWonText.text = m_InitialCoins.ToString("N0");
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

            // Start all reels with staggered delays and durations to stop one by one
            List<Coroutine> spinningCoroutines = new List<Coroutine>();
            for (int r = 0; r < reelCount; r++)
            {
                // Each reel spins longer than the previous to stop from left to right
                float startDelay = r * 0.15f;
                float spinDuration = 1.0f + r * 0.6f; 
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
                StartCoroutine(RewardAnimationRoutine());
            }
            else
            {
                Debug.LogWarning("[SlotMachine] Ad not ready.");
            }
        }

        private IEnumerator AnimateSingleReel(int r, int targetIndex, float duration, float startDelay)
        {
            if (startDelay > 0) yield return new WaitForSeconds(startDelay);

            float speed = UnityEngine.Random.Range(35f, 45f);
            float elapsed = 0f;

            // Spin phase
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentSpeed = Mathf.Lerp(speed, 5f, t * t);
                m_ReelOffsets[r] += currentSpeed * Time.deltaTime;
                UpdateReelPositions(r);
                yield return null;
            }

            // Snap phase
            float symbolsCount = m_AllReelSymbols[r].Count;
            float currentVal = m_ReelOffsets[r];
            float desiredOffsetBase = 2f - targetIndex;
            float targetOffset = desiredOffsetBase + (Mathf.Round((currentVal - desiredOffsetBase) / symbolsCount) * symbolsCount);

            float snapElapsed = 0f;
            float snapDuration = 0.45f;
            float startOffset = m_ReelOffsets[r];

            while (snapElapsed < snapDuration)
            {
                snapElapsed += Time.deltaTime;
                float t = snapElapsed / snapDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                m_ReelOffsets[r] = Mathf.Lerp(startOffset, targetOffset, t);
                UpdateReelPositions(r);
                yield return null;
            }

            m_ReelOffsets[r] = targetOffset;
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
            StartCoroutine(RewardAnimationRoutine());
        }

        private void HandleAdClosed()
        {
            if (m_IsAdShowing && !m_RewardClaimed)
            {
                Debug.Log("[SlotMachine] Ad Closed without reward.");
                m_IsAdShowing = false;
            }
        }

        private IEnumerator RewardAnimationRoutine()
        {
            m_RewardClaimed = true;
            m_IsAdShowing = false;
            
            if (m_AnimatedCoinsText != null)
            {
                if (m_CoinsWonText != null) m_CoinsWonText.gameObject.SetActive(false);
                m_AnimatedCoinsText.gameObject.SetActive(true);
                m_AnimatedCoinsText.text = m_InitialCoins.ToString();
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
                if (m_AnimatedCoinsText != null) m_AnimatedCoinsText.text = current.ToString();
                yield return null;
            }
            
            if (m_AnimatedCoinsText != null)
            {
                m_AnimatedCoinsText.text = targetCoins.ToString();
                // Final punch animation on the reward text
                StartCoroutine(PunchSymbolRoutine(m_AnimatedCoinsText.transform, 1.2f));
            }
            
            if (UserDataManager.Instance != null && additionalCoins > 0)
            {
                UserDataManager.Instance.AddArrowsCurrency(additionalCoins);
            }
            
            if (AdsManager.Instance != null) AdsManager.Instance.SpawnCoinsSmallExplosion();
            if (SoundManager.Instance != null) SoundManager.Instance.PlayMediumCheer();

            yield return new WaitForSeconds(2.5f);
            Close();
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
            Destroy(gameObject);
        }
    }
}
