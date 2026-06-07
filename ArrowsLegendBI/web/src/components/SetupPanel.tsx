import type { HealthResponse } from "../api/client";

type Props = {
  health: HealthResponse | null;
  onRetry: () => void;
  loading: boolean;
};

export function SetupPanel({ health, onRetry, loading }: Props) {
  if (!health || health.bigquery) return null;

  const steps = health.setup_steps ?? [];
  const auth = health.auth;

  return (
    <section className="setup-panel">
      <h2>BigQuery not connected</h2>
      <p>
        The dashboard cannot read GA4 data until Google credentials are configured
        on <strong>this Mac</strong>. This is local-only — nothing is stored in git.
      </p>

      {health.bigquery_error && (
        <pre className="setup-error">{health.bigquery_error}</pre>
      )}

      <ul className="setup-checklist">
        <li className={auth?.service_account_file_exists ? "ok" : ""}>
          Service account JSON via <code>.env</code>
          {auth?.service_account_path && (
            <span className="muted"> — {auth.service_account_path}</span>
          )}
        </li>
        <li className={auth?.adc_file_exists ? "ok" : ""}>
          gcloud Application Default Credentials
          {!auth?.gcloud_installed && (
            <span className="muted"> (gcloud CLI not found in PATH)</span>
          )}
        </li>
      </ul>

      <ol className="setup-steps">
        {steps.map((step) => (
          <li key={step}>{step}</li>
        ))}
      </ol>

      <div className="setup-actions">
        <button type="button" onClick={onRetry} disabled={loading}>
          {loading ? "Checking…" : "Retry connection"}
        </button>
        <a
          href="https://console.cloud.google.com/bigquery?project=arrowsmaster-6b84f"
          target="_blank"
          rel="noreferrer"
        >
          Open BigQuery console
        </a>
      </div>
    </section>
  );
}
