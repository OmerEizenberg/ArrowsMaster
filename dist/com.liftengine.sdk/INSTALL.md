# LiftEngine SDK 1.1.2 — Install

## Requirements
- Unity 2021.3 or newer
- AppLovin MAX 8.0+ (installed first)
- Newtonsoft Json (`com.unity.nuget.newtonsoft-json` 3.2.1+)

## Install
1. Install AppLovin MAX from the Unity Package Manager or MAX dashboard export.
2. In Unity: **Window → Package Manager → + → Add package from tarball** and select `com.liftengine.sdk-1.1.2.tgz`.
   - Or copy the `com.liftengine.sdk` folder into your project's `Packages/` directory.
3. Create settings: **Assets/Resources/LiftEngineSettings.asset** (or use **Window → LiftEngine → Integration Manager**).
4. Paste your LiftEngine API key, set environment, and enter MAX ad unit IDs.
5. Initialize **after** MAX is initialized. See `Documentation~/INTEGRATION.md`.
6. Optional: paste `Documentation~/CURSOR_INTEGRATION_PROMPT.md` into Cursor after the package is imported.

Do not unpack or replace the DLLs. Runtime logic is precompiled.
