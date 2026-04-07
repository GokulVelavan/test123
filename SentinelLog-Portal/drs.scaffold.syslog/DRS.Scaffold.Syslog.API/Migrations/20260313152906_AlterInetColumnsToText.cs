using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations
{
    /// <inheritdoc />
    public partial class AlterInetColumnsToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL requires USING to cast inet → text explicitly.
            // ALTER COLUMN with a type change is not allowed without it.
            migrationBuilder.Sql(
                "ALTER TABLE usersession ALTER COLUMN ipaddress TYPE text USING ipaddress::text;");

            migrationBuilder.Sql(
                "ALTER TABLE source ALTER COLUMN ipaddress TYPE text USING ipaddress::text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert text → inet (only safe if all values are valid IP addresses)
            migrationBuilder.Sql(
                "ALTER TABLE usersession ALTER COLUMN ipaddress TYPE inet USING ipaddress::inet;");

            migrationBuilder.Sql(
                "ALTER TABLE source ALTER COLUMN ipaddress TYPE inet USING ipaddress::inet;");
        }
    }
}
