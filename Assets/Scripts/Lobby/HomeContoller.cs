using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro; 

using Assets.Scripts.Core;
using Assets.Scripts.Data;

namespace Assets.Scripts.Lobby
{
    public class HomeContoller : MonoBehaviour
    {
        [SerializeField] private GameObject m_LobbyUI;
        [SerializeField] private GameObject m_GameUI;


        [SerializeField] private GameObject m_CalanderLayer;
        [SerializeField] private GameObject m_SettingsLayer;
        [SerializeField] private GameObject m_DonateLayer;
        [SerializeField] private GameObject m_NoAdsLayer;
        [SerializeField] private GameObject m_ShopLayer;
        [SerializeField] private GameObject m_NoAdsCoinsBundleButton;
        [SerializeField] private GameObject m_NoAdsBadge;
        [SerializeField] private GameObject m_LobbyAdReadyImage;

        [SerializeField] private TextMeshProUGUI m_TitleText;
        [SerializeField] private TextMeshProUGUI m_LevelText;
        [SerializeField] private TextMeshProUGUI m_DifficultyText;
        [SerializeField] private TextMeshProUGUI m_LobbyCurrencyText;
        [SerializeField] private TextMeshProUGUI m_ShopCurrencyText;

        [SerializeField] private Color m_CircleColor;
        [SerializeField] private Color m_SuperHardColor;
        [SerializeField] private Color m_NightmareColor;
        [SerializeField] private Color m_HardColor;
        [SerializeField] private Color m_EasyColor;
        
        [SerializeField] private Color m_LevelColor;
        [SerializeField] private MonthlyChallengeController m_MonthlyChallengeController;

        // Currency animation
        [SerializeField] private float m_CoinAnimDuration = 1.5f;
        [SerializeField] private float m_CoinScalePunch = 2.25f;
        // Static so the last displayed value is remembered across OnDisable/OnEnable cycles
        private static int s_LastDisplayedCurrencyValue = -1;
        private Coroutine m_CoinCountCoroutine;
        private Coroutine m_LobbyScaleCoroutine;
        private Coroutine m_ShopScaleCoroutine;

        private void OnEnable()
        {
            //PlayerPrefs.DeleteAll();
            RefreshLobbyUI();

            int currentCoins = UserDataManager.Instance.ArrowsCurrency;

            if (s_LastDisplayedCurrencyValue < 0)
            {
                // Very first time ever — set immediately, no animation
                s_LastDisplayedCurrencyValue = currentCoins;
                SetCurrencyTextImmediate(currentCoins);
            }
            else if (s_LastDisplayedCurrencyValue != currentCoins)
            {
                // Coins changed while we were away — animate from last known to current
                SetCurrencyTextImmediate(s_LastDisplayedCurrencyValue);
                UpdateCurrencyUI(currentCoins);
            }
            else
            {
                // No change — just display current value instantly
                SetCurrencyTextImmediate(currentCoins);
            }

            UserDataManager.Instance.OnLevelChanged += RefreshLobbyUI;
            UserDataManager.Instance.OnCurrencyChanged += UpdateCurrencyUI;
            
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnNoAdsStatusChanged += HandleNoAdsStatusChanged;
                IAPManager.Instance.OnPurchaseSuccess += HandlePurchaseSuccess;
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived += HandleCoinsRewardReceived;
            }

            if(GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                OnCalanderButtonClicked();
            }
        }

        private void OnDisable()
        {
            UserDataManager.Instance.OnLevelChanged -= RefreshLobbyUI;
            UserDataManager.Instance.OnCurrencyChanged -= UpdateCurrencyUI;

            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.OnNoAdsStatusChanged -= HandleNoAdsStatusChanged;
                IAPManager.Instance.OnPurchaseSuccess -= HandlePurchaseSuccess;
            }

            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.OnCoinsRewardReceived -= HandleCoinsRewardReceived;
            }

            // Stop any running animation coroutines
            if (m_CoinCountCoroutine != null) StopCoroutine(m_CoinCountCoroutine);
            if (m_LobbyScaleCoroutine != null) StopCoroutine(m_LobbyScaleCoroutine);
            if (m_ShopScaleCoroutine != null) StopCoroutine(m_ShopScaleCoroutine);
        }

        private void Update()
        {
            UpdateLobbyAdReadyImage();
        }

        private void SetCurrencyTextImmediate(int amount)
        {
            string formatted = amount.ToString("N0");
            if (m_LobbyCurrencyText != null) m_LobbyCurrencyText.text = formatted;
            if (m_ShopCurrencyText != null) m_ShopCurrencyText.text = formatted;
        }

        private void UpdateCurrencyUI(int newAmount)
        {
            if (m_CoinCountCoroutine != null) StopCoroutine(m_CoinCountCoroutine);
            m_CoinCountCoroutine = StartCoroutine(AnimateCurrencyText(s_LastDisplayedCurrencyValue, newAmount));

            // Scale punch on both texts
            if (m_LobbyCurrencyText != null)
            {
                if (m_LobbyScaleCoroutine != null) StopCoroutine(m_LobbyScaleCoroutine);
                m_LobbyScaleCoroutine = StartCoroutine(ScalePunch(m_LobbyCurrencyText.transform));
            }
            if (m_ShopCurrencyText != null)
            {
                if (m_ShopScaleCoroutine != null) StopCoroutine(m_ShopScaleCoroutine);
                m_ShopScaleCoroutine = StartCoroutine(ScalePunch(m_ShopCurrencyText.transform));
            }
        }

        private IEnumerator AnimateCurrencyText(int fromValue, int toValue)
        {
            float elapsed = 0f;
            float duration = m_CoinAnimDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease out for a satisfying deceleration
                float easedT = 1f - (1f - t) * (1f - t);
                int current = (int)Mathf.Lerp(fromValue, toValue, easedT);
                s_LastDisplayedCurrencyValue = current;
                SetCurrencyTextImmediate(current);
                yield return null;
            }

            // Ensure final value is exact
            s_LastDisplayedCurrencyValue = toValue;
            SetCurrencyTextImmediate(toValue);
            m_CoinCountCoroutine = null;
        }

        private IEnumerator ScalePunch(Transform target)
        {
            Vector3 originalScale = Vector3.one;
            Vector3 punchScale = originalScale * m_CoinScalePunch;
            float halfDuration = m_CoinAnimDuration * 0.35f;

            // Scale up
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(originalScale, punchScale, t);
                yield return null;
            }

            // Scale back down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                target.localScale = Vector3.Lerp(punchScale, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }

        private void HandlePurchaseSuccess(string productId)
        {
            HideShop();
        }

        private void HandleCoinsRewardReceived()
        {
            HideShop();
        }

        private void HandleNoAdsStatusChanged(bool hasNoAds)
        {
            if (hasNoAds)
            {
                if (m_NoAdsLayer != null) m_NoAdsLayer.SetActive(false);
            }
            RefreshLobbyUI();
        }

        public void RefreshLobbyUI()
        {
            // Ensure GameUI is hidden when refreshing lobby (returning to lobby)
            if (m_GameUI != null) m_GameUI.SetActive(false);
            else if (GameManager.Instance != null && GameManager.Instance.m_GameUI != null)
                GameManager.Instance.m_GameUI.gameObject.SetActive(false);

            m_TitleText.text = "Arrows Master";
            
            string levelId;
            string folder;
            
            if (GameManager.Instance != null && !GameManager.Instance.p_isLevelProgression)
            {
                m_LevelText.text = $"Challenge {m_MonthlyChallengeController.p_CurrentMonth}/{m_MonthlyChallengeController.p_CurrentDay}/{m_MonthlyChallengeController.p_CurrentYear}";
                int month = m_MonthlyChallengeController.p_CurrentMonth;
                int day = m_MonthlyChallengeController.p_CurrentDay;
                int year = m_MonthlyChallengeController.p_CurrentYear;
                levelId = $"level{month + day + (year % 10)}";
                folder = "ChallengeLevels";
            }
            else
            {
                m_LevelText.text = $"Level {UserDataManager.Instance.CurrentLevel}";
                levelId = $"level{UserDataManager.Instance.CurrentLevel}";
                folder = "Levels";
            }

            TextAsset jsonFile = null;
            
            if (folder == "Levels" && GameManager.Instance != null && GameManager.Instance.levelManager != null)
            {
                // Use the LevelManager to get the correct level file, handling looping if max level is reached
                jsonFile = GameManager.Instance.levelManager.GetLevelTextAsset(levelId);
            }
            else
            {
                // Fallback for Challenge levels or if LevelManager is not available
                jsonFile = Resources.Load<TextAsset>($"{folder}/{levelId}");
            }
            
            if (jsonFile != null)
            {
                LevelData data = JsonUtility.FromJson<LevelData>(jsonFile.text);
                int totalPoints = 0;
                if (data != null && data.arrows != null)
                {
                    foreach (var arrow in data.arrows)
                    {
                        if (arrow.path != null) totalPoints += arrow.path.Count;
                    }
                }

                if (totalPoints < 120)
                {
                    m_DifficultyText.text = "Easy Level";
                    Color c = m_EasyColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (totalPoints < 400)
                {
                    m_DifficultyText.text = "Hard Level";
                    Color c = m_HardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else if (totalPoints < 900)
                {
                    m_DifficultyText.text = "Super Hard Level";
                    Color c = m_SuperHardColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
                else
                {
                    m_DifficultyText.text = "Nightmare Level";
                    Color c = m_NightmareColor; c.a = 1f;
                    m_DifficultyText.color = c;
                }
            }
            else
            {
                m_DifficultyText.text = "Level Info Unavailable";
                Color c = m_EasyColor; c.a = 1f;
                m_DifficultyText.color = c;
            }

            m_LevelText.color = m_LevelColor;

            // Hide the No Ads + Coins bundle button if the user already has No Ads
            if (m_NoAdsCoinsBundleButton != null && IAPManager.Instance != null)
            {
                m_NoAdsCoinsBundleButton.SetActive(!IAPManager.Instance.HasNoAds);
                m_NoAdsBadge.SetActive(!IAPManager.Instance.HasNoAds);
            }

            UpdateLobbyAdReadyImage();
        }

        private void UpdateLobbyAdReadyImage()
        {
            if (m_LobbyAdReadyImage == null) return;

            bool isCooldownActive = false;
            string cooldownEndKey = "ShopAdCooldownEnd";

            if (PlayerPrefs.HasKey(cooldownEndKey))
            {
                string storedValue = PlayerPrefs.GetString(cooldownEndKey);
                if (long.TryParse(storedValue, out long binaryTime))
                {
                    System.DateTime cooldownEndTime = System.DateTime.FromBinary(binaryTime);
                    if (System.DateTime.Now < cooldownEndTime)
                    {
                        isCooldownActive = true;
                    }
                }
            }

            m_LobbyAdReadyImage.SetActive(!isCooldownActive);
        }
        
        public void OnSettingsButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_SettingsLayer.activeInHierarchy)
            {
                m_SettingsLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(true);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
            }
        }
        public void OnDonateButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_DonateLayer.activeInHierarchy)
            {
                m_DonateLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
                m_DonateLayer.SetActive(true);
            }
        }

        public void OnNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_NoAdsLayer.activeInHierarchy)
            {
                m_NoAdsLayer.SetActive(false);
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(false);
                m_ShopLayer.SetActive(false);
                m_NoAdsLayer.SetActive(true);
            }
        }

        public void OnBuyDonationButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.Donate199);
            m_DonateLayer.SetActive(false);
        }

        public void OnBuyNoAdsButtonClicked()
        {
            SoundManager.Instance.PlayClick();
            IAPManager.Instance.PurchaseNoAds(ProductTypeID.NoAds999);
            m_NoAdsLayer.SetActive(false);
        }

        public void OnCalanderButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            if(m_CalanderLayer.activeInHierarchy)
            {
                m_CalanderLayer.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
            }else{
                m_SettingsLayer.SetActive(false);
                m_CalanderLayer.SetActive(true);
                m_ShopLayer.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = false;
            }
            RefreshLobbyUI();
        }
        public void OnHomeButtonClicked()
        {
            SoundManager.Instance.PlayClick();

            m_SettingsLayer.SetActive(false);
            m_CalanderLayer.SetActive(false);
            m_ShopLayer.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.p_isLevelProgression = true;
            RefreshLobbyUI();
        }

        public void OnShopButtonClicked()
        {
            if (m_ShopLayer.activeInHierarchy)
            {
                HideShop();
            }
            else
            {
                ShowShop();
            }
        }

        public void ShowShop()
        {
            SoundManager.Instance.PlayShop();
            m_SettingsLayer.SetActive(false);
            m_DonateLayer.SetActive(false);
            m_NoAdsLayer.SetActive(false);
            m_ShopLayer.SetActive(true);
        }

        public void HideShop()
        {
            SoundManager.Instance.PlayClick();
            m_ShopLayer.SetActive(false);
        }

        public void OnBuyProductButtonClicked(string productId)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            if (IAPManager.Instance != null)
            {
                IAPManager.Instance.BuyProduct(productId);
            }
            else
            {
                Debug.LogError("[HomeContoller] IAPManager.Instance is null!");
            }
        }

        public void OnWatchAdForCoinsButtonClicked()
        {
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.ShowRewardedForCoins();
            }
            else
            {
                Debug.LogError("[HomeContoller] AdsManager.Instance is null!");
            }
        }

        public void OnPlayButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            string levelName = $"level{UserDataManager.Instance.CurrentLevel}";
            Debug.Log($"[HomeContoller] Play clicked. Starting Level: {levelName}");
            
            SwitchToGameUI();
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartLevel(levelName);
            }
        }

        public void OnCalenderPlayButtonClicked()
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
            
            if (m_MonthlyChallengeController == null)
            {
                Debug.LogError("[HomeContoller] MonthlyChallengeController reference missing!");
                return;
            }

            int month = m_MonthlyChallengeController.p_CurrentMonth;
            int day = m_MonthlyChallengeController.p_CurrentDay;
            int year = m_MonthlyChallengeController.p_CurrentYear;

            string levelName = $"level{month + day + (year % 10)}";
            Debug.Log($"[HomeContoller] Calendar Play clicked. Starting Challenge: {levelName}");

            SwitchToGameUI();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartChallengeLevel(levelName, year, month, day);
            }
        }

        private void SwitchToGameUI()
        {
            // Use local references if assigned, otherwise fallback to GameManager
            GameObject lobby = m_LobbyUI;
            GameObject game = m_GameUI;

            if (lobby == null && GameManager.Instance != null) lobby = GameManager.Instance.m_LobbyUI;
            if (game == null && GameManager.Instance != null) game = GameManager.Instance.m_GameUI.gameObject;

            if (lobby != null)
            {
                Debug.Log($"[HomeContoller] Hiding Lobby UI: {lobby.name}");
                lobby.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[HomeContoller] Could not find Lobby UI reference to hide!");
            }

            if (game != null)
            {
                Debug.Log($"[HomeContoller] Showing Game UI: {game.name}");
                game.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[HomeContoller] Could not find Game UI reference to show!");
            }
        }
    }
}
