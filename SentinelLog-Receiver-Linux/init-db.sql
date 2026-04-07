-- Syslog-NG PostgreSQL Database Initialization
--
-- =============================================
-- IMPORTANT: Before running this, configure PostgreSQL to allow remote connections:
--
-- 1. Edit postgresql.conf (usually at /etc/postgresql/<version>/main/postgresql.conf):
--      listen_addresses = '*'
--
-- 2. Edit pg_hba.conf (same folder) — add this line at the end:
--      host    Syslog    postgres    0.0.0.0/0    md5
--
-- 3. Restart PostgreSQL:
--      sudo systemctl restart postgresql
--
-- On Windows PostgreSQL: use pgAdmin > Server > Properties > Connection to verify,
-- and edit pg_hba.conf from the data directory.
-- =============================================
--
-- HOW TO RUN:
--
-- Using psql (run the whole file):
--   psql -U postgres -f init-db.sql
--
-- Using pgAdmin:
--   1. Right-click Databases > Create > Database > Name: Syslog > Save
--   2. Click on 'Syslog' database > Open Query Tool
--   3. Copy-paste everything BELOW the \c line and run it
-- =============================================

-- Step 1: Create the database (psql only — pgAdmin: create DB manually)
CREATE DATABASE "Syslog";

-- Step 2: Connect to Syslog (psql only — pgAdmin: select Syslog DB first)
\c "Syslog"

-- Step 3: Create the systemlogs table
CREATE TABLE IF NOT EXISTS public.systemlogs (
    id              BIGSERIAL       PRIMARY KEY,
    log_datetime    TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    host            VARCHAR(255)    NOT NULL DEFAULT 'unknown',
    program         VARCHAR(255)    NOT NULL DEFAULT 'unknown',
    pid             VARCHAR(50)     NOT NULL DEFAULT '0',
    message         TEXT            NOT NULL,
    source_ip       VARCHAR(50)     NOT NULL DEFAULT '0.0.0.0'
);

-- Step 4: Create indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_systemlogs_datetime ON public.systemlogs (log_datetime DESC);
CREATE INDEX IF NOT EXISTS idx_systemlogs_host     ON public.systemlogs (host);
CREATE INDEX IF NOT EXISTS idx_systemlogs_source   ON public.systemlogs (source_ip);
CREATE INDEX IF NOT EXISTS idx_systemlogs_program  ON public.systemlogs (program);
