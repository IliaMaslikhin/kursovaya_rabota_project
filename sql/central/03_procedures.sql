-- Основная логика перенесена в процедуры; функции-обёртки делегируют вызовы сюда.

-- При изменении OUT-параметров PostgreSQL не позволяет CREATE OR REPLACE.
-- Чтобы автосинхронизация могла обновлять БД со старых версий, удаляем старые варианты процедур.
DROP PROCEDURE IF EXISTS public.sp_ingest_events(integer);
DROP PROCEDURE IF EXISTS public.sp_events_enqueue(text, text, jsonb);
DROP PROCEDURE IF EXISTS public.sp_events_requeue(bigint[]);
DROP PROCEDURE IF EXISTS public.sp_events_cleanup(interval);
DROP PROCEDURE IF EXISTS public.sp_policy_upsert(text, numeric, numeric, numeric);
DROP PROCEDURE IF EXISTS public.sp_asset_upsert(text, text, text, text);

CREATE OR REPLACE PROCEDURE public.sp_ingest_events(
  IN p_limit int,
  OUT processed int)
LANGUAGE plpgsql
AS $$
BEGIN
  processed := 0;

  -- discard/close out any unexpected event types so they don't block the queue
  UPDATE public.events_inbox
  SET processed_at = now()
  WHERE processed_at IS NULL
    AND event_type IS DISTINCT FROM 'HC_MEASUREMENT_BATCH';

  WITH cte AS (
    SELECT id, source_plant, payload_json
    FROM public.events_inbox
    WHERE processed_at IS NULL
      AND event_type = 'HC_MEASUREMENT_BATCH'
      AND payload_json ? 'asset_code'
      AND NULLIF(trim(payload_json->>'asset_code'), '') IS NOT NULL
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
      AND p.last_thk IS NOT NULL
      AND p.last_date IS NOT NULL
      AND (p.prev_date IS NULL OR p.last_date IS NULL OR p.prev_date <= p.last_date)
      AND (p.prev_thk IS NULL OR p.last_thk IS NULL OR p.prev_thk >= p.last_thk)
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
  PERFORM pg_notify('events_ingest', jsonb_build_object('processed', processed, 'ts', now())::text);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_enqueue(
  IN p_event_type text,
  IN p_source_plant text,
  IN p_payload jsonb,
  OUT p_id bigint)
LANGUAGE plpgsql
AS $$
BEGIN
  IF p_event_type IS NULL OR trim(p_event_type) = '' THEN
    RAISE EXCEPTION 'event_type is required';
  END IF;
  IF p_event_type <> 'HC_MEASUREMENT_BATCH' THEN
    RAISE EXCEPTION 'unsupported event_type: %', p_event_type;
  END IF;

  INSERT INTO public.events_inbox(event_type, source_plant, payload_json)
  VALUES (p_event_type, p_source_plant, COALESCE(p_payload, '{}'::jsonb))
  RETURNING id INTO p_id;

  PERFORM pg_notify('events_enqueue', jsonb_build_object(
    'id', p_id,
    'event_type', p_event_type,
    'source_plant', p_source_plant,
    'ts', now())::text);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_requeue(
  IN p_ids bigint[],
  OUT n int)
LANGUAGE plpgsql
AS $$
BEGIN
  UPDATE public.events_inbox SET processed_at = NULL WHERE id = ANY(p_ids);
  GET DIAGNOSTICS n = ROW_COUNT;

  PERFORM pg_notify('events_requeue', jsonb_build_object(
    'count', n,
    'ids', p_ids,
    'ts', now())::text);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_cleanup(
  IN p_older_than interval,
  OUT n int)
LANGUAGE plpgsql
AS $$
BEGIN
  DELETE FROM public.events_inbox
  WHERE processed_at IS NOT NULL
    AND processed_at < now() - p_older_than;
  GET DIAGNOSTICS n = ROW_COUNT;

  PERFORM pg_notify('events_cleanup', jsonb_build_object(
    'count', n,
    'older_than', p_older_than,
    'ts', now())::text);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_policy_upsert(
  IN p_name text,
  IN p_low numeric,
  IN p_med numeric,
  IN p_high numeric,
  OUT p_id bigint)
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO public.risk_policies(name, threshold_low, threshold_med, threshold_high)
  VALUES (p_name, p_low, p_med, p_high)
  ON CONFLICT (name) DO UPDATE
    SET threshold_low  = EXCLUDED.threshold_low,
        threshold_med  = EXCLUDED.threshold_med,
        threshold_high = EXCLUDED.threshold_high
  RETURNING id INTO p_id;

  IF p_id IS NULL THEN
    SELECT id INTO p_id FROM public.risk_policies WHERE name = p_name;
  END IF;
END$$;

CREATE OR REPLACE PROCEDURE public.sp_asset_upsert(
  IN p_asset_code text,
  IN p_name text,
  IN p_type text,
  IN p_plant_code text,
  OUT p_id bigint)
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO public.assets_global(asset_code, name, type, plant_code)
  VALUES (p_asset_code, COALESCE(p_name, p_asset_code), p_type, p_plant_code)
  ON CONFLICT (asset_code) DO UPDATE
    SET name       = COALESCE(EXCLUDED.name,       public.assets_global.name),
        type       = COALESCE(EXCLUDED.type,       public.assets_global.type),
        plant_code = COALESCE(EXCLUDED.plant_code, public.assets_global.plant_code)
  RETURNING id INTO p_id;

  IF p_id IS NULL THEN
    SELECT id INTO p_id FROM public.assets_global WHERE asset_code = p_asset_code;
  END IF;
END$$;
