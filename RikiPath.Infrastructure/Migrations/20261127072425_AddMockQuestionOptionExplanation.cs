using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockQuestionOptionExplanation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "MockQuestionOptions",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "SortOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "Description", "Name", "SortOrder" },
                values: new object[] { "N5_GRAMMAR", "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N5", "Kiến thức ngôn ngữ (Ngữ pháp)", 2 });

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 4);

            migrationBuilder.InsertData(
                table: "CertificateSections",
                columns: new[] { "Id", "CertificationLevelId", "Code", "CreatedBy", "CreatedDate", "Description", "DurationMinutes", "IsDeleted", "MaxScore", "ModifiedBy", "ModifiedDate", "Name", "PassingScore", "SortOrder" },
                values: new object[] { 16, 1, "N5_READING", null, new DateTime(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc).AddTicks(5308), "Phần thi đọc hiểu văn bản, thông báo và đoạn hội thoại ngắn cấp độ N5", null, false, 0, null, null, "Đọc hiểu", 0, 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "MockQuestionOptions");

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 1,
                column: "SortOrder",
                value: 0);

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Code", "Description", "Name", "SortOrder" },
                values: new object[] { "N5_GRAMMAR_READING", "Phần thi kiểm tra cấu trúc ngữ pháp và đọc hiểu đoạn văn ngắn cấp độ N5", "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu", 0 });

            migrationBuilder.UpdateData(
                table: "CertificateSections",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 0);
        }
    }
}
