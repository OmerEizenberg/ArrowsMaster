-- ROI by country using D30 cumulative LTV vs UA spend.
-- Requires a spend table (Singular export, manual sheet, or ad network BQ dump).
--
-- ROI = LTV_d30 / CPI   where CPI = spend / installs
-- ROAS (same as ROI when spend is ad spend) = revenue_d30_total / spend

-- Step 1: Run ltv_by_country_d30.sql logic or use its result as `ltv_matrix`.
-- Step 2: Join spend below.

-- Example spend schema (replace table + columns):
--   country STRING, spend_usd FLOAT64, period_start DATE, period_end DATE

/*
WITH ltv AS (
  -- paste country + d30 from ltv_by_country_d30.sql output, or CTE from that query
  SELECT country, d30 AS ltv_d30_usd, players FROM ...
),
spend AS (
  SELECT
    country,
    SUM(spend_usd) AS spend_usd,
    SUM(installs) AS paid_installs
  FROM `YOUR_PROJECT.YOUR_DATASET.ua_spend_by_country`
  WHERE period_start >= DATE_SUB(CURRENT_DATE(), INTERVAL 90 DAY)
  GROUP BY 1
)
SELECT
  l.country,
  l.players AS cohort_installs,
  l.ltv_d30_usd,
  s.spend_usd,
  s.paid_installs,
  SAFE_DIVIDE(s.spend_usd, s.paid_installs) AS cpi_usd,
  SAFE_DIVIDE(l.ltv_d30_usd, SAFE_DIVIDE(s.spend_usd, s.paid_installs)) AS roi_d30,
  SAFE_DIVIDE(l.ltv_d30_usd * l.players, s.spend_usd) AS roas_d30_blended
FROM ltv l
LEFT JOIN spend s ON l.country = s.country
ORDER BY roi_d30 DESC;
*/

-- Singular alternative (no SQL): Reports → Cohort → Group by Country → LTV D30 / CPI.
