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

DROP FOREIGN TABLE IF EXISTS central_ft.events_inbox;
CREATE FOREIGN TABLE central_ft.events_inbox (
  id           BIGINT,
  event_type   TEXT,
  source_plant TEXT,
  payload_json JSONB,
  created_at   TIMESTAMPTZ,
  processed_at TIMESTAMPTZ
)
SERVER central_srv
OPTIONS (schema_name 'public', table_name 'events_inbox');

-- If central needs explicit creds, use:
-- CREATE USER MAPPING IF NOT EXISTS FOR CURRENT_USER SERVER central_srv
--   OPTIONS (user 'erp_owner', password 'CHANGE_ME');
