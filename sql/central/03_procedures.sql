-- Основная логика перенесена в процедуры; функции-обёртки делегируют вызовы сюда.

-- При изменении OUT-параметров PostgreSQL не позволяет CREATE OR REPLACE.
-- Чтобы автосинхронизация могла обновлять БД со старых версий, удаляем старые варианты процедур.
-- Убираем старую модель очереди событий (events_inbox + ingest/notify).
DROP FUNCTION IF EXISTS public.fn_events_enqueue(text, text, jsonb);
DROP FUNCTION IF EXISTS public.fn_events_peek(int);
DROP FUNCTION IF EXISTS public.fn_ingest_events(int);
DROP FUNCTION IF EXISTS public.fn_events_requeue(bigint[]);
DROP FUNCTION IF EXISTS public.fn_events_cleanup(interval);

DROP PROCEDURE IF EXISTS public.sp_ingest_events(integer);
DROP PROCEDURE IF EXISTS public.sp_events_enqueue(text, text, jsonb);
DROP PROCEDURE IF EXISTS public.sp_events_requeue(bigint[]);
DROP PROCEDURE IF EXISTS public.sp_events_cleanup(interval);

DROP TABLE IF EXISTS public.events_inbox;

DROP PROCEDURE IF EXISTS public.sp_policy_upsert(text, numeric, numeric, numeric);
DROP PROCEDURE IF EXISTS public.sp_asset_upsert(text, text, text, text);

-- Прямые вставки батчей замеров из заводов (public.measurement_batches)
-- автоматически обновляют справочник и аналитику.
DROP FUNCTION IF EXISTS public.trg_measurement_batches_bi_fn();

CREATE OR REPLACE FUNCTION public.trg_measurement_batches_bi_fn()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
  v_asset_code text;
  v_source     text;
BEGIN
  v_asset_code := NULLIF(trim(NEW.asset_code), '');
  IF v_asset_code IS NULL THEN
    RAISE EXCEPTION 'asset_code is required';
  END IF;
  NEW.asset_code := v_asset_code;

  v_source := NULLIF(trim(NEW.source_plant), '');
  IF v_source IS NULL THEN
    RAISE EXCEPTION 'source_plant is required';
  END IF;
  NEW.source_plant := v_source;

  -- ensure asset exists for FK (measurement_batches -> assets_global / analytics_cr -> assets_global)
  INSERT INTO public.assets_global(asset_code, name, type, plant_code)
  VALUES (NEW.asset_code, NEW.asset_code, NULL, NEW.source_plant)
  ON CONFLICT (asset_code) DO UPDATE
    SET plant_code = COALESCE(EXCLUDED.plant_code, public.assets_global.plant_code);

  -- upsert analytics snapshot
  INSERT INTO public.analytics_cr(asset_code, prev_thk, prev_date, last_thk, last_date, cr, updated_at)
  VALUES (
    NEW.asset_code,
    NEW.prev_thk,
    NEW.prev_date,
    NEW.last_thk,
    NEW.last_date,
    public.fn_calc_cr(NEW.prev_thk, NEW.prev_date, NEW.last_thk, NEW.last_date),
    now()
  )
  ON CONFLICT (asset_code) DO UPDATE
    SET prev_thk   = EXCLUDED.prev_thk,
        prev_date  = EXCLUDED.prev_date,
        last_thk   = EXCLUDED.last_thk,
        last_date  = EXCLUDED.last_date,
        cr         = EXCLUDED.cr,
        updated_at = now();

  RETURN NEW;
END$$;

DROP TRIGGER IF EXISTS trg_measurement_batches_bi ON public.measurement_batches;
CREATE TRIGGER trg_measurement_batches_bi
BEFORE INSERT ON public.measurement_batches
FOR EACH ROW EXECUTE FUNCTION public.trg_measurement_batches_bi_fn();

CREATE OR REPLACE PROCEDURE public.sp_policy_upsert(
  IN p_name text,
  IN p_low numeric,
  IN p_med numeric,
  IN p_high numeric,
  OUT p_id bigint)
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO public.risk_policies(name, threshold_low, threshold_med, threshold_high)
  VALUES (p_name, p_low, p_med, p_high)
  ON CONFLICT (name) DO UPDATE
    SET threshold_low  = EXCLUDED.threshold_low,
        threshold_med  = EXCLUDED.threshold_med,
        threshold_high = EXCLUDED.threshold_high
  RETURNING id INTO p_id;

  IF p_id IS NULL THEN
    SELECT id INTO p_id FROM public.risk_policies WHERE name = p_name;
  END IF;
END$$;

CREATE OR REPLACE PROCEDURE public.sp_asset_upsert(
  IN p_asset_code text,
  IN p_name text,
  IN p_type text,
  IN p_plant_code text,
  OUT p_id bigint)
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO public.assets_global(asset_code, name, type, plant_code)
  VALUES (p_asset_code, COALESCE(p_name, p_asset_code), p_type, p_plant_code)
  ON CONFLICT (asset_code) DO UPDATE
    SET name       = COALESCE(EXCLUDED.name,       public.assets_global.name),
        type       = COALESCE(EXCLUDED.type,       public.assets_global.type),
        plant_code = COALESCE(EXCLUDED.plant_code, public.assets_global.plant_code)
  RETURNING id INTO p_id;

  IF p_id IS NULL THEN
    SELECT id INTO p_id FROM public.assets_global WHERE asset_code = p_asset_code;
  END IF;
END$$;
