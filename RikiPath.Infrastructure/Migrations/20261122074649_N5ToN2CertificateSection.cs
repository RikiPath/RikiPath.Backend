using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class N5ToN2CertificateSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CertificateSections",
                columns: new[] { "Id", "CertificationLevelId", "Code", "CreatedBy", "CreatedDate", "Description", "DurationMinutes", "IsDeleted", "MaxScore", "ModifiedBy", "ModifiedDate", "Name", "PassingScore", "SortOrder" },
                values: new object[,]
                {
                    { 8, 3, "N3_VOCAB", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5300), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N3", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 9, 3, "N3_GRAMMAR", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5301), "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N3", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp)", 0, 0 },
                    { 10, 3, "N3_READING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5302), "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N3", null, false, 0, null, null, "Đọc hiểu", 0, 0 },
                    { 11, 3, "N3_LISTENING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5303), "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N3", null, false, 0, null, null, "Nghe hiểu", 0, 0 },
                    { 12, 4, "N2_VOCAB", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5304), "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N2", null, false, 0, null, null, "Kiến thức ngôn ngữ (Từ vựng)", 0, 0 },
                    { 13, 4, "N2_GRAMMAR", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5305), "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N2", null, false, 0, null, null, "Kiến thức ngôn ngữ (Ngữ pháp)", 0, 0 },
                    { 14, 4, "N2_READING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5306), "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N2", null, false, 0, null, null, "Đọc hiểu", 0, 0 },
                    { 15, 4, "N2_LISTENING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5307), "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N2", null, false, 0, null, null, "Nghe hiểu", 0, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 15);
        }
    }
}
