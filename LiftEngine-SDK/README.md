# LiftEngine SDK v1.1.0

Unity package for AppLovin MAX monetization optimization.

## Install

1. Open Unity (**2021.3 LTS** or newer)
2. **Window → Package Manager → + → Add package from tarball…**
3. Select `com.liftengine.sdk-1.1.0.tgz`

## Docs (inside the package)

After install, open:

- `Documentation~/INTEGRATION.md` — full setup guide
- `Documentation~/CURSOR_INTEGRATION_PROMPT.md` — one-shot AI wiring prompt

## What you configure

- **API key** (from LiftEngine — staging for QA, production after sign-off)
- Your existing **MAX ad unit IDs** (iOS + Android)

Nothing else is required from your side for LiftEngine tracking.

## Requirements

| | Minimum |
|---|---------|
| Unity | 2021.3 LTS |
| AppLovin MAX | 8.0+ |
| Newtonsoft JSON | 3.2.1 |

**Init order:** Consent / ATT → `MaxSdk.InitializeSdk()` → `LiftEngineSdk.Initialize()`
