-- Run only on a database that received the superseded StudyCourse coaching migration.
-- Removes feedback, draft workflow metadata and the migration history entry from that implementation.
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116032424_AddStudyCourseCoaching') THEN
        DROP TABLE IF EXISTS "CourseFeedbackIssues";

        ALTER TABLE "StudyCourseSessions"
            DROP COLUMN IF EXISTS "Evaluation",
            DROP COLUMN IF EXISTS "EvaluationScore",
            DROP COLUMN IF EXISTS "EvaluatedAt";

        ALTER TABLE "StudyCourseHomework"
            DROP COLUMN IF EXISTS "SessionId",
            DROP COLUMN IF EXISTS "ScanReference",
            DROP COLUMN IF EXISTS "Score";

        ALTER TABLE "StudyCourses"
            DROP COLUMN IF EXISTS "CoachingOutcomeJson",
            DROP COLUMN IF EXISTS "RemediationPlan",
            DROP COLUMN IF EXISTS "RemediationAt",
            DROP COLUMN IF EXISTS "RemediationCompletedAt",
            DROP COLUMN IF EXISTS "MentorRating",
            DROP COLUMN IF EXISTS "MentorFeedback",
            DROP COLUMN IF EXISTS "MentorRatedAt";

        DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116032424_AddStudyCourseCoaching';
    END IF;
END;
$$;

COMMIT;
