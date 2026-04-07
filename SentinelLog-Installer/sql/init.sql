-- Syslog-NG PostgreSQL Schema Initialization
-- This script is executed by the installer to create the database and table.

-- Create the systemlogs table if it does not exist
CREATE TABLE IF NOT EXISTS public.systemlogs (
    id              BIGSERIAL       PRIMARY KEY,
    log_datetime    TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    host            VARCHAR(255)    NOT NULL DEFAULT 'unknown',
    program         VARCHAR(255)    NOT NULL DEFAULT 'unknown',
    pid             VARCHAR(50)     NOT NULL DEFAULT '0',
    message         TEXT            NOT NULL,
    source_ip       VARCHAR(50)     NOT NULL DEFAULT '0.0.0.0'
);

-- Create indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_systemlogs_datetime ON public.systemlogs (log_datetime DESC);
CREATE INDEX IF NOT EXISTS idx_systemlogs_host     ON public.systemlogs (host);
CREATE INDEX IF NOT EXISTS idx_systemlogs_source   ON public.systemlogs (source_ip);
CREATE INDEX IF NOT EXISTS idx_systemlogs_program  ON public.systemlogs (program);

-- Grant permissions to syslog user (will use the configured username)
-- This is handled by the installer PowerShell script dynamically.
