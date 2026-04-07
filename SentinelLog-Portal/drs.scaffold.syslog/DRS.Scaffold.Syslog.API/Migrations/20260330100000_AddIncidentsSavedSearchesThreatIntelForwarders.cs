using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations;

/// <inheritdoc />
[Migration("20260330100000_AddIncidentsSavedSearchesThreatIntelForwarders")]
public partial class AddIncidentsSavedSearchesThreatIntelForwarders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- ── Incidents ──────────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS incident (
                id              BIGSERIAL       PRIMARY KEY,
                title           VARCHAR(200)    NOT NULL,
                description     TEXT            NULL,
                status          VARCHAR(30)     NOT NULL DEFAULT 'Open',
                priority        VARCHAR(20)     NOT NULL DEFAULT 'Medium',
                assigneeid      BIGINT          NULL,
                alerteventid    BIGINT          NULL,
                tags            TEXT            NULL,
                notes           TEXT            NULL,
                affectedhosts   TEXT            NULL,
                resolvedat      TIMESTAMPTZ     NULL,
                resolvedbyid    BIGINT          NULL,
                createdat       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
                updatedat       TIMESTAMPTZ     NULL,
                createdbyid     BIGINT          NULL,
                isdeleted       BOOLEAN         NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_incident_status    ON incident (status);
            CREATE INDEX IF NOT EXISTS ix_incident_priority  ON incident (priority);
            CREATE INDEX IF NOT EXISTS ix_incident_createdat ON incident (createdat);
            CREATE INDEX IF NOT EXISTS ix_incident_isdeleted ON incident (isdeleted);

            -- ── Saved Searches ──────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS savedsearch (
                id          BIGSERIAL   PRIMARY KEY,
                name        VARCHAR(150) NOT NULL,
                description TEXT         NULL,
                queryjson   JSONB        NOT NULL DEFAULT '{}',
                userid      BIGINT       NULL,
                isshared    BOOLEAN      NOT NULL DEFAULT FALSE,
                createdat   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updatedat   TIMESTAMPTZ  NULL,
                isdeleted   BOOLEAN      NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_savedsearch_userid    ON savedsearch (userid);
            CREATE INDEX IF NOT EXISTS ix_savedsearch_isshared  ON savedsearch (isshared);
            CREATE INDEX IF NOT EXISTS ix_savedsearch_isdeleted ON savedsearch (isdeleted);

            -- ── Threat Indicators ──────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS threatindicator (
                id             BIGSERIAL    PRIMARY KEY,
                type           VARCHAR(20)  NOT NULL DEFAULT 'IP',
                value          VARCHAR(512) NOT NULL,
                threatcategory VARCHAR(50)  NULL,
                confidence     INTEGER      NOT NULL DEFAULT 80,
                source         VARCHAR(100) NULL,
                description    TEXT         NULL,
                origin         VARCHAR(20)  NOT NULL DEFAULT 'Manual',
                isactive       BOOLEAN      NOT NULL DEFAULT TRUE,
                expiresat      TIMESTAMPTZ  NULL,
                createdat      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updatedat      TIMESTAMPTZ  NULL,
                isdeleted      BOOLEAN      NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_threatindicator_type      ON threatindicator (type);
            CREATE INDEX IF NOT EXISTS ix_threatindicator_value     ON threatindicator (value);
            CREATE INDEX IF NOT EXISTS ix_threatindicator_isactive  ON threatindicator (isactive);
            CREATE INDEX IF NOT EXISTS ix_threatindicator_isdeleted ON threatindicator (isdeleted);

            -- ── Log Forwarders ─────────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS logforwarder (
                id               BIGSERIAL    PRIMARY KEY,
                name             VARCHAR(150) NOT NULL,
                protocol         VARCHAR(10)  NOT NULL DEFAULT 'UDP',
                host             VARCHAR(255) NOT NULL,
                port             INTEGER      NOT NULL DEFAULT 514,
                format           VARCHAR(20)  NOT NULL DEFAULT 'RFC5424',
                severityfilter   VARCHAR(100) NULL,
                sourcefilter     TEXT         NULL,
                enabled          BOOLEAN      NOT NULL DEFAULT TRUE,
                tlscertpath      TEXT         NULL,
                description      TEXT         NULL,
                forwardedcount   BIGINT       NOT NULL DEFAULT 0,
                errorcount       BIGINT       NOT NULL DEFAULT 0,
                lastforwardedat  TIMESTAMPTZ  NULL,
                createdat        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updatedat        TIMESTAMPTZ  NULL,
                isdeleted        BOOLEAN      NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_logforwarder_enabled   ON logforwarder (enabled);
            CREATE INDEX IF NOT EXISTS ix_logforwarder_isdeleted ON logforwarder (isdeleted);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS logforwarder;
            DROP TABLE IF EXISTS threatindicator;
            DROP TABLE IF EXISTS savedsearch;
            DROP TABLE IF EXISTS incident;
            """);
    }
}
