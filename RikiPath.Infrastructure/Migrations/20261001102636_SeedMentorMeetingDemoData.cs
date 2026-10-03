using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedMentorMeetingDemoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "MentorAvailabilities",
                columns: new[] { "Id", "AdminName", "CreatedBy", "CreatedDate", "EndTime", "IsApproved", "IsBooked", "IsDeleted", "MentorId", "ModifiedBy", "ModifiedDate", "RejectedAt", "RejectionReason", "StartTime" },
                values: new object[,]
                {
                    { 990001, "Seed Demo", null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), true, false, false, 900002, null, null, null, null, new DateTime(2026, 10, 2, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990002, "Seed Demo", null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 3, 14, 0, 0, 0, DateTimeKind.Unspecified), true, false, false, 900002, null, null, null, null, new DateTime(2026, 10, 3, 13, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990003, "Seed Demo", null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 4, 10, 0, 0, 0, DateTimeKind.Unspecified), true, false, false, 900003, null, null, null, null, new DateTime(2026, 10, 4, 9, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 990004, "Seed Demo", null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 5, 14, 0, 0, 0, DateTimeKind.Unspecified), true, false, false, 900003, null, null, null, null, new DateTime(2026, 10, 5, 13, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "AiGradingQuota", "CreatedBy", "CreatedDate", "Description", "DurationDays", "IsActive", "IsDeleted", "IsPopular", "IsTrial", "MeetingSessionCount", "ModifiedBy", "ModifiedDate", "Name", "Price", "SortOrder" },
                values: new object[] { 7, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gói demo gồm 12 buổi ôn luyện 1-1 với Mentor, thời hạn 90 ngày. Dữ liệu thanh toán demo được seed để kiểm thử đặt lịch trên frontend.", 90, true, false, false, false, 12, null, null, "Meeting 1-1 với Mentor - 12 buổi", 459000m, 7 });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "CreatedBy", "CreatedDate", "CurrentStreak", "DailyStudyMinutes", "Email", "EmailNotificationsEnabled", "FirebaseUid", "FirstName", "IsDeleted", "IsEmailVerified", "IsPhoneVerified", "LastName", "LastStudyDate", "LongestStreak", "ModifiedBy", "ModifiedDate", "PasswordHash", "PasswordSalt", "PhoneNumber", "Role", "StudyTimePreference", "SystemNotificationsEnabled", "TargetCertificationLevelId" },
                values: new object[] { 900006, null, null, new DateTime(2026, 9, 28, 0, 0, 0, 0, DateTimeKind.Utc), 0, 30, "learner.meeting@rikipath.local", true, null, "Meeting", false, true, false, "Test Learner", null, 0, null, null, new byte[] { 9, 206, 219, 58, 155, 234, 107, 145, 121, 174, 134, 51, 96, 253, 64, 216, 157, 114, 92, 36, 123, 206, 67, 217, 207, 166, 54, 71, 254, 251, 156, 200, 185, 110, 198, 135, 221, 146, 243, 40, 237, 229, 16, 133, 163, 161, 156, 79, 138, 103, 85, 25, 114, 81, 99, 182, 25, 73, 212, 198, 212, 192, 54, 129 }, new byte[] { 231, 202, 97, 95, 11, 9, 242, 47, 62, 77, 67, 36, 170, 78, 120, 81, 56, 69, 47, 36, 209, 212, 107, 244, 15, 34, 189, 110, 57, 66, 205, 213, 224, 19, 245, 192, 104, 146, 130, 44, 54, 107, 2, 15, 109, 136, 118, 220, 228, 44, 110, 219, 31, 100, 161, 164, 63, 231, 196, 79, 120, 106, 181, 71 }, null, "Learner", null, true, null });

            migrationBuilder.InsertData(
                table: "FeatureSubscriptionPlan",
                columns: new[] { "FeaturesId", "SubscriptionPlansId" },
                values: new object[,]
                {
                    { 7, 7 },
                    { 8, 7 }
                });

            migrationBuilder.InsertData(
                table: "UserSubscriptions",
                columns: new[] { "Id", "AiGradingQuota", "AiGradingUsedCount", "AmountPaid", "CreatedBy", "CreatedDate", "EndDate", "IsDeleted", "MeetingSessionsIncluded", "ModifiedBy", "ModifiedDate", "PaymentExpiresAt", "PaymentStatus", "PaymentTransactionId", "PurchasedAt", "StartDate", "SubscriptionPlanId", "UserId" },
                values: new object[] { 990001, 0, 0, 459000m, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), false, 12, null, null, null, "Paid", "DEMO-MENTOR-12-SESSIONS", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, 900006 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 7, 7 });

            migrationBuilder.DeleteData(
                table: "FeatureSubscriptionPlan",
                keyColumns: new[] { "FeaturesId", "SubscriptionPlansId" },
                keyValues: new object[] { 8, 7 });

            migrationBuilder.DeleteData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990001);

            migrationBuilder.DeleteData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990002);

            migrationBuilder.DeleteData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990003);

            migrationBuilder.DeleteData(
                table: "MentorAvailabilities",
                keyColumn: "Id",
                keyValue: 990004);

            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001);

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 900006);
        }
    }
}
