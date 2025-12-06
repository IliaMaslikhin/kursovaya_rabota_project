CREATE OR REPLACE PROCEDURE public.sp_insert_measurement_batch_prc(
  IN p_asset_code   TEXT,
  IN p_points       JSONB,
  IN p_source_plant TEXT,
  OUT p_inserted    INTEGER
)
LANGUAGE plpgsql
AS $$
DECLARE
  v_asset_id  BIGINT;
  v_prev_date timestamptz;
  v_prev_thk  numeric;
  v_last_date timestamptz;
  v_last_thk  numeric;
  v_source    text := COALESCE(NULLIF(trim(p_source_plant),''), 'KRNPZ');
BEGIN
  p_inserted := 0;

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

  GET DIAGNOSTICS p_inserted = ROW_COUNT;

  IF p_inserted > 0 THEN
    INSERT INTO central_ft.events_inbox
      (id, event_type, source_plant, payload_json, created_at, processed_at)
    VALUES
      (DEFAULT,
       'HC_MEASUREMENT_BATCH',
       v_source,
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
END$$;
