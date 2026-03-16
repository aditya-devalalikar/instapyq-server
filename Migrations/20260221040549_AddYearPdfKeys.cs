using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddYearPdfKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnswerKeyKey",
                table: "Years",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionPaperKey",
                table: "Years",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerKeyKey",
                table: "Years");

            migrationBuilder.DropColumn(
                name: "QuestionPaperKey",
                table: "Years");
        }
    }
}
