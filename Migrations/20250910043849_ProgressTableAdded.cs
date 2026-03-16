using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class ProgressTableAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "Progress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: true),
                    SubjectId = table.Column<string>(type: "text", nullable: true),
                    SubjectName = table.Column<string>(type: "text", nullable: true),
                    ExamId = table.Column<string>(type: "text", nullable: true),
                    ExamName = table.Column<string>(type: "text", nullable: true),
                    YearId = table.Column<string>(type: "text", nullable: true),
                    YearName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Progress", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Progress");

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<string>(type: "text", nullable: true),
                    ExamName = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: true),
                    SubjectId = table.Column<string>(type: "text", nullable: true),
                    SubjectName = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<string>(type: "text", nullable: true),
                    YearName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                });
        }
    }
}
