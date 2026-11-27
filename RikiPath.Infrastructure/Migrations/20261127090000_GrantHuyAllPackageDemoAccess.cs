using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations;

/// <summary>
/// Demo-only grant for huydqse151428@fpt.edu.vn. The grant is resolved by
/// email at migration time because this account is created outside HasData.
/// </summary>
public partial class GrantHuyAllPackageDemoAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $grant_huy_all_packages$
            DECLARE
                learner_id integer;
            BEGIN
                SELECT "Id"
                INTO learner_id
                FROM "Users"
                WHERE lower(btrim("Email")) = lower('huydqse151428@fpt.edu.vn')
                  AND NOT "IsDeleted"
                ORDER BY "Id"
                LIMIT 1;

                IF learner_id IS NULL THEN
                    RAISE NOTICE 'Demo package grant skipped: learner email was not found.';
                    RETURN;
                END IF;

                INSERT INTO "SubscriptionPlans"
                    ("Id", "Name", "Description", "DurationDays", "Price", "AiGradingQuota",
                     "MeetingSessionCount", "CertificateLevelId", "PlanCategory", "IsPopular",
                     "IsTrial", "IsActive", "SortOrder", "Revision", "CreatedDate", "IsDeleted")
                VALUES
                    (8101, 'Luyện nói AI - 1 lượt', 'Một lượt luyện nói và chấm phát âm tiếng Nhật bằng AI.', 0, 1000, 1, 0, NULL, 'speaking', FALSE, FALSE, TRUE, 10, 1, TIMESTAMP '2026-11-23 00:00:00', FALSE),
                    (8102, 'Luyện viết AI - 1 lượt', 'Một lượt chấm bài viết tiếng Nhật bằng AI.', 0, 1000, 1, 0, NULL, 'writing', FALSE, FALSE, TRUE, 11, 1, TIMESTAMP '2026-11-23 00:00:00', FALSE),
                    (8103, 'Giải thích đáp án AI - 1 lượt', 'Một lượt giải thích chi tiết đáp án đề thi thử bằng AI.', 0, 1000, 1, 0, NULL, 'ai_grading', FALSE, FALSE, TRUE, 12, 1, TIMESTAMP '2026-11-23 00:00:00', FALSE),
                    (8104, 'Lộ trình học AI - 1 lượt', 'Một lượt tạo lộ trình học cá nhân hóa bằng AI.', 0, 1000, 1, 0, NULL, 'learning_path', FALSE, FALSE, TRUE, 13, 1, TIMESTAMP '2026-11-23 00:00:00', FALSE),
                    (8105, 'Mentor - 1 buổi', 'Một buổi ôn luyện với mentor.', 30, 1000, 0, 1, NULL, 'mentor_legacy', FALSE, FALSE, TRUE, 14, 1, TIMESTAMP '2026-11-23 00:00:00', FALSE)
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "FeatureSubscriptionPlan" ("FeaturesId", "SubscriptionPlansId")
                VALUES (13, 8101), (14, 8102), (6, 8103), (12, 8104), (7, 8105), (8, 8105)
                ON CONFLICT ("FeaturesId", "SubscriptionPlansId") DO NOTHING;

                -- One billion is the largest practical demo quota without
                -- risking integer overflow in existing aggregate queries.
                INSERT INTO "UserSubscriptions"
                    ("UserId", "SubscriptionPlanId", "MentorId", "AmountPaid", "PaymentStatus",
                     "PaymentTransactionId", "StartDate", "EndDate", "AiGradingQuota",
                     "AiGradingUsedCount", "DurationDaysSnapshot", "PlanNameSnapshot",
                     "PlanCategorySnapshot", "PlanRevisionSnapshot", "MeetingSessionsIncluded",
                     "MeetingSessionsUsed", "PurchasedAt", "CreatedDate", "IsDeleted")
                SELECT learner_id,
                       p."Id",
                       CASE WHEN p."MeetingSessionCount" > 0
                            THEN (SELECT "Id" FROM "Users" WHERE "Id" = 900002 LIMIT 1)
                            ELSE NULL END,
                       0,
                       'Paid',
                       CASE p."Id"
                           WHEN 6 THEN 'DEMO-HUY-UNLIMITED-SELF-STUDY'
                           WHEN 7 THEN 'DEMO-HUY-MENTOR-ACCESS'
                           WHEN 8104 THEN 'DEMO-HUY-UNLIMITED-LEARNING-PATH'
                           ELSE 'DEMO-HUY-FULL-PLAN-' || p."Id"::text
                       END,
                       TIMESTAMP '2026-10-01 00:00:00',
                       CASE WHEN p."Id" IN (8101, 8102, 8103, 8104)
                            THEN NULL
                            ELSE TIMESTAMP '2099-12-31 23:59:59' END,
                       CASE WHEN p."MeetingSessionCount" > 0 THEN 0 ELSE 1000000000 END,
                       0,
                       p."DurationDays",
                       p."Name",
                       p."PlanCategory",
                       p."Revision",
                       p."MeetingSessionCount",
                       0,
                       TIMESTAMP '2026-10-01 00:00:00',
                       NOW(),
                       FALSE
                FROM "SubscriptionPlans" p
                WHERE p."Id" IN (1, 2, 3, 4, 5, 6, 7, 8101, 8102, 8103, 8104, 8105)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "UserSubscriptions" s
                      WHERE s."UserId" = learner_id
                        AND s."PaymentTransactionId" = CASE p."Id"
                            WHEN 6 THEN 'DEMO-HUY-UNLIMITED-SELF-STUDY'
                            WHEN 7 THEN 'DEMO-HUY-MENTOR-ACCESS'
                            WHEN 8104 THEN 'DEMO-HUY-UNLIMITED-LEARNING-PATH'
                            ELSE 'DEMO-HUY-FULL-PLAN-' || p."Id"::text
                        END
                  );
            END $grant_huy_all_packages$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM "UserSubscriptions" s
            USING "Users" u
            WHERE s."UserId" = u."Id"
              AND lower(btrim(u."Email")) = lower('huydqse151428@fpt.edu.vn')
              AND s."PaymentTransactionId" LIKE 'DEMO-HUY-FULL-PLAN-%';
            """);
    }
}
