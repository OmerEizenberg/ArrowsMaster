# LiftEngine SDK for Unity

**First official LiftEngine Unity SDK** (1.1.7). Ad monetization layer for **AppLovin MAX**. LiftEngine sits on top of your existing MAX integration: you keep control of when ads show, and you route load/show through `LiftEngineSdk` after MAX is initialized.

---

## Requirements

| | Minimum |
|---|---------|
| Unity | 2021.3 LTS |
| AppLovin MAX | 8.0+ |
| Newtonsoft JSON | 3.2.1 |

LiftEngine runs **on top of** MAX — you must already have MAX initialized in your project.

---

## Quick Start

1. Unzip the SDK and install with **Package Manager → Add package from disk** → select `com.liftengine.sdk/package.json`. After import, `using LiftEngine;` in `Assets/` scripts must compile with no `csc.rsp` or `extern alias`.
2. Install **AppLovin MAX** and **Newtonsoft JSON** if not already present
3. Open **Window → LiftEngine → Integration Manager**
4. Copy the example `LiftEngineSettings` to `Assets/Resources/LiftEngineSettings.asset` (or create it from Integration Manager) and enter your API key + MAX ad unit IDs
5. Initialize LiftEngine **after** `MaxSdk.InitializeSdk()` succeeds
6. Route ad load/show calls through `LiftEngineSdk`

```csharp
using LiftEngine;

// After MAX init:
LiftEngineSdk.Initialize();
LiftEngineSdk.SetAttribution("Organic", "facebook ads");
LiftEngineSdk.SendReport();

// Show rewarded (apply your business rules first):
LiftEngineSdk.ShowAd(LiftEngineAdFormat.Rewarded, null, new LiftEngineShowAdCallbacks
{
    OnAdRewarded = () => GrantReward(),
    OnAdHidden = () => ResumeGame()
});
```

---

## Documentation

| Document | Audience |
|----------|----------|
| [Integration Guide](Documentation~/INTEGRATION.md) | Full setup, API reference, QA checklist |
| [AI Integration Prompt](Documentation~/CURSOR_INTEGRATION_PROMPT.md) | One-shot Cursor prompt for automatic wiring |
| [Changelog](CHANGELOG.md) | First official release notes |

---

## Init Order (critical)

```
Consent / ATT  →  MaxSdk.InitializeSdk()  →  LiftEngineSdk.Initialize()
```

Never initialize LiftEngine before MAX.

---

## Fallback

If settings are missing, the API key is empty, or init fails, your game should continue using direct MAX calls. LiftEngine never blocks ad delivery.

---

## Support

Contact your LiftEngine account manager. For integration issues, include Unity version, MAX version, platform, and `[LiftEngine]` log excerpts from a staging build.
