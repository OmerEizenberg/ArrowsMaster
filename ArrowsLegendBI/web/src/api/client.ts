export type AuthStatus = {
  ready: boolean;
  method: string | null;
  gcloud_installed: boolean;
  adc_file_exists: boolean;
  service_account_env_set: boolean;
  service_account_file_exists: boolean;
  service_account_path: string | null;
  env_file_exists: boolean;
};

export type HealthResponse = {
  status: string;
  bigquery: boolean;
  bigquery_error: string | null;
  gcp_project_id: string;
  ga4_property_name?: string;
  auth?: AuthStatus;
  setup_steps?: string[];
};

export type MetaResponse = {
  gcp_project_id: string;
  ga4_property_id: string;
  ga4_property_name: string;
  bigquery_dataset: string;
  events_table_pattern: string;
};

export type LtvRow = Record<string, string | number | null>;

export type LtvResponse = {
  days_back: number;
  row_count: number;
  rows: LtvRow[];
};

async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(path);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
  return res.json() as Promise<T>;
}

export const api = {
  health: () => getJson<HealthResponse>("/health"),
  meta: () => getJson<MetaResponse>("/api/meta"),
  ltvByCountry: (daysBack: number) =>
    getJson<LtvResponse>(`/api/ltv-by-country?days_back=${daysBack}`),
};
