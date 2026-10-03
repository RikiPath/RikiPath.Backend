using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleKanji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Kanjis",
                columns: new[] { "Id", "AudioUrl", "CertificationLevelId", "Character", "ContentAuthorId", "CreatedBy", "CreatedDate", "IsApproved", "IsDeleted", "KunYomi", "Meaning", "ModifiedBy", "ModifiedDate", "OnYomi", "ReviewNote", "ReviewedByName", "ReviewedDate", "SinoVietnamese", "Status", "StrokeCount", "StrokeOrderImageUrl" },
                values: new object[,]
                {
                    { 910001, null, 1, "日", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "ひ、か", "Mặt trời; ngày", null, null, "ニチ、ジツ", null, null, null, "Nhật", "Published", 4, null },
                    { 910002, null, 1, "月", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "つき", "Mặt trăng; tháng", null, null, "ゲツ、ガツ", null, null, null, "Nguyệt", "Published", 4, null },
                    { 910003, null, 1, "火", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "ひ、ほ", "Lửa", null, null, "カ", null, null, null, "Hỏa", "Published", 4, null },
                    { 910004, null, 1, "水", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "みず", "Nước", null, null, "スイ", null, null, null, "Thủy", "Published", 4, null },
                    { 910005, null, 1, "木", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "き、こ", "Cây; gỗ", null, null, "モク、ボク", null, null, null, "Mộc", "Published", 4, null },
                    { 910006, null, 1, "金", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "かね、かな", "Vàng; tiền", null, null, "キン、コン", null, null, null, "Kim", "Published", 8, null },
                    { 910007, null, 1, "土", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "つち", "Đất", null, null, "ド、ト", null, null, null, "Thổ", "Published", 3, null },
                    { 910008, null, 1, "山", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "やま", "Núi", null, null, "サン", null, null, null, "Sơn", "Published", 3, null },
                    { 910009, null, 1, "川", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "かわ", "Sông", null, null, "セン", null, null, null, "Xuyên", "Published", 3, null },
                    { 910010, null, 1, "人", 900004, null, new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "ひと", "Người", null, null, "ジン、ニン", null, null, null, "Nhân", "Published", 2, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910001);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910002);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910003);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910004);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910005);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910006);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910007);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910008);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910009);

            migrationBuilder.DeleteData(
                table: "Kanjis",
                keyColumn: "Id",
                keyValue: 910010);
        }
    }
}
