using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSessionsToAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sessions",
                table: "DailyStudySummary");

            migrationBuilder.AddColumn<int>(
                name: "CdSeconds",
                table: "DailyStudySummary",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwSeconds",
                table: "DailyStudySummary",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CdSeconds",
                table: "DailyStudySummary");

            migrationBuilder.DropColumn(
                name: "SwSeconds",
                table: "DailyStudySummary");

            migrationBuilder.AddColumn<string>(
                name: "Sessions",
                table: "DailyStudySummary",
                type: "text",
                nullable: true);
        }
    }
}
