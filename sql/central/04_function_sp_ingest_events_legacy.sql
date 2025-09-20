-- Legacy function version named sp_ingest_events() RETURNS void (kept for completeness)
CREATE OR REPLACE FUNCTION public.sp_ingest_events()
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
  WITH pending AS (
    SELECT id, source_plant, payload_json
    FROM public.events_inbox
    WHERE processed_at IS NULL
    ORDER BY id
    FOR UPDATE SKIP LOCKED
  ),
  p_ext AS (
    SELECT
      p.id,
      p.source_plant,
      NULLIF(trim(p.payload_json->>'asset_code'), '') AS asset_code,
      CASE WHEN jsonb_typeof(p.payload_json->'prev_thk') IN ('number','string')
           THEN NULLIF(p.payload_json->>'prev_thk','')::numeric END AS prev_thk,
      CASE WHEN NULLIF(p.payload_json->>'prev_date','') IS NOT NULL
           THEN (p.payload_json->>'prev_date')::timestamptz END AS prev_date,
      CASE WHEN jsonb_typeof(p.payload_json->'last_thk') IN ('number','string')
           THEN NULLIF(p.payload_json->>'last_thk','')::numeric END AS last_thk,
      CASE WHEN NULLIF(p.payload_json->>'last_date','') IS NOT NULL
           THEN (p.payload_json->>'last_date')::timestamptz END AS last_date
    FROM pending p
  ),
  ins_assets AS (
    INSERT INTO public.assets_global(asset_code, name, type, plant_code)
    SELECT DISTINCT x.asset_code, x.asset_code, NULL, x.source_plant
    FROM p_ext x
    WHERE x.asset_code IS NOT NULL
    ON CONFLICT (asset_code) DO NOTHING
    RETURNING asset_code
  ),
  upsert_cr AS (
    INSERT INTO public.analytics_cr(asset_code, prev_thk, prev_date, last_thk, last_date, cr, updated_at)
    SELECT
      x.asset_code,
      x.prev_thk,
      x.prev_date,
      x.last_thk,
      x.last_date,
      CASE
        WHEN x.prev_thk IS NOT NULL AND x.last_thk IS NOT NULL
         AND x.prev_date IS NOT NULL AND x.last_date IS NOT NULL
        THEN (x.prev_thk - x.last_thk) / GREATEST(1, DATE_PART('day', x.last_date - x.prev_date))
        ELSE NULL
      END AS cr,
      now()
    FROM p_ext x
    WHERE x.asset_code IS NOT NULL
    ON CONFLICT (asset_code) DO UPDATE
      SET prev_thk   = EXCLUDED.prev_thk,
          prev_date  = EXCLUDED.prev_date,
          last_thk   = EXCLUDED.last_thk,
          last_date  = EXCLUDED.last_date,
          cr         = EXCLUDED.cr,
          updated_at = now()
    RETURNING asset_code
  )
  UPDATE public.events_inbox e
  SET processed_at = now()
  FROM pending p
  WHERE e.id = p.id;
END;
$$;
