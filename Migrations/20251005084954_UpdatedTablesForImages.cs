using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTablesForImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerImages",
                table: "Questions");

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

            migrationBuilder.DropColumn(
                name: "QuestionImage",
                table: "Questions");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "QuestionAnswerImages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ImageType",
                table: "QuestionAnswerImages",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "QuestionAnswerImages");

            migrationBuilder.DropColumn(
                name: "ImageType",
                table: "QuestionAnswerImages");

            migrationBuilder.AddColumn<string>(
                name: "AnswerImages",
                table: "Questions",
                type: "text",
                nullable: true);

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

            migrationBuilder.AddColumn<string>(
                name: "QuestionImage",
                table: "Questions",
                type: "text",
                nullable: true);
        }
    }
}
