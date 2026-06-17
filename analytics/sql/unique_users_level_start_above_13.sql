-- Unique users who started a campaign level ABOVE 13 (level 14+) on a given day.
-- level_id format in app: level14, level15, ... (see GameManager.StartLevel)
--
-- Replace @target_date or use the default (yesterday UTC in BigQuery).
-- Run in BigQuery console or: cd ArrowsLegendBI && npm run query:level-above-13

DECLARE target_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY);

SELECT
  target_date AS report_date,
  COUNT(DISTINCT user_pseudo_id) AS unique_users_started_above_level_13,
  COUNT(*) AS level_start_events_above_level_13
FROM `arrowsmaster-6b84f.analytics_520375039.events_*`
WHERE _TABLE_SUFFIX = FORMAT_DATE('%Y%m%d', target_date)
  AND event_name = 'level_start'
  AND REGEXP_CONTAINS(
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level_id'),
    r'^level(1[4-9]|[2-9][0-9]|[1-9][0-9]{2,})$'
  );

-- Optional: same query with Android + app version filter (uncomment and run separately)
/*
DECLARE target_date DATE DEFAULT DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY);

SELECT
  target_date AS report_date,
  COUNT(DISTINCT user_pseudo_id) AS unique_users,
  COUNT(*) AS level_start_events
FROM `arrowsmaster-6b84f.analytics_520375039.events_*`
WHERE _TABLE_SUFFIX = FORMAT_DATE('%Y%m%d', target_date)
  AND event_name = 'level_start'
  AND platform = 'ANDROID'
  AND app_info.version >= '1.1.01'
  AND REGEXP_CONTAINS(
    (SELECT value.string_value FROM UNNEST(event_params) WHERE key = 'level_id'),
    r'^level(1[4-9]|[2-9][0-9]|[1-9][0-9]{2,})$'
  );
*/
