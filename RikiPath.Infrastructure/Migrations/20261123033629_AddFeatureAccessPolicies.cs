using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureAccessPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessMode",
                table: "Features",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FREE");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Features",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Features",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreditFeatureKey",
                table: "Features",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCoreFree",
                table: "Features",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RoutePrefixesJson",
                table: "Features",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Features",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "PLAN_REQUIRED", "self_study", "LESSONS_CONTENT", null, "Truy cập kho bài học JLPT đầy đủ theo cấp độ kèm nghe hiểu, đọc hiểu và lưu tiến độ.", "Thư viện bài học JLPT", "[\"/lessons\",\"/lesson-player\",\"/riki/listening\",\"/riki/reading\"]", 10 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "IsCoreFree", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "FREE", "core_tools", "JAPANESE_LIBRARY", null, "Tra cứu ngữ pháp, luyện viết Hiragana, Katakana và Kanji theo thứ tự nét miễn phí.", true, "Thư viện Kana, Kanji & Ngữ pháp", "[\"/grammar\",\"/kanji-writing\",\"/vocabulary-detail\"]", 3 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "FREE", "practice_exam", "PRACTICE_EXERCISES", null, "Làm bài luyện tập củng cố kỹ năng và bộ đề theo cấp độ JLPT.", "[\"/lesson-practice\",\"/practice-sets\"]", 20 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "FREE", "practice_exam", "MOCK_TESTS", null, "Luyện đề thi thử JLPT mô phỏng đầy đủ 4 kỹ năng và theo dõi kết quả.", "Đề thi thử JLPT", "[\"/mock-test\",\"/mock-tests\",\"/exam-result\"]", 21 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "FREE", "self_study", "STANDARD_ROADMAP", null, "Xem lộ trình học JLPT tiêu chuẩn theo mục tiêu, kết quả đánh giá năng lực và tiến độ học.", "Lộ trình tiêu chuẩn & Tiến độ học", "[\"/roadmap\"]", 11 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "CREDIT_REQUIRED", "ai_services", "AI_MOCK_EXPLANATION", "mock_test_explanation", "Nhận phân tích chi tiết đáp án đề thi thử từ trợ lý AI.", "Giải thích câu hỏi bằng AI", null, 33 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "DISABLED", "mentor_legacy", "MENTOR_1ON1_LEGACY", null, "Gói cũ bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor (đã thay bằng lớp học nhóm).", null, 90 });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "RoutePrefixesJson", "SortOrder" },
                values: new object[] { "DISABLED", "mentor_legacy", "MENTOR_ROOM_LEGACY", null, "Cấu hình lịch 1-1 cũ (lớp học nhóm dùng cơ chế ghi danh khóa học riêng).", null, 91 });

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "Id", "AccessMode", "Category", "Code", "CreatedBy", "CreatedDate", "CreditFeatureKey", "Description", "IsCoreFree", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[,]
                {
                    { 9, "FREE", "core_tools", "PERSONAL_NOTEBOOK", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, "Tạo thư mục, tệp ghi chú, trang bài học, highlight và mục đã lưu cá nhân miễn phí.", true, false, null, null, "Sổ tay cá nhân & Mini Notebook", "[\"/kanji-notebook\"]", 1 },
                    { 10, "FREE", "core_tools", "SRS_STUDY", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, "Ôn thẻ đến hạn, 4 chế độ ôn tự do, quản lý thẻ cá nhân và thư viện thẻ mẫu miễn phí.", true, false, null, null, "Ôn tập SRS & Thư viện thẻ mẫu", "[\"/daily-srs\"]", 2 }
                });

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "Id", "AccessMode", "Category", "Code", "CreatedBy", "CreatedDate", "CreditFeatureKey", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name", "RoutePrefixesJson", "SortOrder" },
                values: new object[,]
                {
                    { 11, "FREE", "practice_exam", "PLACEMENT_TEST", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, "Làm bài đánh giá trình độ JLPT đầu vào để xác định cấp độ học phù hợp.", false, null, null, "Đánh giá năng lực đầu vào", "[\"/placement\"]", 19 },
                    { 12, "CREDIT_REQUIRED", "ai_services", "AI_LEARNING_PATH", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "learning_path", "Tạo kế hoạch học tập chi tiết theo ngày bằng AI dựa trên điểm đánh giá và ngày thi.", false, null, null, "Lộ trình học cá nhân hóa bằng AI", null, 30 },
                    { 13, "CREDIT_REQUIRED", "ai_services", "AI_SPEAKING", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "speaking", "Thu âm tiếng Nhật và nhận điểm phát âm, ngữ điệu cùng nhận xét chi tiết từ AI.", false, null, null, "Luyện nói & Chấm phát âm AI", "[\"/riki/speaking\",\"/speech\"]", 31 },
                    { 14, "CREDIT_REQUIRED", "ai_services", "AI_WRITING", null, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "writing", "Viết câu và bài luận tiếng Nhật, nhận sửa lỗi ngữ pháp và chấm điểm từ AI.", false, null, null, "Luyện viết & Chấm bài luận AI", "[\"/riki/essay\",\"/sentence-studio\"]", 32 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Features_Code",
                table: "Features",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Features_Code",
                table: "Features");

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DropColumn(
                name: "AccessMode",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "CreditFeatureKey",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "IsCoreFree",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "RoutePrefixesJson",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Features");

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Truy cập lesson, từ vựng, ngữ pháp và kanji theo các chứng chỉ tiếng Nhật được cấu hình trong hệ thống.", "Thư viện học tiếng Nhật" });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Luyện viết hiragana, katakana và kanji theo thứ tự nét, xem lại lỗi và tiến độ luyện tập.", "Luyện viết Kana và Kanji" });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "Làm bài luyện tập theo kỹ năng và cấp độ chứng chỉ tiếng Nhật.");

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Luyện đề thi thử và theo dõi kết quả theo chứng chỉ tiếng Nhật có trong hệ thống.", "Đề thi thử chứng chỉ" });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Xem tiến độ học lesson, kết quả luyện tập và lịch sử làm đề.", "Theo dõi tiến độ học" });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Sử dụng các chức năng AI hiện có để nhận gợi ý lộ trình và phản hồi bài làm tiếng Nhật.", "Hỗ trợ học tập bằng AI" });

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 7,
                column: "Description",
                value: "Gói bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor.");

            migrationBuilder.UpdateData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "Chọn lịch trống của Mentor; mỗi buổi đã đặt có phòng học trực tuyến realtime.");
        }
    }
}
