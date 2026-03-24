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
        [SerializeField] private RectTransform m_SymbolsParent; // Container for the symbols
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

        private List<TextMeshProUGUI> m_ReelSymbols = new List<TextMeshProUGUI>();
        private int[] m_AvailableMultipliers;
        private int m_InitialCoins;
        private bool m_IsSpinning = false;
        private bool m_IsAdShowing = false;
        private int m_CurrentMultiplier = 1;
        private bool m_RewardClaimed = false;
        
        private float m_ReelOffset = 0f; // Current scroll offset (normalized to symbols)

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
            Debug.Log("[SlotMachine] Initializing Reel...");
            
            if (m_SymbolTemplate == null)
            {
                Debug.LogError("[SlotMachine] ERROR: Symbol Template is not assigned in the Inspector!");
                return;
            }
            if (m_SymbolsParent == null)
            {
                Debug.LogError("[SlotMachine] ERROR: Symbols Parent is not assigned in the Inspector!");
                return;
            }

            // Get multipliers from config zones
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

            // Clear existing symbols
            foreach (var sym in m_ReelSymbols) if (sym != null) Destroy(sym.gameObject);
            m_ReelSymbols.Clear();

            // Create 5 symbols
            if (m_SymbolTemplate != null && m_SymbolsParent != null)
            {
                m_SymbolTemplate.gameObject.SetActive(false);
                for (int i = 0; i < 5; i++)
                {
                    MultiplierZone zone = GetWeightedMultiplierFromConfig();
                    TextMeshProUGUI sym = Instantiate(m_SymbolTemplate, m_SymbolsParent);
                    sym.gameObject.SetActive(true);
                    sym.text = "X" + zone.multiplier;
                    m_ReelSymbols.Add(sym);
                }
                Debug.Log($"[SlotMachine] Successfully created {m_ReelSymbols.Count} reel symbols.");
            }

            UpdateReelPositions();
            
            // Hide legacy pointer if it exists in the prefab
            Transform pointer = transform.Find("Popup/Pointer");
            if (pointer == null) pointer = transform.Find("Pointer");
            if (pointer != null) pointer.gameObject.SetActive(false);
        }

        private int PickWinnerIndexFromCurrentReel()
        {
            float totalWeight = 0;
            Dictionary<int, float> weightsPerIndex = new Dictionary<int, float>();
            
            for (int i = 0; i < m_ReelSymbols.Count; i++)
            {
                int val = 2;
                int.TryParse(m_ReelSymbols[i].text.Replace("X", ""), out val);
                
                // Find weight in config for this multiplier value
                float weight = 1.0f;
                if (m_Config != null && m_Config.zones != null)
                {
                    foreach (var z in m_Config.zones)
                    {
                        if (z.multiplier == val)
                        {
                            weight = z.weight;
                            break;
                        }
                    }
                }
                
                weightsPerIndex[i] = weight;
                totalWeight += weight;
            }
            
            float r = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var pair in weightsPerIndex)
            {
                cumulative += pair.Value;
                if (r <= cumulative) return pair.Key;
            }
            return 2;
        }

        private void OnDestroy()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnMultiplyRewardReceived -= HandleRewardReceived;
                AdsManager.Instance.OnAdClosed -= HandleAdClosed;
            }
        }

        private void UpdateReelPositions()
        {
            int count = m_ReelSymbols.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                // Calculate position relative to container center
                float posIndex = (i + m_ReelOffset) % count;
                if (posIndex < 0) posIndex += count;

                float distanceFromCenter = posIndex - 2f;
                m_ReelSymbols[i].rectTransform.anchoredPosition = new Vector2(0, -distanceFromCenter * m_SymbolSpacing);

                float absDist = Mathf.Abs(distanceFromCenter);
                float normalizedDist = Mathf.Clamp01(absDist / 2f); 

                float alpha = Mathf.Lerp(m_MaxAlpha, m_MinAlpha, normalizedDist);
                float scale = Mathf.Lerp(m_MaxScale, m_MinScale, normalizedDist);

                if (absDist <= 0.2f)
                {
                    int.TryParse(m_ReelSymbols[i].text.Replace("X", ""), out m_CurrentMultiplier);
                }

                Color c = m_ReelSymbols[i].color;
                c.a = alpha;
                m_ReelSymbols[i].color = c;
                m_ReelSymbols[i].transform.localScale = Vector3.one * scale;
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
            
            // PRE-DETERMINE THE WINNER based on weights of the 5 symbols currently in the reel
            int winnerIndex = PickWinnerIndexFromCurrentReel();
            
            float speed = UnityEngine.Random.Range(30f, 40f); 
            float duration = 2.0f; // Shortened to 2.0s
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                    
                float t = elapsed / duration;
                float currentSpeed = Mathf.Lerp(speed, 2f, t * t);
                m_ReelOffset += currentSpeed * Time.deltaTime;
                UpdateReelPositions();
                yield return null;
            }

            // Forced snap target calculation:
            // Goal: m_ReelSymbols[winnerIndex] ends up at center (posIndex 2)
            // Equation: (winnerIndex + targetOffset) % 5 = 2
            // targetOffset should be roughly near current offset
            float count = m_ReelSymbols.Count;
            float currentVal = m_ReelOffset;
            float desiredOffsetBase = 2f - winnerIndex;
            // Shift desiredOffsetBase by multiples of 'count' to be closest to currentVal
            float targetOffset = desiredOffsetBase + (Mathf.Round((currentVal - desiredOffsetBase) / count) * count);

            float snapElapsed = 0f;
            float snapDuration = 0.5f;
            float startOffset = m_ReelOffset;

            while (snapElapsed < snapDuration)
            {
                snapElapsed += Time.deltaTime;
                float t = snapElapsed / snapDuration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                m_ReelOffset = Mathf.Lerp(startOffset, targetOffset, t);
                UpdateReelPositions();
                yield return null;
            }

            m_ReelOffset = targetOffset;
            UpdateReelPositions();
            m_IsSpinning = false;
            
            // Punch Animation on the winning symbol
            StartCoroutine(PunchSymbolRoutine(m_ReelSymbols[winnerIndex].transform));

            Debug.Log($"[SlotMachine] Spin Forced on X{m_CurrentMultiplier} (Symbol {winnerIndex}). Waiting 0.5s...");

            yield return new WaitForSeconds(0.5f);

            Debug.Log("[SlotMachine] Requesting Rewarded Video Ad...");
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
