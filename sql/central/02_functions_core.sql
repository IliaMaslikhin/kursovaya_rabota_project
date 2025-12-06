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
  INSERT INTO public.assets_global(asset_code, name, type, plant_code)
  VALUES (p_asset_code, COALESCE(p_name, p_asset_code), p_type, p_plant_code)
  ON CONFLICT (asset_code) DO UPDATE
    SET name       = COALESCE(EXCLUDED.name,       public.assets_global.name),
        type       = COALESCE(EXCLUDED.type,       public.assets_global.type),
        plant_code = COALESCE(EXCLUDED.plant_code, public.assets_global.plant_code)
  RETURNING id INTO v_id;

  IF v_id IS NULL THEN
    SELECT id INTO v_id FROM public.assets_global WHERE asset_code = p_asset_code;
  END IF;
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
  INSERT INTO public.risk_policies(name, threshold_low, threshold_med, threshold_high)
  VALUES (p_name, p_low, p_med, p_high)
  ON CONFLICT (name) DO UPDATE
    SET threshold_low  = EXCLUDED.threshold_low,
        threshold_med  = EXCLUDED.threshold_med,
        threshold_high = EXCLUDED.threshold_high
  RETURNING id INTO v_id;

  IF v_id IS NULL THEN
    SELECT id INTO v_id FROM public.risk_policies WHERE name = p_name;
  END IF;
  RETURN v_id;
END$$;

-- 3) enqueue event
CREATE OR REPLACE FUNCTION public.fn_events_enqueue(
  p_event_type   text,
  p_source_plant text,
  p_payload      jsonb
) RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE v_id bigint;
BEGIN
  INSERT INTO public.events_inbox(event_type, source_plant, payload_json)
  VALUES (p_event_type, p_source_plant, COALESCE(p_payload, '{}'::jsonb))
  RETURNING id INTO v_id;
  RETURN v_id;
END$$;

-- 4) peek
CREATE OR REPLACE FUNCTION public.fn_events_peek(p_limit int DEFAULT 100)
RETURNS TABLE(id bigint, event_type text, source_plant text, payload_json jsonb, created_at timestamptz)
LANGUAGE sql
AS $$
  SELECT id, event_type, source_plant, payload_json, created_at
  FROM public.events_inbox
  WHERE processed_at IS NULL
  ORDER BY id
  LIMIT GREATEST(1, p_limit);
$$;

-- 5) ingest batch
CREATE OR REPLACE FUNCTION public.fn_ingest_events(p_limit int DEFAULT 1000)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE processed int := 0;
BEGIN
  WITH cte AS (
    SELECT id, source_plant, payload_json
    FROM public.events_inbox
    WHERE processed_at IS NULL
    ORDER BY id
    LIMIT GREATEST(1, p_limit)
    FOR UPDATE SKIP LOCKED
  ),
  parsed AS (
    SELECT
      c.id,
      c.source_plant,
      NULLIF(trim(c.payload_json->>'asset_code'), '') AS asset_code,
      CASE WHEN jsonb_typeof(c.payload_json->'prev_thk') IN ('number','string')
           THEN NULLIF(c.payload_json->>'prev_thk','')::numeric END AS prev_thk,
      CASE WHEN NULLIF(c.payload_json->>'prev_date','') IS NOT NULL
           THEN (c.payload_json->>'prev_date')::timestamptz END AS prev_date,
      CASE WHEN jsonb_typeof(c.payload_json->'last_thk') IN ('number','string')
           THEN NULLIF(c.payload_json->>'last_thk','')::numeric END AS last_thk,
      CASE WHEN NULLIF(c.payload_json->>'last_date','') IS NOT NULL
           THEN (c.payload_json->>'last_date')::timestamptz END AS last_date
    FROM cte c
  ),
  upsert_assets AS (
    INSERT INTO public.assets_global(asset_code, name, type, plant_code)
    SELECT DISTINCT asset_code, asset_code, NULL, source_plant
    FROM parsed
    WHERE asset_code IS NOT NULL
    ON CONFLICT (asset_code) DO NOTHING
    RETURNING 1
  ),
  upsert_cr AS (
    INSERT INTO public.analytics_cr(asset_code, prev_thk, prev_date, last_thk, last_date, cr, updated_at)
    SELECT
      p.asset_code,
      p.prev_thk, p.prev_date,
      p.last_thk, p.last_date,
      public.fn_calc_cr(p.prev_thk, p.prev_date, p.last_thk, p.last_date),
      now()
    FROM parsed p
    WHERE p.asset_code IS NOT NULL
    ON CONFLICT (asset_code) DO UPDATE
      SET prev_thk   = EXCLUDED.prev_thk,
          prev_date  = EXCLUDED.prev_date,
          last_thk   = EXCLUDED.last_thk,
          last_date  = EXCLUDED.last_date,
          cr         = EXCLUDED.cr,
          updated_at = now()
    RETURNING 1
  )
  UPDATE public.events_inbox e
  SET processed_at = now()
  FROM cte
  WHERE e.id = cte.id;

  GET DIAGNOSTICS processed = ROW_COUNT;
  RETURN processed;
END$$;

-- 6) requeue
CREATE OR REPLACE FUNCTION public.fn_events_requeue(p_ids bigint[])
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE n int;
BEGIN
  UPDATE public.events_inbox SET processed_at = NULL WHERE id = ANY(p_ids);
  GET DIAGNOSTICS n = ROW_COUNT;
  RETURN n;
END$$;

-- 7) cleanup
CREATE OR REPLACE FUNCTION public.fn_events_cleanup(p_older_than interval DEFAULT '30 days')
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE n int;
BEGIN
  DELETE FROM public.events_inbox
  WHERE processed_at IS NOT NULL
    AND processed_at < now() - p_older_than;
  GET DIAGNOSTICS n = ROW_COUNT;
  RETURN n;
END$$;

-- 8) eval risk
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
