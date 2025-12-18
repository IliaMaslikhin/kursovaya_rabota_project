-- 0) calc helper
CREATE OR REPLACE FUNCTION public.fn_calc_cr(prev_thk numeric, prev_date timestamptz, last_thk numeric, last_date timestamptz)
RETURNS numeric
LANGUAGE sql
IMMUTABLE
AS $$
  SELECT CASE
    WHEN prev_thk IS NOT NULL AND last_thk IS NOT NULL AND prev_date IS NOT NULL AND last_date IS NOT NULL
      THEN (prev_thk - last_thk) / GREATEST(1, DATE_PART('day', last_date - prev_date))
    ELSE NULL
  END
$$;

-- 1) asset upsert
CREATE OR REPLACE FUNCTION public.fn_asset_upsert(
  p_asset_code  text,
  p_name        text DEFAULT NULL,
  p_type        text DEFAULT NULL,
  p_plant_code  text DEFAULT NULL
) RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE v_id bigint;
BEGIN
  CALL public.sp_asset_upsert(p_asset_code, p_name, p_type, p_plant_code, v_id);
  RETURN v_id;
END$$;

-- 2) policy upsert
CREATE OR REPLACE FUNCTION public.fn_policy_upsert(
  p_name text,
  p_low  numeric,
  p_med  numeric,
  p_high numeric
) RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE v_id bigint;
BEGIN
  CALL public.sp_policy_upsert(p_name, p_low, p_med, p_high, v_id);
  RETURN v_id;
END$$;

-- 3) eval risk
CREATE OR REPLACE FUNCTION public.fn_eval_risk(
  p_asset_code  text,
  p_policy_name text DEFAULT 'default'
) RETURNS TABLE(
  asset_code     text,
  cr             numeric,
  level          text,
  threshold_low  numeric,
  threshold_med  numeric,
  threshold_high numeric
)
LANGUAGE sql
AS $$
  WITH a AS (
    SELECT asset_code, cr
    FROM public.analytics_cr
    WHERE asset_code = p_asset_code
  ),
  pol AS (
    SELECT threshold_low, threshold_med, threshold_high
    FROM public.risk_policies
    WHERE name = p_policy_name
  )
  SELECT
    p_asset_code,
    a.cr,
    CASE
      WHEN a.cr IS NULL THEN 'UNKNOWN'
      WHEN a.cr >= pol.threshold_high THEN 'HIGH'
      WHEN a.cr >= pol.threshold_med THEN 'MEDIUM'
      WHEN a.cr >= pol.threshold_low THEN 'LOW'
      ELSE 'OK'
    END AS level,
    pol.threshold_low, pol.threshold_med, pol.threshold_high
  FROM a FULL JOIN pol ON true;
$$;

-- 9) summary json
CREATE OR REPLACE FUNCTION public.fn_asset_summary_json(
  p_asset_code  text,
  p_policy_name text DEFAULT 'default'
) RETURNS jsonb
LANGUAGE sql
AS $$
  SELECT jsonb_build_object(
    'asset', jsonb_build_object(
      'asset_code', ag.asset_code,
      'name',       ag.name,
      'type',       ag.type,
      'plant_code', ag.plant_code
    ),
    'analytics', jsonb_build_object(
      'prev_thk',   ac.prev_thk,
      'prev_date',  ac.prev_date,
      'last_thk',   ac.last_thk,
      'last_date',  ac.last_date,
      'cr',         ac.cr,
      'updated_at', ac.updated_at
    ),
    'risk', to_jsonb(r)
  )
  FROM public.assets_global ag
  LEFT JOIN public.analytics_cr ac ON ac.asset_code = ag.asset_code
  LEFT JOIN LATERAL (
    SELECT level, threshold_low, threshold_med, threshold_high, cr
    FROM public.fn_eval_risk(p_asset_code, p_policy_name)
  ) r ON true
  WHERE ag.asset_code = p_asset_code;
$$;

-- 10) top by cr
CREATE OR REPLACE FUNCTION public.fn_top_assets_by_cr(p_limit int DEFAULT 50)
RETURNS TABLE(asset_code text, cr numeric, updated_at timestamptz)
LANGUAGE sql
AS $$
  SELECT asset_code, cr, updated_at
  FROM public.analytics_cr
  WHERE cr IS NOT NULL
  ORDER BY cr DESC NULLS LAST
  LIMIT GREATEST(1, p_limit);
$$;

-- 11) plant cr stats
CREATE OR REPLACE FUNCTION public.fn_plant_cr_stats(
  p_plant text,
  p_from timestamptz,
  p_to timestamptz
) RETURNS TABLE(cr_mean numeric, cr_p90 numeric, assets_count int)
LANGUAGE sql
AS $$
  SELECT
    avg(ac.cr) AS cr_mean,
    percentile_cont(0.9) WITHIN GROUP (ORDER BY ac.cr) AS cr_p90,
    count(*) AS assets_count
  FROM public.analytics_cr ac
  JOIN public.assets_global ag ON ag.asset_code = ac.asset_code
  WHERE (p_plant IS NULL OR lower(ag.plant_code) = lower(p_plant))
    AND ac.updated_at BETWEEN p_from AND p_to
    AND ac.cr IS NOT NULL;
$$;
