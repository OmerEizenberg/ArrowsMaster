import { useMemo } from "react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { LtvRow } from "../api/client";

type Props = {
  rows: LtvRow[];
  loading: boolean;
  daysBack: number;
  onDaysBackChange: (n: number) => void;
  onRefresh: () => void;
};

function num(row: LtvRow, key: string): number {
  const v = row[key];
  return typeof v === "number" ? v : Number(v) || 0;
}

export function LtvByCountry({
  rows,
  loading,
  daysBack,
  onDaysBackChange,
  onRefresh,
}: Props) {
  const topChart = useMemo(() => {
    return [...rows]
      .map((r) => ({
        country: String(r.country ?? "—"),
        d30: num(r, "d30"),
        players: num(r, "players"),
      }))
      .filter((r) => r.players > 0)
      .sort((a, b) => b.d30 - a.d30)
      .slice(0, 12);
  }, [rows]);

  const dayColumns = useMemo(() => {
    if (!rows.length) return [];
    return Object.keys(rows[0]).filter((k) => /^d\d+$/.test(k));
  }, [rows]);

  return (
    <section className="panel">
      <div className="panel-head">
        <div>
          <h2>LTV by country (D1–D30)</h2>
          <p className="panel-desc">
            Cumulative average revenue per install from IAP + ads (BigQuery /
            GA4 export).
          </p>
        </div>
        <div className="panel-actions">
          <label>
            Cohort window
            <select
              value={daysBack}
              onChange={(e) => onDaysBackChange(Number(e.target.value))}
              disabled={loading}
            >
              <option value={60}>60 days</option>
              <option value={90}>90 days</option>
              <option value={120}>120 days</option>
            </select>
          </label>
          <button type="button" onClick={onRefresh} disabled={loading}>
            {loading ? "Loading…" : "Refresh"}
          </button>
        </div>
      </div>

      {topChart.length > 0 && (
        <div className="chart-wrap">
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={topChart} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#2a3444" />
              <XAxis dataKey="country" tick={{ fill: "#8b97a8", fontSize: 11 }} />
              <YAxis tick={{ fill: "#8b97a8", fontSize: 11 }} />
              <Tooltip
                contentStyle={{
                  background: "#1c2330",
                  border: "1px solid #2a3444",
                  borderRadius: 8,
                }}
              />
              <Bar dataKey="d30" name="LTV D30 ($)" fill="#4f8cff" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Country</th>
              <th>Players</th>
              {dayColumns.map((d) => (
                <th key={d}>{d.toUpperCase()}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && !loading && (
              <tr>
                <td colSpan={2 + dayColumns.length} className="empty">
                  No data — check BigQuery auth and GA4 export.
                </td>
              </tr>
            )}
            {rows.map((row) => (
              <tr key={String(row.country)}>
                <td>{String(row.country ?? "—")}</td>
                <td>{num(row, "players").toLocaleString()}</td>
                {dayColumns.map((d) => (
                  <td key={d}>{num(row, d).toFixed(4)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
