import { useCallback, useEffect, useState } from "react";
import { api, type HealthResponse, type LtvRow, type MetaResponse } from "./api/client";
import { LtvByCountry } from "./components/LtvByCountry";
import { SetupPanel } from "./components/SetupPanel";
import { StatusBanner } from "./components/StatusBanner";
import "./App.css";

export default function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [meta, setMeta] = useState<MetaResponse | null>(null);
  const [ltvRows, setLtvRows] = useState<LtvRow[]>([]);
  const [daysBack, setDaysBack] = useState(90);
  const [loading, setLoading] = useState(true);
  const [ltvLoading, setLtvLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadLtv = useCallback(async (days: number) => {
    setLtvLoading(true);
    try {
      const res = await api.ltvByCountry(days);
      setLtvRows(res.rows);
    } catch (e) {
      setLtvRows([]);
      setError(e instanceof Error ? e.message : "Failed to load LTV data");
    } finally {
      setLtvLoading(false);
    }
  }, []);

  const bootstrap = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [h, m] = await Promise.all([api.health(), api.meta()]);
      setHealth(h);
      setMeta(m);
      if (h.bigquery) {
        await loadLtv(daysBack);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "API unreachable — is the server running?");
    } finally {
      setLoading(false);
    }
  }, [daysBack, loadLtv]);

  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  const refreshLtv = () => {
    if (health?.bigquery) void loadLtv(daysBack);
  };

  const onDaysBackChange = (n: number) => {
    setDaysBack(n);
    if (health?.bigquery) void loadLtv(n);
  };

  return (
    <div className="app">
      <StatusBanner
        health={health}
        meta={meta}
        error={error}
        loading={loading}
      />

      <nav className="nav-rail" aria-label="Dashboard sections">
        <span className="nav-item active">Monetization</span>
        <span className="nav-item disabled" title="Coming soon">
          Levels
        </span>
        <span className="nav-item disabled" title="Coming soon">
          Retention
        </span>
      </nav>

      <SetupPanel health={health} onRetry={() => void bootstrap()} loading={loading} />

      <main>
        <LtvByCountry
          rows={ltvRows}
          loading={loading || ltvLoading}
          daysBack={daysBack}
          onDaysBackChange={onDaysBackChange}
          onRefresh={refreshLtv}
        />
      </main>

      <footer className="footer">
        Same repo as Unity · data layer in <code>analytics/</code> · UI in{" "}
        <code>ArrowsLegendBI/</code>
      </footer>
    </div>
  );
}
