CREATE OR REPLACE FUNCTION public.trg_measurements_ai_fn()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO public.local_events(event_type, payload_json)
  VALUES ('MEAS_INSERT', jsonb_build_object('point_id', NEW.point_id, 'ts', NEW.ts, 'thickness', NEW.thickness));
  RETURN NEW;
END$$;

DROP TRIGGER IF EXISTS trg_measurements_ai ON public.measurements;
CREATE TRIGGER trg_measurements_ai
AFTER INSERT ON public.measurements
FOR EACH ROW EXECUTE FUNCTION public.trg_measurements_ai_fn();

CREATE OR REPLACE FUNCTION public.trg_assets_local_ad_fn()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  v_asset_code text;
  v_source     text;
BEGIN
  v_asset_code := NULLIF(trim(OLD.asset_code), '');
  IF v_asset_code IS NULL THEN
    RETURN OLD;
  END IF;

  v_source := CASE
    WHEN upper(current_database()) LIKE '%KNPZ%' OR upper(current_database()) LIKE '%KRNPZ%' THEN 'KNPZ'
    ELSE 'ANPZ'
  END;

  INSERT INTO central_ft.asset_cleanup_requests(source_plant, asset_code)
  VALUES (v_source, v_asset_code);

  RETURN OLD;
END$$;

DROP TRIGGER IF EXISTS trg_assets_local_ad ON public.assets_local;
CREATE TRIGGER trg_assets_local_ad
AFTER DELETE ON public.assets_local
FOR EACH ROW EXECUTE FUNCTION public.trg_assets_local_ad_fn();
