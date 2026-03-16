using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class ExamProgressAddedAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamProgress",
                columns: table => new
                {
                    ExamProgressId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(nullable: false),
                    ModeType = table.Column<string>(nullable: true),
                    YearId = table.Column<int>(nullable: true),
                    SubjectIds = table.Column<string>(nullable: true),
                    TopicIds = table.Column<string>(nullable: true),
                    QuestionCount = table.Column<int>(nullable: false),
                    AttemptedCount = table.Column<int>(nullable: false),
                    CorrectCount = table.Column<int>(nullable: false),
                    WrongCount = table.Column<int>(nullable: false),
                    SkippedCount = table.Column<int>(nullable: false),
                    Elim1 = table.Column<int>(nullable: false),
                    Elim1Correct = table.Column<int>(nullable: false),
                    Elim1Wrong = table.Column<int>(nullable: false),
                    Elim2 = table.Column<int>(nullable: false),
                    Elim2Correct = table.Column<int>(nullable: false),
                    Elim2Wrong = table.Column<int>(nullable: false),
                    Elim3 = table.Column<int>(nullable: false),
                    Elim3Correct = table.Column<int>(nullable: false),
                    Elim3Wrong = table.Column<int>(nullable: false),
                    StartedAt = table.Column<DateTime>(nullable: false),
                    CompletedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamProgress", x => x.ExamProgressId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamProgress");
        }
    }
}
