# ArrowsLegendBI — Developer handoff context

**Purpose:** Share this file (and optionally `../analytics/`) with another developer or their coding agent to continue the BI dashboard sub-project inside the Arrows Master Unity monorepo.

**Last updated:** 2026-06-03  
**Owner context:** Product owner wants web BI in-repo (not Unity UI). BigQuery showed unavailable until Google credentials are configured locally on the developer machine.

---

## 1. Project goal

Build and maintain **ArrowsLegendBI** — a local web BI dashboard for the mobile game **Arrows Master** — as a **sibling folder** in the same git repo as the Unity game. One clone = game code + analytics SQL + BI UI.

**Not in scope:** Unity runtime UI for analytics, shipping BI inside the game APK, committing secrets.

**In scope:** Dashboards for GA4/Firebase data via BigQuery export; iterative SQL + React panels; optional Mac desktop wrap later (Tauri/Electron).

---

## 2. Repository layout

```
ArrowsMaster/                          # Unity game root
├── Assets/Scripts/Core/
│   ├── FirebaseManager.cs             # Firebase Analytics events
│   └── GameManager.cs                 # level_start, level_end, etc.
├── analytics/                         # Shared data layer (SQL + config + CLI)
│   ├── config.json                    # GCP project, GA4 property, BQ dataset
│   ├── sql/                           # BigQuery queries
│   └── scripts/                       # auth_setup.sh, run_ltv_matrix.py
├── ArrowsLegendBI/                    # ← THIS SUB-PROJECT
│   ├── web/                           # React 19 + Vite 6 + Recharts
│   ├── server/                        # FastAPI → runs analytics/sql/*.sql
│   ├── scripts/check_bigquery.py
│   ├── .env.example                   # GOOGLE_APPLICATION_CREDENTIALS
│   ├── README.md                      # Setup for humans
│   └── HANDOFF.md                     # This file
├── ArrowsLegend.sln                   # Committed VS/Rider solution (BI + Unity folders)
└── ArrowsLegend.code-workspace        # Cursor/VS Code multi-root
```

Unity-generated `*.sln` / `*.csproj` remain **gitignored** except `!ArrowsLegend.sln`.

---

## 3. Data pipeline (already wired)

```
Unity game
  → Firebase Analytics (FirebaseManager.cs)
  → GA4 property "Arrows Master" (ID 520375039)
  → BigQuery export (dataset analytics_520375039)
  → analytics/sql/*.sql
  → ArrowsLegendBI/server (FastAPI)
  → ArrowsLegendBI/web (charts/tables)
```

### GCP / GA4 identifiers (`analytics/config.json`)

| Field | Value |
|-------|--------|
| `gcp_project_id` | `arrowsmaster-6b84f` |
| `ga4_property_id` | `520375039` |
| `ga4_property_name` | `Arrows Master` |
| `bigquery_dataset` | `analytics_520375039` |
| `events_table_pattern` | `arrowsmaster-6b84f.analytics_520375039.events_*` |

### Key client events (for future dashboards)

From `Assets/Scripts/Core/FirebaseManager.cs` and `GameManager.cs`:

| Event | Params | Use |
|-------|--------|-----|
| `level_start` | `level_id`, `attempt_count` | Funnel, difficulty |
| `level_end` | `level_id`, `success`, `score` | Completion / fail rate |
| `purchase` | `value`, `currency` | IAP revenue |
| `ad_impression` | `value` (USD per AdsManager) | Ad revenue |
| `tutorial_begin` / `tutorial_complete` | — | Onboarding |
| `Ret_2`, `Ret_7`, … `Ret_90` | — | Retention milestones |
| `session7` | — | Session milestone |
| Booster clicks | — | Feature usage |

### Existing SQL

| File | Status |
|------|--------|
| `analytics/sql/ltv_by_country_d30.sql` | **Used by BI** — LTV d1–d30 by country (IAP + ads) |
| `analytics/sql/roi_by_country_d30.sql` | Template — needs UA spend table |
| `analytics/sql/verify_bigquery_export.sql` | Sanity check for export |

### Existing CLI (parallel to BI)

```bash
cd analytics
./scripts/auth_setup.sh
.venv/bin/python scripts/run_ltv_matrix.py --output output/ltv_by_country_d30.csv
```

---

## 4. ArrowsLegendBI stack

| Layer | Tech | Port |
|-------|------|------|
| Frontend | React 19, Vite 6, Recharts 2, TypeScript | 5173 |
| Backend | FastAPI, uvicorn, google-cloud-bigquery, pandas | 8000 |
| Dev orchestration | `npm run dev` (concurrently) | — |

### API endpoints (`server/main.py`)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Auth diagnostics + `SELECT 1` against BigQuery |
| GET | `/api/meta` | Returns `analytics/config.json` |
| GET | `/api/ltv-by-country?days_back=90` | Runs `ltv_by_country_d30.sql` |

Vite proxies `/health` and `/api/*` to port 8000 in dev.

### Frontend structure

- `web/src/App.tsx` — bootstrap, loads health + LTV
- `web/src/components/StatusBanner.tsx` — connection pills
- `web/src/components/SetupPanel.tsx` — shown when `bigquery: false`
- `web/src/components/LtvByCountry.tsx` — bar chart + table (top countries D30)
- Nav placeholders (disabled): **Levels**, **Retention**

---

## 5. Local setup (developer must do)

### Install

```bash
cd ArrowsLegendBI
npm run setup          # Python venv + npm deps
```

### Auth (required — blocker if missing)

**Option A — Service account (recommended)**

1. GCP → IAM → Service account on `arrowsmaster-6b84f`
2. Roles: **BigQuery Data Viewer**, **BigQuery Job User**
3. Download JSON key (never commit)
4. `cp .env.example .env` and set:
   ```
   GOOGLE_APPLICATION_CREDENTIALS=/absolute/path/to/key.json
   ```

**Option B — gcloud ADC**

```bash
brew install --cask google-cloud-sdk
gcloud auth application-default login
gcloud config set project arrowsmaster-6b84f
```

### Verify

```bash
npm run check:bq       # exit 0 + "bigquery": true
npm run dev            # http://localhost:5173
```

### GA4 prerequisite

GA4 Admin → **BigQuery Link** enabled for property `520375039`. Without export, auth succeeds but queries return no/empty tables.

---

## 6. Known issues / current state

| Issue | Cause | Fix |
|-------|--------|-----|
| Dashboard shows **BigQuery unavailable** | No ADC and no `.env` on machine | Section 5 |
| `gcloud: command not found` | CLI not installed | Service account path or `brew install --cask google-cloud-sdk` |
| Empty LTV table after auth | BQ export off or immature cohort SQL | Enable GA4 link; run `verify_bigquery_export.sql` in BQ console |
| Python 3.9 warnings | System venv used 3.9 | Prefer `python3.11 -m venv server/.venv` in setup script (optional improvement) |

**Product owner machine:** Auth was not configured at handoff time; BI scaffold is complete, data connection is the first task for the developer.

---

## 7. Suggested next work (priority)

1. **Unblock data:** Confirm `npm run check:bq` passes on dev machine; document any IAM/BQ export gaps.
2. **Level funnel dashboard** — new `analytics/sql/level_funnel.sql` + React panel:
   - Starts vs completes vs fail rate by `level_id`
   - Filter main vs challenge levels (`Challenge_` prefix in analytics)
3. **Retention dashboard** — D1/D7 from `first_open` + `Ret_*` events or standard GA4 retention logic in SQL.
4. **Revenue split** — IAP (`purchase`) vs ads (`ad_impression`) over time.
5. **ROI panel** — wire `roi_by_country_d30.sql` once UA spend table exists (Singular/ad network BQ dump).
6. **UX:** Date range picker, loading skeletons, export CSV from UI.
7. **Optional:** Tauri Mac app wrapping `web/dist` + local API.

---

## 8. Conventions for agents

- **Minimize scope** — BI changes stay under `ArrowsLegendBI/` and `analytics/sql/`; do not modify Unity gameplay unless explicitly asked.
- **Reuse** `analytics/config.json` and `analytics/sql/` — do not duplicate GCP IDs.
- **Never commit** `.env`, `service-account*.json`, `analytics/output/*.csv`, `node_modules/`, `server/.venv/`.
- **SQL changes** — add files under `analytics/sql/`; expose via new FastAPI routes in `server/main.py`.
- **UI** — match existing dark theme in `web/src/App.css`; add nav items when panels are ready (enable disabled tabs).
- **Test locally** — `npm run check:bq`, `npm run build`, manual smoke on `npm run dev`.

---

## 9. Commands cheat sheet

```bash
# From repo root
open ArrowsLegend.code-workspace    # Cursor: Unity + BI

# BI only
cd ArrowsLegendBI
npm run setup
npm run check:bq
npm run dev
npm run build

# Analytics CLI (no UI)
cd analytics && ./scripts/auth_setup.sh
```

---

## 10. Files to read first (agent onboarding)

1. `ArrowsLegendBI/HANDOFF.md` (this file)
2. `ArrowsLegendBI/server/main.py`
3. `analytics/config.json`
4. `analytics/sql/ltv_by_country_d30.sql`
5. `Assets/Scripts/Core/FirebaseManager.cs` (event names)
6. `ArrowsLegendBI/web/src/App.tsx`

---

## 11. Copy-paste prompt for the developer's agent

```
You are taking over ArrowsLegendBI inside the ArrowsMaster Unity monorepo.

Read ArrowsLegendBI/HANDOFF.md fully, then:
1. Run `cd ArrowsLegendBI && npm run setup && npm run check:bq` and fix BigQuery auth if degraded (service account .env or gcloud ADC).
2. Confirm GA4→BigQuery export for property 520375039.
3. Implement the next dashboard: level funnel (level_start/level_end from events_*), following existing patterns in server/main.py and web/src/components/LtvByCountry.tsx.

Do not change Unity game code unless required. Keep secrets out of git.
```

---

## 12. Contact / access notes for PM

Developer needs:

- Access to GCP project `arrowsmaster-6b84f` (BigQuery + optional service account key creation)
- Access to GA4 property `520375039` (verify BigQuery link)
- This git repo clone

No Firebase console required for BI if BigQuery export is already flowing.
