"""
Local API for ArrowsLegendBI — queries BigQuery using SQL from ../analytics/sql/.

Auth (pick one):
  1. Service account JSON via GOOGLE_APPLICATION_CREDENTIALS in ArrowsLegendBI/.env
  2. gcloud: gcloud auth application-default login
"""

from __future__ import annotations

import json
import os
import pathlib
import shutil
from typing import Any

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from google.api_core import exceptions as gcp_exceptions
from google.cloud import bigquery

BI_ROOT = pathlib.Path(__file__).resolve().parent.parent
REPO_ROOT = BI_ROOT.parent
ANALYTICS_DIR = REPO_ROOT / "analytics"
CONFIG_PATH = ANALYTICS_DIR / "config.json"
SQL_DIR = ANALYTICS_DIR / "sql"
ENV_PATH = BI_ROOT / ".env"

load_dotenv(ENV_PATH)

app = FastAPI(title="ArrowsLegendBI API", version="0.1.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://127.0.0.1:5173",
        "http://localhost:5173",
    ],
    allow_credentials=True,
    allow_methods=["GET"],
    allow_headers=["*"],
)

ADC_PATH = pathlib.Path.home() / ".config/gcloud/application_default_credentials.json"


def load_config() -> dict[str, Any]:
    if not CONFIG_PATH.is_file():
        raise HTTPException(500, f"Missing analytics config: {CONFIG_PATH}")
    return json.loads(CONFIG_PATH.read_text())


def auth_status() -> dict[str, Any]:
    creds_env = os.environ.get("GOOGLE_APPLICATION_CREDENTIALS", "").strip()
    creds_path = pathlib.Path(creds_env).expanduser() if creds_env else None
    creds_file_ok = bool(creds_path and creds_path.is_file())
    adc_ok = ADC_PATH.is_file()
    gcloud_ok = shutil.which("gcloud") is not None

    if creds_file_ok:
        method = "service_account"
        ready = True
    elif adc_ok:
        method = "application_default"
        ready = True
    else:
        method = None
        ready = False

    return {
        "ready": ready,
        "method": method,
        "gcloud_installed": gcloud_ok,
        "adc_file_exists": adc_ok,
        "adc_path": str(ADC_PATH),
        "service_account_env_set": bool(creds_env),
        "service_account_file_exists": creds_file_ok,
        "service_account_path": str(creds_path) if creds_path else None,
        "env_file": str(ENV_PATH),
        "env_file_exists": ENV_PATH.is_file(),
    }


def auth_setup_steps() -> list[str]:
    auth = auth_status()
    if auth["ready"]:
        return []

    steps = [
        "Create a GCP service account with BigQuery Data Viewer + Job User on project arrowsmaster-6b84f.",
        "Download the JSON key (never commit it).",
        f"Copy ArrowsLegendBI/.env.example to ArrowsLegendBI/.env and set GOOGLE_APPLICATION_CREDENTIALS=/full/path/to/key.json",
        "Restart: cd ArrowsLegendBI && npm run dev",
    ]
    if auth["gcloud_installed"]:
        steps.insert(
            0,
            "Or run: gcloud auth application-default login && gcloud config set project arrowsmaster-6b84f",
        )
    else:
        steps.insert(
            0,
            "Install Google Cloud CLI (brew install --cask google-cloud-sdk) OR use a service account JSON (recommended).",
        )
    steps.append(
        "In GA4 Admin → Product links → BigQuery Link: enable export for property 520375039.",
    )
    return steps


def bq_client() -> bigquery.Client:
    cfg = load_config()
    return bigquery.Client(project=cfg["gcp_project_id"])


@app.get("/health")
def health() -> dict[str, Any]:
    cfg = load_config()
    auth = auth_status()
    bq_ok = False
    bq_error: str | None = None

    if not auth["ready"]:
        bq_error = (
            "No Google credentials found. Set GOOGLE_APPLICATION_CREDENTIALS in "
            f"{ENV_PATH} or run gcloud auth application-default login."
        )
    else:
        try:
            bq_client().query("SELECT 1 AS ok").result()
            bq_ok = True
        except Exception as exc:  # noqa: BLE001
            bq_error = str(exc)

    return {
        "status": "ok" if bq_ok else "degraded",
        "bigquery": bq_ok,
        "bigquery_error": bq_error,
        "gcp_project_id": cfg["gcp_project_id"],
        "ga4_property_name": cfg.get("ga4_property_name"),
        "auth": auth,
        "setup_steps": auth_setup_steps(),
    }


@app.get("/api/meta")
def meta() -> dict[str, Any]:
    return load_config()


@app.get("/api/ltv-by-country")
def ltv_by_country(
    days_back: int = Query(90, ge=7, le=365),
) -> dict[str, Any]:
    if not auth_status()["ready"]:
        raise HTTPException(
            401,
            "BigQuery auth not configured. See /health setup_steps or ArrowsLegendBI/README.md.",
        )

    sql = (SQL_DIR / "ltv_by_country_d30.sql").read_text()
    sql = sql.replace(
        "DECLARE start_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 90 DAY);",
        f"DECLARE start_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL {days_back} DAY);",
    )
    try:
        df = bq_client().query(sql).to_dataframe()
    except gcp_exceptions.GoogleAPICallError as exc:
        raise HTTPException(502, f"BigQuery error: {exc}") from exc
    rows = df.to_dict(orient="records")
    return {"days_back": days_back, "row_count": len(rows), "rows": rows}
