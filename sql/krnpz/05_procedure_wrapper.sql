-- При изменении OUT-параметров PostgreSQL не позволяет CREATE OR REPLACE.
-- Чтобы апгрейд со старых версий проходил автоматически, удаляем старую процедуру.
DROP PROCEDURE IF EXISTS public.sp_insert_measurement_batch_prc(text, jsonb, text);

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
  v_existing_last_date timestamptz;
  v_existing_last_thk  numeric;
  v_batch_first_date   timestamptz;
  v_batch_first_thk    numeric;
  v_has_rows           boolean;
  v_has_violation      boolean;
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

  SELECT m.ts, m.thickness
  INTO v_existing_last_date, v_existing_last_thk
  FROM public.measurements m
  JOIN public.measurement_points mp ON mp.id = m.point_id
  WHERE mp.asset_id = v_asset_id
  ORDER BY m.ts DESC, m.id DESC
  LIMIT 1;

  WITH parsed AS (
    SELECT ordinality AS ord,
           NULLIF(trim(x.value->>'label'),'') AS label,
           NULLIF(x.value->>'ts','')::timestamptz AS ts,
           NULLIF(x.value->>'thickness','')::numeric AS thickness,
           NULLIF(x.value->>'note','') AS note
    FROM jsonb_array_elements_with_ordinality(p_points) AS x(value, ordinality)
  ),
  valid AS (
    SELECT * FROM parsed
    WHERE label IS NOT NULL AND ts IS NOT NULL AND thickness IS NOT NULL AND thickness > 0
  ),
  checks AS (
    SELECT
      EXISTS (SELECT 1 FROM valid) AS has_rows,
      (SELECT ts FROM valid ORDER BY ord LIMIT 1) AS first_ts,
      (SELECT thickness FROM valid ORDER BY ord LIMIT 1) AS first_thk,
      EXISTS (
        SELECT 1
        FROM (
          SELECT ts, thickness,
                 lag(ts) OVER (ORDER BY ord) AS prev_ts,
                 lag(thickness) OVER (ORDER BY ord) AS prev_thk
          FROM valid
        ) v
        WHERE (prev_ts IS NOT NULL AND ts <= prev_ts)
           OR (prev_thk IS NOT NULL AND thickness > prev_thk)
      ) AS has_violation
  )
  SELECT has_rows, first_ts, first_thk, has_violation
  INTO v_has_rows, v_batch_first_date, v_batch_first_thk, v_has_violation
  FROM checks;

  IF NOT v_has_rows THEN
    RAISE EXCEPTION 'no valid measurement points';
  END IF;
  IF v_has_violation THEN
    RAISE EXCEPTION 'points must be strictly increasing by ts and non-increasing by thickness';
  END IF;
  IF v_existing_last_date IS NOT NULL AND v_batch_first_date <= v_existing_last_date THEN
    RAISE EXCEPTION 'incoming measurements must be newer than latest stored ts=%', v_existing_last_date;
  END IF;
  IF v_existing_last_thk IS NOT NULL AND v_batch_first_thk > v_existing_last_thk THEN
    RAISE EXCEPTION 'thickness must not increase compared to latest stored value=%', v_existing_last_thk;
  END IF;

  WITH parsed AS (
    SELECT ordinality AS ord,
           NULLIF(trim(x.value->>'label'),'') AS label,
           NULLIF(x.value->>'ts','')::timestamptz AS ts,
           NULLIF(x.value->>'thickness','')::numeric AS thickness,
           NULLIF(x.value->>'note','') AS note
    FROM jsonb_array_elements(p_points) WITH ORDINALITY AS x(value, ordinality)
  ),
  valid AS (
    SELECT * FROM parsed
    WHERE label IS NOT NULL AND ts IS NOT NULL AND thickness IS NOT NULL AND thickness > 0
  ),
  lbls AS (
    SELECT DISTINCT label FROM valid
  )
  INSERT INTO public.measurement_points(asset_id, label)
  SELECT v_asset_id, l.label FROM lbls l
  ON CONFLICT (asset_id, label) DO NOTHING;

  WITH parsed AS (
    SELECT ordinality AS ord,
           NULLIF(trim(x.value->>'label'),'') AS label,
           NULLIF(x.value->>'ts','')::timestamptz AS ts,
           NULLIF(x.value->>'thickness','')::numeric AS thickness,
           NULLIF(x.value->>'note','') AS note
    FROM jsonb_array_elements(p_points) WITH ORDINALITY AS x(value, ordinality)
  ),
  valid AS (
    SELECT * FROM parsed
    WHERE label IS NOT NULL AND ts IS NOT NULL AND thickness IS NOT NULL AND thickness > 0
  )
  INSERT INTO public.measurements(point_id, ts, thickness, note)
  SELECT mp.id,
         v.ts,
         v.thickness,
         v.note
  FROM valid v
  JOIN public.measurement_points mp
    ON mp.asset_id = v_asset_id
   AND mp.label = v.label;

  GET DIAGNOSTICS p_inserted = ROW_COUNT;

  WITH ordered AS (
    SELECT m.ts, m.thickness, row_number() OVER (ORDER BY m.ts DESC, m.id DESC) AS rn
    FROM public.measurements m
    JOIN public.measurement_points mp ON mp.id = m.point_id
    WHERE mp.asset_id = v_asset_id
  )
  SELECT
    MAX(ts) FILTER (WHERE rn = 2) AS prev_date,
    MAX(thickness) FILTER (WHERE rn = 2) AS prev_thk,
    MAX(ts) FILTER (WHERE rn = 1) AS last_date,
    MAX(thickness) FILTER (WHERE rn = 1) AS last_thk
  INTO v_prev_date, v_prev_thk, v_last_date, v_last_thk
  FROM ordered;

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
