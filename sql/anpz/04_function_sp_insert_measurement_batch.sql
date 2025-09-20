CREATE OR REPLACE FUNCTION public.sp_insert_measurement_batch(
  p_asset_code   TEXT,
  p_points       JSONB,
  p_source_plant TEXT
) RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
  v_asset_id  BIGINT;
  v_inserted  INTEGER := 0;
  v_prev_date timestamptz;
  v_prev_thk  numeric;
  v_last_date timestamptz;
  v_last_thk  numeric;
BEGIN
  IF p_asset_code IS NULL OR trim(p_asset_code) = '' THEN
    RAISE EXCEPTION 'asset_code is required';
  END IF;
  IF p_points IS NULL OR jsonb_typeof(p_points) <> 'array' THEN
    RAISE EXCEPTION 'p_points must be JSONB array';
  END IF;

  WITH s AS (SELECT id FROM public.assets_local WHERE asset_code = p_asset_code LIMIT 1),
       i AS (
         INSERT INTO public.assets_local(asset_code, location, status)
         SELECT p_asset_code, NULL, NULL
         WHERE NOT EXISTS (SELECT 1 FROM s)
         RETURNING id
       )
  SELECT COALESCE((SELECT id FROM s), (SELECT id FROM i)) INTO v_asset_id;

  WITH lbls AS (
    SELECT DISTINCT NULLIF(trim(x->>'label'),'') AS label
    FROM jsonb_array_elements(p_points) x
    WHERE NULLIF(trim(x->>'label'),'') IS NOT NULL
  )
  INSERT INTO public.measurement_points(asset_id, label)
  SELECT v_asset_id, l.label FROM lbls l
  ON CONFLICT (asset_id, label) DO NOTHING;

  WITH ins AS (
    INSERT INTO public.measurements(point_id, ts, thickness, note)
    SELECT mp.id,
           NULLIF(x->>'ts','')::timestamptz,
           NULLIF(x->>'thickness','')::numeric,
           NULLIF(x->>'note','')
    FROM jsonb_array_elements(p_points) x
    JOIN public.measurement_points mp
      ON mp.asset_id = v_asset_id
     AND mp.label = NULLIF(trim(x->>'label'),'')
    WHERE NULLIF(x->>'ts','') IS NOT NULL
      AND NULLIF(x->>'thickness','') IS NOT NULL
    RETURNING ts, thickness
  )
  SELECT
    MIN(ts),
    (SELECT thickness FROM ins ORDER BY ts ASC  LIMIT 1),
    MAX(ts),
    (SELECT thickness FROM ins ORDER BY ts DESC LIMIT 1)
  INTO v_prev_date, v_prev_thk, v_last_date, v_last_thk
  FROM ins;

  GET DIAGNOSTICS v_inserted = ROW_COUNT;

  IF v_inserted > 0 THEN
    INSERT INTO central_ft.events_inbox
      (id, event_type, source_plant, payload_json, created_at, processed_at)
    VALUES
      (DEFAULT,
       'MEASUREMENT_BATCH_ADDED',
       COALESCE(p_source_plant, 'ANPZ'),
       jsonb_build_object(
         'asset_code', p_asset_code,
         'prev_thk',   v_prev_thk,
         'prev_date',  v_prev_date,
         'last_thk',   v_last_thk,
         'last_date',  v_last_date
       ),
       DEFAULT,
       NULL);
  END IF;

  RETURN v_inserted;
END$$;
