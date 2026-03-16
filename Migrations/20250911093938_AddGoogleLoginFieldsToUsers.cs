using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleLoginFieldsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleProfilePicture",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGoogleLoginOnly",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleProfilePicture",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsGoogleLoginOnly",
                table: "Users");
        }
    }
}
