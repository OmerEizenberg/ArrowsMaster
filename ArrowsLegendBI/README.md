# ArrowsLegendBI

Web BI dashboard for **Arrows Master**, living in the same git repo as the Unity game. Data comes from GA4 / Firebase via the shared BigQuery export configured in [`../analytics/config.json`](../analytics/config.json).

## Prerequisites

1. **BigQuery export** enabled for GA4 property `520375039` (GA4 Admin → BigQuery Link).
2. **Google credentials on your Mac** (pick one — required or the dashboard shows *BigQuery unavailable*):

   **Option A — Service account (recommended, no gcloud needed)**

   1. [GCP Console](https://console.cloud.google.com/iam-admin/serviceaccounts?project=arrowsmaster-6b84f) → Create service account → add roles **BigQuery Data Viewer** + **BigQuery Job User**.
   2. Create JSON key, save outside the repo (e.g. `~/keys/arrows-bi.json`).
   3. Copy `.env.example` → `.env` and set:
      ```bash
      GOOGLE_APPLICATION_CREDENTIALS=/Users/you/keys/arrows-bi.json
      ```

   **Option B — gcloud login**

   ```bash
   brew install --cask google-cloud-sdk   # if gcloud is missing
   ../analytics/scripts/auth_setup.sh
   ```

3. **Node.js** 20+ and **Python** 3.10+.

### Verify auth

```bash
cd ArrowsLegendBI
server/.venv/bin/python scripts/check_bigquery.py
```

Should print `"bigquery": true`. If not, follow the printed setup steps.

## Quick start

```bash
cd ArrowsLegendBI
python3 -m venv server/.venv
server/.venv/bin/pip install -r server/requirements.txt
npm install --prefix web
npm run dev
```

- Dashboard: http://localhost:5173  
- API: http://localhost:8000  

`npm run dev` starts both the FastAPI server and the Vite dev server.

## Open with the Unity project

- **Cursor / VS Code**: open [`../ArrowsLegend.code-workspace`](../ArrowsLegend.code-workspace) (Unity + BI roots).
- **Rider / Visual Studio**: open [`../ArrowsLegend.sln`](../ArrowsLegend.sln) — includes an **ArrowsLegendBI** solution folder alongside Unity (Unity still generates its own `*.csproj` locally when you open the Editor).

## Layout

| Path | Role |
|------|------|
| `web/` | React + Vite dashboard (charts, tables) |
| `server/` | FastAPI — runs SQL from `../analytics/sql/` |
| `../analytics/` | Shared config, SQL, and CLI export scripts |

## API (local)

| Endpoint | Description |
|----------|-------------|
| `GET /health` | Auth + BigQuery connectivity check |
| `GET /api/meta` | GA4 property metadata from config |
| `GET /api/ltv-by-country` | LTV d1–d30 matrix by country |

## Desktop app (later)

The web app can be wrapped as a Mac app with [Tauri](https://tauri.app/) or Electron without moving out of this folder. Start with the browser workflow above.

## Secrets

Never commit service account JSON or API keys. Use `gcloud auth application-default login` or place credentials outside the repo and set `GOOGLE_APPLICATION_CREDENTIALS`.
