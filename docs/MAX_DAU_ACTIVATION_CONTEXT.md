# MAX SDK Activation & DAU Gap — Project Context

Last updated: **2026-07-03**  
Package: `com.everybodygames.arrowsmaster`  
Current bundle in repo: **1.1.34** (target ship: **1.1.35** with fixes below)

Use this file when debugging Firebase DAU vs AppLovin MAX DAU, tier-1 adROAS cohorts, or consent/init regressions.

---

## 1. The problem (symptom)

**Firebase** (audience e.g. *Android Tier 1 - ADROAS FULL*): ~**1,700 DAU/day**  
**AppLovin MAX** User Activity: ~**52 DAU/day** (July 2026)

Same period, MAX dashboard also showed:
- **DAV** (daily ad viewers): ~234
- **Ad Viewer Rate**: ~452% (DAV ÷ DAU > 100%)

That pattern means: many devices **do** show ads (impression pipeline works), but AppLovin’s **unique-user / DAU** counter is much lower than Firebase’s active-user count.

---

## 2. How MAX defines metrics (official)

| Metric | Definition |
|--------|------------|
| **DAU** | Unique users who **launched the app with MAX SDK initialized** (server-side identity, typically GAID-based on Android) |
| **DAV** | Unique users who **viewed at least one ad** that day |
| **Ad Viewer Rate** | DAV ÷ DAU (can exceed 100% when DAU and DAV use different filtering/dedup rules) |

Firebase DAU ≠ MAX DAU by design. Healthy Unity+MAX apps often see MAX at **~85–95%** of Firebase for the same cohort/timezone. Gaps **>20%** warrant investigation.

---

## 3. Init flow (ground up)

### Entry points
- `AdsManager` — `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` → `Awake()` → `InitializeSDK()`
- Health check: every **20s**, retries init if not initialized (`AdHealthCheckRoutine`)

### Init sequence (`AdsManager.InitializeSDK`)
1. **iOS only**: ATT wait (up to 30s), then proceed
2. **Consent** (`ConsentManager.RequestConsent`) — see §5
3. **Main thread**: `ConsentManager.ApplyConsentToMax()` → `MaxSdk.SetExtraParameter("consent_flow_enabled", "false")` → `MaxSdkBase.InvokeEventsOnUnityMainThread = true` → `MaxSdk.SetSdkKey` → `MaxSdk.InitializeSdk()`
4. **Watchdog**: `WaitForMaxSdkInitCallback()` — 30s timeout; replays handler if native init succeeded but callback missed; else `max_sdk_init_failed` + retry

### Success telemetry
- Firebase funnel: **`max_sdk_initialized`** (+ `app_version` param) — fires in `OnMaxSdkInitialized`
- Failure: **`max_sdk_init_failed`** (+ `reason`, truncated to 100 chars)

### Ad serving (after init)
- Direct MAX and/or **LiftEngine** (`TryStartLiftEngine` after MAX init)
- LiftEngine uses same ad unit IDs via `MaxMediationAdapter`
- Banner: deferred create; prewarm from lobby (`HomeController`), not at cold start

### Key files
| File | Role |
|------|------|
| `Assets/Scripts/Core/AdsManager.cs` | MAX init, ads, revenue to Firebase/Singular |
| `Assets/Scripts/Core/ConsentManager.cs` | Google UMP + MAX privacy flags |
| `Assets/Scripts/Core/TermsConsentManager.cs` | Cosmetic terms popup only (does **not** gate MAX in 1.1.35) |
| `Assets/Scripts/Core/FirebaseManager.cs` | Analytics, funnel events |
| `Assets/Scripts/Core/DeviceIntegritySignals.cs` | Analytics-only bot/install signals (§8) |
| `ProjectSettings/AppLovinInternalSettings.json` | AppLovin built-in consent flow (must stay disabled in code — §5) |
| `Assets/MaxSdk/Resources/AppLovinSettings.asset` | SDK key (must match code) |
| `Assets/Plugins/Android/mainTemplate.gradle` | Native deps (play-services-ads-identifier, applovin-sdk 13.6.3, UMP) |

### Android ad unit IDs
- Interstitial: `8cf59aa021b449bf`
- Rewarded: `a9110da25686aa62`
- Banner: `b3d625776838cd3e`

---

## 4. Version history — what was broken when

| Versions | Dates (approx) | MAX init for tier-1 |
|----------|----------------|---------------------|
| **1.1.01–1.1.11** | May 26 – Jun 4 | Ungated; early MAX migration, unstable |
| **1.1.12–1.1.26** | Jun 5 – 22 | **Hard-gated** on `TermsConsentManager.HasAccepted` — many installs **never** init MAX |
| **1.1.27–1.1.33** | Jun 22 – 30 | Init **ungated** from terms, but **poisoned consent** bug (§5) |
| **1.1.34** (Jun 30 commit) | Shipped regressions: full UMP block every launch, duplicate CMP, no main-thread events, no init watchdog |
| **1.1.35** (current code) | All fixes in §6 | **First fully correct build** |

### Adoption context (July 2026)
- ~**61%+** of users still on **≤1.1.26** (gated era) in 30-day rollups
- ~**28%** of ADROAS 7-day actives on **≥1.1.29** — still not enough alone to explain 52 MAX DAU vs 1,700 Firebase

**Force update** (`RemoteConfigManager.KEY_FORCE_UPDATE_VERSION_ANDROID` → `HomeController`) is required to move old installs onto 1.1.35+.

---

## 5. Consent architecture (critical)

### Two systems — only one should run GDPR UI
1. **Google UMP** — `ConsentManager` (code)
2. **AppLovin built-in flow** — `AppLovinInternalSettings.json` (`consentFlowEnabled: true`)

**Must** call before init:
```csharp
MaxSdk.SetExtraParameter("consent_flow_enabled", "false");
```
Jun-19 regression: both enabled → double dialogs → tier-1 init failures.

### Poisoned consent bug (1.1.27–1.1.33)
- Legacy keys: `MaxConsentResolvedForInit`, `MaxHasUserConsent`
- On UMP **timeout/error/offline**, code stored `hasUserConsent=false` **permanently**
- Every later session: skipped UMP, called `MaxSdk.SetHasUserConsent(false)` — US users never needed consent but were treated as no-consent → poor fill, likely excluded from MAX DAU

### 1.1.35 `ConsentManager` behavior
- **Only successful UMP completion** persisted (`UmpConsentResolved_v2`, `UmpConsentNotRequired_v2`)
- **Legacy keys discarded** on first launch after update
- `SetHasUserConsent(true)` only when consent **not required** (non-GDPR)
- GDPR: **never** `SetHasUserConsent(false)` — TCF string from UMP is read by MAX SDK
- **State machine** gates init wait:
  - `Pending` → wait up to **15s** for UMP status roundtrip
  - `NotRequired` / `Completed` / `Failed` → proceed to init immediately
  - `FormRequired` → wait for GDPR form (up to **60s**)
- Returning users with stored resolution: **zero wait**, UMP refreshes in background

---

## 6. Fixes applied in current codebase (ship as 1.1.35)

- [x] Poisoned consent: no persist on failure; heal legacy installs
- [x] Init wait only for real GDPR form (maximize init coverage)
- [x] `consent_flow_enabled=false` (single CMP)
- [x] `InvokeEventsOnUnityMainThread = true` (ILRD / Singular revenue)
- [x] Init callback watchdog + `MaxSdk.IsInitialized()` replay
- [x] `DeviceIntegritySignals` — analytics only (§8)

### Not changed (by design)
- No blocking of ads/users based on integrity signals
- Terms popup remains cosmetic / non-blocking for MAX

---

## 7. Root causes summary (ranked)

1. **Old app versions in field** (~70% daily actives ≤1.1.28) — fixes don’t reach them without force update  
2. **Poisoned consent** on 1.1.27–1.1.33 — init runs but identity/consent degraded  
3. **Invalid / bot / low-quality UA traffic** — Firebase counts all launches; MAX filters by GAID/validity  
4. **Residual gaps on 1.1.35+** (expected small): bounce sessions, GDPR deny, ad blockers, no GAID, timezone, offline asymmetry (Firebase caches events; MAX needs live session)

---

## 8. Device integrity signals (analytics only)

**File:** `DeviceIntegritySignals.cs`  
**When:** After Firebase init in `FirebaseManager`

| User property | Values | Suspicious pattern |
|---------------|--------|-------------------|
| `install_source` | `play_store`, `sideload`, `other_store`, `unknown` | High `sideload` in paid cohort |
| `play_services` | `yes`, `no`, `unknown` | High `no` |
| `ad_id_status` | `available`, `limited`, `missing`, `error` | High `missing` |
| `device_class` | `emulator`, `physical`, `unknown` | High `emulator` |
| `device_brand` | manufacturer (truncated) | many `generic` |
| `debug_build` | `yes`, `no` | high `yes` in prod |

**Firebase Explore:** compare ADROAS Tier 1 vs filter `install_source=play_store` AND `device_class=physical` AND `ad_id_status=available`.

**Commercial actions (no code):** Singular fraud prevention, sub-publisher blacklist, pause suspect source 3–4 days, Play Integrity + App Check (future enforcement).

---

## 9. Diagnostic playbook

### A. Firebase funnel (same day, ADROAS audience, by `app_version`)

| Event | Meaning |
|-------|---------|
| DAU (Firebase) | All active users |
| `passed_terms` | SDK init allowed (1.1.27+) |
| `max_sdk_initialized` | MAX callback fired (client-side; can be > MAX dashboard DAU) |
| `max_sdk_init_failed` | Init failed — check `reason` (`callback_timeout`, etc.) |
| `passed_consent_approve` / `deny` | UMP outcome |

**Interpretation:**
- `max_sdk_initialized` ≈ Firebase, MAX DAU low → **AppLovin server-side / identity**
- `max_sdk_initialized` low, `passed_terms` high → **init callback / network**
- Both low on old versions → **version gating / poisoned consent**

### B. MAX dashboard
- User Activity: DAU, DAV, Ad Viewer Rate
- Remove segment filters — check total Android DAU vs tier-1 only
- Align timezone with Firebase (MAX often US Pacific)
- Mediation Debugger on device: CMP / TCF — AppLovin vendor in UMP message?

### C. Healthy benchmark (1.1.35+ only, after force update propagates)
- MAX DAU / Firebase DAU ≥ **~80–95%** for same cohort
- Below 80% persistent → traffic quality or EEA consent deny rate → UA / AppLovin support

---

## 10. Operational checklist after shipping 1.1.35

1. Build & release **1.1.35** to Play  
2. Set **`ForceUpdateVersionAndroid`** in Remote Config → `1.1.35` (or minimum `1.1.32` then bump)  
3. Verify AdMob UMP privacy message lists **AppLovin + mediated networks** as TCF vendors  
4. Enable / review **Singular fraud prevention**  
5. Run diagnostic §9A baseline, re-check after **7 days**  
6. Optional: open AppLovin ticket with Ad Viewer Rate >100% screenshot + Firebase vs MAX numbers

---

## 11. Expected gaps on 1.1.35+ (not bugs)

| Cause | Firebase | MAX DAU |
|-------|----------|---------|
| User closes app before init completes | ✓ | ✗ |
| GDPR user closes consent form | ✓ | ✗ or limited |
| DNS/ad blocker blocks applovin.com | ✓ | ✗ |
| No GAID / LAT / emulator | ✓ | ✗ or degraded |
| Offline session (Firebase uploads later) | ✓ (delayed) | ✗ |
| Bot/farm install | ✓ | filtered |

---

## 12. Related git commits (reference)

| Commit | Date | Note |
|--------|------|------|
| `8feb8a4a` | 2026-05-26 | Move to AppLovin MAX |
| `4f78aa32` | 2026-06-22 | Fix consent flow |
| `4220f36a` | 2026-06-23 | Tier-1 ads fix (init replay, scene defer) |
| `415263d6` | 2026-06-27 | 1.1.32 tier-1 fix |
| `1269cdb2` | 2026-06-30 | "all set for max + no T&C fake" — **regressions** |
| Current | 2026-07-03 | Consent heal, init gating, watchdog, integrity signals |

---

## 13. Quick commands

```bash
# Version at MAX migration
git show 8feb8a4a --stat

# Terms gating in old build (example 1.1.13)
git show 7c63c5f2:Assets/Scripts/Core/AdsManager.cs | grep -n "HasAccepted"

# ConsentManager at 1.1.33 (poisoned persist)
git show 1269cdb2^:Assets/Scripts/Core/ConsentManager.cs | head -120
```

---

## 14. Contacts / links

- AppLovin MAX User Activity docs: https://support.axon.ai/en/max/max-dashboard/reports/user-activity-reporting  
- AppLovin terms/consent + UMP: https://support.axon.ai/en/max/unity/overview/terms-and-privacy-policy-flow  
- SDK key: AppLovin dashboard → Account → Keys (must match `AdsManager.MaxSdkKey` and `AppLovinSettings.asset`)

---

*When updating this doc: bump "Last updated", bundle version, and checklist status after each release.*
