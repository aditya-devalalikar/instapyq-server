using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class TableNameChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mains_questions_Topics_TopicId",
                table: "mains_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_mains_questions_Years_YearId",
                table: "mains_questions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_mains_questions",
                table: "mains_questions");

            migrationBuilder.RenameTable(
                name: "mains_questions",
                newName: "Mains");

            migrationBuilder.RenameIndex(
                name: "IX_mains_questions_YearId",
                table: "Mains",
                newName: "IX_Mains_YearId");

            migrationBuilder.RenameIndex(
                name: "IX_mains_questions_TopicId",
                table: "Mains",
                newName: "IX_Mains_TopicId");

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Mains",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Mains",
                table: "Mains",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Mains_Topics_TopicId",
                table: "Mains",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mains_Years_YearId",
                table: "Mains",
                column: "YearId",
                principalTable: "Years",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mains_Topics_TopicId",
                table: "Mains");

            migrationBuilder.DropForeignKey(
                name: "FK_Mains_Years_YearId",
                table: "Mains");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Mains",
                table: "Mains");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Mains");

            migrationBuilder.RenameTable(
                name: "Mains",
                newName: "mains_questions");

            migrationBuilder.RenameIndex(
                name: "IX_Mains_YearId",
                table: "mains_questions",
                newName: "IX_mains_questions_YearId");

            migrationBuilder.RenameIndex(
                name: "IX_Mains_TopicId",
                table: "mains_questions",
                newName: "IX_mains_questions_TopicId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_mains_questions",
                table: "mains_questions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_mains_questions_Topics_TopicId",
                table: "mains_questions",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_mains_questions_Years_YearId",
                table: "mains_questions",
                column: "YearId",
                principalTable: "Years",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
