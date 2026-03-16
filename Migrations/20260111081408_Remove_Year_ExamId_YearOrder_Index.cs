using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Year_ExamId_YearOrder_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Years_ExamId_YearOrder",
                table: "Years");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Years",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Topics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Subjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Exams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Years_ExamId",
                table: "Years",
                column: "ExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Years_ExamId",
                table: "Years");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Years");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Exams");

            migrationBuilder.CreateIndex(
                name: "IX_Years_ExamId_YearOrder",
                table: "Years",
                columns: new[] { "ExamId", "YearOrder" },
                unique: true);
        }
    }
}
