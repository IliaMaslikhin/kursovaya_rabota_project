-- Legacy function version named sp_ingest_events() RETURNS void (kept for compatibility)
-- Теперь делегирует основной реализации fn_ingest_events с лимитом.
CREATE OR REPLACE FUNCTION public.sp_ingest_events()
RETURNS void
LANGUAGE plpgsql
AS $$
BEGIN
  PERFORM public.fn_ingest_events(1000);
END;
$$;
