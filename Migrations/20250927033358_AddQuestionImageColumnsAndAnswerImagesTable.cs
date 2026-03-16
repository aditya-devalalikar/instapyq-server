using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImageColumnsAndAnswerImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OptionAImage",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionBImage",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionCImage",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionDImage",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionAnswerImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    ImageId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionAnswerImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionAnswerImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "ImageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestionAnswerImages_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswerImages_ImageId",
                table: "QuestionAnswerImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionAnswerImages_QuestionId",
                table: "QuestionAnswerImages",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionAnswerImages");

            migrationBuilder.DropColumn(
                name: "OptionAImage",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OptionBImage",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OptionCImage",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "OptionDImage",
                table: "Questions");
        }
    }
}
