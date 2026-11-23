using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RikiPath.Infrastructure.Migrations;

/// <summary>
/// Demo-only access grant for the already registered learner account
/// <c>huydqse151428@fpt.edu.vn</c>. It is deliberately idempotent and does
/// not change authorization rules for any other account.
/// </summary>
public partial class GrantHuyFullDemoAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $grant_huy_full_access$
            DECLARE
                learner_id integer;
                mentor_subscription_id integer;
                booking_exists boolean;
            BEGIN
                SELECT "Id"
                INTO learner_id
                FROM "Users"
                WHERE lower(btrim("Email")) = lower('huydqse151428@fpt.edu.vn')
                  AND NOT "IsDeleted"
                ORDER BY "Id"
                LIMIT 1;

                IF learner_id IS NULL THEN
                    RAISE NOTICE 'Demo access skipped: learner email was not found.';
                    RETURN;
                END IF;

                -- The learning-path plan exists in current seeds. This keeps
                -- the demo grant safe on an older database as well.
                INSERT INTO "SubscriptionPlans"
                    ("Id", "Name", "Description", "DurationDays", "Price", "AiGradingQuota",
                     "MeetingSessionCount", "CertificateLevelId", "PlanCategory", "IsPopular",
                     "IsTrial", "IsActive", "SortOrder", "Revision", "CreatedDate", "IsDeleted")
                VALUES
                    (8104, 'Mua 1 lượt AI đề xuất lộ trình',
                     'Gói credit cho một lượt AI đề xuất lộ trình tự học.', 0, 1000, 1,
                     0, NULL, 'learning_path', FALSE, FALSE, TRUE, 8, 1,
                     TIMESTAMP '2026-10-01 00:00:00', FALSE)
                ON CONFLICT ("Id") DO NOTHING;

                -- The quota is an integer in the existing schema. One billion
                -- uses is effectively unlimited for this demo and leaves room
                -- for other active subscriptions without overflowing the API's
                -- integer aggregate.
                INSERT INTO "UserSubscriptions"
                    ("UserId", "SubscriptionPlanId", "AmountPaid", "PaymentStatus",
                     "PaymentTransactionId", "StartDate", "EndDate", "AiGradingQuota",
                     "AiGradingUsedCount", "DurationDaysSnapshot", "PlanNameSnapshot",
                     "PlanCategorySnapshot", "PlanRevisionSnapshot", "MeetingSessionsIncluded",
                     "MeetingSessionsUsed", "PurchasedAt", "CreatedDate", "IsDeleted")
                SELECT learner_id, 6, 0, 'Paid', 'DEMO-HUY-UNLIMITED-SELF-STUDY',
                       TIMESTAMP '2026-10-01 00:00:00', TIMESTAMP '2099-12-31 23:59:59',
                       1000000000, 0, 90, p."Name", 'self_study', p."Revision", 0, 0,
                       TIMESTAMP '2026-10-01 00:00:00', NOW(), FALSE
                FROM "SubscriptionPlans" p
                WHERE p."Id" = 6
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserSubscriptions" s
                      WHERE s."UserId" = learner_id
                        AND s."PaymentTransactionId" = 'DEMO-HUY-UNLIMITED-SELF-STUDY'
                  );

                INSERT INTO "UserSubscriptions"
                    ("UserId", "SubscriptionPlanId", "AmountPaid", "PaymentStatus",
                     "PaymentTransactionId", "StartDate", "EndDate", "AiGradingQuota",
                     "AiGradingUsedCount", "DurationDaysSnapshot", "PlanNameSnapshot",
                     "PlanCategorySnapshot", "PlanRevisionSnapshot", "MeetingSessionsIncluded",
                     "MeetingSessionsUsed", "PurchasedAt", "CreatedDate", "IsDeleted")
                SELECT learner_id, 8104, 0, 'Paid', 'DEMO-HUY-UNLIMITED-LEARNING-PATH',
                       TIMESTAMP '2026-10-01 00:00:00', NULL, 1000000000, 0, 0, p."Name",
                       'learning_path', p."Revision", 0, 0,
                       TIMESTAMP '2026-10-01 00:00:00', NOW(), FALSE
                FROM "SubscriptionPlans" p
                WHERE p."Id" = 8104
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserSubscriptions" s
                      WHERE s."UserId" = learner_id
                        AND s."PaymentTransactionId" = 'DEMO-HUY-UNLIMITED-LEARNING-PATH'
                  );

                INSERT INTO "UserSubscriptions"
                    ("UserId", "SubscriptionPlanId", "MentorId", "AmountPaid", "PaymentStatus",
                     "PaymentTransactionId", "StartDate", "EndDate", "AiGradingQuota",
                     "AiGradingUsedCount", "DurationDaysSnapshot", "PlanNameSnapshot",
                     "PlanCategorySnapshot", "PlanRevisionSnapshot", "MeetingSessionsIncluded",
                     "MeetingSessionsUsed", "PurchasedAt", "CreatedDate", "IsDeleted")
                SELECT learner_id, 7, 900002, 0, 'Paid', 'DEMO-HUY-MENTOR-ACCESS',
                       TIMESTAMP '2026-10-01 00:00:00', TIMESTAMP '2099-12-31 23:59:59',
                       0, 0, 90, p."Name", 'mentor_legacy', p."Revision", p."MeetingSessionCount", 0,
                       TIMESTAMP '2026-10-01 00:00:00', NOW(), FALSE
                FROM "SubscriptionPlans" p
                WHERE p."Id" = 7
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserSubscriptions" s
                      WHERE s."UserId" = learner_id
                        AND s."PaymentTransactionId" = 'DEMO-HUY-MENTOR-ACCESS'
                  );

                SELECT s."Id"
                INTO mentor_subscription_id
                FROM "UserSubscriptions" s
                WHERE s."UserId" = learner_id
                  AND s."PaymentTransactionId" = 'DEMO-HUY-MENTOR-ACCESS'
                  AND NOT s."IsDeleted"
                ORDER BY s."Id" DESC
                LIMIT 1;

                -- MentorAvailabilityConfig already seeds slot 990001. Add one
                -- learner booking to that slot and keep the slot counter in sync.
                IF mentor_subscription_id IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM "MentorAvailabilities" a
                       WHERE a."Id" = 990001 AND a."BookedCount" < a."MaxLearners"
                   )
                THEN
                    SELECT EXISTS (
                        SELECT 1 FROM "MentorBookings" b
                        WHERE b."UserSubscriptionId" = mentor_subscription_id
                          AND NOT b."IsDeleted"
                    ) INTO booking_exists;

                    IF NOT booking_exists THEN
                        INSERT INTO "MentorBookings"
                            ("UserSubscriptionId", "MentorAvailabilityId", "LearnerId", "Status",
                             "Question", "ScheduledAt", "MeetingLink", "IsDeleted", "CreatedDate")
                        VALUES
                            (mentor_subscription_id, 990001, learner_id, 'Assigned',
                             'Buổi mentor demo cho tài khoản kiểm thử.',
                             TIMESTAMP '2026-10-02 09:00:00',
                             'http://localhost:5173/mentor-meeting/a1000000-0000-0000-0000-000000990001',
                             FALSE, NOW());

                        UPDATE "MentorAvailabilities"
                        SET "BookedCount" = "BookedCount" + 1
                        WHERE "Id" = 990001 AND "BookedCount" < "MaxLearners";
                    END IF;
                END IF;
            END $grant_huy_full_access$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $revoke_huy_full_access$
            DECLARE
                learner_id integer;
                had_booking boolean;
            BEGIN
                SELECT "Id"
                INTO learner_id
                FROM "Users"
                WHERE lower(btrim("Email")) = lower('huydqse151428@fpt.edu.vn')
                ORDER BY "Id"
                LIMIT 1;

                IF learner_id IS NULL THEN
                    RETURN;
                END IF;

                SELECT EXISTS (
                    SELECT 1
                    FROM "MentorBookings" b
                    JOIN "UserSubscriptions" s ON s."Id" = b."UserSubscriptionId"
                    WHERE s."UserId" = learner_id
                      AND s."PaymentTransactionId" = 'DEMO-HUY-MENTOR-ACCESS'
                      AND b."MentorAvailabilityId" = 990001
                      AND NOT b."IsDeleted"
                ) INTO had_booking;

                DELETE FROM "MentorBookings" b
                USING "UserSubscriptions" s
                WHERE b."UserSubscriptionId" = s."Id"
                  AND s."UserId" = learner_id
                  AND s."PaymentTransactionId" = 'DEMO-HUY-MENTOR-ACCESS';

                IF had_booking THEN
                    UPDATE "MentorAvailabilities"
                    SET "BookedCount" = GREATEST(0, "BookedCount" - 1)
                    WHERE "Id" = 990001;
                END IF;

                DELETE FROM "UserSubscriptions"
                WHERE "UserId" = learner_id
                  AND "PaymentTransactionId" IN (
                      'DEMO-HUY-UNLIMITED-SELF-STUDY',
                      'DEMO-HUY-UNLIMITED-LEARNING-PATH',
                      'DEMO-HUY-MENTOR-ACCESS'
                  );
            END $revoke_huy_full_access$;
            """);
    }
}
