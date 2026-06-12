# LiftEngine Unity SDK

Portable bid-floor optimization SDK for LiftEngine API + AppLovin MAX mediation.

## Quick Start

1. Open **Window → LiftEngine → Integration Manager**
2. Click **Create Settings Asset**
3. Set your **API Key** (staging: `test-api-key` for mock)
4. Confirm MAX SDK key and ad unit IDs
5. Enable **Debug Mode** for testing tools

## Runtime Integration

```csharp
using LiftEngine;

// After consent / ATT — typically from AdsManager
LiftEngineSdk.Initialize();

// Once — from AppsFlyer / Singular attribution callback
LiftEngineSdk.SetAttribution("Organic", "facebook ads");

// From IAPManager on purchase
LiftEngineSdk.NotifyPurchase(4.99f);

// Show ads (your game applies business rules first)
if (CanShowRewarded())
{
    LiftEngineSdk.ShowAd(LiftEngineAdFormat.Rewarded, null, new LiftEngineShowAdCallbacks
    {
        OnAdRewarded = () => GrantReward(),
        OnAdHidden = () => ResumeGame()
    });
}
```

## Testing Guide

### Editor — no device


| Test           | How                                                |
| -------------- | -------------------------------------------------- |
| Settings asset | Integration Manager → Create Settings Asset        |
| Payload shape  | Debug tab → Preview Predict Payload (edit mode OK) |
| Checklist      | Integration tab                                    |


### Play Mode — staging API

1. Set `environment = Staging`, `apiKey = test-api-key`
2. Enable **Debug Mode** + **Verbose Logging**
3. Enter Play Mode
4. Call `LiftEngineSdk.Initialize()` from test script or enable `autoInitialize`


| Test              | Steps                      | Expected                                             |
| ----------------- | -------------------------- | ---------------------------------------------------- |
| Health            | Debug → Ping Health        | `OK`, console `{"status":"ok"}`                      |
| Predict + prewarm | Debug → Run Predict        | Console logs multiplier attempts, state → Ready      |
| Show rewarded     | Debug → Show Ad (Rewarded) | MAX ad displays                                      |
| Attribution       | Set Organic + media source | Next predict payload has `install_type: organic`     |
| Purchase          | Simulate Purchase 4.99     | Payload shows `ltv_gross_up_to_date`, `payer_ind: 1` |
| Counters          | Show 2 ads                 | `ad_number_*` / `daily_ad_number` increment (0-based) |
| Clear state       | Clear Context Prefs        | Counters reset                                       |


### Device build (Android / iOS)

1. Build development build with test API key
2. Watch logcat / Xcode for `[LiftEngine]` tags
3. Verify sequence: **prewarm→ Load → Show → Track**
4. After dismiss: auto prewarm starts (next predict from multipliers)

### SignalBus (optional subscribe)

```csharp
LiftEngineSignalBus.BidFloorPredictionFailed += s => Debug.Log("Predict failed: " + s.Format);
LiftEngineSignalBus.AdPrewarmCompleted += s => Debug.Log($"Prewarm {s.Format}: {s.Success}");
```

## Architecture

- **LiftEngineSdk** — static facade
- **ReportContextService** — PlayerPrefs counters, LTV, eCPM history
- **AdPrewarmService** —  waterfall 
- **MaxMediationAdapter** — MAX wrapper

## Notes

- Game business rules (cooldowns, level gates, no-ads IAP) stay in client code
- LevelPlay adapter is stubbed
- Mock API may omit `prediction` field — SDK uses `defaultPredictionFallback` (settings)

