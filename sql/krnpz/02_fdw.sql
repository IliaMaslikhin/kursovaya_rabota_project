CREATE EXTENSION IF NOT EXISTS postgres_fdw;

CREATE SERVER IF NOT EXISTS central_srv
  FOREIGN DATA WRAPPER postgres_fdw
  OPTIONS (host 'localhost', dbname 'central', port '5432');

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_user_mappings m
    JOIN pg_foreign_server s ON s.oid = m.srvid
    WHERE s.srvname = 'central_srv'
      AND m.umuser = (SELECT oid FROM pg_roles WHERE rolname = SESSION_USER)
  ) THEN
    EXECUTE format('CREATE USER MAPPING FOR %I SERVER central_srv OPTIONS (user %L)',
                   SESSION_USER, SESSION_USER);
  END IF;
END$$;

CREATE SCHEMA IF NOT EXISTS central_ft AUTHORIZATION CURRENT_USER;

DROP FOREIGN TABLE IF EXISTS central_ft.measurement_batches;
CREATE FOREIGN TABLE central_ft.measurement_batches (
  -- Важно: не включаем identity/default колонки (id/created_at).
  -- postgres_fdw не знает remote DEFAULT и может отправить NULL, ломая INSERT.
  source_plant TEXT,
  asset_code   TEXT,
  prev_thk     NUMERIC,
  prev_date    TIMESTAMPTZ,
  last_thk     NUMERIC,
  last_date    TIMESTAMPTZ,
  last_label   TEXT,
  last_note    TEXT
)
SERVER central_srv
OPTIONS (schema_name 'public', table_name 'measurement_batches');

-- If central needs explicit creds, use:
-- CREATE USER MAPPING IF NOT EXISTS FOR CURRENT_USER SERVER central_srv
--   OPTIONS (user 'erp_owner', password 'CHANGE_ME');
