# LiftEngine SDK — Integration Guide

**First official LiftEngine Unity SDK (1.1.7).** This guide is for game teams integrating the LiftEngine Unity SDK with **AppLovin MAX**. It covers what you receive, what you must provide, and how to wire the SDK into your project.

---

## What LiftEngine Includes

| Component | Description |
|-----------|-------------|
| **Runtime SDK** | Unity package that optimizes ad monetization on top of AppLovin MAX |
| **Settings asset** | `LiftEngineSettings` ScriptableObject — your API key, ad unit IDs, and environment |
| **Integration Manager** | Unity Editor window: **Window → LiftEngine → Integration Manager** |
| **Sample** | Optional sample scene driver under `Samples~/BasicIntegration/` |
| **Documentation** | This guide and the one-shot AI integration prompt |

### Supported ad formats

- Banner  
- Interstitial  
- Rewarded  

### What LiftEngine handles for you

- Ad preloading in the background  
- Automatic reload after an ad is dismissed  
- Graceful fallback to standard MAX loading when LiftEngine is unavailable  

### What stays in your game code

- Consent / ATT / GDPR flows  
- AppLovin MAX SDK initialization and SDK key  
- Business rules: cooldowns, level gates, “remove ads” IAP, when to show ads  
- MMP attribution callbacks (AppsFlyer, Singular, Adjust, etc.)  
- IAP purchase notifications  

---

## Requirements

### Unity & packages

| Requirement | Minimum |
|-------------|---------|
| Unity | **2021.3 LTS** or newer |
| AppLovin MAX | **8.0+** (8.6.x tested) |
| Newtonsoft JSON | **3.2.1** (`com.unity.nuget.newtonsoft-json`) |

### From LiftEngine (we provide)

| Item | Notes |
|------|-------|
| **Unity package** | `com.liftengine.sdk` (`.tgz` or private registry) |
| **API key** | Per environment — **Staging** for QA, **Production** for live builds |

### From your team (you provide)

| Item | Where it comes from |
|------|---------------------|
| **AppLovin MAX SDK key** | AppLovin dashboard → your existing MAX integration |
| **MAX ad unit IDs** | AppLovin dashboard — Banner, Interstitial, Rewarded for **iOS and Android** |
| **Working MAX integration** | MAX must initialize successfully **before** LiftEngine |
| **MMP attribution** | Install type (`Organic` / `Non-organic`) and media source from your MMP |
| **iOS ATT state** | Whether IDFA tracking is authorized (iOS builds only) |
| **IAP hook** | Call `NotifyPurchase` when a real-money purchase completes |

---

## Package Installation

Unzip the LiftEngine SDK zip. Install from the **`com.liftengine.sdk` folder**, not a `.tgz`.

1. **Window → Package Manager → + → Add package from disk…**
2. Select `com.liftengine.sdk/package.json` inside the unzipped folder.
3. Keep that folder on disk. Unity references it in `Packages/manifest.json` (`file:`). Do not delete it after import.

Do not use **Add package from tarball**. Do not copy the folder into the project's `Packages/` directory. Do not add `csc.rsp` or `extern alias` for LiftEngine.

### Verify installation

- Package Manager shows **com.liftengine.sdk** version **1.1.7** (first official LiftEngine SDK)  
- **Window → LiftEngine → Integration Manager** opens without errors  
- A script under `Assets/` with `using LiftEngine;` and `LiftEngineSdk.Initialize(...)` compiles with no extra compiler flags  
- Console shows no CS0246 / CS0436 for LiftEngine types  

---

## Configuration

### Step 1 — Create settings asset

The zip includes an example asset at `Examples/LiftEngineSettings.asset`. You can also import **Package Manager → LiftEngine SDK → Samples → Example LiftEngineSettings**.

The live asset **must** sit at `Assets/Resources/LiftEngineSettings.asset`.

1. Copy the example into `Assets/Resources/LiftEngineSettings.asset`, **or** use **Window → LiftEngine → Integration Manager → Create Settings Asset**  
2. Replace `YOUR_LIFTENGINE_API_KEY` and the `YOUR_*_AD_UNIT_ID` placeholders with your real values

### Step 2 — Fill in settings

| Field | Value |
|-------|-------|
| **Environment** | `Staging` for QA builds, `Production` for store builds |
| **API Key** | Key we provide (never commit production keys to public repos) |
| **iOS Banner / Interstitial / Rewarded** | Your MAX ad unit IDs |
| **Android Banner / Interstitial / Rewarded** | Your MAX ad unit IDs |
| **Auto Initialize** | **Off** — initialize from your AdsManager after MAX is ready |
| **Debug Mode** | **On** for QA only |
| **Verbose Logging** | **On** during integration, **Off** for production |

### Step 3 — MAX dashboard

No LiftEngine-specific changes are required in the AppLovin dashboard beyond your normal MAX setup. Use the same ad unit IDs in `LiftEngineSettings` that you already use with MAX.

---

## Runtime Integration Checklist

Follow this order exactly:

```
1. User consent / ATT (your existing flow)
2. MaxSdk.InitializeSdk()          ← MAX first
3. On MAX init success:
     a. LiftEngineSdk.Initialize(settings)
     b. LiftEngineSdk.SetAttribution(installType, mediaSource)
     c. LiftEngineSdk.SetIdfaApproved(bool)   ← iOS only
     d. LiftEngineSdk.SendReport()
4. Route ad calls through LiftEngineSdk (see API below)
5. On IAP success: LiftEngineSdk.NotifyPurchase(amountUsd)
6. On attribution update: SetAttribution + SendReport again
```

### Required context hooks

Call these from game code. Do not duplicate session/ad-count logic — the SDK handles that when ads go through `LiftEngineSdk`.

| Call | When |
|------|------|
| `SetAttribution(installType, mediaSource)` | On init and every MMP update. `installType` must be `"Organic"` or `"Non-organic"`. |
| `SetIdfaApproved(bool)` | iOS, after ATT |
| `SendReport()` | After init and after attribution changes |
| `NotifyPurchase(amountUsd)` | Every real-money IAP success |
| `SetCountryCode(string)` | Optional |

When LiftEngine is ready, route **all** interstitial, rewarded, and banner impressions through `LiftEngineSdk.ShowAd`. Direct MAX bypass skips LiftEngine reporting.

### Initialization example

```csharp
using LiftEngine;
using UnityEngine;

// Call from your AdsManager after MaxSdk.InitializeSdk() succeeds.
private void OnMaxSdkInitialized()
{
    var settings = Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
    if (settings == null || string.IsNullOrWhiteSpace(settings.apiKey))
    {
        // No LiftEngine — keep using direct MaxSdk calls
        return;
    }

    LiftEngineSdkCallbacks.OnSdkInitializedEvent += status =>
    {
        _liftEngineReady = status == LiftEngineInitializationStatus.Success;
        if (!_liftEngineReady)
        {
            // Fall back to direct MAX load/show
            LoadAdsViaMaxDirectly();
        }
    };

    LiftEngineSdk.Initialize(settings);

#if UNITY_IOS && !UNITY_EDITOR
    LiftEngineSdk.SetIdfaApproved(YourAttHelper.IsTrackingAuthorized());
#endif
    LiftEngineSdk.SetAttribution("Organic", "your_media_source");
    LiftEngineSdk.SendReport();

    // Preload ads via direct MAX immediately — do not wait for LiftEngine callback
    EnsureDirectMaxAdsLoaded();
}
```

For init retry, health-check recovery, and keeping direct MAX loaded while LiftEngine warms up, see **Production resilience (recommended)** below.

### Showing ads

Apply your business rules first, then call LiftEngine:

```csharp
if (!CanShowRewardedAd()) return;

if (_liftEngineReady && LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Rewarded))
{
    LiftEngineSdk.ShowAd(LiftEngineAdFormat.Rewarded, null, new LiftEngineShowAdCallbacks
    {
        OnAdRewarded = () => GrantReward(),
        OnAdHidden = () => ResumeGameplay(),
        OnAdDisplayFailed = msg => FallbackOrRetry(msg)
    });
}
else if (_liftEngineReady)
{
    LiftEngineSdk.LoadAd(LiftEngineAdFormat.Rewarded);
}
else
{
    // Direct MAX fallback
    MaxSdk.ShowRewardedAd(rewardedAdUnitId);
}
```

### IAP hook

```csharp
// In your IAPManager when purchase succeeds:
LiftEngineSdk.NotifyPurchase((float)purchaseAmountUsd);
```

### Attribution updates

When your MMP fires an attribution callback:

```csharp
string installType = isOrganic ? "Organic" : "Non-organic";
LiftEngineSdk.SetAttribution(installType, mediaSource);
LiftEngineSdk.SendReport();
```

---

## Public API Reference

| Method | When to call |
|--------|--------------|
| `Initialize()` / `Initialize(settings)` | After MAX init succeeds |
| `IsInitialized` | Check init state |
| `SetAttribution(installType, mediaSource)` | After MMP callback; use `"Organic"` or `"Non-organic"` |
| `SetIdfaApproved(bool)` | iOS — after ATT prompt |
| `SetCountryCode(string)` | Optional manual override |
| `SendReport()` | After context changes (init, attribution, significant events) |
| `NotifyPurchase(float amountUsd)` | On successful IAP |
| `LoadAd(format)` | Preload a format |
| `IsAdReady(format)` | Before show |
| `GetPrewarmState(format)` | Debug / UI loading indicators |
| `ShowAd(format, params, callbacks)` | Display ad |
| `HideBanner()` / `DestroyBanner()` | Banner lifecycle |
| `CheckHealth(callback)` | QA — verify API connectivity |
| `SetVerboseLogging(bool)` | Enable `[LiftEngine]` logs |

### Callbacks (`LiftEngineSdkCallbacks`)

| Event | Use |
|-------|-----|
| `OnSdkInitializedEvent` | Know when routing can switch to LiftEngine |
| `OnAdLoadedEvent` | Refresh “ad ready” UI |
| `OnAdDisplayedEvent` / `OnAdHiddenEvent` | Pause / resume game |
| `OnAdRewardedEvent` | Grant reward (or use `ShowAd` callbacks) |
| `OnAdRevenuePaidEvent` | Revenue analytics |

### Optional signals (`LiftEngineSignalBus`)

```csharp
LiftEngineSignalBus.AdReadyStateChanged += signal =>
    RefreshAdButton(signal.Format, signal.IsReady);
```

---

## Fallback Behavior

LiftEngine is designed to **never block your game from showing ads**:

| Condition | Behavior |
|-----------|----------|
| Settings missing or API key empty | Skip LiftEngine; use direct MAX |
| LiftEngine init fails | Fall back to direct MAX load/show |
| Ad not ready on show | `LoadAd` + retry, or direct MAX |
| Show display failed | Your callback fires; reload via LiftEngine or MAX |

Always keep your existing direct MAX code paths as fallback during integration and in production.

---

## Production resilience (recommended)

These three patterns keep ads available while LiftEngine initializes and recover from missed init callbacks. They do **not** change LiftEngine APIs — they are AdsManager responsibilities.

### 1. Preload direct MAX while LiftEngine warms up

LiftEngine init is async. **Do not block the player** waiting for `_liftEngineReady`.

On MAX init success, call both:

```csharp
TryStartLiftEngine();
EnsureDirectMaxAdsLoaded(); // preload IS/RV/banner via direct MAX immediately
```

`EnsureDirectMaxAdsLoaded()` should subscribe MAX callbacks if needed, then call your existing `LoadInterstitial()`, `LoadRewarded()`, and banner load when applicable. Route logic uses `_liftEngineReady ? LiftEngineSdk : MaxSdk` so direct MAX fills the gap until LiftEngine is ready.

### 2. LiftEngine init retry loop

Init can miss a callback (slow network, consent blip, app backgrounded). Add retry state:

```csharp
private int _liftEngineInitRetryAttempt;
private float _liftEngineInitStartedRealtime = -1f;
private float _lastLiftEngineBackgroundRetryRealtime = -1f;
private Coroutine _liftEngineInitRetryCoroutine;

private const float LiftEngineInitCallbackTimeoutSeconds = 45f;
private const int LiftEngineMaxInitRetryAttempts = 3;
private const float LiftEngineInitRetryBaseDelaySeconds = 5f;
private const float LiftEngineBackgroundRetryIntervalSeconds = 60f;
```

In `TryStartLiftEngine()`, record `_liftEngineInitStartedRealtime = Time.realtimeSinceStartup`.

In `OnLiftEngineSdkInitialized`:

```csharp
_liftEngineInitSettled = true;
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
        ScheduleLiftEngineInitRetry(); // exponential backoff: 5s, 10s, 20s…
}
```

`ResetAndRetryLiftEngineInit()` should reset `_liftEngineInitSettled = false`, call `LiftEngineSdk.Initialize(settings)`, `ApplyLiftEngineContext()`, and `EnsureDirectMaxAdsLoaded()`.

### 3. Health-check recovery (`EnsureLiftEngineInitialized`)

Call from your existing periodic health/recovery loop (e.g. every 20s):

```csharp
private void EnsureLiftEngineInitialized()
{
    if (!maxInitialized || !_liftEngineEnabled || _liftEngineReady || settings == null)
        return;

    if (!_liftEngineStartRequested) { TryStartLiftEngine(); return; }

    // Init callback never arrived — retry after 45s
    if (!_liftEngineInitSettled &&
        Time.realtimeSinceStartup - _liftEngineInitStartedRealtime > LiftEngineInitCallbackTimeoutSeconds)
    {
        _liftEngineInitRetryAttempt++;
        ResetAndRetryLiftEngineInit();
        return;
    }

    // Init failed earlier — background retry every 60s for the whole session
    if (_liftEngineInitSettled &&
        Time.realtimeSinceStartup - _lastLiftEngineBackgroundRetryRealtime > LiftEngineBackgroundRetryIntervalSeconds)
    {
        _lastLiftEngineBackgroundRetryRealtime = Time.realtimeSinceStartup;
        _liftEngineInitRetryAttempt = 0;
        ResetAndRetryLiftEngineInit();
    }
}
```

In the same health loop: if `_liftEngineReady` → ensure LiftEngine ads loaded; else → `EnsureDirectMaxAdsLoaded()`.

### Optional: interstitial fulfilling a rewarded request

Some games show an **interstitial as fallback** when the user tapped “watch ad for reward” but rewarded is not ready (`pendingRewardType` is already set). The user still expects the reward when the ad closes.

In your **interstitial hidden** handler (direct MAX or LiftEngine `OnAdHidden` / `ShowAd` callback), add:

```csharp
if (pendingRewardType != RewardType.None && format == Interstitial)
    GrantPendingReward(); // same logic as rewarded OnAdRewarded
```

This means: “user asked for a rewarded placement; we showed an interstitial instead; grant the reward on dismiss.” Only apply if your game uses this fallback pattern.

---

## QA Verification

### Editor / staging

1. Set **Environment = Staging**, use staging API key  
2. Enable **Debug Mode** and **Verbose Logging**  
3. Enter Play Mode after MAX initializes  
4. **Integration Manager → Debug** tab:  
   - **Ping Health** → expect OK  
   - **Run Prewarm** → ad format reaches Ready state  
   - **Show Ad** → MAX test ad appears  

### Device build

1. Development build with staging key  
2. Filter logs for `[LiftEngine]`  
3. Confirm sequence: MAX init → LiftEngine init → ads load → show → dismiss → reload  
4. Test with airplane mode briefly — game should still show ads via MAX fallback  

---

## Firebase / GA4 analytics (recommended)

LiftEngine does **not** log to Firebase. Your game owns analytics.

| Event | When |
|-------|------|
| **`ad_viewed`** | Every ad display |
| **`ad_impression`** | ILRD with **revenue > 0** only |

Shared parameters: `ad_platform=AppLovinMAX`, `ad_format`, `mediation_path` (`liftengine` or `direct_max`). Include `value` / `currency` (USD) on `ad_impression` only.

- `OnAdDisplayedEvent` → `ad_viewed` (interstitial, rewarded, banner on LiftEngine path)
- `OnAdHiddenEvent` → resume game / reload ads — do not log `ad_viewed` here
- `OnAdRevenuePaidEvent` → `ad_impression` when `Revenue > 0`
- When `_liftEngineReady`, ignore direct MAX display/ILRD handlers for the same impression (no double-count)

---

### Before production release

- [ ] Environment = **Production**  
- [ ] Production API key in build pipeline secrets (not in git)  
- [ ] Debug Mode = **Off**  
- [ ] Verbose Logging = **Off**  
- [ ] Tested on iOS and Android device builds  
- [ ] IAP and attribution hooks verified  

---

## What to Send LiftEngine

When requesting onboarding or support, include:

| Item | Example |
|------|---------|
| Game / bundle ID | `com.studio.mygame` |
| Unity version | `2022.3.20f1` |
| MAX SDK version | `8.6.3` |
| Platforms | iOS, Android |
| MMP | AppsFlyer / Singular / Adjust |
| Staging build logs | `[LiftEngine]` lines from init through first show |
| Settings screenshot | Integration Manager (redact API key) |

**Do not send:** production API keys in email or public tickets.

---

## Support

Contact your LiftEngine account manager with:

- Unity version, MAX version, platform  
- Staging `[LiftEngine]` log excerpt  
- Description of expected vs actual behavior  

For AI-assisted integration, use **CURSOR_INTEGRATION_PROMPT.md** (included in this package).
