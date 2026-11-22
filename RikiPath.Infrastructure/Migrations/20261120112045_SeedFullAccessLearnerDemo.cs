using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedFullAccessLearnerDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "DurationDaysSnapshot", "EndDate", "MeetingSessionsUsed", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot", "PurchasedAt" },
                values: new object[] { 90, new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), 0, "mentor_legacy", "Meeting 1-1 với Mentor - 12 buổi", 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            // The demo learning-path subscription uses plan 8104.  This plan is
            // intentionally created here because older databases already contain
            // the self-study plans (1-6) but do not have the learning-path credit
            // plan yet.  ON CONFLICT keeps the migration safe when an admin has
            // already created the plan manually.
            migrationBuilder.Sql(
                """
                INSERT INTO "SubscriptionPlans"
                    ("Id", "Name", "Description", "DurationDays", "Price", "AiGradingQuota",
                     "MeetingSessionCount", "CertificateLevelId", "PlanCategory", "IsPopular",
                     "IsTrial", "IsActive", "SortOrder", "Revision", "CreatedDate", "ModifiedDate",
                     "CreatedBy", "ModifiedBy", "IsDeleted")
                VALUES
                    (8104, 'Mua 1 lượt AI đề xuất lộ trình',
                     'Gói credit cho một lượt AI đề xuất lộ trình tự học.', 0, 1000, 1,
                     0, NULL, 'learning_path', FALSE, FALSE, TRUE, 8, 1,
                     TIMESTAMP '2026-10-01 00:00:00', NULL, NULL, NULL, FALSE)
                ON CONFLICT ("Id") DO NOTHING;
                """);

            migrationBuilder.InsertData(
                table: "UserSubscriptions",
                columns: new[] { "Id", "AiGradingQuota", "AiGradingUsedCount", "AmountPaid", "CertificateLevelId", "CreatedBy", "CreatedDate", "DurationDaysSnapshot", "EndDate", "IsDeleted", "MentorId", "MentorPracticeOutcomeJson", "MentorReviewCompletedAt", "MentorReviewPlan", "MentorReviewSessionAt", "ModifiedBy", "ModifiedDate", "PaymentExpiresAt", "PaymentStatus", "PaymentTransactionId", "PendingMentorAvailabilityId", "PendingMentorQuestion", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot", "PurchasedAt", "StartDate", "SubscriptionPlanId", "UserId" },
                values: new object[,]
                {
                    { 990005, 15, 0, 3000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 90, new DateTime(2099, 12, 31, 23, 59, 59, 0, DateTimeKind.Unspecified), false, null, null, null, null, null, null, null, null, "Paid", "DEMO-FULL-ACCESS-SELF-STUDY", null, null, "self_study", "Tự học tiếng Nhật - 3 tháng", 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, 900006 },
                    { 990006, 1, 0, 1000m, null, null, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0, null, false, null, null, null, null, null, null, null, null, "Paid", "DEMO-FULL-ACCESS-LEARNING-PATH", null, null, "learning_path", "Mua 1 lượt AI đề xuất lộ trình", 1, new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 8104, 900006 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990005);

            migrationBuilder.DeleteData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990006);

            migrationBuilder.UpdateData(
                table: "UserSubscriptions",
                keyColumn: "Id",
                keyValue: 990001,
                columns: new[] { "DurationDaysSnapshot", "EndDate", "MeetingSessionsUsed", "PlanCategorySnapshot", "PlanNameSnapshot", "PlanRevisionSnapshot", "PurchasedAt" },
                values: new object[] { null, new DateTime(2026, 12, 30, 23, 59, 59, 0, DateTimeKind.Unspecified), 1, null, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }
    }
}
