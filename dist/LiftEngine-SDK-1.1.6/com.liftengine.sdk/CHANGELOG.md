# Changelog

All notable changes to the LiftEngine Unity SDK are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

## [1.1.6] - 2026-08-16

### Fixed
- Runtime DLL no longer references `UnityEditor` (removed `EditorUserBuildSettings` from OS detection) — Unity 6 iOS/Android player builds were failing even with Editor plugin disabled
- `link.xml` no longer preserves `LiftEngine.Editor` for player IL2CPP

## [1.1.5] - 2026-08-16

### Fixed
- `LiftEngine.Editor.dll` PluginImporter disables Any / iOS / Android / standalone so Unity 6 Bee player builds do not pull UnityEditor into the iOS/Android player

## [1.1.4] - 2026-08-16

### Changed
- Studio install is **Add package from disk** (select `package.json`). Tarball install is not supported.

## [1.1.3] - 2026-08-16

### Fixed
- Tarball has `package.json` at the archive root (Unity Add from tarball)
- Runtime DLL ships at package-root `Plugins/` so `Assembly-CSharp` can use `LiftEngineSdk`
- No asmdef named `LiftEngine.Runtime` (no DLL name collision)
- PluginImporter `.meta` files included for both DLLs (`isExplicitlyReferenced: 0`)
- `LiftEngineSettings.cs` not shipped as source (type lives in the runtime DLL)

## [1.1.2] - 2026-08-15

### Changed
- Customer integration guide and AI prompt cover wiring only
- Package description is integration-focused

## [1.1.1] - 2026-07-11

### Security
- Customer `LiftEngineSettings` stripped to API key, environment, and MAX ad unit IDs only
- Internal tuning moved to compiled runtime defaults (not customer-editable)
- Public API renamed: `OnOptimization*` events, `OptimizationUnavailable` signal, `AdPrewarmState.Optimizing`
- Internal classes renamed and customer-visible logs sanitized
- IL2CPP `link.xml` included in customer package

### Changed
- Integration Manager shows customer-facing settings only
- Analytics and full report-data wiring added to AI integration prompt

## [1.1.0] - 2026-07-11

### Added
- Optimization callbacks on the public API
- Firebase / GA4 analytics wiring guidance in integration docs

### Changed
- Customer documentation sanitized — integration guide and AI prompt only
- Integration Manager debug UI uses generic terminology

### Fixed
- Merged latest MAX mediation improvements from production branch

## [1.0.0] - 2026-07-06

### Added
- Customer integration guide (`Documentation~/INTEGRATION.md`)
- One-shot Cursor AI integration prompt (`Documentation~/CURSOR_INTEGRATION_PROMPT.md`)
- Distribution and DLL packaging guide (internal — not shipped to customers)
- Build script for customer `.tgz` packages
- Public event types for optimization callbacks and operation errors
- IL2CPP link preservation file

### Changed
- Tightened public API — internal API models no longer exposed to integrators
- README rewritten for customer-facing onboarding

### Security
- Core runtime intended for precompiled DLL distribution to customers

## [0.1.0] - Initial

- Ad monetization optimization SDK for AppLovin MAX
- Integration Manager editor window
- Basic integration sample
