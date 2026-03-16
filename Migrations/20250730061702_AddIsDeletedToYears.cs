using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Years",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Years");
        }
    }
}
