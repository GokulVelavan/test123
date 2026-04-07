using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations;

[Migration("20260326100000")]
public partial class AddAlertEventNotifiedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE alertevent ADD COLUMN IF NOT EXISTS notifiedat TIMESTAMPTZ NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE alertevent DROP COLUMN IF EXISTS notifiedat;");
    }
}
