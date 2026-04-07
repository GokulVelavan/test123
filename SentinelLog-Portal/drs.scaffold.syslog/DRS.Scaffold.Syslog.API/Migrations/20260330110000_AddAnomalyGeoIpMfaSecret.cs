using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations;

/// <inheritdoc />
[Migration("20260330110000_AddAnomalyGeoIpMfaSecret")]
public partial class AddAnomalyGeoIpMfaSecret : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- ── Anomaly Detection Rules ────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS anomalyrule (
                id                    BIGSERIAL    PRIMARY KEY,
                name                  VARCHAR(200) NOT NULL,
                description           TEXT         NULL,
                metrictype            VARCHAR(50)  NOT NULL DEFAULT 'EpsTotal',
                baselinewindowminutes INTEGER      NOT NULL DEFAULT 60,
                thresholdmultiplier   DOUBLE PRECISION NOT NULL DEFAULT 3.0,
                absoluteminimum       DOUBLE PRECISION NOT NULL DEFAULT 5.0,
                severity              VARCHAR(20)  NOT NULL DEFAULT 'High',
                autocreateincident    BOOLEAN      NOT NULL DEFAULT FALSE,
                enabled               BOOLEAN      NOT NULL DEFAULT TRUE,
                lastfiredat           TIMESTAMPTZ  NULL,
                firecount             BIGINT       NOT NULL DEFAULT 0,
                createdat             TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                updatedat             TIMESTAMPTZ  NULL,
                isdeleted             BOOLEAN      NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_anomalyrule_enabled    ON anomalyrule (enabled);
            CREATE INDEX IF NOT EXISTS ix_anomalyrule_metrictype ON anomalyrule (metrictype);
            CREATE INDEX IF NOT EXISTS ix_anomalyrule_isdeleted  ON anomalyrule (isdeleted);

            -- ── Anomaly Events ─────────────────────────────────────────────────
            CREATE TABLE IF NOT EXISTS anomalyevent (
                id              BIGSERIAL    PRIMARY KEY,
                ruleid          BIGINT       NOT NULL REFERENCES anomalyrule(id) ON DELETE CASCADE,
                affectedentity  VARCHAR(255) NULL,
                baselinevalue   DOUBLE PRECISION NOT NULL DEFAULT 0,
                observedvalue   DOUBLE PRECISION NOT NULL DEFAULT 0,
                deviationratio  DOUBLE PRECISION NOT NULL DEFAULT 0,
                details         TEXT         NULL,
                acknowledged    BOOLEAN      NOT NULL DEFAULT FALSE,
                acknowledgedby  VARCHAR(100) NULL,
                acknowledgedat  TIMESTAMPTZ  NULL,
                detectedat      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                isdeleted       BOOLEAN      NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS ix_anomalyevent_detectedat    ON anomalyevent (detectedat);
            CREATE INDEX IF NOT EXISTS ix_anomalyevent_acknowledged   ON anomalyevent (acknowledged);
            CREATE INDEX IF NOT EXISTS ix_anomalyevent_isdeleted      ON anomalyevent (isdeleted);

            -- ── GeoIP fields on source table ───────────────────────────────────
            ALTER TABLE source
                ADD COLUMN IF NOT EXISTS geocountrycode VARCHAR(2)   NULL,
                ADD COLUMN IF NOT EXISTS geocountry     VARCHAR(100) NULL,
                ADD COLUMN IF NOT EXISTS geocity        VARCHAR(100) NULL,
                ADD COLUMN IF NOT EXISTS geolat         DOUBLE PRECISION NULL,
                ADD COLUMN IF NOT EXISTS geolon         DOUBLE PRECISION NULL,
                ADD COLUMN IF NOT EXISTS geoasn         VARCHAR(60)  NULL;

            -- ── Real TOTP secret on appuser ────────────────────────────────────
            ALTER TABLE appuser
                ADD COLUMN IF NOT EXISTS mfasecret VARCHAR(128) NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS anomalyevent;
            DROP TABLE IF EXISTS anomalyrule;
            ALTER TABLE source
                DROP COLUMN IF EXISTS geocountrycode,
                DROP COLUMN IF EXISTS geocountry,
                DROP COLUMN IF EXISTS geocity,
                DROP COLUMN IF EXISTS geolat,
                DROP COLUMN IF EXISTS geolon,
                DROP COLUMN IF EXISTS geoasn;
            ALTER TABLE appuser DROP COLUMN IF EXISTS mfasecret;
            """);
    }
}
