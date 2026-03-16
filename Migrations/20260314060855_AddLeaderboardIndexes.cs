using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserDailyProgress_Date",
                table: "UserDailyProgress",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyProgress_UserId_Date",
                table: "UserDailyProgress",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamProgress_ModeType_CompletedAt",
                table: "ExamProgress",
                columns: new[] { "ModeType", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamProgress_UserId_CompletedAt",
                table: "ExamProgress",
                columns: new[] { "UserId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDailyProgress_Date",
                table: "UserDailyProgress");

            migrationBuilder.DropIndex(
                name: "IX_UserDailyProgress_UserId_Date",
                table: "UserDailyProgress");

            migrationBuilder.DropIndex(
                name: "IX_ExamProgress_ModeType_CompletedAt",
                table: "ExamProgress");

            migrationBuilder.DropIndex(
                name: "IX_ExamProgress_UserId_CompletedAt",
                table: "ExamProgress");
        }
    }
}
