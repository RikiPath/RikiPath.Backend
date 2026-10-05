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
                values: new object[] { 7, 0, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GÃ³i demo gá»“m 12 buá»•i Ã´n luyá»‡n 1-1 vá»›i Mentor, thá»i háº¡n 90 ngÃ y. Dá»¯ liá»‡u thanh toÃ¡n demo Ä‘Æ°á»£c seed Ä‘á»ƒ kiá»ƒm thá»­ Ä‘áº·t lá»‹ch trÃªn frontend.", 90, true, false, false, false, 12, null, null, "Meeting 1-1 vá»›i Mentor - 12 buá»•i", 459000m, 7 });

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
        }
    }
}

