#!/usr/bin/env bash
# One-time auth for BigQuery (Arrows Master / arrowsmaster-6b84f)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if ! command -v gcloud >/dev/null 2>&1; then
  echo "Install Google Cloud CLI: https://cloud.google.com/sdk/docs/install"
  exit 1
fi

echo "Logging in and setting Application Default Credentials..."
gcloud auth login
gcloud auth application-default login
gcloud config set project arrowsmaster-6b84f

echo ""
echo "Verify export (optional, requires BigQuery export enabled in GA4):"
echo "  bq query --use_legacy_sql=false < sql/verify_bigquery_export.sql"
echo ""
echo "Export LTV matrix:"
echo "  .venv/bin/python scripts/run_ltv_matrix.py --output output/ltv_by_country_d30.csv"
