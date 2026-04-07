using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations;

/// <inheritdoc />
[Migration("20260325140000_AddSourceGroups")]
public partial class AddSourceGroups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS sourcegroup (
                id          BIGSERIAL PRIMARY KEY,
                name        VARCHAR(200)  NOT NULL,
                description VARCHAR(500)  NULL,
                color       VARCHAR(20)   NULL,
                createdat   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
                isdeleted   BOOLEAN       NOT NULL DEFAULT FALSE
            );

            CREATE INDEX IF NOT EXISTS ix_sourcegroup_name      ON sourcegroup (name);
            CREATE INDEX IF NOT EXISTS ix_sourcegroup_isdeleted ON sourcegroup (isdeleted);

            CREATE TABLE IF NOT EXISTS sourcegroupmember (
                id        BIGSERIAL PRIMARY KEY,
                groupid   BIGINT NOT NULL REFERENCES sourcegroup(id)  ON DELETE CASCADE,
                sourceid  BIGINT NOT NULL REFERENCES source(id)       ON DELETE CASCADE,
                CONSTRAINT uq_sourcegroupmember UNIQUE (groupid, sourceid)
            );

            CREATE INDEX IF NOT EXISTS ix_sourcegroupmember_groupid  ON sourcegroupmember (groupid);
            CREATE INDEX IF NOT EXISTS ix_sourcegroupmember_sourceid ON sourcegroupmember (sourceid);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS sourcegroupmember;
            DROP TABLE IF EXISTS sourcegroup;
            """);
    }
}
