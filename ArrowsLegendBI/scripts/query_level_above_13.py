#!/usr/bin/env python3
"""Run unique_users_level_start_above_13.sql for yesterday (or --date YYYY-MM-DD)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from server.main import auth_setup_steps, auth_status, bq_client, load_config  # noqa: E402

SQL_PATH = ROOT.parent / "analytics" / "sql" / "unique_users_level_start_above_13.sql"


def main() -> int:
    parser = argparse.ArgumentParser(description="Unique users with level_start above level 13")
    parser.add_argument(
        "--date",
        help="Report date YYYY-MM-DD (default: yesterday in BigQuery project timezone)",
    )
    args = parser.parse_args()

    auth = auth_status()
    if not auth["ready"]:
        print("BigQuery auth not configured.\n")
        for i, step in enumerate(auth_setup_steps(), 1):
            print(f"  {i}. {step}")
        return 1

    cfg = load_config()
    client = bq_client()

    if args.date:
        query = f"""
DECLARE target_date DATE DEFAULT DATE('{args.date}');

SELECT
  target_date AS report_date,
  COUNT(DISTINCT user_pseudo_id) AS unique_users_started_above_level_13,
  COUNT(*) AS level_start_events_above_level_13
FROM `{cfg['gcp_project_id']}.{cfg['bigquery_dataset']}.events_*`
WHERE _TABLE_SUFFIX = FORMAT_DATE('%Y%m%d', target_date)
  AND event_name = 'level_start'
  AND REGEXP_CONTAINS(
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level_id'),
    r'^level(1[4-9]|[2-9][0-9]|[1-9][0-9]{{2,}})$'
  );
"""
    else:
        query = SQL_PATH.read_text()
        # Use only the first statement (before optional comment block)
        query = query.split("-- Optional:")[0].strip()

    print(f"Project: {cfg['gcp_project_id']}")
    print(f"Dataset: {cfg['bigquery_dataset']}")
    if args.date:
        print(f"Date: {args.date}")
    else:
        print("Date: yesterday (BigQuery CURRENT_DATE - 1 day)")
    print()

    job = client.query(query)
    rows = list(job.result())
    if not rows:
        print("No rows returned.")
        return 0

    for row in rows:
        print(dict(row))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
