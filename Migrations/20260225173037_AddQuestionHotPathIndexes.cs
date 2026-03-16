using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionHotPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Questions_ExamId_SubjectId_TopicId_YearId_QuestionId",
                table: "Questions",
                columns: new[] { "ExamId", "SubjectId", "TopicId", "YearId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_YearId_QuestionId",
                table: "Questions",
                columns: new[] { "YearId", "QuestionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_ExamId_SubjectId_TopicId_YearId_QuestionId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_YearId_QuestionId",
                table: "Questions");
        }
    }
}
