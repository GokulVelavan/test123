using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations
{
    /// <inheritdoc />
    [Migration("20260325120000_AddMfaApiKeyLicensePlatformConfig")]
    public partial class AddMfaApiKeyLicensePlatformConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #93 — MFA flag on appuser
            migrationBuilder.Sql(
                "ALTER TABLE appuser ADD COLUMN IF NOT EXISTS mfaenabled boolean NOT NULL DEFAULT false;");

            // #97-98 — API Keys table
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS apikey (
                    id          bigserial    PRIMARY KEY,
                    name        varchar(100),
                    description text,
                    keyhash     text,
                    keyprefix   varchar(16),
                    enabled     boolean      NOT NULL DEFAULT true,
                    expiresat   timestamptz,
                    lastusedat  timestamptz,
                    createdat   timestamptz  NOT NULL DEFAULT now(),
                    createdby   varchar(100),
                    isdeleted   boolean      NOT NULL DEFAULT false
                );
                """);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_apikey_enabled ON apikey(enabled);");

            // #101-102 — License table
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS license (
                    id          bigserial    PRIMARY KEY,
                    licensekey  text,
                    licensee    varchar(200),
                    plan        varchar(50),
                    maxsources  integer,
                    maxeps      bigint,
                    expiresat   timestamptz,
                    isactive    boolean      NOT NULL DEFAULT true,
                    activatedat timestamptz,
                    createdat   timestamptz  NOT NULL DEFAULT now(),
                    isdeleted   boolean      NOT NULL DEFAULT false
                );
                """);

            // #86-90/#95-96 — Platform config key/value store
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platformconfig (
                    id         bigserial    PRIMARY KEY,
                    category   varchar(50),
                    key        varchar(100),
                    value      text,
                    createdat  timestamptz  NOT NULL DEFAULT now(),
                    updatedat  timestamptz
                );
                """);

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ix_platformconfig_cat_key ON platformconfig(category, key);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE appuser DROP COLUMN IF EXISTS mfaenabled;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS apikey;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS license;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS platformconfig;");
        }
    }
}
