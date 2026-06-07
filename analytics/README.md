# Analytics (data layer)

Shared **GA4 / BigQuery** config and SQL for Arrows Master. Used by:

- CLI scripts in `scripts/` (CSV export)
- **[`../ArrowsLegendBI`](../ArrowsLegendBI)** — local web dashboard

## Config

`config.json` — GCP project, GA4 property `520375039`, BigQuery dataset.

## Auth

```bash
./scripts/auth_setup.sh
```

## BI dashboard

```bash
cd ../ArrowsLegendBI && npm run setup && npm run dev
```

Open http://localhost:5173 after the API and web servers start.
