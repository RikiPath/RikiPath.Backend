using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCertificateSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CertificateSections",
                columns: new[] { "Id", "CertificationLevelId", "Code", "CreatedBy", "CreatedDate", "Description", "DurationMinutes", "IsDeleted", "MaxScore", "ModifiedBy", "ModifiedDate", "Name", "PassingScore", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "N5_VOCAB", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2484), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N5", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 2, 1, "N5_GRAMMAR_READING", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2492), "Phần thi kiểm tra cấu trúc ngữ pháp và đọc hiểu đoạn văn ngắn cấp độ N5", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu", 0, 0 },
                    { 3, 1, "N5_LISTENING", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2494), "Phần thi nghe hiểu tình huống giao tiếp hàng ngày cấp độ N5", null, false, 0, null, null, "Nghe hiểu", 0, 0 },
                    { 4, 2, "N4_VOCAB", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2496), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N4", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 5, 2, "N4_GRAMMAR", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2497), "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N4", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp)", 0, 0 },
                    { 6, 2, "N4_READING", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2499), "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N4", null, false, 0, null, null, "Đọc hiểu", 0, 0 },
                    { 7, 2, "N4_LISTENING", null, new DateTime(2026, 10, 27, 9, 41, 52, 301, DateTimeKind.Utc).AddTicks(2501), "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N4", null, false, 0, null, null, "Nghe hiểu", 0, 0 }
                });

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9333));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9346));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9349));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9350));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9352));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9425));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9426));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9428));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9429));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9432));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9433));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9434));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9436));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9437));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9439));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9440));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9442));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9445));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9446));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9448));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9449));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9450));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9452));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9453));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9455));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9456));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9458));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9459));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9461));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9462));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9464));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9500));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9502));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9505));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9506));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9508));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9510));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9511));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9513));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9514));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9516));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9517));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9519));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9520));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9521));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9522));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9552));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9555));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9556));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9558));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9559));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9645));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9647));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9648));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9650));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9651));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9652));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9654));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9655));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9656));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9657));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9659));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9660));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9661));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9662));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9665));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9667));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9668));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9669));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9671));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9672));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9673));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9675));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9676));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9678));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9744));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9745));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9746));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9748));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9749));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9751));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9752));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9754));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9755));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9756));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9758));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9759));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9760));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9762));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9763));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9765));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 9, 41, 52, 273, DateTimeKind.Utc).AddTicks(9766));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6493));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6507));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6509));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6510));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6512));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6516));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6517));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6519));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6520));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6522));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6523));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6525));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6526));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6528));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6529));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6574));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6576));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6578));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6580));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6581));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6582));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6584));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6585));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6587));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6589));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6590));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6591));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6593));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6594));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6596));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6597));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6599));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6600));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6602));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6604));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6605));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6607));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6608));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6610));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6695));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6697));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6698));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6700));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6701));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6702));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6704));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6739));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6742));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6744));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6745));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6746));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6748));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6749));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6750));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6752));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6753));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6755));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6756));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6757));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6759));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6760));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6831));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6833));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6835));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6836));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6839));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6840));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6842));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6843));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6845));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6846));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6847));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6848));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6850));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6851));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6852));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6854));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6855));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6857));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6858));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6860));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6861));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6863));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6864));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6865));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6914));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6916));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6917));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6919));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6920));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6922));

            migrationBuilder.UpdateData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92,
                column: "CreatedDate",
                value: new DateTime(2026, 10, 27, 8, 12, 15, 804, DateTimeKind.Utc).AddTicks(6923));
        }
    }
}
