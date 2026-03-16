using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace pqy_server.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContentHtml = table.Column<string>(type: "text", nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPages", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ContentPages",
                columns: new[] { "Id", "ContentHtml", "ContentJson", "CreatedAt", "IsPublished", "Slug", "Title", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 1, null, "[]", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), true, "faqs", "FAQs", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, "", null, new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), true, "privacy-policy", "Privacy Policy", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, "", null, new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), true, "terms-conditions", "Terms & Conditions", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, "", null, new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), true, "about-us", "About Us", new DateTime(2026, 2, 24, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPages_Slug",
                table: "ContentPages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPages");
        }
    }
}
