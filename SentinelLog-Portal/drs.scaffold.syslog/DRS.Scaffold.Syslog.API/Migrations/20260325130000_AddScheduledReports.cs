using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DRS.Scaffold.Syslog.API.Migrations
{
    /// <inheritdoc />
    [Migration("20260325130000")]
    public partial class AddScheduledReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS scheduledreport (
                    id            bigserial PRIMARY KEY,
                    name          varchar(200) NOT NULL,
                    description   text,
                    schedule      varchar(100),
                    filtersjson   text,
                    format        varchar(20) NOT NULL DEFAULT 'csv',
                    enabled       boolean NOT NULL DEFAULT true,
                    lastrunathz   timestamptz,
                    createdat     timestamptz NOT NULL DEFAULT now(),
                    isdeleted     boolean NOT NULL DEFAULT false
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS scheduledreport;");
        }
    }
}
