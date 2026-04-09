-- Nocturne PostgreSQL role bootstrap
-- ==================================
--
-- Nocturne requires two separate non-privileged PostgreSQL roles:
--
--   nocturne_migrator  Runs EF Core migrations. Owns the schema and all
--                      tables so it can ALTER/CREATE/DROP. Has NOBYPASSRLS
--                      so that combined with FORCE ROW LEVEL SECURITY on
--                      each tenant table, even the migrator cannot read PHI
--                      across tenants.
--
--   nocturne_app       Runs at request time. Owns nothing, so it cannot
--                      disable or alter RLS policies. Has NOBYPASSRLS, so
--                      tenant_isolation policies are always enforced.
--
-- Run this file ONCE per database, as a PostgreSQL superuser, BEFORE
-- starting Nocturne for the first time. Aspire and the self-hosted
-- docker-compose bundle run an equivalent script automatically via the
-- Postgres container's /docker-entrypoint-initdb.d/ mechanism; this file is
-- for bring-your-own PostgreSQL deployments (managed PostgreSQL, existing
-- shared instances).
--
-- Usage:
--   1. Edit this file and replace the two REPLACE_ME passwords below with
--      strong, distinct values. Store them in your secrets manager.
--   2. Connect to the target database and run the file:
--        psql -U <superuser> -d <nocturne-database> -f bootstrap-roles.sql
--   3. Set two connection strings in Nocturne's configuration:
--        ConnectionStrings__NocturneDb          (uses nocturne_app)
--        ConnectionStrings__NocturneDbMigrator  (uses nocturne_migrator)
--   4. Start Nocturne. Migrations will run under nocturne_migrator, then
--      the app will connect as nocturne_app. Startup will fail loudly if
--      either role is misconfigured.
--
-- This script is idempotent: re-running it is safe and will reset role
-- attributes and passwords to the values you've configured here.

\set ON_ERROR_STOP on

DO $$
DECLARE
    migrator_password text := 'REPLACE_ME_MIGRATOR_PASSWORD';
    app_password text := 'REPLACE_ME_APP_PASSWORD';
    current_db text := current_database();
BEGIN
    IF migrator_password = 'REPLACE_ME_MIGRATOR_PASSWORD'
       OR app_password = 'REPLACE_ME_APP_PASSWORD' THEN
        RAISE EXCEPTION
            'You must edit bootstrap-roles.sql and replace the REPLACE_ME passwords before running it.';
    END IF;

    -- nocturne_migrator: owns the schema, runs migrations
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'nocturne_migrator') THEN
        EXECUTE format(
            'CREATE ROLE nocturne_migrator LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD %L',
            migrator_password);
    ELSE
        EXECUTE format(
            'ALTER ROLE nocturne_migrator LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD %L',
            migrator_password);
    END IF;

    -- nocturne_app: runtime-only, owns nothing
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'nocturne_app') THEN
        EXECUTE format(
            'CREATE ROLE nocturne_app LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD %L',
            app_password);
    ELSE
        EXECUTE format(
            'ALTER ROLE nocturne_app LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE PASSWORD %L',
            app_password);
    END IF;

    -- Hand ownership of the current database and the public schema to the
    -- migrator so it can run DDL. If the database already has tables owned
    -- by a different user, run REASSIGN OWNED BY <that user> TO nocturne_migrator
    -- manually as a superuser before running Nocturne.
    EXECUTE format('ALTER DATABASE %I OWNER TO nocturne_migrator', current_db);
    EXECUTE 'ALTER SCHEMA public OWNER TO nocturne_migrator';

    -- Runtime role needs to connect and use the schema, but nothing more.
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO nocturne_app', current_db);
    EXECUTE 'GRANT USAGE ON SCHEMA public TO nocturne_app';

    -- Default privileges: any object created by nocturne_migrator in schema
    -- public automatically grants CRUD to nocturne_app. This is scoped with
    -- FOR ROLE nocturne_migrator so future migrations inherit the grants
    -- without requiring a follow-up ALTER DEFAULT PRIVILEGES step.
    EXECUTE 'ALTER DEFAULT PRIVILEGES FOR ROLE nocturne_migrator IN SCHEMA public '
         || 'GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO nocturne_app';
    EXECUTE 'ALTER DEFAULT PRIVILEGES FOR ROLE nocturne_migrator IN SCHEMA public '
         || 'GRANT USAGE, SELECT ON SEQUENCES TO nocturne_app';

    -- Also grant on anything that already exists (e.g. if Nocturne has been
    -- run before against this database under a different configuration).
    -- No-op on fresh databases.
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO nocturne_app';
    EXECUTE 'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO nocturne_app';
END
$$;
