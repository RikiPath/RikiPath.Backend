using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedJlptCertificateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CertificateTypes",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsActive", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name", "SortOrder" },
                values: new object[] { 1, null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "Japanese-Language Proficiency Test", true, false, null, null, "JLPT", 1 });

            migrationBuilder.InsertData(
                table: "CertificateLevels",
                columns: new[] { "Id", "CertificationId", "Code", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "N5", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N5", false, null, null, 1 },
                    { 2, 1, "N4", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N4", false, null, null, 2 },
                    { 3, 1, "N3", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N3", false, null, null, 3 },
                    { 4, 1, "N2", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N2", false, null, null, 4 },
                    { 5, 1, "N1", null, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), "JLPT N1", false, null, null, 5 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CertificateLevels",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CertificateTypes",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
