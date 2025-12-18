-- Генерация массовых событий замеров для нагрузочных тестов.
-- Запускать вручную (НЕ входит в обязательный деплой).
-- Пример:
--   \i sql/central/99_mass_test_events.sql
--   -- или psql -f sql/central/99_mass_test_events.sql -v asset_prefix='LOAD' -v n_assets=100 -v points_per_asset=50

\set asset_prefix 'LOAD'
\set n_assets 10
\set points_per_asset 5

DO $$
DECLARE
  asset_prefix text := current_setting('asset_prefix', true);
  n_assets int := COALESCE(current_setting('n_assets', true)::int, 10);
  points_per_asset int := COALESCE(current_setting('points_per_asset', true)::int, 5);
  i int;
  j int;
  asset_code text;
  base_ts timestamptz := now() - interval '30 days';
BEGIN
  IF asset_prefix IS NULL OR asset_prefix = '' THEN
    asset_prefix := 'LOAD';
  END IF;

  FOR i IN 1..GREATEST(1, n_assets) LOOP
    asset_code := asset_prefix || '-' || lpad(i::text, 4, '0');
    FOR j IN 1..GREATEST(1, points_per_asset) LOOP
      INSERT INTO public.measurement_batches(source_plant, asset_code, prev_thk, prev_date, last_thk, last_date)
      VALUES (
        'ANPZ',
        asset_code,
        10.0 + i * 0.01,
        base_ts + (j-1) * interval '1 day',
        10.0 + i * 0.01 - j * 0.02,
        base_ts + j * interval '1 day'
      );
    END LOOP;
  END LOOP;
END $$;
