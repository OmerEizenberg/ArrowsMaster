-- Quick sanity check: GA4 BigQuery export for Arrows Master (property 520375039).
-- Run in BigQuery console or via MCP / run_ltv_matrix.py

SELECT
  MIN(PARSE_DATE('%Y%m%d', event_date)) AS min_event_date,
  MAX(PARSE_DATE('%Y%m%d', event_date)) AS max_event_date,
  COUNT(*) AS event_rows,
  COUNT(DISTINCT user_pseudo_id) AS users,
  COUNTIF(event_name = 'first_open') AS first_open_events,
  COUNTIF(event_name = 'purchase') AS purchases,
  COUNTIF(event_name = 'ad_impression') AS ad_impressions
FROM `arrowsmaster-6b84f.analytics_520375039.events_*`
WHERE _TABLE_SUFFIX BETWEEN FORMAT_DATE('%Y%m%d', DATE_SUB(CURRENT_DATE(), INTERVAL 7 DAY))
  AND FORMAT_DATE('%Y%m%d', CURRENT_DATE());
