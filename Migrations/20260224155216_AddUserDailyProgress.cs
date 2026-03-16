using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDailyProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Progress");

            migrationBuilder.CreateTable(
                name: "UserDailyProgress",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    ExamId = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Correct = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyProgress", x => new { x.UserId, x.Date, x.SubjectId, x.ExamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyProgress_UserId",
                table: "UserDailyProgress",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDailyProgress");

            migrationBuilder.CreateTable(
                name: "Progress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<int>(type: "integer", nullable: true),
                    ExamName = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    SubjectName = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<int>(type: "integer", nullable: true),
                    YearName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Progress", x => x.Id);
                });
        }
    }
}
