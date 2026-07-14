# Changelog

All notable changes to the LiftEngine Unity SDK are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/).

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
- Multi-model optimization support (ML, algorithmic, and baseline paths)
- Public optimization callback events without exposing internal response fields
- Firebase / GA4 analytics wiring guidance in integration docs

### Changed
- Customer documentation sanitized — integration guide and AI prompt only
- Integration Manager debug UI uses generic "optimization" terminology
- `LiftEngineAdInfo` includes placement metadata for analytics
- LiftEngine track placement IDs are hardcoded — integrators only configure API key and MAX ad unit IDs

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
