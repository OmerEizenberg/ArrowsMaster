# LiftEngine SDK 1.1.7 — First official release

Use **Add package from disk**. Do not use Add package from tarball.

## Requirements
- Unity 2021.3 or newer
- AppLovin MAX 8.0+ (installed first)
- Newtonsoft Json (`com.unity.nuget.newtonsoft-json` 3.2.1+)

## Install
1. Unzip the LiftEngine SDK zip and keep the `com.liftengine.sdk` folder on disk.
2. In Unity: **Window → Package Manager → + → Add package from disk…**
3. Select `com.liftengine.sdk/package.json`.
4. Create settings: copy the example from **Package Manager → LiftEngine SDK → Samples → Example LiftEngineSettings** to `Assets/Resources/LiftEngineSettings.asset`, or use **Window → LiftEngine → Integration Manager**.
5. Replace placeholders with your LiftEngine API key, environment, and MAX ad unit IDs.
6. Initialize **after** MAX is initialized. See `Documentation~/INTEGRATION.md`.
7. Optional: paste `Documentation~/CURSOR_INTEGRATION_PROMPT.md` into Cursor after the package is imported.

After import, a script under `Assets/` with `using LiftEngine;` must compile with no `csc.rsp` and no `extern alias`.

Do not copy the folder into the project's `Packages/` directory. Do not unpack or replace the DLLs. Do not delete the unzipped SDK folder — Unity keeps a `file:` path to it.
