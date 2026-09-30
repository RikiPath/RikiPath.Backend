using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedLanguageSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "LanguageSkills",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese vocabulary learning skill.", false, null, null, "Vocabulary" },
                    { 2, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese kanji learning skill.", false, null, null, "Kanji" },
                    { 3, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese grammar learning skill.", false, null, null, "Grammar" },
                    { 4, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese listening comprehension skill.", false, null, null, "Listening" },
                    { 5, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese reading comprehension skill.", false, null, null, "Reading" }
                });

            migrationBuilder.Sql("""SELECT setval(pg_get_serial_sequence('"LanguageSkills"', 'Id'), GREATEST(COALESCE((SELECT MAX("Id") FROM "LanguageSkills"), 1), 1), true);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "LanguageSkills", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "LanguageSkills", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "LanguageSkills", keyColumn: "Id", keyValue: 3);
            migrationBuilder.DeleteData(table: "LanguageSkills", keyColumn: "Id", keyValue: 4);
            migrationBuilder.DeleteData(table: "LanguageSkills", keyColumn: "Id", keyValue: 5);
        }
    }
}
