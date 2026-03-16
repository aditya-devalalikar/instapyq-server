using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Logs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Logs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Logs");
        }
    }
}
