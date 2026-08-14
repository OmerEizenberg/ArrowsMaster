using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing.MiniJSON;

/// <summary>
/// Tenjin MMP bootstrap: Connect on every launch, IAP validation, AppLovin ILRD, customer user id.
/// </summary>
public class TenjinManager : MonoBehaviour
{
    private const string AndroidSdkKey = "VQWZYSSNXWSH7FCNEZRGWFAX6J5D4LZH";
    private const string IosSdkKey = "ENR8DD1PVXHUXNUVWXV45S6GSXSDW7NS";

    public static string SdkKey
    {
        get
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IosSdkKey;
#elif UNITY_ANDROID && !UNITY_EDITOR
            return AndroidSdkKey;
#else
            // Editor / other platforms: default to Android key for tooling; Connect is skipped in Editor.
            return AndroidSdkKey;
#endif
        }
    }

    public static TenjinManager Instance { get; private set; }

    private BaseTenjin _tenjin;
    private bool _connected;
    private string _pendingCustomerUserId;

    [Serializable]
    private class AppLovinImpressionData
    {
        public string creative_id;
        public string placement;
        public string format;
        public string country;
        public string ad_revenue_currency;
        public string network_placement;
        public string revenue_precision;
        public string ad_unit_id;
        public double revenue;
        public string network_name;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null)
            return;

        var go = new GameObject("TenjinManager");
        go.AddComponent<TenjinManager>();
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
        // First launch = install; every later launch = app open / session.
        ConnectIfNeeded();
    }

    /// <summary>
    /// Tenjin requires Connect on resume from background for accurate session / app-open metrics.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ConnectIfNeeded();
    }

    private void ConnectIfNeeded()
    {
#if UNITY_EDITOR
        Debug.Log("[TenjinManager] Skipping Connect in Editor.");
        return;
#else
        Connect();
#endif
    }

    public void Connect()
    {
        try
        {
            _tenjin = Tenjin.getInstance(SdkKey);

#if UNITY_ANDROID
            _tenjin.SetAppStoreType(AppStoreType.googleplay);
#endif

            // ATT is already requested by AdsManager/IOSAdsHelper — do not prompt again via Tenjin.
            _tenjin.Connect();
            _connected = true;
            Debug.Log("[TenjinManager] Connected.");

            if (!string.IsNullOrEmpty(_pendingCustomerUserId))
            {
                _tenjin.SetCustomerUserId(_pendingCustomerUserId);
                _pendingCustomerUserId = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TenjinManager] Connect failed: {e.Message}");
        }
    }

    public void SetCustomerUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        userId = userId.Trim();
        if (_tenjin == null || !_connected)
        {
            _pendingCustomerUserId = userId;
            return;
        }

        _tenjin.SetCustomerUserId(userId);
    }

    public void SendEvent(string eventName)
    {
        if (!EnsureReady() || string.IsNullOrEmpty(eventName))
            return;

        _tenjin.SendEvent(eventName);
    }

    public void SendEvent(string eventName, string value)
    {
        if (!EnsureReady() || string.IsNullOrEmpty(eventName))
            return;

        _tenjin.SendEvent(eventName, value);
    }

    /// <summary>
    /// Validates and sends a Unity IAP purchase receipt to Tenjin (Google Play / Apple).
    /// </summary>
    public void TrackPurchase(UnityEngine.Purchasing.Product product)
    {
        if (product == null || !EnsureReady())
            return;

        try
        {
            double unitPrice = decimal.ToDouble(product.metadata.localizedPrice);
            string currencyCode = product.metadata.isoCurrencyCode;
            string productId = product.definition.id;
            string receipt = product.receipt;

            if (string.IsNullOrEmpty(receipt))
            {
                Debug.LogWarning($"[TenjinManager] Empty receipt for {productId}; skipping Transaction.");
                return;
            }

            var wrapper = Json.Deserialize(receipt) as Dictionary<string, object>;
            if (wrapper == null)
            {
                Debug.LogWarning($"[TenjinManager] Could not parse receipt for {productId}.");
                return;
            }

            string store = wrapper.ContainsKey("Store") ? wrapper["Store"] as string : null;
            string payload = wrapper.ContainsKey("Payload") ? wrapper["Payload"] as string : null;

#if UNITY_ANDROID
            if (string.Equals(store, "GooglePlay", StringComparison.Ordinal))
            {
                var googleDetails = Json.Deserialize(payload) as Dictionary<string, object>;
                if (googleDetails == null)
                {
                    Debug.LogWarning($"[TenjinManager] Could not parse Google Play payload for {productId}.");
                    return;
                }

                string googleJson = googleDetails.ContainsKey("json") ? googleDetails["json"] as string : null;
                string googleSig = googleDetails.ContainsKey("signature") ? googleDetails["signature"] as string : null;
                _tenjin.Transaction(productId, currencyCode, 1, unitPrice, null, googleJson, googleSig);
                Debug.Log($"[TenjinManager] Android Transaction sent for {productId}.");
                return;
            }
#elif UNITY_IOS
            string transactionId = product.transactionID;
            // Payload is the base64 ASN.1 receipt for Apple.
            _tenjin.Transaction(productId, currencyCode, 1, unitPrice, transactionId, payload, null);
            Debug.Log($"[TenjinManager] iOS Transaction sent for {productId}.");
            return;
#endif

            Debug.LogWarning($"[TenjinManager] Unsupported store '{store}' for {productId}; skipping Transaction.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TenjinManager] TrackPurchase failed: {e.Message}");
        }
    }

    /// <summary>
    /// Sends AppLovin MAX impression-level revenue to Tenjin (paid ILRD feature).
    /// </summary>
    public void TrackAppLovinImpression(
        string creativeId,
        string placement,
        string format,
        string networkPlacement,
        string revenuePrecision,
        string adUnitId,
        double revenue,
        string networkName)
    {
        if (!EnsureReady() || revenue <= 0d || string.IsNullOrEmpty(networkName))
            return;

        try
        {
            string country = null;
            try
            {
                if (MaxSdk.IsInitialized())
                    country = MaxSdk.GetSdkConfiguration().CountryCode;
            }
            catch
            {
                // MAX may not be ready yet; country is optional.
            }

            var impressionData = new AppLovinImpressionData
            {
                creative_id = creativeId ?? string.Empty,
                placement = placement ?? string.Empty,
                format = format ?? string.Empty,
                country = country ?? string.Empty,
                ad_revenue_currency = "USD",
                network_placement = networkPlacement ?? string.Empty,
                revenue_precision = revenuePrecision ?? string.Empty,
                ad_unit_id = adUnitId ?? string.Empty,
                revenue = revenue,
                network_name = networkName
            };

            string jsonString = JsonUtility.ToJson(impressionData);
            _tenjin.AppLovinImpressionFromJSON(jsonString);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TenjinManager] TrackAppLovinImpression failed: {e.Message}");
        }
    }

    public void TrackAppLovinImpression(MaxSdkBase.AdInfo adInfo)
    {
        if (adInfo == null)
            return;

        TrackAppLovinImpression(
            adInfo.CreativeIdentifier,
            adInfo.Placement,
            adInfo.AdFormat,
            adInfo.NetworkPlacement,
            adInfo.RevenuePrecision,
            adInfo.AdUnitIdentifier,
            adInfo.Revenue,
            adInfo.NetworkName);
    }

    private bool EnsureReady()
    {
#if UNITY_EDITOR
        return false;
#else
        if (_tenjin != null && _connected)
            return true;

        Connect();
        return _tenjin != null && _connected;
#endif
    }
}
