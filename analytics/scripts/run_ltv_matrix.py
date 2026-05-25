#!/usr/bin/env python3
"""
Run ltv_by_country_d30.sql in BigQuery and save a CSV matrix (country × d1..d30).

Prerequisites:
  pip install google-cloud-bigquery pandas
  gcloud auth application-default login
  GA4 → BigQuery Link enabled for arrowsmaster-6b84f

Usage:
  python run_ltv_matrix.py
  python run_ltv_matrix.py --output ltv_by_country.csv
"""

from __future__ import annotations

import argparse
import pathlib
import sys

try:
    from google.cloud import bigquery
except ImportError:
    print("Install: pip install google-cloud-bigquery pandas", file=sys.stderr)
    raise

CONFIG_PATH = pathlib.Path(__file__).resolve().parent.parent / "config.json"
SQL_PATH = pathlib.Path(__file__).resolve().parent.parent / "sql" / "ltv_by_country_d30.sql"

DEFAULT_PROPERTY_ID = "520375039"
PROJECT_ID = "arrowsmaster-6b84f"


def main() -> None:
    parser = argparse.ArgumentParser(description="Export LTV d1-d30 by country from BigQuery")
    parser.add_argument(
        "--property-id",
        default=DEFAULT_PROPERTY_ID,
        help=f"GA4 property ID (default: {DEFAULT_PROPERTY_ID})",
    )
    parser.add_argument("--output", default="ltv_by_country_d30.csv", help="Output CSV path")
    parser.add_argument("--days-back", type=int, default=90, help="Install cohort lookback window")
    args = parser.parse_args()

    sql = SQL_PATH.read_text()
    # Legacy placeholder support if SQL is edited without baked-in dataset id
    sql = sql.replace("YOUR_GA4_PROPERTY_ID", args.property_id)
    sql = sql.replace(
        "DECLARE start_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 90 DAY);",
        f"DECLARE start_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL {args.days_back} DAY);",
    )

    client = bigquery.Client(project=PROJECT_ID)
    df = client.query(sql).to_dataframe()
    out = pathlib.Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(out, index=False)
    print(f"Wrote {len(df)} countries → {out.resolve()}")
    if len(df):
        print(df.head(10).to_string())


if __name__ == "__main__":
    main()
