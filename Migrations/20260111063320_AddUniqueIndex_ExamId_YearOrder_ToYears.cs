using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndex_ExamId_YearOrder_ToYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Years_ExamId",
                table: "Years");

            migrationBuilder.CreateIndex(
                name: "IX_Years_ExamId_YearOrder",
                table: "Years",
                columns: new[] { "ExamId", "YearOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Years_ExamId_YearOrder",
                table: "Years");

            migrationBuilder.CreateIndex(
                name: "IX_Years_ExamId",
                table: "Years",
                column: "ExamId");
        }
    }
}
