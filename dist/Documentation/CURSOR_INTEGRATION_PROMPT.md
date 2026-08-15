# LiftEngine — One-Shot AI Integration Prompt

Copy everything inside the block below and paste it into Cursor (Agent mode) after importing **com.liftengine.sdk** and **AppLovin MAX** into the Unity project.

---

## Prompt (copy from here)

```
Integrate the LiftEngine Unity SDK (com.liftengine.sdk) with our existing AppLovin MAX ads setup.

## Goal
Route Banner, Interstitial, and Rewarded ads through LiftEngineSdk while keeping our existing MAX integration as fallback. Do not change unrelated game systems.

## Prerequisites — verify first
Before writing code, confirm:
1. Package `com.liftengine.sdk` is installed (Window → LiftEngine → Integration Manager exists)
2. Package `com.applovin.mediation.ads` is installed and MAX already initializes in the project
3. Find the main ads manager script (search: AdsManager, AdManager, MonetizationManager, MaxSdk.InitializeSdk)
4. Find the IAP manager script (search: IAPManager, PurchaseManager, ProcessPurchase, OnPurchaseSuccess)

If LiftEngineSettings.asset does not exist at Assets/Resources/LiftEngineSettings.asset:
- Tell me to open Window → LiftEngine → Integration Manager → Create Settings Asset
- STOP and wait — I will fill API key and ad unit IDs manually

## Hard rules
- MAX MUST initialize BEFORE LiftEngine. Never call LiftEngineSdk.Initialize() before MaxSdk.InitializeSdk() succeeds.
- Keep all existing business rules (cooldowns, level gates, remove-ads IAP, consent) unchanged — only swap the ad load/show layer.
- If LiftEngine settings are missing or apiKey is empty, skip LiftEngine entirely and keep current MAX behavior.
- If LiftEngine init fails, fall back to direct MaxSdk load/show — never block the player from seeing ads.
- Do NOT modify files inside Packages/com.liftengine.sdk/
- Do NOT remove existing MaxSdk callback subscriptions — they are needed for fallback.

## Integration pattern

### State flags (add to ads manager)
```csharp
private bool _liftEngineEnabled;
private bool _liftEngineReady;
private bool _liftEngineInitSettled;
private bool _liftEngineStartRequested;
private bool _liftEngineCallbacksSubscribed;
private int _liftEngineInitRetryAttempt;
private float _liftEngineInitStartedRealtime = -1f;
private float _lastLiftEngineBackgroundRetryRealtime = -1f;
private Coroutine _liftEngineInitRetryCoroutine;

private const float LiftEngineInitCallbackTimeoutSeconds = 45f;
private const int LiftEngineMaxInitRetryAttempts = 3;
private const float LiftEngineInitRetryBaseDelaySeconds = 5f;
private const float LiftEngineBackgroundRetryIntervalSeconds = 60f;
```

### Step 1 — Start LiftEngine after MAX init
In the existing MaxSdk.OnSdkInitialized / OnSdkInitialized callback (after isInitialized = true):

```csharp
TryStartLiftEngine();
EnsureDirectMaxAdsLoaded(); // always preload ads via direct MAX while LiftEngine warms up
if (!_liftEngineEnabled)
{
    LoadInterstitial();
    LoadRewarded();
}
```

`EnsureDirectMaxAdsLoaded()` = subscribe MAX callbacks if needed, then call existing LoadInterstitial/LoadRewarded/banner load. Players must never wait for LiftEngine before ads preload.

Implement TryStartLiftEngine():
```csharp
using LiftEngine;

private void TryStartLiftEngine()
{
    if (_liftEngineStartRequested) return;

    var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
    if (settings == null || string.IsNullOrWhiteSpace(settings.apiKey))
    {
        _liftEngineEnabled = false;
        Debug.Log("[AdsManager] LiftEngine disabled — missing settings or API key.");
        return;
    }

    _liftEngineStartRequested = true;
    _liftEngineEnabled = true;
    _liftEngineInitStartedRealtime = Time.realtimeSinceStartup;
    SubscribeLiftEngineCallbacks();
    LiftEngineSdk.Initialize(settings);
    ApplyLiftEngineContext();
}

private void ApplyLiftEngineContext()
{
    if (!_liftEngineEnabled) return;

#if UNITY_IOS && !UNITY_EDITOR
    // Use our existing ATT helper if we have one; otherwise skip
    if (TryGetIdfaAuthorized(out bool authorized))
        LiftEngineSdk.SetIdfaApproved(authorized);
#endif

    ApplyLiftEngineAttribution();
    LiftEngineSdk.SendReport();
}

private void ApplyLiftEngineAttribution()
{
    // Wire to our MMP — search codebase for AppsFlyer, Singular, Adjust attribution callbacks
    // installType must be "Organic" or "Non-organic"
    // mediaSource = network name from MMP, or empty string if unknown
    if (TryGetAttribution(out string installType, out string mediaSource))
        LiftEngineSdk.SetAttribution(installType, mediaSource);
}

private void SubscribeLiftEngineCallbacks()
{
    if (_liftEngineCallbacksSubscribed) return;
    LiftEngineSdkCallbacks.OnSdkInitializedEvent += OnLiftEngineSdkInitialized;
    LiftEngineSdkCallbacks.OnAdLoadedEvent += OnLiftEngineAdLoaded;
    LiftEngineSdkCallbacks.OnAdDisplayedEvent += OnLiftEngineAdDisplayed;
    LiftEngineSdkCallbacks.OnAdHiddenEvent += OnLiftEngineAdHidden;
    LiftEngineSdkCallbacks.OnAdRevenuePaidEvent += OnLiftEngineAdRevenuePaid;
    LiftEngineSignalBus.AdReadyStateChanged += OnLiftEngineAdReadyStateChanged;
    _liftEngineCallbacksSubscribed = true;
}

private void OnLiftEngineSdkInitialized(LiftEngineInitializationStatus status)
{
    if (_liftEngineInitSettled) return;
    _liftEngineInitSettled = true;
    CancelLiftEngineInitRetry();

    if (status == LiftEngineInitializationStatus.Success)
    {
        _liftEngineReady = true;
        _liftEngineInitRetryAttempt = 0;
    }
    else
    {
        _liftEngineReady = false;
        _liftEngineInitRetryAttempt++;
        EnsureDirectMaxAdsLoaded();
        if (_liftEngineInitRetryAttempt < LiftEngineMaxInitRetryAttempts)
            ScheduleLiftEngineInitRetry();
    }
    RefreshAdReadyState();
}

private void EnsureDirectMaxAdsLoaded()
{
    if (!maxInitialized) return;
    EnsureMaxCallbacksSubscribed();
    if (!interstitialReady) LoadInterstitial();
    if (!rewardedReady) LoadRewarded();
    if (bannerShowRequested && !bannerCreated) LoadBanner();
    RefreshAdReadyState();
}

private void EnsureLiftEngineInitialized()
{
    if (!maxInitialized || !_liftEngineEnabled || _liftEngineReady || settings == null) return;
    if (!_liftEngineStartRequested) { TryStartLiftEngine(); return; }

    if (!_liftEngineInitSettled &&
        Time.realtimeSinceStartup - _liftEngineInitStartedRealtime > LiftEngineInitCallbackTimeoutSeconds)
    {
        _liftEngineInitRetryAttempt++;
        ResetAndRetryLiftEngineInit();
        return;
    }

    if (_liftEngineInitSettled &&
        Time.realtimeSinceStartup - _lastLiftEngineBackgroundRetryRealtime > LiftEngineBackgroundRetryIntervalSeconds)
    {
        _lastLiftEngineBackgroundRetryRealtime = Time.realtimeSinceStartup;
        _liftEngineInitRetryAttempt = 0;
        ResetAndRetryLiftEngineInit();
    }
}

private void ResetAndRetryLiftEngineInit()
{
    CancelLiftEngineInitRetry();
    _liftEngineInitSettled = false;
    _liftEngineInitStartedRealtime = Time.realtimeSinceStartup;
    LiftEngineSdk.Initialize(settings);
    ApplyLiftEngineContext();
    EnsureDirectMaxAdsLoaded();
}

private void ScheduleLiftEngineInitRetry()
{
    CancelLiftEngineInitRetry();
    _liftEngineInitRetryCoroutine = StartCoroutine(RetryLiftEngineInitRoutine());
}

private IEnumerator RetryLiftEngineInitRoutine()
{
    float delay = LiftEngineInitRetryBaseDelaySeconds *
        Mathf.Pow(2f, Mathf.Min(4, _liftEngineInitRetryAttempt - 1));
    yield return new WaitForSeconds(delay);
    _liftEngineInitRetryCoroutine = null;
    if (!maxInitialized || _liftEngineReady) yield break;
    ResetAndRetryLiftEngineInit();
}

private void CancelLiftEngineInitRetry()
{
    if (_liftEngineInitRetryCoroutine == null) return;
    StopCoroutine(_liftEngineInitRetryCoroutine);
    _liftEngineInitRetryCoroutine = null;
}

private void OnDestroy()
{
    UnsubscribeLiftEngineCallbacks(); // mirror Subscribe, use -= on same events
}
```

Call `EnsureLiftEngineInitialized()` from your existing periodic health/recovery loop (e.g. every 20s). In that same loop: if `_liftEngineReady` ensure LiftEngine formats are loading; else call `EnsureDirectMaxAdsLoaded()`.

### Step 2 — Route IsAdReady checks
Wherever we check MaxSdk.IsInterstitialReady / IsRewardedAdReady / banner ready:
```csharp
bool ready = _liftEngineReady
    ? LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Interstitial)  // or Rewarded / Banner
    : MaxSdk.IsInterstitialReady(adUnitId);
```

### Step 3 — Route Show calls
Wherever we call MaxSdk.ShowInterstitial / ShowRewardedAd / ShowBanner:
```csharp
if (_liftEngineReady && LiftEngineSdk.IsAdReady(format))
{
    LiftEngineSdk.ShowAd(format, null, new LiftEngineShowAdCallbacks
    {
        OnAdDisplayed = () => HandleAdDisplayed(format),
        OnAdHidden = () => HandleAdHidden(format),
        OnAdRewarded = () => HandleAdRewarded(format),
        OnAdDisplayFailed = msg => HandleAdDisplayFailed(format, msg)
    });
}
else
{
    // existing direct MaxSdk show call
}
```

Map HandleAdDisplayed/Hidden/Rewarded/DisplayFailed to our EXISTING interstitial/rewarded/banner callback logic — do not duplicate reward granting.

**Interstitial fulfilling rewarded (optional):** If our game shows an interstitial when the user tapped "watch ad for reward" but rewarded is not ready (`pendingRewardType` already set), grant the reward in the **interstitial hidden** handler:
```csharp
// In HandleAdHidden when format == Interstitial:
if (pendingRewardType != None)
    GrantPendingReward(); // user asked for RV; IS was the fallback — still owe the reward
```
Only add this if we already use interstitial-as-rewarded-fallback in the codebase.

### Step 4 — Route Load calls
Wherever we call MaxSdk.LoadInterstitial / LoadRewardedAd / CreateBanner:
```csharp
if (_liftEngineReady)
    LiftEngineSdk.LoadAd(LiftEngineAdFormat.Interstitial); // or Rewarded / Banner
else
    MaxSdk.LoadInterstitial(adUnitId); // existing call
```

### Step 5 — Banner specifics
- Use LiftEngineSdk.ShowAd(LiftEngineAdFormat.Banner, ...) to show
- Use LiftEngineSdk.HideBanner() / DestroyBanner() where we currently hide/destroy MAX banner
- Do not create MAX banner directly when _liftEngineReady is true

### Step 6 — IAP hook
In IAP success handler, add ONE line:
```csharp
LiftEngineSdk.NotifyPurchase((float)purchaseAmountUsd);
```
Use localized price or USD amount — match what we already send to analytics.

### Step 7 — Attribution updates
Wherever MMP attribution callback fires (AppsFlyer, Singular, Adjust, etc.), add:
```csharp
LiftEngineSdk.SetAttribution(installType, mediaSource);
LiftEngineSdk.SendReport();
```
Expose a public/static helper if our MMP bridge cannot reach AdsManager directly:
```csharp
public static void NotifyAttributionUpdated(string installType, string mediaSource)
{
    if (Instance == null) return;
    LiftEngineSdk.SetAttribution(installType, mediaSource);
    LiftEngineSdk.SendReport();
}
```

### Step 8 — Context hooks (game responsibility)

Call these from game code. Do not duplicate ad-count or session logic in the game — the SDK handles that when ads go through `LiftEngineSdk`.

| Call | When |
|------|------|
| `SetAttribution(installType, mediaSource)` | On init + every MMP update. `installType` must be `"Organic"` or `"Non-organic"`. |
| `SetIdfaApproved(bool)` | iOS after ATT prompt |
| `SendReport()` | After init + after attribution changes |
| `NotifyPurchase(amountUsd)` | Every real-money IAP success |
| `SetCountryCode(code)` | Optional |

**Critical:** Route **all** interstitial, rewarded, and banner impressions through `LiftEngineSdk.ShowAd` when `_liftEngineReady`. Direct MAX bypass skips LiftEngine reporting.

**IAP amount:** Pass USD float to `NotifyPurchase`. Use the same value we send to analytics.

### Step 9 — Firebase / GA4 analytics (game-owned)

**Subscribe in `SubscribeLiftEngineCallbacks` (already added above):**
- `OnAdDisplayedEvent` → log `ad_viewed` (interstitial, rewarded, **and banner** on LiftEngine path)
- `OnAdHiddenEvent` → resume gameplay / reload UI state — **do not** log `ad_viewed` here
- `OnAdRevenuePaidEvent` → log `ad_impression` when `Revenue > 0`

**Event rules:**

| Event | When | Notes |
|-------|------|-------|
| `ad_viewed` | Every ad display | Use `OnAdDisplayedEvent` for interstitial/rewarded/banner on LiftEngine path |
| `ad_viewed` | Direct MAX banner only | Use `OnAdRevenuePaidEvent` as view signal when no display callback exists |
| `ad_impression` | ILRD with revenue > 0 only | Use `OnAdRevenuePaidEvent`; include `value` + `currency` (USD) |

**Shared parameters (both events):**

| Parameter | Value |
|-----------|-------|
| `ad_platform` | `AppLovinMAX` |
| `ad_format` | `interstitial`, `rewarded`, or `banner` |
| `mediation_path` | `liftengine` when `_liftEngineReady`, else `direct_max` |
| `ad_placement` | From `LiftEngineAdInfo.MaxPlacement` when available |
| `revenue_precision` | From `LiftEngineAdInfo.RevenuePrecision` when available |
| `value` / `currency` | `ad_impression` only — USD revenue |

**Implement handlers:**
```csharp
private void OnLiftEngineAdDisplayed(LiftEngineAdInfo info)
{
    if (!_liftEngineReady || info == null) return;
    LogAdViewed(BuildAnalyticsPayload(info, FormatToName(info.Format), "liftengine"));
    // pause game / mute audio if we do that on existing MAX display callbacks
}

private void OnLiftEngineAdHidden(LiftEngineAdInfo info)
{
    if (!_liftEngineReady || info == null) return;
    // mirror existing OnAdHidden logic — resume game, reload ads
}

private void OnLiftEngineAdRevenuePaid(LiftEngineAdInfo info)
{
    if (!_liftEngineReady || info == null) return;

    var payload = BuildAnalyticsPayload(info, FormatToName(info.Format), "liftengine");

    // ad_viewed for all formats (including banner) comes from OnAdDisplayedEvent on the LiftEngine path.
    // Do not log ad_viewed here — ILRD is for ad_impression only.

    if (info.Revenue > 0)
        LogAdImpression(payload); // includes value + currency USD
}

private static string FormatToName(LiftEngineAdFormat format) => format switch
{
    LiftEngineAdFormat.Interstitial => "interstitial",
    LiftEngineAdFormat.Rewarded => "rewarded",
    LiftEngineAdFormat.Banner => "banner",
    _ => "unknown"
};
```

**Double-counting guard — REQUIRED in direct MAX ILRD handlers:**
```csharp
private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
{
    if (_liftEngineReady) return; // LiftEngine callbacks own analytics
    // existing direct MAX analytics...
}

private void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
{
    if (_liftEngineReady) return;
    // existing direct MAX analytics...
}

private void OnBannerAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
{
    if (_liftEngineReady) return;
    // existing direct MAX analytics...
}
```
Apply the same `_liftEngineReady` guard to direct MAX `OnAdDisplayed` handlers if they also log `ad_viewed`.

Map `LogAdViewed` / `LogAdImpression` to our existing Firebase/GA4 helper (search: FirebaseManager, LogEvent, ad_viewed, ad_impression).

## Deliverables
1. List every file you modified and why
2. Confirm init order: consent → MAX → LiftEngine
3. Confirm fallback paths exist for init failure and missing settings
4. Confirm LiftEngine context hooks: SetAttribution, SetIdfaApproved (iOS), SendReport on init, NotifyPurchase on IAP
5. Confirm EnsureDirectMaxAdsLoaded runs on MAX init and during LiftEngine init retry
6. Confirm EnsureLiftEngineInitialized in periodic health loop (45s timeout, 60s background retry)
7. Confirm Firebase ad_viewed / ad_impression wired with double-count guards on direct MAX callbacks
8. Note any MMP/ATT/IAP helpers you could not find — I will wire those manually
9. Note if interstitial-as-rewarded-fallback exists — wire hidden handler if so
10. Do not commit LiftEngineSettings.asset with real API keys

## Testing checklist (print for me)
- [ ] Play Mode: MAX init log, then LiftEngine init log
- [ ] Integration Manager → Ping Health (staging)
- [ ] Interstitial shows via LiftEngine
- [ ] Rewarded shows and reward grants correctly
- [ ] Banner show/hide works
- [ ] Disable apiKey in settings → direct MAX still works
- [ ] Ads preload via direct MAX before LiftEngine ready (no empty ad buttons on launch)
- [ ] LiftEngine init timeout → retry + direct MAX still serves ads
- [ ] MMP attribution wired → `SetAttribution` + `SendReport` on callback
- [ ] IAP purchase → `NotifyPurchase` called
- [ ] `ad_viewed` fires on LiftEngine ad display (and banner ILRD)
- [ ] `ad_impression` fires on LiftEngine revenue-paid with revenue > 0
- [ ] Direct MAX ILRD handlers skip logging when `_liftEngineReady` (no double-count)
```

## Prompt (copy to here)

---

## Tips 

1. **Fill settings first** — the prompt tells the AI to stop if `LiftEngineSettings.asset` is missing. Create it and add the API key before running the prompt.  
2. **Run in Agent mode** — the integration touches multiple files.  
3. **Review the diff** — especially MMP and ATT wiring; those vary per project.  
4. **Staging first** — use staging API key and Debug Mode before production.  
