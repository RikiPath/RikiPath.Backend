using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HasDataSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminName",
                table: "MentorAvailabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "MentorAvailabilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "MentorAvailabilities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "MentorAvailabilities",
                type: "text",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Features",
                columns: new[] { "Id", "CreatedBy", "CreatedDate", "Description", "IsDeleted", "ModifiedBy", "ModifiedDate", "Name" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập lesson, từ vựng, ngữ pháp và kanji theo các chứng chỉ tiếng Nhật được cấu hình trong hệ thống.", false, null, null, "Thư viện học tiếng Nhật" },
                    { 2, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện viết hiragana, katakana và kanji theo thứ tự nét, xem lại lỗi và tiến độ luyện tập.", false, null, null, "Luyện viết Kana và Kanji" },
                    { 3, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Làm bài luyện tập theo kỹ năng và cấp độ chứng chỉ tiếng Nhật.", false, null, null, "Bài luyện tập theo kỹ năng" },
                    { 4, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Luyện đề thi thử và theo dõi kết quả theo chứng chỉ tiếng Nhật có trong hệ thống.", false, null, null, "Đề thi thử chứng chỉ" },
                    { 5, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Xem tiến độ học lesson, kết quả luyện tập và lịch sử làm đề.", false, null, null, "Theo dõi tiến độ học" },
                    { 6, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sử dụng các chức năng AI hiện có để nhận gợi ý lộ trình và phản hồi bài làm tiếng Nhật.", false, null, null, "Hỗ trợ học tập bằng AI" },
                    { 7, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gói bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor.", false, null, null, "12 buổi meeting 1-1 với Mentor" },
                    { 8, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chọn lịch trống của Mentor; mỗi buổi đã đặt có phòng học trực tuyến realtime.", false, null, null, "Đặt lịch và phòng học trực tuyến" }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "ModifiedBy", "ModifiedDate", "Name", "Price", "SortOrder" },
                values: new object[,]
                {
                    { 1, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tuần.", 7, true, false, false, false, null, null, "Tự học tiếng Nhật - 1 tuần", 79000m, 6 },
                    { 2, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tuần.", 14, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tuần", 119000m, 5 },
                    { 3, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tuần.", 21, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tuần", 159000m, 4 },
                    { 4, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 1 tháng.", 30, true, false, true, false, null, null, "Tự học tiếng Nhật - 1 tháng", 199000m, 3 },
                    { 5, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 2 tháng.", 60, true, false, false, false, null, null, "Tự học tiếng Nhật - 2 tháng", 349000m, 2 },
                    { 6, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Truy cập nội dung ôn luyện các chứng chỉ tiếng Nhật trong 3 tháng.", 90, true, false, false, false, null, null, "Tự học tiếng Nhật - 3 tháng", 459000m, 1 }
                });

            migrationBuilder.InsertData(
                table: "FeatureSubscriptionPlan",
                columns: new[] { "FeaturesId", "SubscriptionPlansId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 1, 3 },
                    { 1, 4 },
                    { 1, 5 },
                    { 1, 6 },
                    { 2, 1 },
                    { 2, 2 },
                    { 2, 3 },
                    { 2, 4 },
                    { 2, 5 },
                    { 2, 6 },
                    { 3, 1 },
                    { 3, 2 },
                    { 3, 3 },
                    { 3, 4 },
                    { 3, 5 },
                    { 3, 6 },
                    { 4, 1 },
                    { 4, 2 },
                    { 4, 3 },
                    { 4, 4 },
                    { 4, 5 },
                    { 4, 6 },
                    { 5, 1 },
                    { 5, 2 },
                    { 5, 3 },
                    { 5, 4 },
                    { 5, 5 },
                    { 5, 6 },
                    { 6, 1 },
                    { 6, 2 },
                    { 6, 3 },
                    { 6, 4 },
                    { 6, 5 },
                    { 6, 6 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 1, 6 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 2, 6 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 3, 6 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 4, 6 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 5, 6 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 2 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 3 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 4 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 5 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 6, 6 });

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Features",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "AdminName",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "MentorAvailabilities");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "MentorAvailabilities");
        }
    }
}
