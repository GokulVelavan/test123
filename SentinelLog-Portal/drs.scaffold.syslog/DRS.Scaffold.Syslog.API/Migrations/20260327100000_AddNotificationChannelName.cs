using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations;

[Migration("20260327100000")]
public partial class AddNotificationChannelName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE notificationchannel ADD COLUMN IF NOT EXISTS name VARCHAR(100) NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE notificationchannel DROP COLUMN IF EXISTS name;");
    }
}
