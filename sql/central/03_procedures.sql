-- wrappers to show in Procedures
CREATE OR REPLACE PROCEDURE public.sp_ingest_events(p_limit int DEFAULT 1000)
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_ingest_events(p_limit);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_enqueue(p_event_type text, p_source_plant text, p_payload jsonb)
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_events_enqueue(p_event_type, p_source_plant, p_payload);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_requeue(p_ids bigint[])
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_events_requeue(p_ids);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_events_cleanup(p_older_than interval DEFAULT '30 days')
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_events_cleanup(p_older_than);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_policy_upsert(p_name text, p_low numeric, p_med numeric, p_high numeric)
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_policy_upsert(p_name, p_low, p_med, p_high);
END$$;

CREATE OR REPLACE PROCEDURE public.sp_asset_upsert(p_asset_code text, p_name text DEFAULT NULL, p_type text DEFAULT NULL, p_plant_code text DEFAULT NULL)
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_asset_upsert(p_asset_code, p_name, p_type, p_plant_code);
END$$;
