import type { HealthResponse, MetaResponse } from "../api/client";

type Props = {
  health: HealthResponse | null;
  meta: MetaResponse | null;
  error: string | null;
  loading: boolean;
};

export function StatusBanner({ health, meta, error, loading }: Props) {
  const bqOk = health?.bigquery === true;

  return (
    <header className="status-banner">
      <div className="brand">
        <span className="brand-mark" aria-hidden />
        <div>
          <h1>Arrows Legend BI</h1>
          <p className="subtitle">
            {meta?.ga4_property_name ?? "Arrows Master"} · GA4{" "}
            {meta?.ga4_property_id ?? "—"}
          </p>
        </div>
      </div>
      <div className="status-pills">
        {loading && <span className="pill pill-muted">Connecting…</span>}
        {!loading && bqOk && (
          <span className="pill pill-ok">BigQuery connected</span>
        )}
        {!loading && !bqOk && (
          <span className="pill pill-warn" title={health?.bigquery_error ?? ""}>
            BigQuery unavailable
          </span>
        )}
        {meta && (
          <span className="pill pill-muted">{meta.gcp_project_id}</span>
        )}
      </div>
      {error && <p className="banner-error">{error}</p>}
      {!bqOk && health?.bigquery_error && (
        <p className="banner-hint">
          Run{" "}
          <code>analytics/scripts/auth_setup.sh</code> then refresh. BigQuery
          export must be enabled for GA4.
        </p>
      )}
    </header>
  );
}
