using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsPremiumFromUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "HideFromLeaderboard",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideFromLeaderboard",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
