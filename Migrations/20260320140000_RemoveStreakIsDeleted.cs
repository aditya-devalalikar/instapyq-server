using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStreakIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Streaks_UserId_IsDeleted",
                table: "Streaks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Streaks");

            migrationBuilder.CreateIndex(
                name: "IX_Streaks_UserId",
                table: "Streaks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Streaks_UserId",
                table: "Streaks");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Streaks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Streaks_UserId_IsDeleted",
                table: "Streaks",
                columns: new[] { "UserId", "IsDeleted" });
        }
    }
}
