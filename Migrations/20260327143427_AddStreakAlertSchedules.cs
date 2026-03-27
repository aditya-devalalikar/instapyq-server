using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakAlertSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreakAlertSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StreakId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    LocalHour = table.Column<int>(type: "integer", nullable: false),
                    LocalMinute = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true),
                    NextFireUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSentUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSentLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakAlertSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreakAlertSchedules_Streaks_StreakId",
                        column: x => x.StreakId,
                        principalTable: "Streaks",
                        principalColumn: "StreakId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreakAlertSchedules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreakAlertSchedules_IsActive_NextFireUtc",
                table: "StreakAlertSchedules",
                columns: new[] { "IsActive", "NextFireUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StreakAlertSchedules_LeaseUntilUtc",
                table: "StreakAlertSchedules",
                column: "LeaseUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StreakAlertSchedules_StreakId_LocalHour_LocalMinute",
                table: "StreakAlertSchedules",
                columns: new[] { "StreakId", "LocalHour", "LocalMinute" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreakAlertSchedules_UserId",
                table: "StreakAlertSchedules",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreakAlertSchedules");
        }
    }
}
