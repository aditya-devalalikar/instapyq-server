using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStudySummary",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalSeconds = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<short>(type: "smallint", nullable: false),
                    PerStreak = table.Column<string>(type: "text", nullable: true),
                    Sessions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStudySummary", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_DailyStudySummary_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Streaks",
                columns: table => new
                {
                    StreakId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    SpecificDays = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    IsTimer = table.Column<bool>(type: "boolean", nullable: false),
                    TargetMinutes = table.Column<int>(type: "integer", nullable: true),
                    Alerts = table.Column<string>(type: "text", nullable: true),
                    CurrentStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streaks", x => x.StreakId);
                    table.ForeignKey(
                        name: "FK_Streaks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreakMonthlyProgress",
                columns: table => new
                {
                    StreakId = table.Column<int>(type: "integer", nullable: false),
                    YearMonth = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DaysMask = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakMonthlyProgress", x => new { x.StreakId, x.YearMonth });
                    table.ForeignKey(
                        name: "FK_StreakMonthlyProgress_Streaks_StreakId",
                        column: x => x.StreakId,
                        principalTable: "Streaks",
                        principalColumn: "StreakId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStudySummary_UserId_Date",
                table: "DailyStudySummary",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_StreakMonthlyProgress_UserId_YearMonth",
                table: "StreakMonthlyProgress",
                columns: new[] { "UserId", "YearMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_Streaks_ClientId",
                table: "Streaks",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Streaks_UserId_IsDeleted",
                table: "Streaks",
                columns: new[] { "UserId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStudySummary");

            migrationBuilder.DropTable(
                name: "StreakMonthlyProgress");

            migrationBuilder.DropTable(
                name: "Streaks");
        }
    }
}
