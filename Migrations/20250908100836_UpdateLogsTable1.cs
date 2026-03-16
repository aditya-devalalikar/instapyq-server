using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLogsTable1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Logs_Users_UserId",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Logs_UserId",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Application",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "EnvironmentName",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "MachineName",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "StackTrace",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "Logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Application",
                table: "Logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "Logs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentName",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineName",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StackTrace",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "Logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Logs_UserId",
                table: "Logs",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Logs_Users_UserId",
                table: "Logs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }
    }
}
