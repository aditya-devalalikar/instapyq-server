using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_Status_ExpiresAt",
                table: "Orders",
                columns: new[] { "UserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnumLabels_EnumType_EnumName",
                table: "EnumLabels",
                columns: new[] { "EnumType", "EnumName" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOtps_Email_CreatedAt",
                table: "EmailOtps",
                columns: new[] { "Email", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_Status_ExpiresAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_EnumLabels_EnumType_EnumName",
                table: "EnumLabels");

            migrationBuilder.DropIndex(
                name: "IX_EmailOtps_Email_CreatedAt",
                table: "EmailOtps");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");
        }
    }
}
