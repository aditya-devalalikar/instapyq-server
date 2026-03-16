using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pqy_server.Migrations
{
    public partial class MakeProgressIdsInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Explicitly cast string columns to integer
            migrationBuilder.Sql(
                @"ALTER TABLE ""Progress"" 
                  ALTER COLUMN ""YearId"" TYPE integer USING ""YearId""::integer;"
            );

            migrationBuilder.Sql(
                @"ALTER TABLE ""Progress"" 
                  ALTER COLUMN ""SubjectId"" TYPE integer USING ""SubjectId""::integer;"
            );

            migrationBuilder.Sql(
                @"ALTER TABLE ""Progress"" 
                  ALTER COLUMN ""QuestionId"" TYPE integer USING ""QuestionId""::integer;"
            );

            migrationBuilder.Sql(
                @"ALTER TABLE ""Progress"" 
                  ALTER COLUMN ""ExamId"" TYPE integer USING ""ExamId""::integer;"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "YearId",
                table: "Progress",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                table: "Progress",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "QuestionId",
                table: "Progress",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ExamId",
                table: "Progress",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
