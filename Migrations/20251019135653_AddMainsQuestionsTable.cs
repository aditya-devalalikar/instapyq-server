using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddMainsQuestionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mains_questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<int>(type: "integer", nullable: false),
                    PaperType = table.Column<int>(type: "integer", nullable: false),
                    PaperNumber = table.Column<int>(type: "integer", nullable: false),
                    OptionalSubject = table.Column<int>(type: "integer", nullable: true),
                    Section = table.Column<string>(type: "text", nullable: false),
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    Marks = table.Column<int>(type: "integer", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: true),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mains_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mains_questions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "TopicId");
                    table.ForeignKey(
                        name: "FK_mains_questions_Years_YearId",
                        column: x => x.YearId,
                        principalTable: "Years",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mains_questions_TopicId",
                table: "mains_questions",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_mains_questions_YearId",
                table: "mains_questions",
                column: "YearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mains_questions");
        }
    }
}
