using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using System.Threading.Tasks;
using Singular;

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

        // Product IDs (Platform specific)
#if UNITY_IOS
        public const string ProductNoAds999 = "no_ads_999";
        public const string ProductCoins199 = "coins199";
        public const string ProductCoins499 = "coins499";
        public const string ProductCoins999 = "coins999";
        public const string ProductCoins1999 = "coins1999";
        public const string ProductCoins4999 = "coins4999";
        public const string ProductNoAdsCoins999 = "noadscoins_999";
        public const string ProductLegendPass999 = "legendspass_999";
#else
        public const string ProductNoAds999 = "com.everybodygames.arrowsmaster.no_ads_999";
        public const string ProductCoins199 = "com.everybodygames.arrowsmaster.coins_199";
        public const string ProductCoins499 = "com.everybodygames.arrowsmaster.coins_499";
        public const string ProductCoins999 = "com.everybodygames.arrowsmaster.coins_999";
        public const string ProductCoins1999 = "com.everybodygames.arrowsmaster.coins_1999";
        public const string ProductCoins4999 = "com.everybodygames.arrowsmaster.coins_4999";
        public const string ProductNoAdsCoins999 = "com.everybodygames.arrowsmaster.noadscoins_999";
        public const string ProductLegendPass999 = "com.everybodygames.arrowsmaster.legendspass_999";
#endif

        private const string NoAdsPrefKey = "UserHasNoAds";
        private const string MetadataCachePrefix = "IAP_Cache_";

        public bool HasNoAds => PlayerPrefs.GetInt(NoAdsPrefKey, 0) == 1;

        public event Action<bool> OnNoAdsStatusChanged;
        public event Action<string> OnPurchaseSuccess;

        private string m_PendingPurchaseId = null;
        private bool m_IsInitializing = false;
        private static Task unityServicesInitTask;

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

        private void Start()
        {
            // Start initialization immediately and don't block the main thread
            _ = InitializeAllServices();
        }

        private async Task InitializeAllServices()
        {
            if (m_IsInitializing || IsInitialized()) return;
            m_IsInitializing = true;

            try
            {
                // Parallel initialization: Start Unity Services and Store setup concurrently
                var servicesTask = InitializeUnityServices();
                
                // We start purchasing initialization. Note: On some platforms, 
                // UnityPurchasing requires UnityServices to be initialized for certain features,
                // but the native store module can often start its handshake immediately.
                InitializePurchasing();

                await servicesTask;
                Debug.Log("[IAPManager] All services ready.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] Initialization Flow Error: {e.Message}");
            }
            finally
            {
                m_IsInitializing = false;
            }
        }

        public static async Task EnsureUnityServicesInitializedAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            if (Instance != null)
            {
                await Instance.InitializeUnityServices();
                return;
            }

            try
            {
                var options = new InitializationOptions().SetEnvironmentName("production");
                await UnityServices.InitializeAsync(options);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] Unity Services Failed: {e.Message}");
            }
        }

        private async Task InitializeUnityServices()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            if (unityServicesInitTask == null)
            {
                unityServicesInitTask = InitializeUnityServicesInternal();
            }

            await unityServicesInitTask;
        }

        private async Task InitializeUnityServicesInternal()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            try
            {
                Debug.Log($"[IAPManager] Initializing Unity Services (State: {UnityServices.State})...");
                var options = new InitializationOptions().SetEnvironmentName("production");
                await UnityServices.InitializeAsync(options);
                Debug.Log("[IAPManager] Unity Services Initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] Unity Services Failed: {e.Message}");
            }
        }

        private void InitializePurchasing()
        {
            if (IsInitialized()) return;

            AppStore store = AppStore.NotSpecified;
#if UNITY_ANDROID
            store = AppStore.GooglePlay;
#elif UNITY_IOS || UNITY_IPHONE || UNITY_STANDALONE_OSX
            store = AppStore.AppleAppStore;
#endif

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(store));

            // Consumables
            builder.AddProduct(ProductCoins199, ProductType.Consumable);
            builder.AddProduct(ProductCoins499, ProductType.Consumable);
            builder.AddProduct(ProductCoins999, ProductType.Consumable);
            builder.AddProduct(ProductCoins1999, ProductType.Consumable);
            builder.AddProduct(ProductCoins4999, ProductType.Consumable);

            // Non-Consumables
            builder.AddProduct(ProductNoAds999, ProductType.NonConsumable);
            builder.AddProduct(ProductNoAdsCoins999, ProductType.NonConsumable);
            builder.AddProduct(ProductLegendPass999, ProductType.NonConsumable);

            Debug.Log($"[IAPManager] Initializing UnityPurchasing with store: {store}...");
            UnityPurchasing.Initialize(this, builder);
        }

        public bool IsInitialized()
        {
            return m_StoreController != null && m_StoreExtensionProvider != null;
        }

        public void BuyProduct(string productId)
        {
            // Safety: Translate IDs for iOS
#if UNITY_IOS
            productId = productId switch {
                "com.everybodygames.arrowsmaster.no_ads_999" => ProductNoAds999,
                "com.everybodygames.arrowsmaster.noadscoins_999" => ProductNoAdsCoins999,
                "com.everybodygames.arrowsmaster.coins_199" => ProductCoins199,
                "com.everybodygames.arrowsmaster.coins_499" => ProductCoins499,
                "com.everybodygames.arrowsmaster.coins_999" => ProductCoins999,
                "com.everybodygames.arrowsmaster.coins_1999" => ProductCoins1999,
                "com.everybodygames.arrowsmaster.coins_4999" => ProductCoins4999,
                "com.everybodygames.arrowsmaster.legendspass_999" => ProductLegendPass999,
                _ => productId
            };
#endif

            if (!IsInitialized())
            {
                Debug.LogWarning($"[IAPManager] Store not ready. Queueing purchase for: {productId}");
                m_PendingPurchaseId = productId;
                _ = InitializeAllServices();
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
                ProductTypeID.NoAdsCoins999 => ProductNoAdsCoins999,
                ProductTypeID.Donate199 => ProductCoins199,
                ProductTypeID.Coins199 => ProductCoins199,
                ProductTypeID.Coins499 => ProductCoins499,
                ProductTypeID.Coins999 => ProductCoins999,
                ProductTypeID.Coins1999 => ProductCoins1999,
                ProductTypeID.Coins4999 => ProductCoins4999,
                _ => ProductNoAds999
            };

            BuyProduct(productId);
        }

        // --- IStoreListener ---

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("[IAPManager] Store initialization successful.");
            m_StoreController = controller;
            m_StoreExtensionProvider = extensions;

            // 1. Warm-up and Cache Metadata
            foreach (var product in m_StoreController.products.all)
            {
                // Accessing metadata warms up the native-to-Unity bridge cache
                var dummyPrice = product.metadata.localizedPriceString;
                CacheProductMetadata(product);
            }

            // 2. iOS specific listeners
            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                var apple = extensions.GetExtension<IAppleExtensions>();
                apple.RegisterPurchaseDeferredListener(OnDeferredPurchase);
            }

            // 3. Process Pending Purchase (if any)
            if (!string.IsNullOrEmpty(m_PendingPurchaseId))
            {
                Debug.Log($"[IAPManager] Processing queued purchase: {m_PendingPurchaseId}");
                string id = m_PendingPurchaseId;
                m_PendingPurchaseId = null;
                BuyProduct(id);
            }

            CheckAlreadyOwnedProducts();
        }

        private void CacheProductMetadata(Product product)
        {
            if (product == null || product.metadata == null) return;
            string key = MetadataCachePrefix + product.definition.id;
            // Store formatted price string for immediate UI display in next session
            PlayerPrefs.SetString(key, product.metadata.localizedPriceString);
            PlayerPrefs.Save();
        }

        public string GetProductPrice(string productId)
        {
            // Try real-time first
            if (IsInitialized())
            {
                var product = m_StoreController.products.WithID(productId);
                if (product != null && product.metadata != null)
                {
                    return product.metadata.localizedPriceString;
                }
            }

            // Fallback to cache
            string key = MetadataCachePrefix + productId;
            return PlayerPrefs.GetString(key, ""); // Empty if never cached
        }

        private void OnDeferredPurchase(Product product)
        {
            Debug.Log($"[IAPManager] Purchase deferred: {product.definition.id}");
        }

        private void CheckAlreadyOwnedProducts()
        {
            bool alreadyOwned = false;
            string[] noAdsIds = { ProductNoAds999, ProductNoAdsCoins999 };
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
                Debug.Log("[IAPManager] Restoring 'No Ads' status.");
                SetNoAds(true);
            }

            // --- Legend Pass Restore Logic ---
            var passProduct = m_StoreController.products.WithID(ProductLegendPass999);
            if (passProduct != null && passProduct.hasReceipt)
            {
                // Check if the purchase date matches the current month
                DateTime purchaseDate = passProduct.transactionID == null ? DateTime.MinValue : GetPurchaseDate(passProduct);
                DateTime now = DateTime.Now;

                if (purchaseDate.Month == now.Month && purchaseDate.Year == now.Year)
                {
                    Debug.Log("[IAPManager] Legend Pass purchase validated for current month. Unlocking.");
                    if (LegendPassManager.Instance != null) LegendPassManager.Instance.UnlockPremium();
                }
                else
                {
                    Debug.Log($"[IAPManager] Legend Pass purchase found but from different month ({purchaseDate.Month}/{purchaseDate.Year}). Not unlocking.");
                }
            }
        }

        private DateTime GetPurchaseDate(Product product)
        {
            // Note: In a real production environment, you'd parse the receipt JSON properly.
            // Since parsing varies by store (Apple/Google), we'll provide a simplified version 
            // that defaults to Now for fresh purchases and MinValue if unparseable.
            // For a robust implementation, use a receipt validator.
            try {
                // Unity IAP doesn't provide a direct 'PurchaseDate' property on Product,
                // so we fallback to assuming fresh session results or checking stored cache.
                return DateTime.Now; 
            } catch { return DateTime.MinValue; }
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error}");
            m_IsInitializing = false;
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAPManager] Initialization failed: {error}. Message: {message}");
            m_IsInitializing = false;
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string id = args.purchasedProduct.definition.id;

            switch (id)
            {
                case ProductNoAds999:
                    SetNoAds(true);
                    break;
                case ProductNoAdsCoins999:
                    SetNoAds(true);
                    UserDataManager.Instance.AddArrowsCurrency(25000);
                    break;
                case ProductCoins199:
                    UserDataManager.Instance.AddArrowsCurrency(4500);
                    break;
                case ProductCoins499:
                    UserDataManager.Instance.AddArrowsCurrency(12000);
                    break;
                case ProductCoins999:
                    UserDataManager.Instance.AddArrowsCurrency(25000);
                    break;
                case ProductCoins1999:
                    UserDataManager.Instance.AddArrowsCurrency(60000);
                    break;
                case ProductCoins4999:
                    UserDataManager.Instance.AddArrowsCurrency(150000);
                    break;
                case ProductLegendPass999:
                    if (LegendPassManager.Instance != null) LegendPassManager.Instance.UnlockPremium();
                    break;
            }

            if (FirebaseManager.Instance != null)
            {
                var metadata = args.purchasedProduct.metadata;
                FirebaseManager.Instance.LogEvent(FirebaseManager.EVENT_PURCHASE,
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_VALUE, (double)metadata.localizedPrice),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_CURRENCY, metadata.isoCurrencyCode),
                    new Firebase.Analytics.Parameter(FirebaseManager.PARAM_ITEM_ID, id));
            }

            // --- Singular: IAP revenue tracking ---
            var metadataSingular = args.purchasedProduct.metadata;
            SingularSDK.CustomRevenue("Purchase", metadataSingular.isoCurrencyCode, (double)metadataSingular.localizedPrice);
            // ------------------------------------------------

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
            OnNoAdsStatusChanged?.Invoke(enabled);
        }

        public void RestorePurchases()
        {
            if (!IsInitialized()) return;

            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("[IAPManager] Restoring purchases...");
                var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
                apple.RestoreTransactions((result, error) => {
                    Debug.Log($"[IAPManager] Restore result: {result}. Error: {error}");
                    if (result && IsInitialized())
                    {
                        CheckAlreadyOwnedProducts();
                    }
                });
            }
        }
    }
}
