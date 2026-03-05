using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace Assets.Scripts.Core
{
    public enum ProductTypeID
    {
        NoAds999,
        NoAds499,
        NoAds199,
        Donate199,
        NoAdsCoins999,
        Coins199,
        Coins499,
        Coins999,
        Coins1999,
        Coins4999
    }

    public class IAPManager : MonoBehaviour, IDetailedStoreListener
    {
        public static IAPManager Instance { get; private set; }

        private IStoreController m_StoreController;
        private IExtensionProvider m_StoreExtensionProvider;

        // Product IDs
        public const string ProductNoAds999 = "com.everybodygames.arrowsmaster.no_ads_999";
        public const string ProductNoAds499 = "com.everybodygames.arrowsmaster.no_ads_499";
        public const string ProductNoAds199 = "com.everybodygames.arrowsmaster.no_ads_199";
        public const string ProductDonate199 = "com.everybodygames.arrowsmaster.donate_199";

        // New Product IDs
        public const string ProductCoins199 = "com.everybodygames.arrowsmaster.coins_199";
        public const string ProductCoins499 = "com.everybodygames.arrowsmaster.coins_499";
        public const string ProductCoins999 = "com.everybodygames.arrowsmaster.coins_999";
        public const string ProductCoins1999 = "com.everybodygames.arrowsmaster.coins_1999";
        public const string ProductCoins4999 = "com.everybodygames.arrowsmaster.coins_4999";
        public const string ProductNoAdsCoins999 = "com.everybodygames.arrowsmaster.noadscoins_999";

        private const string NoAdsPrefKey = "UserHasNoAds";

        public bool HasNoAds => PlayerPrefs.GetInt(NoAdsPrefKey, 0) == 1;

        public event Action<bool> OnNoAdsStatusChanged;
        public event Action<string> OnPurchaseSuccess;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject iapGO = new GameObject("IAPManager");
                iapGO.AddComponent<IAPManager>();
                DontDestroyOnLoad(iapGO);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            try
            {
                // Ensure Unity Services are initialized correctly
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log($"[IAPManager] Initializing Unity Services (Current State: {UnityServices.State})...");
                    var options = new InitializationOptions().SetEnvironmentName("production");
                    await UnityServices.InitializeAsync(options);
                }

                Debug.Log($"[IAPManager] Unity Services Initialized. State: {UnityServices.State}");
                InitializePurchasing();
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] Unity Services Initialization Failed: {e.Message}");
                // Even if services fail, we try to initialize purchasing once in case it's a transient issue
                InitializePurchasing();
            }
        }

        private void InitializePurchasing()
        {
            if (IsInitialized()) return;

            // Choosing the module based on platform can be more reliable than auto-detection
            AppStore store = AppStore.NotSpecified;
#if UNITY_ANDROID
            store = AppStore.GooglePlay;
#elif UNITY_IOS || UNITY_IPHONE || UNITY_STANDALONE_OSX
            store = AppStore.AppleAppStore;
#endif

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(store));

            // Add products with explicit store IDs to ensure consistency across platforms
            builder.AddProduct(ProductNoAds999, UnityEngine.Purchasing.ProductType.NonConsumable);
            builder.AddProduct(ProductNoAds499, UnityEngine.Purchasing.ProductType.NonConsumable);
            builder.AddProduct(ProductNoAds199, UnityEngine.Purchasing.ProductType.NonConsumable);
            builder.AddProduct(ProductDonate199, UnityEngine.Purchasing.ProductType.NonConsumable);

            // New Coin Products (Consumable)
            builder.AddProduct(ProductCoins199, UnityEngine.Purchasing.ProductType.Consumable);
            builder.AddProduct(ProductCoins499, UnityEngine.Purchasing.ProductType.Consumable);
            builder.AddProduct(ProductCoins999, UnityEngine.Purchasing.ProductType.Consumable);
            builder.AddProduct(ProductCoins1999, UnityEngine.Purchasing.ProductType.Consumable);
            builder.AddProduct(ProductCoins4999, UnityEngine.Purchasing.ProductType.Consumable);

            // No Ads + Coins bundle (Non-Consumable)
            builder.AddProduct(ProductNoAdsCoins999, UnityEngine.Purchasing.ProductType.NonConsumable);

            Debug.Log($"[IAPManager] Calling UnityPurchasing.Initialize with store: {store}...");
            UnityPurchasing.Initialize(this, builder);
        }

        public bool IsInitialized()
        {
            return m_StoreController != null && m_StoreExtensionProvider != null;
        }

        public void BuyProduct(string productId)
        {
            if (!IsInitialized())
            {
                Debug.LogWarning($"[IAPManager] BuyProduct called but store not initialized. Attempting re-init for: {productId}");
                InitializePurchasing();
                // We don't return here because InitializePurchasing is async in nature (via UnityPurchasing.Initialize)
                // The user will need to tap again once initialized, or we can improve this with a pending purchase queue.
                return;
            }

            Debug.Log($"[IAPManager] Initiating purchase for: {productId}");
            m_StoreController.InitiatePurchase(productId);
        }

        public void PurchaseNoAds(ProductTypeID type)
        {
            string productId = type switch
            {
                ProductTypeID.NoAds999 => ProductNoAds999,
                ProductTypeID.NoAds499 => ProductNoAds499,
                ProductTypeID.NoAds199 => ProductNoAds199,
                ProductTypeID.Donate199 => ProductDonate199,
                ProductTypeID.NoAdsCoins999 => ProductNoAdsCoins999,
                ProductTypeID.Coins199 => ProductCoins199,
                ProductTypeID.Coins499 => ProductCoins499,
                ProductTypeID.Coins999 => ProductCoins999,
                ProductTypeID.Coins1999 => ProductCoins1999,
                ProductTypeID.Coins4999 => ProductCoins4999,
                _ => ProductNoAds999
            };

            BuyProduct(productId);
        }

        // --- IStoreListener Implementation ---

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log($"[IAPManager] Initialization successful. Store: {StandardPurchasingModule.Instance().appStore}");
            m_StoreController = controller;
            m_StoreExtensionProvider = extensions;

            // iOS specific features
            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                var apple = extensions.GetExtension<IAppleExtensions>();
                // Handle "Ask to Buy" deferred purchases
                apple.RegisterPurchaseDeferredListener(OnDeferredPurchase);
            }

            // Check if user already owns No Ads (Cross-session check)
            CheckAlreadyOwnedProducts();
        }

        private void OnDeferredPurchase(Product product)
        {
            Debug.Log($"[IAPManager] Purchase deferred (e.g., Ask to Buy): {product.definition.id}");
        }

        private void CheckAlreadyOwnedProducts()
        {
            // Specifically check for non-consumable "No Ads" products
            // This is crucial for iOS restoration requirements
            bool alreadyOwned = false;
            
            string[] noAdsIds = { ProductNoAds999, ProductNoAds499, ProductNoAds199, ProductNoAdsCoins999 };
            foreach (var id in noAdsIds)
            {
                var product = m_StoreController.products.WithID(id);
                if (product != null && product.hasReceipt)
                {
                    alreadyOwned = true;
                    Debug.Log($"[IAPManager] Found owned non-consumable: {id}");
                    break;
                }
            }

            if (alreadyOwned && !HasNoAds)
            {
                Debug.Log("[IAPManager] Restored 'No Ads' status from existing receipt.");
                SetNoAds(true);
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error}. Message: {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string id = args.purchasedProduct.definition.id;

            // Handle compensation based on product ID
            switch (id)
            {
                case ProductNoAds999:
                    Debug.Log($"[IAPManager] No Ads purchased successfully: {id}");
                    SetNoAds(true);

                    break;
                case ProductNoAds499:
                case ProductNoAds199:
                    Debug.Log($"[IAPManager] No Ads purchased successfully: {id}");
                    SetNoAds(true);
                    break;

                case ProductNoAdsCoins999:
                    Debug.Log($"[IAPManager] No Ads + Coins purchased successfully: {id}");
                    SetNoAds(true);
                    UserDataManager.Instance.AddArrowsCurrency(25000); // 25000 coins placeholder as requested
                    break;

                case ProductCoins199:
                     Debug.Log($"[IAPManager] Coins purchased successfully: {id}");
                    UserDataManager.Instance.AddArrowsCurrency(4500); // 4500 coins placeholder as requested
                    break;
                case ProductCoins499:
                     Debug.Log($"[IAPManager] Coins purchased successfully: {id}");
                    UserDataManager.Instance.AddArrowsCurrency(12000); // 12000 coins placeholder as requested
                    break;
                case ProductCoins999:
                     Debug.Log($"[IAPManager] Coins purchased successfully: {id}");
                    UserDataManager.Instance.AddArrowsCurrency(25000); // 25000 coins placeholder as requested
                    break;
                case ProductCoins1999:
                     Debug.Log($"[IAPManager] Coins purchased successfully: {id}");
                    UserDataManager.Instance.AddArrowsCurrency(60000); // 60000 coins placeholder as requested
                    break;
                case ProductCoins4999:
                     Debug.Log($"[IAPManager] Coins purchased successfully: {id}");
                    UserDataManager.Instance.AddArrowsCurrency(150000); // 150000 coins placeholder as requested
                    break;

                case ProductDonate199:
                    Debug.Log($"[IAPManager] Donation purchased successfully: {id}");
                    break;

                default:
                    Debug.LogWarning($"[IAPManager] ProcessPurchase: Unknown product ID {id}");
                    break;
            }

            // --- Analytics: purchase (Log all successful purchases) ---
            if (FirebaseManager.Instance != null)
            {
                var metadata = args.purchasedProduct.metadata;
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_PURCHASE,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, (double)metadata.localizedPrice),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, metadata.isoCurrencyCode),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_ITEM_ID, id));
            }
            // ----------------------------------------------------------

            OnPurchaseSuccess?.Invoke(id);
            SpawnCoinsExplosion();

            return PurchaseProcessingResult.Complete;
        }

        private void SpawnCoinsExplosion()
        {
            GameObject prefab = Resources.Load<GameObject>("CoinsExplosion");
            if (prefab != null)
            {
                Vector3 spawnPos = new Vector3(-0.5f, 2.4f, 60.2f);
                GameObject explosion = Instantiate(prefab, spawnPos, prefab.transform.rotation);
                Destroy(explosion, 7f);
            }
            else
            {
                Debug.LogWarning("[IAPManager] CoinsExplosion prefab not found in Resources.");
            }
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            Debug.LogError($"[IAPManager] Purchase of {product.definition.id} failed: {reason}");
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description)
        {
            Debug.LogError($"[IAPManager] Purchase of {product.definition.id} failed: {description.reason}. {description.message}");
        }

        private void SetNoAds(bool enabled)
        {
            PlayerPrefs.SetInt(NoAdsPrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[IAPManager] No Ads status updated: {enabled}");
            OnNoAdsStatusChanged?.Invoke(enabled);
        }

        // For testing
        public void RestorePurchases()
        {
            if (!IsInitialized()) return;

            if (Application.platform == RuntimePlatform.IPhonePlayer || 
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("[IAPManager] Restoring purchases...");
                var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
                apple.RestoreTransactions((result, error) => {
                    Debug.Log($"[IAPManager] Restore transactions result: {result}. Error (if any): {error}");
                });
            }
            else
            {
                Debug.Log("[IAPManager] Restore transactions not needed or not supported on this platform.");
            }
        }
    }
}
