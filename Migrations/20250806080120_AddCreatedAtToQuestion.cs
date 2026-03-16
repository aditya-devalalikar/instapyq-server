using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtToQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedOn",
                table: "Questions",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Questions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Questions");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Questions",
                newName: "UpdatedOn");
        }
    }
}
