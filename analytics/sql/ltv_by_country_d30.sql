-- Cumulative average revenue per install (LTV) by country, days 1–30.
-- Project: arrowsmaster-6b84f (Firebase / GA4 BigQuery export required).
--
-- GA4 property: Arrows Master (520375039)
-- Dataset: analytics_520375039
--
-- Revenue sources (from client):
--   purchase      → IAP (value + currency; may need FX to USD)
--   ad_impression   → ads (value in USD per AdsManager)
--
-- Output: one row per country, columns d1 … d30 (cumulative avg $ per player).

DECLARE start_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 90 DAY);
DECLARE end_date   DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY);
-- Only count installs old enough to have full 30-day window (set FALSE to include recent installs).
DECLARE require_mature_cohort BOOL DEFAULT TRUE;

WITH events AS (
  SELECT
    user_pseudo_id,
    PARSE_DATE('%Y%m%d', event_date) AS event_date,
    event_name,
    geo.country AS country,
    (SELECT ep.value.double_value FROM UNNEST(event_params) ep WHERE ep.key = 'value') AS value_num,
    (SELECT ep.value.string_value FROM UNNEST(event_params) ep WHERE ep.key = 'currency') AS currency
  FROM `arrowsmaster-6b84f.analytics_520375039.events_*`
  WHERE _TABLE_SUFFIX BETWEEN FORMAT_DATE('%Y%m%d', start_date) AND FORMAT_DATE('%Y%m%d', end_date)
),

installs AS (
  SELECT
    user_pseudo_id,
    country,
    MIN(event_date) AS install_date
  FROM events
  WHERE event_name IN ('first_open', 'first_visit')
    AND country IS NOT NULL
    AND country != ''
  GROUP BY 1, 2
  HAVING install_date BETWEEN start_date AND end_date
    AND (NOT require_mature_cohort OR install_date <= DATE_SUB(CURRENT_DATE(), INTERVAL 30 DAY))
),

revenue_daily AS (
  SELECT
    e.user_pseudo_id,
    i.country,
    i.install_date,
    e.event_date,
    DATE_DIFF(e.event_date, i.install_date, DAY) + 1 AS day_since_install,
    SUM(
      CASE
        WHEN e.event_name = 'ad_impression' THEN COALESCE(e.value_num, 0)
        WHEN e.event_name = 'purchase' THEN COALESCE(e.value_num, 0)  -- TODO: FX if currency != USD
        ELSE 0
      END
    ) AS revenue_usd
  FROM events e
  INNER JOIN installs i ON e.user_pseudo_id = i.user_pseudo_id
  WHERE e.event_name IN ('purchase', 'ad_impression')
    AND DATE_DIFF(e.event_date, i.install_date, DAY) BETWEEN 0 AND 29
  GROUP BY 1, 2, 3, 4, 5
),

-- Cumulative revenue per user through each cohort day 1..30
user_cum AS (
  SELECT
    i.country,
    i.user_pseudo_id,
    d AS day_n,
    COALESCE(SUM(r.revenue_usd), 0) AS cum_revenue_usd
  FROM installs i
  CROSS JOIN UNNEST(GENERATE_ARRAY(1, 30)) AS d
  LEFT JOIN revenue_daily r
    ON r.user_pseudo_id = i.user_pseudo_id
   AND r.day_since_install <= d
  GROUP BY 1, 2, 3
),

country_day_avg AS (
  SELECT
    country,
    day_n,
    AVG(cum_revenue_usd) AS avg_ltv_usd,
    COUNT(DISTINCT user_pseudo_id) AS players
  FROM user_cum
  GROUP BY 1, 2
)

SELECT *
FROM country_day_avg
PIVOT (
  MAX(avg_ltv_usd) FOR day_n IN (
    1 AS d1, 2 AS d2, 3 AS d3, 4 AS d4, 5 AS d5,
    6 AS d6, 7 AS d7, 8 AS d8, 9 AS d9, 10 AS d10,
    11 AS d11, 12 AS d12, 13 AS d13, 14 AS d14, 15 AS d15,
    16 AS d16, 17 AS d17, 18 AS d18, 19 AS d19, 20 AS d20,
    21 AS d21, 22 AS d22, 23 AS d23, 24 AS d24, 25 AS d25,
    26 AS d26, 27 AS d27, 28 AS d28, 29 AS d29, 30 AS d30
  )
)
ORDER BY d30 DESC;
