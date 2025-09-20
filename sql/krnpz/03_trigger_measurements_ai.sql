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
