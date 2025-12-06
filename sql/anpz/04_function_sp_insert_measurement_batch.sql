CREATE OR REPLACE FUNCTION public.sp_insert_measurement_batch(
  p_asset_code   TEXT,
  p_points       JSONB,
  p_source_plant TEXT
) RETURNS INTEGER
LANGUAGE plpgsql
AS $$
DECLARE
  v_clean jsonb;
  v_inserted int;
  v_source text := COALESCE(NULLIF(trim(p_source_plant),''), 'ANPZ');
BEGIN
  IF p_asset_code IS NULL OR trim(p_asset_code) = '' THEN
    RAISE EXCEPTION 'asset_code is required';
  END IF;
  IF p_points IS NULL OR jsonb_typeof(p_points) <> 'array' THEN
    RAISE EXCEPTION 'p_points must be JSONB array';
  END IF;

  -- очистка и валидация точек
  SELECT jsonb_agg(obj) INTO v_clean
  FROM (
    SELECT jsonb_build_object(
             'label', lbl,
             'ts', ts::timestamptz,
             'thickness', thk,
             'note', note
           ) AS obj
    FROM (
      SELECT
        NULLIF(trim(x->>'label'),'') AS lbl,
        (x->>'ts')::timestamptz AS ts,
        NULLIF(x->>'thickness','')::numeric AS thk,
        NULLIF(x->>'note','') AS note
      FROM jsonb_array_elements(p_points) x
    ) s
    WHERE lbl IS NOT NULL
      AND ts IS NOT NULL
      AND thk IS NOT NULL
      AND thk > 0
  ) q;

  IF v_clean IS NULL OR jsonb_array_length(v_clean) = 0 THEN
    RAISE EXCEPTION 'no valid measurement points';
  END IF;

  CALL public.sp_insert_measurement_batch_prc(p_asset_code, v_clean, v_source, v_inserted);
  RETURN COALESCE(v_inserted, 0);
END$$;
