CREATE OR REPLACE PROCEDURE public.sp_insert_measurement_batch_prc(
  p_asset_code   TEXT,
  p_points       JSONB,
  p_source_plant TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.sp_insert_measurement_batch(p_asset_code, p_points, p_source_plant);
END$$;
