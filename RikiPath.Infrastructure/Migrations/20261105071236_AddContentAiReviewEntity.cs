using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentAiReviewEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlacementAssessment",
                table: "MockTests");

            migrationBuilder.CreateTable(
                name: "ContentAiReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentId = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAiReviews", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6122));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6127));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6129));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6130));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6132));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6133));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 11, 5, 7, 12, 34, 453, DateTimeKind.Utc).AddTicks(6134));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1757));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1766));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1768));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1770));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1771));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1774));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1775));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1776));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1830));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1832));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1833));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1834));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1835));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1836));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1837));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1838));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1838));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1840));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1841));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1842));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1843));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1844));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1845));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1845));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1846));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1847));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1848));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1849));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1849));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1850));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1851));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1852));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1852));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1913));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1915));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1915));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1916));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1917));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1918));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1919));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1920));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1920));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1922));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1922));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1923));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1924));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1955));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1958));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1959));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1960));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1961));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1961));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1962));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1963));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(1964));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2006));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2009));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2010));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2011));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2012));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2014));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2015));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2016));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2017));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2019));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2022));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2023));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2024));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2026));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2027));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2028));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2029));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2030));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2032));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2033));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2034));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2036));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2037));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2038));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2039));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2086));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2087));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2088));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2089));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2090));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2092));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2093));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2094));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2096));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2097));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2098));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 26, 16, 6, 11, 768, DateTimeKind.Utc).AddTicks(2099));

            migrationBuilder.CreateIndex(
                name: "IX_ContentAiReviews_ContentType_ContentId_ReviewedAt",
                table: "ContentAiReviews",
                columns: new[] { "ContentType", "ContentId", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentAiReviews");

            migrationBuilder.AddColumn<bool>(
                name: "IsPlacementAssessment",
                table: "MockTests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(8993));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9002));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9004));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9005));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9006));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9008));

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 861, DateTimeKind.Utc).AddTicks(9009));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6562));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6586));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6588));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6589));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6591));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6595));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6596));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6598));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6599));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6602));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6603));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6605));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6606));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6608));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6609));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6610));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6612));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6614));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6616));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6673));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6675));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6676));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6677));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6679));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6680));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6681));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6683));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6684));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6685));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6687));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6689));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6690));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6691));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6694));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6695));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6697));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6698));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6699));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6701));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6702));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6704));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6705));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6707));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6708));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6709));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6757));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6786));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6789));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6790));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6792));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6793));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6795));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6796));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6797));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6799));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6800));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6802));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6803));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6805));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6806));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6808));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6809));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6811));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6812));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6813));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6864));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6865));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6867));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6868));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6869));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6871));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6873));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6874));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6875));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6877));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6878));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6880));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6881));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6883));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6884));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6885));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6887));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6888));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6890));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6891));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6893));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6894));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6895));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6897));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6898));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6899));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 28, 15, 13, 38, 834, DateTimeKind.Utc).AddTicks(6901));
        }
    }
}
