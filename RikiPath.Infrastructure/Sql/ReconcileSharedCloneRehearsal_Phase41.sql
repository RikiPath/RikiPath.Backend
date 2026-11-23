-- Phase 4.1 Additive Forward-Only Schema Reconciliation & Clone Rehearsal Script
-- Target: Isolated QA / Rehearsal clone of Shared Supabase ONLY (refuses to execute on database 'postgres').
-- Properties:
--   1. Single atomic transaction (BEGIN; ... COMMIT;) — fails closed on any precondition or postcondition violation.
--   2. Additive and non-destructive to domain data (no table drops, no column drops, no row truncation).
--   3. Restores missing pre-#53 raw SQL objects (btree_gist, GiST exclusion constraint, deferrable UQs, 22 defaults)
--      AND applies canonical migrations #53-#60 schema + controlled data backfills.
--   4. Matches CertificateSections strictly by ("CertificationLevelId", "Code"), NEVER by hardcoded Id.
--   5. Verifies 100% structural & invariant parity BEFORE recording canonical IDs in "__EFMigrationsHistory".

BEGIN;
SET statement_timeout = '60s';

-- ============================================================================
-- 0. Safety Guard & Strict Preconditions (Fail-Closed)
-- ============================================================================
DO $safety$
BEGIN
    IF current_database() = 'postgres' THEN
        RAISE EXCEPTION 'ABORTED: Refusing to execute reconciliation rehearsal against Shared database "postgres". Use an isolated clone.';
    END IF;

    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'ABORTED: "__EFMigrationsHistory" does not exist.';
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname='public' AND NOT c.convalidated) THEN
        RAISE EXCEPTION 'ABORTED: Existing unvalidated (NOT VALID) constraints detected.';
    END IF;

    IF EXISTS (SELECT 1 FROM (SELECT lower(btrim("Name")) FROM "CertificateTypes" GROUP BY 1 HAVING count(*) > 1) s) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate normalized CertificateType names.';
    END IF;

    IF EXISTS (SELECT 1 FROM (SELECT "SortOrder" FROM "CertificateTypes" GROUP BY 1 HAVING count(*) > 1) s) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate CertificateTypes SortOrder.';
    END IF;

    IF EXISTS (SELECT 1 FROM (SELECT "CertificationId", upper(btrim("Code")) FROM "CertificateLevels" GROUP BY 1,2 HAVING count(*) > 1) s) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate normalized CertificateLevel codes.';
    END IF;

    IF EXISTS (SELECT 1 FROM (SELECT "CertificationId", "SortOrder" FROM "CertificateLevels" GROUP BY 1,2 HAVING count(*) > 1) s) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate CertificateLevels SortOrder.';
    END IF;

    IF EXISTS (SELECT 1 FROM "CertificateSections" WHERE NOT "IsDeleted" GROUP BY "CertificationLevelId", "Code" HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate active CertificateSections (CertificationLevelId, Code).';
    END IF;

    IF EXISTS (
        SELECT 1 FROM "MentorBookingReservations" a
        JOIN "MentorBookingReservations" b ON a."Id" < b."Id"
          AND a."MentorId" = b."MentorId"
          AND a."MentorAvailabilityId" = b."MentorAvailabilityId"
          AND a."ReleasedAtUtc" IS NULL AND b."ReleasedAtUtc" IS NULL
          AND tstzrange(a."StartUtc", a."RestProtectedUntilUtc", '[)') && tstzrange(b."StartUtc", b."RestProtectedUntilUtc", '[)')
    ) THEN
        RAISE EXCEPTION 'ABORTED: Active overlapping MentorBookingReservations detected.';
    END IF;

    IF EXISTS (SELECT 1 FROM "MentorBookingReservations" WHERE "ReleasedAtUtc" IS NULL AND "RestProtectedUntilUtc" <= "StartUtc") THEN
        RAISE EXCEPTION 'ABORTED: Active MentorBookingReservations with invalid RestProtectedUntilUtc <= StartUtc.';
    END IF;

    IF EXISTS (SELECT 1 FROM "StudyCohorts" WHERE "Capacity" <= 0) THEN
        RAISE EXCEPTION 'ABORTED: StudyCohorts with Capacity <= 0 exist.';
    END IF;

    IF EXISTS (SELECT 1 FROM "StudyCourses" WHERE "CohortId" IS NOT NULL AND "Status" = 'Active' GROUP BY "CohortId", "LearnerId" HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'ABORTED: Duplicate active cohort enrollments exist.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM "MentorBookings" b WHERE b."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = b."CreditGrantId")
    ) OR EXISTS (
        SELECT 1 FROM "MentorCreditEntries" e WHERE e."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = e."CreditGrantId")
    ) THEN
        RAISE EXCEPTION 'ABORTED: Orphan CreditGrantId references exist.';
    END IF;

    IF EXISTS (
        SELECT 1 FROM "MentorAvailabilities" m
        WHERE m."GenerationKey" IS NOT NULL
          AND m."MentorScheduleRuleId" IS NULL
          AND NULLIF(to_jsonb(m) ->> 'TeachingProposalId', '') IS NULL
    ) THEN
        RAISE EXCEPTION 'ABORTED: Generated MentorAvailabilities exist with null MentorScheduleRuleId and null TeachingProposalId.';
    END IF;
END $safety$;

-- ============================================================================
-- 1. Enable btree_gist Extension & Verify Search Path Visibility
-- ============================================================================
DO $ext$
DECLARE
    opclass_visible boolean;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist') THEN
        IF NOT EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'btree_gist') THEN
            RAISE EXCEPTION 'ABORTED: btree_gist extension unavailable.';
        END IF;
        CREATE EXTENSION IF NOT EXISTS btree_gist;
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM pg_opclass opc
        JOIN pg_am am ON am.oid = opc.opcmethod
        JOIN pg_namespace n ON n.oid = opc.opcnamespace
        WHERE am.amname = 'gist' AND opc.opcname = 'gist_int4_ops'
          AND n.nspname = ANY (current_schemas(false))
    ) INTO opclass_visible;

    IF NOT opclass_visible THEN
        RAISE EXCEPTION 'ABORTED: gist_int4_ops not visible in search_path (%).', current_setting('search_path');
    END IF;
END $ext$;

-- ============================================================================
-- 2. Restore 22 Missing Column DEFAULT Expressions & Pre-#53 Constraints/Indexes
-- ============================================================================
ALTER TABLE "CourseRetakeRequests" ALTER COLUMN "Reason" SET DEFAULT '';
ALTER TABLE "HomeworkSubmissions" ALTER COLUMN "RevisionCount" SET DEFAULT 0;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "AllowedSessionsPerWeekCsv" SET DEFAULT '2,3,4';
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "CheckoutHoldMinutes" SET DEFAULT 10;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "CourseDurationWeeks" SET DEFAULT 10;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "DefaultSessionsPerWeek" SET DEFAULT 3;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "EnrollmentCutoffMinutesBeforeFirstSession" SET DEFAULT 0;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "LearnerTransferEnabled" SET DEFAULT TRUE;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "MentorReplacementEnabled" SET DEFAULT TRUE;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "MentorReplacementLeadHours" SET DEFAULT 24;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "MinLearnersToStart" SET DEFAULT 1;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "RemediationThresholdComparison" SET DEFAULT '>';
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "RequireAdminRemediationReview" SET DEFAULT TRUE;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "RequireOfficialExamEvidence" SET DEFAULT TRUE;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "TransferAllowedAfterSourceStart" SET DEFAULT FALSE;
ALTER TABLE "MentorGroupPolicies" ALTER COLUMN "WeeklySessionCap" SET DEFAULT 0;
ALTER TABLE "RecommendedLearningPaths" ALTER COLUMN "Status" SET DEFAULT 'Superseded';
ALTER TABLE "RecommendedLearningPaths" ALTER COLUMN "Version" SET DEFAULT 0;
ALTER TABLE "StudyCohorts" ALTER COLUMN "CreatedAtUtc" SET DEFAULT '-infinity'::timestamp without time zone;
ALTER TABLE "StudyCourses" ALTER COLUMN "Currency" SET DEFAULT 'VND';
ALTER TABLE "Users" ALTER COLUMN "LearningMode" SET DEFAULT 'Standard';
ALTER TABLE "Users" ALTER COLUMN "PlacementDecision" SET DEFAULT 'NotTaken';

DO $pre53$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public."CertificateTypes"'::regclass AND conname = 'UQ_CertificateTypes_SortOrder') THEN
        ALTER TABLE "CertificateTypes" ADD CONSTRAINT "UQ_CertificateTypes_SortOrder" UNIQUE ("SortOrder") DEFERRABLE INITIALLY DEFERRED;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public."CertificateLevels"'::regclass AND conname = 'UQ_CertificateLevels_CertificationId_SortOrder') THEN
        ALTER TABLE "CertificateLevels" ADD CONSTRAINT "UQ_CertificateLevels_CertificationId_SortOrder" UNIQUE ("CertificationId", "SortOrder") DEFERRABLE INITIALLY DEFERRED;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'public."MentorBookingReservations"'::regclass AND conname = 'EX_MentorBookingReservation_ActiveMentorRest') THEN
        ALTER TABLE "MentorBookingReservations"
            ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
            EXCLUDE USING gist ("MentorId" WITH =, "MentorAvailabilityId" WITH <>, tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&)
            WHERE ("ReleasedAtUtc" IS NULL);
    END IF;
END $pre53$;

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_CertificateTypes_NormalizedName"
    ON "CertificateTypes" (lower(btrim("Name"::text)));
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_CertificateLevels_CertificationId_NormalizedCode"
    ON "CertificateLevels" ("CertificationId", upper(btrim("Code"::text)));
CREATE INDEX IF NOT EXISTS "IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtUt"
    ON "MentorOutboxEvents" ("DeliveredAtUtc", "AvailableAtUtc", "CreatedAtUtc");

-- ============================================================================
-- 3. Canonical Migrations #53-#60 Schema, Backfills & Indexes (Idempotent)
-- ============================================================================
ALTER TABLE "MentorAvailabilities" DROP CONSTRAINT IF EXISTS "CK_MentorAvailability_GeneratedSlot";

ALTER TABLE "StudyCohorts" ADD COLUMN IF NOT EXISTS "SourceProposalId" integer;

ALTER TABLE "MentorAvailabilities" ADD COLUMN IF NOT EXISTS "TeachingProposalId" integer;

CREATE TABLE IF NOT EXISTS "MentorTeachingProposals" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "MentorId" integer NOT NULL,
    "CertificateLevelId" integer,
    "ProgramType" character varying(32) NOT NULL,
    "CoursePlanId" integer,
    "CourseTitle" character varying(200),
    "CourseObjective" character varying(1500),
    "ExpectedStartDate" date,
    "CourseDurationWeeks" integer NOT NULL,
    "SessionDurationMinutes" integer NOT NULL,
    "WeeklySessionsJson" jsonb NOT NULL,
    "OptionalNotes" character varying(2000),
    "OptionalAttachmentsJson" jsonb NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Revision" integer NOT NULL,
    "SubmissionIdempotencyKey" character varying(128),
    "SubmittedAtUtc" timestamp with time zone,
    "ReviewedAtUtc" timestamp with time zone,
    "ReviewedByAdminId" integer,
    "ReviewReason" character varying(1500),
    "GroupPolicyId" integer,
    "PolicyVersionSnapshot" integer,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    "LastSavedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorTeachingProposals" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorTeachingProposal_Duration" CHECK ("CourseDurationWeeks" BETWEEN 1 AND 52 AND "SessionDurationMinutes" BETWEEN 30 AND 240),
    CONSTRAINT "CK_MentorTeachingProposal_ProgramType" CHECK ("ProgramType" IN ('StandardJlpt', 'IntensiveJlpt', 'ExamPrepJlpt', 'CustomJlpt')),
    CONSTRAINT "CK_MentorTeachingProposal_Revision" CHECK ("Revision" > 0),
    CONSTRAINT "CK_MentorTeachingProposal_Status" CHECK ("Status" IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn')),
    CONSTRAINT "FK_MentorTeachingProposals_CertificateLevels_CertificateLevelId" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingProposals_MentorGroupPolicies_GroupPolicyId" FOREIGN KEY ("GroupPolicyId") REFERENCES "MentorGroupPolicies" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingProposals_StudyCoursePlans_CoursePlanId" FOREIGN KEY ("CoursePlanId") REFERENCES "StudyCoursePlans" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingProposals_Users_MentorId" FOREIGN KEY ("MentorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingProposals_Users_ReviewedByAdminId" FOREIGN KEY ("ReviewedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorTeachingProposalEvents" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "ProposalId" integer NOT NULL,
    "ActorUserId" integer NOT NULL,
    "ActorRole" character varying(32) NOT NULL,
    "Action" character varying(48) NOT NULL,
    "FromStatus" character varying(32),
    "ToStatus" character varying(32),
    "RevisionAfter" integer NOT NULL,
    "Reason" character varying(1500),
    "SnapshotJson" jsonb,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorTeachingProposalEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorTeachingProposalEvent_RevisionAfter" CHECK ("RevisionAfter" > 0),
    CONSTRAINT "FK_MentorTeachingProposalEvents_MentorTeachingProposals_Propos~" FOREIGN KEY ("ProposalId") REFERENCES "MentorTeachingProposals" ("Id") ON DELETE CASCADE
);

UPDATE "MentorAvailabilities" SET "TeachingProposalId" = NULL
WHERE "Id" = 990001;

UPDATE "MentorAvailabilities" SET "TeachingProposalId" = NULL
WHERE "Id" = 990002;

UPDATE "MentorAvailabilities" SET "TeachingProposalId" = NULL
WHERE "Id" = 990003;

UPDATE "MentorAvailabilities" SET "TeachingProposalId" = NULL
WHERE "Id" = 990004;

CREATE INDEX IF NOT EXISTS "IX_StudyCohorts_SourceProposalId" ON "StudyCohorts" ("SourceProposalId") WHERE "SourceProposalId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_MentorAvailabilities_TeachingProposalId" ON "MentorAvailabilities" ("TeachingProposalId") WHERE "TeachingProposalId" IS NOT NULL;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."MentorAvailabilities"') AND conname = 'CK_MentorAvailability_GeneratedSlot') THEN
        ALTER TABLE "MentorAvailabilities" ADD CONSTRAINT "CK_MentorAvailability_GeneratedSlot" CHECK (("GenerationKey" IS NULL AND "PolicyVersion" IS NULL AND "MentorScheduleRuleId" IS NULL AND "LocalBusinessDate" IS NULL AND "GroupPolicyId" IS NULL AND "SessionMinutesSnapshot" IS NULL AND "RestMinutesSnapshot" IS NULL) OR ("GenerationKey" IS NOT NULL AND "PolicyVersion" IS NOT NULL AND "PolicyVersion" > 0 AND ("MentorScheduleRuleId" IS NOT NULL OR "TeachingProposalId" IS NOT NULL) AND "LocalBusinessDate" IS NOT NULL AND (("GroupPolicyId" IS NULL AND "SessionMinutesSnapshot" IS NULL AND "RestMinutesSnapshot" IS NULL AND "MaxLearners" = 1 AND "EndTime" = "StartTime" + INTERVAL '90 minutes') OR ("GroupPolicyId" IS NOT NULL AND "SessionMinutesSnapshot" > 0 AND "RestMinutesSnapshot" >= 0 AND "MaxLearners" BETWEEN 1 AND 4 AND "EndTime" = "StartTime" + "SessionMinutesSnapshot" * INTERVAL '1 minute'))));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposalEvents_ProposalId_CreatedAtUtc" ON "MentorTeachingProposalEvents" ("ProposalId", "CreatedAtUtc");

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_CertificateLevelId" ON "MentorTeachingProposals" ("CertificateLevelId");

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_CoursePlanId" ON "MentorTeachingProposals" ("CoursePlanId");

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_GroupPolicyId" ON "MentorTeachingProposals" ("GroupPolicyId");

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_MentorId_Status_UpdatedAtUtc" ON "MentorTeachingProposals" ("MentorId", "Status", "UpdatedAtUtc");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_MentorId_SubmissionIdempotencyKey" ON "MentorTeachingProposals" ("MentorId", "SubmissionIdempotencyKey") WHERE "SubmissionIdempotencyKey" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_ReviewedByAdminId" ON "MentorTeachingProposals" ("ReviewedByAdminId");

CREATE INDEX IF NOT EXISTS "IX_MentorTeachingProposals_Status_SubmittedAtUtc" ON "MentorTeachingProposals" ("Status", "SubmittedAtUtc");

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."MentorAvailabilities"') AND conname = 'FK_MentorAvailabilities_MentorTeachingProposals_TeachingPropos~') THEN
        ALTER TABLE "MentorAvailabilities" ADD CONSTRAINT "FK_MentorAvailabilities_MentorTeachingProposals_TeachingPropos~" FOREIGN KEY ("TeachingProposalId") REFERENCES "MentorTeachingProposals" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."StudyCohorts"') AND conname = 'FK_StudyCohorts_MentorTeachingProposals_SourceProposalId') THEN
        ALTER TABLE "StudyCohorts" ADD CONSTRAINT "FK_StudyCohorts_MentorTeachingProposals_SourceProposalId" FOREIGN KEY ("SourceProposalId") REFERENCES "MentorTeachingProposals" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

-- (superseded index IX_ReviewHistories_ReviewItemId dropped after IX_ReviewHistories_ReviewItemId_ReviewedAt)

DROP INDEX IF EXISTS "IX_ReviewCards_UserId_KanjiEntryId_Mode";

ALTER TABLE "Vocabularies" ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'Published';

ALTER TABLE "Vocabularies" ADD COLUMN IF NOT EXISTS "UnpublishedAtUtc" timestamp without time zone;

ALTER TABLE "Vocabularies" ADD COLUMN IF NOT EXISTS "UnpublishedReason" character varying(500);

ALTER TABLE "Vocabularies" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "IdempotencyKey" character varying(120);

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "IsCorrect" boolean;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "NewEaseFactor" double precision NOT NULL DEFAULT 2.5;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "NewIntervalDays" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "NewState" character varying(20) NOT NULL DEFAULT 'Learning';

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "PreviousEaseFactor" double precision NOT NULL DEFAULT 2.5;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "PreviousIntervalDays" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "PreviousState" character varying(20) NOT NULL DEFAULT 'New';

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "ResponseTimeMs" integer;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "StudyMode" character varying(20) NOT NULL DEFAULT 'flip';

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "UserAnswer" character varying(500);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "AcceptedReadingsJson" character varying(2000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "ArchivedAt" timestamp without time zone;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "AudioUrl" character varying(1000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "BackText" character varying(1000) NOT NULL DEFAULT '';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "CardType" character varying(30) NOT NULL DEFAULT 'Vocabulary';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "ContextSnippet" character varying(1000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "CorrectCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "ExampleMeaning" character varying(1000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "ExampleSentence" character varying(1000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "FrontText" character varying(500) NOT NULL DEFAULT '';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "IncorrectCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "JlptLevel" character varying(10);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "Lapses" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "LearningState" character varying(20) NOT NULL DEFAULT 'New';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "LessonId" integer;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "MasteredAt" timestamp without time zone;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "Meaning" character varying(500);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "NormalizedContentKey" character varying(300) NOT NULL DEFAULT '';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "PersonalNote" character varying(1000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "Reading" character varying(300);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "Revision" integer NOT NULL DEFAULT 1;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SourceId" integer;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SourceLabel" character varying(300);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SourceLinksJson" character varying(4000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SourceType" character varying(40) NOT NULL DEFAULT 'Manual';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SourceVersion" integer NOT NULL DEFAULT 1;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SrsCardTemplateId" integer;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'Active';

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SuspendedAt" timestamp without time zone;

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "SynonymsJson" character varying(2000);

ALTER TABLE "ReviewCards" ADD COLUMN IF NOT EXISTS "VocabularyId" integer;

ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'Published';

ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "UnpublishedAtUtc" timestamp without time zone;

ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "UnpublishedReason" character varying(500);

ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;

ALTER TABLE "LessonProgresses" ADD COLUMN IF NOT EXISTS "CompletedAt" timestamp without time zone;

ALTER TABLE "LessonProgresses" ADD COLUMN IF NOT EXISTS "FirstViewedAt" timestamp without time zone;

ALTER TABLE "LessonProgresses" ADD COLUMN IF NOT EXISTS "InteractionCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "LessonProgresses" ADD COLUMN IF NOT EXISTS "LastInteractedAt" timestamp without time zone;

ALTER TABLE "Kanjis" ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'Published';

ALTER TABLE "Kanjis" ADD COLUMN IF NOT EXISTS "UnpublishedAtUtc" timestamp without time zone;

ALTER TABLE "Kanjis" ADD COLUMN IF NOT EXISTS "UnpublishedReason" character varying(500);

ALTER TABLE "Kanjis" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;

ALTER TABLE "GrammarPatterns" ADD COLUMN IF NOT EXISTS "Status" character varying(20) NOT NULL DEFAULT 'Published';

ALTER TABLE "GrammarPatterns" ADD COLUMN IF NOT EXISTS "UnpublishedAtUtc" timestamp without time zone;

ALTER TABLE "GrammarPatterns" ADD COLUMN IF NOT EXISTS "UnpublishedReason" character varying(500);

ALTER TABLE "GrammarPatterns" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;

CREATE TABLE IF NOT EXISTS "LearnerHighlights" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "UserId" integer NOT NULL,
    "TargetType" character varying(40) NOT NULL,
    "TargetId" integer,
    "TargetKey" character varying(200),
    "SourcePath" character varying(300),
    "SourceTitle" character varying(200),
    "SelectedText" character varying(1000) NOT NULL,
    "Color" character varying(20) NOT NULL,
    "AnchorPrefix" character varying(200),
    "AnchorSuffix" character varying(200),
    "StartOffset" integer,
    "EndOffset" integer,
    "ContentVersionHash" character varying(128),
    "Note" character varying(1000),
    "TranslationSnapshot" character varying(1000),
    "LinkedReviewCardId" integer,
    "LinkedNoteEntryId" integer,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_LearnerHighlights" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_LearnerHighlights_LearnerNoteEntries_LinkedNoteEntryId" FOREIGN KEY ("LinkedNoteEntryId") REFERENCES "LearnerNoteEntries" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_LearnerHighlights_ReviewCards_LinkedReviewCardId" FOREIGN KEY ("LinkedReviewCardId") REFERENCES "ReviewCards" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_LearnerHighlights_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "SrsCardTemplates" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "CardType" character varying(30) NOT NULL,
    "FrontText" character varying(500) NOT NULL,
    "BackText" character varying(1000) NOT NULL,
    "Reading" character varying(300),
    "Meaning" character varying(500),
    "ExampleSentence" character varying(1000),
    "ExampleMeaning" character varying(1000),
    "UsageNotes" character varying(2000),
    "AcceptedReadingsJson" character varying(2000),
    "SynonymsJson" character varying(2000),
    "AudioUrl" character varying(1000),
    "CertificateLevelId" integer,
    "JlptLevel" character varying(10),
    "LessonId" integer,
    "VocabularyId" integer,
    "KanjiId" integer,
    "GrammarPatternId" integer,
    "Status" character varying(24) NOT NULL,
    "Version" integer NOT NULL DEFAULT 1,
    "SubmittedByUserId" integer,
    "ReviewedByAdminId" integer,
    "SubmittedAtUtc" timestamp without time zone,
    "ReviewedAtUtc" timestamp without time zone,
    "ReviewNote" character varying(1000),
    "UnpublishedAtUtc" timestamp without time zone,
    "UnpublishedReason" character varying(500),
    "NormalizedContentKey" character varying(300) NOT NULL,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_SrsCardTemplates" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SrsCardTemplates_CertificateLevels_CertificateLevelId" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_GrammarPatterns_GrammarPatternId" FOREIGN KEY ("GrammarPatternId") REFERENCES "GrammarPatterns" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_Kanjis_KanjiId" FOREIGN KEY ("KanjiId") REFERENCES "Kanjis" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_Lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES "Lessons" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_Users_ReviewedByAdminId" FOREIGN KEY ("ReviewedByAdminId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_Users_SubmittedByUserId" FOREIGN KEY ("SubmittedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_SrsCardTemplates_Vocabularies_VocabularyId" FOREIGN KEY ("VocabularyId") REFERENCES "Vocabularies" ("Id") ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS "UserSrsPreferences" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "UserId" integer NOT NULL,
    "AutoEnrollAfterLesson" boolean NOT NULL,
    "DailyNewLimit" integer NOT NULL,
    "DailyReviewLimit" integer NOT NULL,
    "PreferredReviewMode" character varying(20) NOT NULL,
    "AutoPlayAudio" boolean NOT NULL,
    "ShowFuriganaHint" boolean NOT NULL,
    "DailyReminderEnabled" boolean NOT NULL,
    "ReminderHour" integer NOT NULL,
    "ReminderMinute" integer NOT NULL,
    "ReminderFrequency" character varying(30) NOT NULL,
    "ReminderDaysJson" character varying(100),
    "EmailReminderEnabled" boolean NOT NULL,
    "TimezoneId" character varying(100) NOT NULL,
    "PreferredLocale" character varying(10) NOT NULL,
    "LastReminderSentAtUtc" timestamp without time zone,
    "LastReminderSentLocalDate" date,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_UserSrsPreferences" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserSrsPreferences_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910001;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910002;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910003;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910004;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910005;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910006;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910007;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910008;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910009;

UPDATE "Kanjis" SET "Status" = 'Published', "UnpublishedAtUtc" = NULL, "UnpublishedReason" = NULL, "Version" = 1
WHERE "Id" = 910010;

CREATE INDEX IF NOT EXISTS "IX_Vocabularies_Status" ON "Vocabularies" ("Status");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReviewHistories_ReviewItemId_IdempotencyKey" ON "ReviewHistories" ("ReviewItemId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_ReviewHistories_ReviewItemId_ReviewedAt" ON "ReviewHistories" ("ReviewItemId", "ReviewedAt");

-- Deduplicate any legacy duplicate ReviewCards before creating strict filtered unique indexes.
WITH ranked_grammar AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "GrammarPointId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "GrammarPointId" IS NOT NULL
)
UPDATE "ReviewHistories" rh
SET "ReviewItemId" = rg.keep_id
FROM ranked_grammar rg
WHERE rh."ReviewItemId" = rg."Id" AND rg."Id" <> rg.keep_id;

WITH ranked_grammar AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "GrammarPointId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "GrammarPointId" IS NOT NULL
)
DELETE FROM "ReviewCards" rc
USING ranked_grammar rg
WHERE rc."Id" = rg."Id" AND rg."Id" <> rg.keep_id;

-- Backfill VocabularyId and KanjiEntryId from linked LearnerNoteEntries when absent.
UPDATE "ReviewCards" rc
SET "VocabularyId" = COALESCE(rc."VocabularyId", lne."VocabularyEntryId"),
    "KanjiEntryId" = COALESCE(rc."KanjiEntryId", lne."KanjiEntryId"),
    "CardType" = CASE
        WHEN COALESCE(rc."KanjiEntryId", lne."KanjiEntryId") IS NOT NULL THEN 'Kanji'
        WHEN rc."GrammarPointId" IS NOT NULL THEN 'Grammar'
        ELSE 'Vocabulary'
    END,
    "SourceType" = CASE
        WHEN rc."VocabularyNoteEntryId" IS NOT NULL THEN 'Notebook'
        WHEN rc."KanjiEntryId" IS NOT NULL THEN 'Kanji'
        WHEN rc."GrammarPointId" IS NOT NULL THEN 'Grammar'
        ELSE 'Manual'
    END
FROM "LearnerNoteEntries" lne
WHERE rc."VocabularyNoteEntryId" = lne."Id";

UPDATE "ReviewCards" rc
SET "CardType" = 'Kanji',
    "SourceType" = CASE WHEN rc."SourceType" = 'Manual' THEN 'Kanji' ELSE rc."SourceType" END
WHERE rc."VocabularyNoteEntryId" IS NULL AND rc."KanjiEntryId" IS NOT NULL;

UPDATE "ReviewCards" rc
SET "CardType" = 'Grammar',
    "SourceType" = CASE WHEN rc."SourceType" = 'Manual' THEN 'Grammar' ELSE rc."SourceType" END
WHERE rc."VocabularyNoteEntryId" IS NULL AND rc."GrammarPointId" IS NOT NULL;

-- Backfill snapshot FrontText/BackText/Reading/Meaning from canonical content banks & notes.
UPDATE "ReviewCards" rc
SET "FrontText" = v."Word",
    "Reading" = v."Reading",
    "Meaning" = v."Meaning",
    "BackText" = v."Meaning",
    "ExampleSentence" = v."ExampleSentence",
    "ExampleMeaning" = v."ExampleSentenceMeaning",
    "AudioUrl" = v."AudioUrl",
    "NormalizedContentKey" = 'vocabulary:' || v."Id"::text
FROM "Vocabularies" v
WHERE rc."VocabularyId" = v."Id" AND rc."FrontText" = '';

UPDATE "ReviewCards" rc
SET "FrontText" = k."Character",
    "Reading" = COALESCE(NULLIF(k."OnYomi", ''), NULLIF(k."KunYomi", ''), k."SinoVietnamese"),
    "Meaning" = k."Meaning",
    "BackText" = k."Meaning",
    "AudioUrl" = k."AudioUrl",
    "NormalizedContentKey" = 'kanji:' || k."Id"::text
FROM "Kanjis" k
WHERE rc."KanjiEntryId" = k."Id" AND rc."FrontText" = '';

UPDATE "ReviewCards" rc
SET "FrontText" = g."Title",
    "Reading" = g."Structure",
    "Meaning" = COALESCE(NULLIF(g."UsageNotes", ''), g."Structure"),
    "BackText" = COALESCE(NULLIF(g."UsageNotes", ''), g."Structure"),
    "ExampleSentence" = g."ExampleSentence",
    "ExampleMeaning" = g."ExampleSentenceMeaning",
    "NormalizedContentKey" = 'grammar:' || g."Id"::text
FROM "GrammarPatterns" g
WHERE rc."GrammarPointId" = g."Id" AND rc."FrontText" = '';

UPDATE "ReviewCards" rc
SET "FrontText" = COALESCE(NULLIF(lne."ManualWord", ''), 'Card #' || rc."Id"::text),
    "Reading" = lne."ManualReading",
    "Meaning" = lne."ManualMeaning",
    "BackText" = COALESCE(NULLIF(lne."ManualMeaning", ''), NULLIF(lne."Note", ''), '-'),
    "PersonalNote" = lne."Note",
    "NormalizedContentKey" = 'noteentry:' || lne."Id"::text
FROM "LearnerNoteEntries" lne
WHERE rc."VocabularyNoteEntryId" = lne."Id" AND rc."FrontText" = '';

UPDATE "ReviewCards"
SET "LearningState" = CASE
    WHEN "Repetitions" >= 3 AND "IntervalDays" >= 21 AND "EaseFactor" >= 2.2 THEN 'Mastered'
    WHEN "Repetitions" >= 2 THEN 'Review'
    WHEN "LastReviewedAt" IS NOT NULL THEN 'Learning'
    ELSE 'New'
END,
"Status" = CASE WHEN "IsDeleted" THEN 'Archived' ELSE 'Active' END,
"MasteredAt" = CASE WHEN "Repetitions" >= 3 AND "IntervalDays" >= 21 AND "EaseFactor" >= 2.2 THEN COALESCE("LastReviewedAt", NOW()) ELSE NULL END,
"ArchivedAt" = CASE WHEN "IsDeleted" THEN COALESCE("ModifiedDate", NOW()) ELSE NULL END;

UPDATE "LessonProgresses"
SET "FirstViewedAt" = COALESCE("FirstViewedAt", "CreatedDate", "LastWatchedAt"),
    "CompletedAt" = CASE WHEN "IsCompleted" THEN COALESCE("CompletedAt", "LastWatchedAt", "ModifiedDate", "CreatedDate") ELSE NULL END;

-- Merge any duplicate VocabularyId / KanjiEntryId / NormalizedContentKey cards produced after backfilling from multiple note entries.
WITH ranked_vocab AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "VocabularyId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "VocabularyId" IS NOT NULL
)
UPDATE "ReviewHistories" rh
SET "ReviewItemId" = rv.keep_id
FROM ranked_vocab rv
WHERE rh."ReviewItemId" = rv."Id" AND rv."Id" <> rv.keep_id;

WITH ranked_vocab AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "VocabularyId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "VocabularyId" IS NOT NULL
)
DELETE FROM "ReviewCards" rc
USING ranked_vocab rv
WHERE rc."Id" = rv."Id" AND rv."Id" <> rv.keep_id;

WITH ranked_kanji AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "KanjiEntryId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "KanjiEntryId" IS NOT NULL
)
UPDATE "ReviewHistories" rh
SET "ReviewItemId" = rk.keep_id
FROM ranked_kanji rk
WHERE rh."ReviewItemId" = rk."Id" AND rk."Id" <> rk.keep_id;

WITH ranked_kanji AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "KanjiEntryId", "Mode"
               ORDER BY "IsDeleted" ASC, "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "KanjiEntryId" IS NOT NULL
)
DELETE FROM "ReviewCards" rc
USING ranked_kanji rk
WHERE rc."Id" = rk."Id" AND rk."Id" <> rk.keep_id;

WITH ranked_key AS (
    SELECT "Id",
           FIRST_VALUE("Id") OVER (
               PARTITION BY "UserId", "NormalizedContentKey", "Mode"
               ORDER BY "Repetitions" DESC, "Id" ASC
           ) AS keep_id
    FROM "ReviewCards"
    WHERE "NormalizedContentKey" <> '' AND "IsDeleted" = false
)
UPDATE "ReviewCards" rc
SET "IsDeleted" = true,
    "Status" = 'Archived',
    "ArchivedAt" = NOW()
FROM ranked_key rk
WHERE rc."Id" = rk."Id" AND rk."Id" <> rk.keep_id;

CREATE INDEX IF NOT EXISTS "IX_ReviewCards_LessonId" ON "ReviewCards" ("LessonId");

CREATE INDEX IF NOT EXISTS "IX_ReviewCards_SrsCardTemplateId" ON "ReviewCards" ("SrsCardTemplateId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReviewCards_UserId_GrammarPointId_Mode" ON "ReviewCards" ("UserId", "GrammarPointId", "Mode") WHERE "GrammarPointId" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReviewCards_UserId_KanjiEntryId_Mode" ON "ReviewCards" ("UserId", "KanjiEntryId", "Mode") WHERE "KanjiEntryId" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReviewCards_UserId_NormalizedContentKey_Mode" ON "ReviewCards" ("UserId", "NormalizedContentKey", "Mode") WHERE "NormalizedContentKey" <> '' AND "IsDeleted" = false;

CREATE INDEX IF NOT EXISTS "IX_ReviewCards_UserId_Status_LearningState_NextReviewDate" ON "ReviewCards" ("UserId", "Status", "LearningState", "NextReviewDate");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReviewCards_UserId_VocabularyId_Mode" ON "ReviewCards" ("UserId", "VocabularyId", "Mode") WHERE "VocabularyId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_ReviewCards_VocabularyId" ON "ReviewCards" ("VocabularyId");

CREATE INDEX IF NOT EXISTS "IX_Lessons_Status" ON "Lessons" ("Status");

CREATE INDEX IF NOT EXISTS "IX_Kanjis_Status" ON "Kanjis" ("Status");

CREATE INDEX IF NOT EXISTS "IX_GrammarPatterns_Status" ON "GrammarPatterns" ("Status");

CREATE INDEX IF NOT EXISTS "IX_LearnerHighlights_LinkedNoteEntryId" ON "LearnerHighlights" ("LinkedNoteEntryId");

CREATE INDEX IF NOT EXISTS "IX_LearnerHighlights_LinkedReviewCardId" ON "LearnerHighlights" ("LinkedReviewCardId");

CREATE INDEX IF NOT EXISTS "IX_LearnerHighlights_UserId_CreatedDate" ON "LearnerHighlights" ("UserId", "CreatedDate");

CREATE INDEX IF NOT EXISTS "IX_LearnerHighlights_UserId_TargetKey" ON "LearnerHighlights" ("UserId", "TargetKey");

CREATE INDEX IF NOT EXISTS "IX_LearnerHighlights_UserId_TargetType_TargetId" ON "LearnerHighlights" ("UserId", "TargetType", "TargetId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_CertificateLevelId" ON "SrsCardTemplates" ("CertificateLevelId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_GrammarPatternId" ON "SrsCardTemplates" ("GrammarPatternId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_KanjiId" ON "SrsCardTemplates" ("KanjiId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_LessonId" ON "SrsCardTemplates" ("LessonId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_NormalizedContentKey" ON "SrsCardTemplates" ("NormalizedContentKey");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_ReviewedByAdminId" ON "SrsCardTemplates" ("ReviewedByAdminId");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_Status_JlptLevel_CardType" ON "SrsCardTemplates" ("Status", "JlptLevel", "CardType");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_SubmittedByUserId_Status" ON "SrsCardTemplates" ("SubmittedByUserId", "Status");

CREATE INDEX IF NOT EXISTS "IX_SrsCardTemplates_VocabularyId" ON "SrsCardTemplates" ("VocabularyId");

CREATE INDEX IF NOT EXISTS "IX_UserSrsPreferences_DailyReminderEnabled_LastReminderSentLoc~" ON "UserSrsPreferences" ("DailyReminderEnabled", "LastReminderSentLocalDate");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserSrsPreferences_UserId" ON "UserSrsPreferences" ("UserId");

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."ReviewCards"') AND conname = 'FK_ReviewCards_Lessons_LessonId') THEN
        ALTER TABLE "ReviewCards" ADD CONSTRAINT "FK_ReviewCards_Lessons_LessonId" FOREIGN KEY ("LessonId") REFERENCES "Lessons" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."ReviewCards"') AND conname = 'FK_ReviewCards_SrsCardTemplates_SrsCardTemplateId') THEN
        ALTER TABLE "ReviewCards" ADD CONSTRAINT "FK_ReviewCards_SrsCardTemplates_SrsCardTemplateId" FOREIGN KEY ("SrsCardTemplateId") REFERENCES "SrsCardTemplates" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."ReviewCards"') AND conname = 'FK_ReviewCards_Vocabularies_VocabularyId') THEN
        ALTER TABLE "ReviewCards" ADD CONSTRAINT "FK_ReviewCards_Vocabularies_VocabularyId" FOREIGN KEY ("VocabularyId") REFERENCES "Vocabularies" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

ALTER TABLE "ReviewHistories" ADD COLUMN IF NOT EXISTS "SessionReference" character varying(100);

ALTER TABLE "MockTestSections" ADD COLUMN IF NOT EXISTS "DurationMinutes" integer;

ALTER TABLE "MockTestSections" ADD COLUMN IF NOT EXISTS "Instructions" character varying(1000);

ALTER TABLE "MockTestSections" ADD COLUMN IF NOT EXISTS "SectionPolicyJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "AiProvenanceJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "AuthorUserId" integer;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "BlueprintPolicyJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ExamKind" integer NOT NULL DEFAULT 0;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "FocusSkillId" integer;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "IncludedSkillsJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "IsArchivedVersion" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "IsManagedWorkflow" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ParentExamId" integer;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "PublishStatus" integer NOT NULL DEFAULT 0;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "PublishedAt" timestamp without time zone;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ReviewHistoryJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ReviewNote" text;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ReviewedAt" timestamp without time zone;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "ReviewedByName" text;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "RightsStatus" character varying(100);

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "SourceReference" character varying(300);

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "SourceType" character varying(40);

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "SubmittedAt" timestamp without time zone;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "TagsJson" jsonb;

ALTER TABLE "MockTests" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 0;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "PassageId" integer;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "PlaybackPolicyJson" jsonb;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "QuestionBankItemId" integer;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "QuestionGroupTitle" character varying(200);

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "QuestionVersion" integer NOT NULL DEFAULT 0;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "SkillMetadataJson" jsonb;

ALTER TABLE "MockQuestions" ADD COLUMN IF NOT EXISTS "TemplateKind" character varying(60);

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "AiProvenanceJson" jsonb;

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "CertificateLevelId" integer;

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "IdempotencyKey" character varying(100);

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "LanguageSkillId" integer;

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "ReviewHistoryJson" jsonb;

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "RightsConfirmed" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "RightsStatus" character varying(100);

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "SourceReference" character varying(300);

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "SourceType" character varying(40) NOT NULL DEFAULT 'ImportExcel';

ALTER TABLE "ContentApprovalRequests" ADD COLUMN IF NOT EXISTS "ValidationSummaryJson" jsonb;

CREATE TABLE IF NOT EXISTS "ContentPassages" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "PassageIdentity" uuid NOT NULL,
    "PassageCode" character varying(80) NOT NULL,
    "LanguageSkillId" integer NOT NULL,
    "CertificateLevelId" integer NOT NULL,
    "Title" character varying(300) NOT NULL,
    "Topic" character varying(200),
    "PassageText" text,
    "Instructions" character varying(1000),
    "ListeningSection" character varying(120),
    "AudioUrl" text,
    "AudioDurationSeconds" integer,
    "AudioTranscript" text,
    "ImageUrl" text,
    "PlaybackPolicyJson" jsonb,
    "SourceType" character varying(40) NOT NULL DEFAULT 'Manual',
    "SourceReference" character varying(300),
    "RightsStatus" character varying(100),
    "AiProvenanceJson" jsonb,
    "ContentFingerprint" character varying(64) NOT NULL,
    "PublishStatus" integer NOT NULL,
    "Version" integer NOT NULL,
    "ParentPassageId" integer,
    "IsArchivedVersion" boolean NOT NULL,
    "AuthorUserId" integer,
    "SubmittedAt" timestamp without time zone,
    "ReviewedAt" timestamp without time zone,
    "PublishedAt" timestamp without time zone,
    "ReviewedByName" text,
    "ReviewNote" text,
    "ReviewHistoryJson" jsonb,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_ContentPassages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ContentPassages_CertificateLevels_CertificateLevelId" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ContentPassages_LanguageSkills_LanguageSkillId" FOREIGN KEY ("LanguageSkillId") REFERENCES "LanguageSkills" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "SrsReviewSessions" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "SessionKey" uuid NOT NULL,
    "UserId" integer NOT NULL,
    "StudyMode" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "FilterCardType" character varying(30),
    "FilterJlptLevel" character varying(10),
    "FilterSourceType" character varying(40),
    "RequestedCount" integer NOT NULL,
    "TotalInitialCards" integer NOT NULL,
    "QueueCardIdsJson" character varying(4000) NOT NULL,
    "CompletedCardIdsJson" character varying(4000) NOT NULL,
    "RetryCardIdsJson" character varying(4000) NOT NULL,
    "ReviewedCount" integer NOT NULL,
    "RememberedCount" integer NOT NULL,
    "ForgotCount" integer NOT NULL,
    "StartedAtUtc" timestamp without time zone NOT NULL,
    "LastActivityAtUtc" timestamp without time zone NOT NULL,
    "CompletedAtUtc" timestamp without time zone,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_SrsReviewSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SrsReviewSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "QuestionBankItems" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "QuestionIdentity" uuid NOT NULL,
    "CertificateLevelId" integer NOT NULL,
    "LanguageSkillId" integer NOT NULL,
    "PassageId" integer,
    "QuestionGroupTitle" character varying(200),
    "GroupSortOrder" integer NOT NULL,
    "TemplateKind" character varying(60) NOT NULL DEFAULT 'VocabMeaning',
    "QuestionType" character varying(40) NOT NULL DEFAULT 'SingleChoice',
    "QuestionText" text NOT NULL,
    "SkillMetadataJson" jsonb,
    "InteractionDataJson" jsonb,
    "AnswerKeyJson" jsonb,
    "OptionsJson" jsonb,
    "AudioUrl" text,
    "AudioDurationSeconds" integer,
    "AudioTranscript" text,
    "PlaybackPolicyJson" jsonb,
    "ImageUrl" text,
    "Explanation" text,
    "Difficulty" character varying(30),
    "Topic" character varying(150),
    "TagsJson" jsonb,
    "SourceType" character varying(40) NOT NULL DEFAULT 'Manual',
    "SourceReference" character varying(300),
    "RightsStatus" character varying(100),
    "AiProvenanceJson" jsonb,
    "ContentFingerprint" character varying(64) NOT NULL,
    "PublishStatus" integer NOT NULL,
    "Version" integer NOT NULL,
    "ParentQuestionId" integer,
    "IsArchivedVersion" boolean NOT NULL,
    "AuthorUserId" integer,
    "SubmittedAt" timestamp without time zone,
    "ReviewedAt" timestamp without time zone,
    "PublishedAt" timestamp without time zone,
    "ReviewedByName" text,
    "ReviewNote" text,
    "ReviewHistoryJson" jsonb,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_QuestionBankItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_QuestionBankItems_CertificateLevels_CertificateLevelId" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_QuestionBankItems_ContentPassages_PassageId" FOREIGN KEY ("PassageId") REFERENCES "ContentPassages" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_QuestionBankItems_LanguageSkills_LanguageSkillId" FOREIGN KEY ("LanguageSkillId") REFERENCES "LanguageSkills" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_ReviewHistories_SessionReference" ON "ReviewHistories" ("SessionReference");

CREATE INDEX IF NOT EXISTS "IX_MockQuestions_PassageId" ON "MockQuestions" ("PassageId");

CREATE INDEX IF NOT EXISTS "IX_MockQuestions_QuestionBankItemId" ON "MockQuestions" ("QuestionBankItemId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ContentApprovalRequests_SubmittedByUserId_IdempotencyKey" ON "ContentApprovalRequests" ("SubmittedByUserId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_ContentPassages_CertificateLevelId_LanguageSkillId_PublishS~" ON "ContentPassages" ("CertificateLevelId", "LanguageSkillId", "PublishStatus");

CREATE INDEX IF NOT EXISTS "IX_ContentPassages_ContentFingerprint" ON "ContentPassages" ("ContentFingerprint");

CREATE INDEX IF NOT EXISTS "IX_ContentPassages_LanguageSkillId" ON "ContentPassages" ("LanguageSkillId");

CREATE INDEX IF NOT EXISTS "IX_ContentPassages_PassageIdentity" ON "ContentPassages" ("PassageIdentity");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_AuthorUserId" ON "QuestionBankItems" ("AuthorUserId");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_CertificateLevelId_LanguageSkillId_Publis~" ON "QuestionBankItems" ("CertificateLevelId", "LanguageSkillId", "PublishStatus", "IsArchivedVersion");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_ContentFingerprint" ON "QuestionBankItems" ("ContentFingerprint");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_LanguageSkillId" ON "QuestionBankItems" ("LanguageSkillId");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_PassageId" ON "QuestionBankItems" ("PassageId");

CREATE INDEX IF NOT EXISTS "IX_QuestionBankItems_QuestionIdentity" ON "QuestionBankItems" ("QuestionIdentity");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SrsReviewSessions_SessionKey" ON "SrsReviewSessions" ("SessionKey");

CREATE INDEX IF NOT EXISTS "IX_SrsReviewSessions_UserId_Status_LastActivityAtUtc" ON "SrsReviewSessions" ("UserId", "Status", "LastActivityAtUtc");

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."MockQuestions"') AND conname = 'FK_MockQuestions_ContentPassages_PassageId') THEN
        ALTER TABLE "MockQuestions" ADD CONSTRAINT "FK_MockQuestions_ContentPassages_PassageId" FOREIGN KEY ("PassageId") REFERENCES "ContentPassages" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."MockQuestions"') AND conname = 'FK_MockQuestions_QuestionBankItems_QuestionBankItemId') THEN
        ALTER TABLE "MockQuestions" ADD CONSTRAINT "FK_MockQuestions_QuestionBankItems_QuestionBankItemId" FOREIGN KEY ("QuestionBankItemId") REFERENCES "QuestionBankItems" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- (superseded index IX_LearnerNoteEntries_VocabularyListId dropped after IX_LearnerNoteEntries_VocabularyListId_IsDeleted)

ALTER TABLE "LearnerNotes" ADD COLUMN IF NOT EXISTS "FolderId" integer;

ALTER TABLE "LearnerNotes" ADD COLUMN IF NOT EXISTS "IsDefault" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "LearnerNotes" ADD COLUMN IF NOT EXISTS "Revision" integer NOT NULL DEFAULT 1;

ALTER TABLE "LearnerNotes" ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "ContextSnippet" character varying(600);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "ExampleMeaning" character varying(500);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "ExampleSentence" character varying(500);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "GrammarPatternId" integer;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "HighlightColor" character varying(24);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "IsFavorite" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "ItemKind" character varying(32) NOT NULL DEFAULT 'manual';

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "NotebookPageId" integer;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "Revision" integer NOT NULL DEFAULT 1;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "SourceId" integer;

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "SourcePath" character varying(300);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "SourceTitle" character varying(200);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "SourceType" character varying(40);

ALTER TABLE "LearnerNoteEntries" ADD COLUMN IF NOT EXISTS "TagsJson" jsonb NOT NULL DEFAULT ('[]'::jsonb);

CREATE TABLE IF NOT EXISTS "NotebookFolders" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "UserId" integer NOT NULL,
    "Name" character varying(120) NOT NULL,
    "NormalizedName" character varying(120) NOT NULL,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "IsDefault" boolean NOT NULL DEFAULT FALSE,
    "Revision" integer NOT NULL DEFAULT 1,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_NotebookFolders" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_NotebookFolders_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "NotebookPages" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "UserId" integer NOT NULL,
    "LearnerNoteId" integer NOT NULL,
    "Title" character varying(200) NOT NULL,
    "ContentJson" jsonb NOT NULL DEFAULT ('{"type":"doc","version":1,"blocks":[]}'::jsonb),
    "ContentPlainText" text NOT NULL DEFAULT '',
    "ContentFormatVersion" integer NOT NULL DEFAULT 1,
    "ChecklistTotalCount" integer NOT NULL DEFAULT 0,
    "ChecklistCompletedCount" integer NOT NULL DEFAULT 0,
    "IsFavorite" boolean NOT NULL DEFAULT FALSE,
    "FavoritedAtUtc" timestamp without time zone,
    "IsDefault" boolean NOT NULL DEFAULT FALSE,
    "SourceType" character varying(40),
    "SourceId" integer,
    "SourcePath" character varying(300),
    "SourceTitle" character varying(200),
    "SortOrder" integer NOT NULL DEFAULT 0,
    "LastAutosavedAtUtc" timestamp without time zone,
    "Revision" integer NOT NULL DEFAULT 1,
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_NotebookPages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_NotebookPages_LearnerNotes_LearnerNoteId" FOREIGN KEY ("LearnerNoteId") REFERENCES "LearnerNotes" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_NotebookPages_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_LearnerNotes_FolderId" ON "LearnerNotes" ("FolderId");

CREATE INDEX IF NOT EXISTS "IX_LearnerNotes_UserId_FolderId_IsDeleted_SortOrder" ON "LearnerNotes" ("UserId", "FolderId", "IsDeleted", "SortOrder");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_LearnerNotes_UserId_IsDefault" ON "LearnerNotes" ("UserId") WHERE "IsDeleted" = false AND "IsDefault" = true;

CREATE INDEX IF NOT EXISTS "IX_LearnerNoteEntries_GrammarPatternId" ON "LearnerNoteEntries" ("GrammarPatternId");

CREATE INDEX IF NOT EXISTS "IX_LearnerNoteEntries_NotebookPageId_IsDeleted_SortOrder" ON "LearnerNoteEntries" ("NotebookPageId", "IsDeleted", "SortOrder");

CREATE INDEX IF NOT EXISTS "IX_LearnerNoteEntries_VocabularyListId_IsDeleted" ON "LearnerNoteEntries" ("VocabularyListId", "IsDeleted");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotebookFolders_UserId_IsDefault" ON "NotebookFolders" ("UserId") WHERE "IsDeleted" = false AND "IsDefault" = true;

CREATE INDEX IF NOT EXISTS "IX_NotebookFolders_UserId_IsDeleted_SortOrder" ON "NotebookFolders" ("UserId", "IsDeleted", "SortOrder");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotebookFolders_UserId_NormalizedName" ON "NotebookFolders" ("UserId", "NormalizedName") WHERE "IsDeleted" = false;

CREATE INDEX IF NOT EXISTS "IX_NotebookPages_LearnerNoteId_IsDeleted_SortOrder" ON "NotebookPages" ("LearnerNoteId", "IsDeleted", "SortOrder");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotebookPages_UserId_IsDefault" ON "NotebookPages" ("UserId") WHERE "IsDeleted" = false AND "IsDefault" = true;

CREATE INDEX IF NOT EXISTS "IX_NotebookPages_UserId_IsDeleted_IsFavorite" ON "NotebookPages" ("UserId", "IsDeleted", "IsFavorite");

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."LearnerNoteEntries"') AND conname = 'FK_LearnerNoteEntries_GrammarPatterns_GrammarPatternId') THEN
        ALTER TABLE "LearnerNoteEntries" ADD CONSTRAINT "FK_LearnerNoteEntries_GrammarPatterns_GrammarPatternId" FOREIGN KEY ("GrammarPatternId") REFERENCES "GrammarPatterns" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."LearnerNoteEntries"') AND conname = 'FK_LearnerNoteEntries_NotebookPages_NotebookPageId') THEN
        ALTER TABLE "LearnerNoteEntries" ADD CONSTRAINT "FK_LearnerNoteEntries_NotebookPages_NotebookPageId" FOREIGN KEY ("NotebookPageId") REFERENCES "NotebookPages" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = to_regclass('public."LearnerNotes"') AND conname = 'FK_LearnerNotes_NotebookFolders_FolderId') THEN
        ALTER TABLE "LearnerNotes" ADD CONSTRAINT "FK_LearnerNotes_NotebookFolders_FolderId" FOREIGN KEY ("FolderId") REFERENCES "NotebookFolders" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

DO $$
DECLARE
    rec RECORD;
    ere RECORD;
    v_match text[];
    v_meta jsonb;
    v_folder_name text;
    v_norm_folder text;
    v_file_name text;
    v_clean_desc text;
    v_body text;
    v_blocks jsonb;
    v_doc jsonb;
    v_folder_id int;
    v_page_id int;
    v_has_default_folder boolean;
    v_has_default_file boolean;
    v_has_default_page boolean;
    v_fav boolean;
    v_src_path text;
    v_src_title text;
    v_kind text;
    v_color text;
    v_clean_note text;
    v_tags jsonb;
    v_chk_total int;
    v_chk_done int;
BEGIN
    FOR rec IN
        SELECT "Id", "UserId", "Name", "Description", "FolderId", "CreatedDate", "ModifiedDate"
        FROM "LearnerNotes"
        WHERE NOT "IsDeleted" AND "FolderId" IS NULL
        ORDER BY "Id"
    LOOP
        v_meta := '{}'::jsonb;
        v_match := regexp_match(COALESCE(rec."Description", ''), '<!--rp-nb:(\{[\s\S]*?\})-->');
        IF v_match IS NOT NULL AND array_length(v_match, 1) >= 1 THEN
            BEGIN
                v_meta := v_match[1]::jsonb;
            EXCEPTION WHEN OTHERS THEN
                v_meta := '{}'::jsonb;
            END;
        END IF;

        v_folder_name := NULLIF(BTRIM(COALESCE(v_meta ->> 'folder', '')), '');
        IF v_folder_name IS NULL AND POSITION(' :: ' IN COALESCE(rec."Name", '')) > 0 THEN
            v_folder_name := NULLIF(BTRIM(split_part(rec."Name", ' :: ', 1)), '');
        END IF;
        IF v_folder_name IS NULL THEN
            v_folder_name := 'Sổ tay chung';
        END IF;
        v_folder_name := LEFT(v_folder_name, 120);
        v_norm_folder := UPPER(v_folder_name);

        IF POSITION(' :: ' IN COALESCE(rec."Name", '')) > 0 THEN
            v_file_name := NULLIF(BTRIM(SUBSTRING(rec."Name" FROM POSITION(' :: ' IN rec."Name") + 4)), '');
        ELSE
            v_file_name := NULLIF(BTRIM(COALESCE(rec."Name", '')), '');
        END IF;
        IF v_file_name IS NULL THEN
            v_file_name := 'Ghi chú chung';
        END IF;
        v_file_name := LEFT(v_file_name, 200);

        v_clean_desc := NULLIF(BTRIM(regexp_replace(COALESCE(rec."Description", ''), '<!--rp-nb:[\s\S]*?-->', '', 'g')), '');

        SELECT "Id" INTO v_folder_id
        FROM "NotebookFolders"
        WHERE "UserId" = rec."UserId" AND "NormalizedName" = v_norm_folder AND NOT "IsDeleted"
        LIMIT 1;

        IF v_folder_id IS NULL THEN
            SELECT EXISTS (
                SELECT 1 FROM "NotebookFolders"
                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
            ) INTO v_has_default_folder;

            INSERT INTO "NotebookFolders" (
                "UserId", "Name", "NormalizedName", "SortOrder", "IsDefault", "Revision", "CreatedDate", "ModifiedDate", "IsDeleted"
            )
            VALUES (
                rec."UserId",
                v_folder_name,
                v_norm_folder,
                0,
                (NOT v_has_default_folder AND v_folder_name = 'Sổ tay chung'),
                1,
                COALESCE(rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                COALESCE(rec."ModifiedDate", NOW() AT TIME ZONE 'UTC'),
                false
            )
            RETURNING "Id" INTO v_folder_id;
        END IF;

        IF rec."FolderId" IS NULL THEN
            SELECT EXISTS (
                SELECT 1 FROM "LearnerNotes"
                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
            ) INTO v_has_default_file;

            UPDATE "LearnerNotes"
            SET "FolderId" = v_folder_id,
                "Name" = v_file_name,
                "Description" = v_clean_desc,
                "IsDefault" = ("IsDefault" OR (NOT v_has_default_file AND v_file_name IN ('Ghi chú nhanh', 'Ghi chú tổng quan'))),
                "ModifiedDate" = COALESCE("ModifiedDate", "CreatedDate", NOW() AT TIME ZONE 'UTC')
            WHERE "Id" = rec."Id";
        END IF;

        SELECT "Id" INTO v_page_id
        FROM "NotebookPages"
        WHERE "LearnerNoteId" = rec."Id" AND NOT "IsDeleted"
        ORDER BY "Id"
        LIMIT 1;

        IF v_page_id IS NULL THEN
            v_body := COALESCE(NULLIF(BTRIM(COALESCE(v_meta ->> 'body', '')), ''), COALESCE(v_clean_desc, ''), '');
            IF v_body = '' THEN
                v_blocks := '[]'::jsonb;
            ELSE
                SELECT COALESCE(jsonb_agg(
                    jsonb_build_object(
                        'id', 'mig_' || line_idx::text,
                        'type', CASE
                            WHEN line_txt LIKE '### %' THEN 'heading'
                            WHEN line_txt LIKE '## %' THEN 'heading'
                            WHEN line_txt LIKE '# %' THEN 'heading'
                            WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' THEN 'checklist_item'
                            WHEN line_txt LIKE '- [ ] %' THEN 'checklist_item'
                            WHEN line_txt LIKE '- %' OR line_txt LIKE '* %' THEN 'bullet_item'
                            ELSE 'paragraph'
                        END,
                        'runs', CASE
                            WHEN BTRIM(line_txt) = '' THEN '[]'::jsonb
                            ELSE jsonb_build_array(jsonb_build_object(
                                'text', CASE
                                    WHEN line_txt LIKE '### %' THEN SUBSTRING(line_txt FROM 5)
                                    WHEN line_txt LIKE '## %' THEN SUBSTRING(line_txt FROM 4)
                                    WHEN line_txt LIKE '# %' THEN SUBSTRING(line_txt FROM 3)
                                    WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' OR line_txt LIKE '- [ ] %' THEN SUBSTRING(line_txt FROM 7)
                                    WHEN line_txt LIKE '- %' OR line_txt LIKE '* %' THEN SUBSTRING(line_txt FROM 3)
                                    ELSE line_txt
                                END
                            ))
                        END
                    )
                    || CASE
                        WHEN line_txt LIKE '### %' THEN '{"level":3}'::jsonb
                        WHEN line_txt LIKE '## %' THEN '{"level":2}'::jsonb
                        WHEN line_txt LIKE '# %' THEN '{"level":1}'::jsonb
                        WHEN line_txt LIKE '- [x] %' OR line_txt LIKE '- [X] %' THEN '{"checked":true}'::jsonb
                        WHEN line_txt LIKE '- [ ] %' THEN '{"checked":false}'::jsonb
                        ELSE '{}'::jsonb
                    END
                ), '[]'::jsonb)
                INTO v_blocks
                FROM regexp_split_to_table(REPLACE(REPLACE(v_body, E'\r\n', E'\n'), E'\r', E'\n'), E'\n') WITH ORDINALITY AS t(line_txt, line_idx);
            END IF;

            v_doc := jsonb_build_object('type', 'doc', 'version', 1, 'blocks', v_blocks);
            SELECT
                COUNT(*) FILTER (WHERE b ->> 'type' = 'checklist_item'),
                COUNT(*) FILTER (WHERE b ->> 'type' = 'checklist_item' AND COALESCE((b ->> 'checked')::boolean, false))
            INTO v_chk_total, v_chk_done
            FROM jsonb_array_elements(v_blocks) AS b;

            BEGIN
                v_fav := COALESCE((v_meta ->> 'favorite')::boolean, false);
            EXCEPTION WHEN OTHERS THEN
                v_fav := false;
            END;
            v_src_path := NULLIF(LEFT(COALESCE(v_meta ->> 'sourcePath', ''), 300), '');
            v_src_title := NULLIF(LEFT(COALESCE(v_meta ->> 'sourceTitle', ''), 200), '');

            SELECT EXISTS (
                SELECT 1 FROM "NotebookPages"
                WHERE "UserId" = rec."UserId" AND "IsDefault" AND NOT "IsDeleted"
            ) INTO v_has_default_page;

            INSERT INTO "NotebookPages" (
                "UserId", "LearnerNoteId", "Title", "ContentJson", "ContentPlainText",
                "ContentFormatVersion", "ChecklistTotalCount", "ChecklistCompletedCount",
                "IsFavorite", "FavoritedAtUtc", "IsDefault", "SourcePath", "SourceTitle",
                "SortOrder", "Revision", "CreatedDate", "ModifiedDate", "IsDeleted"
            )
            VALUES (
                rec."UserId",
                rec."Id",
                v_file_name,
                v_doc,
                v_body,
                1,
                COALESCE(v_chk_total, 0),
                COALESCE(v_chk_done, 0),
                v_fav,
                CASE WHEN v_fav THEN COALESCE(rec."ModifiedDate", rec."CreatedDate", NOW() AT TIME ZONE 'UTC') ELSE NULL END,
                (NOT v_has_default_page AND v_folder_name = 'Sổ tay chung'),
                v_src_path,
                v_src_title,
                1,
                1,
                COALESCE(rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                COALESCE(rec."ModifiedDate", rec."CreatedDate", NOW() AT TIME ZONE 'UTC'),
                false
            )
            RETURNING "Id" INTO v_page_id;
        END IF;
    END LOOP;

    FOR ere IN
        SELECT "Id", "VocabularyListId", "VocabularyEntryId", "KanjiEntryId", "Note"
        FROM "LearnerNoteEntries"
        WHERE NOT "IsDeleted" AND "NotebookPageId" IS NULL
        ORDER BY "Id"
    LOOP
        SELECT "Id" INTO v_page_id
        FROM "NotebookPages"
        WHERE "LearnerNoteId" = ere."VocabularyListId" AND NOT "IsDeleted"
        ORDER BY "Id"
        LIMIT 1;

        v_meta := '{}'::jsonb;
        v_match := regexp_match(COALESCE(ere."Note", ''), '<!--rp-entry:(\{[\s\S]*?\})-->');
        IF v_match IS NOT NULL AND array_length(v_match, 1) >= 1 THEN
            BEGIN
                v_meta := v_match[1]::jsonb;
            EXCEPTION WHEN OTHERS THEN
                v_meta := '{}'::jsonb;
            END;
        END IF;

        v_kind := CASE
            WHEN ere."VocabularyEntryId" IS NOT NULL THEN 'vocabulary'
            WHEN ere."KanjiEntryId" IS NOT NULL THEN 'kanji'
            WHEN LOWER(COALESCE(v_meta ->> 'kind', '')) IN ('vocabulary', 'kanji', 'grammar', 'highlight', 'note', 'manual')
                THEN LOWER(v_meta ->> 'kind')
            ELSE 'manual'
        END;

        v_color := CASE
            WHEN LOWER(COALESCE(v_meta ->> 'color', '')) IN ('yellow', 'mint', 'sakura')
                THEN LOWER(v_meta ->> 'color')
            ELSE NULL
        END;

        v_src_path := NULLIF(LEFT(COALESCE(v_meta ->> 'sourcePath', ''), 300), '');
        v_src_title := NULLIF(LEFT(COALESCE(v_meta ->> 'sourceTitle', ''), 200), '');
        v_fav := false;
        BEGIN
            v_fav := COALESCE((v_meta ->> 'favorite')::boolean, false);
        EXCEPTION WHEN OTHERS THEN
            v_fav := false;
        END;

        v_tags := CASE
            WHEN jsonb_typeof(v_meta -> 'tags') = 'array' THEN v_meta -> 'tags'
            ELSE '[]'::jsonb
        END;

        v_clean_note := NULLIF(BTRIM(regexp_replace(COALESCE(ere."Note", ''), '<!--rp-entry:[\s\S]*?-->', '', 'g')), '');

        UPDATE "LearnerNoteEntries"
        SET "NotebookPageId" = v_page_id,
            "ItemKind" = v_kind,
            "HighlightColor" = v_color,
            "SourcePath" = COALESCE("SourcePath", v_src_path),
            "SourceTitle" = COALESCE("SourceTitle", v_src_title),
            "ExampleSentence" = COALESCE("ExampleSentence", NULLIF(LEFT(COALESCE(v_meta ->> 'example', ''), 500), '')),
            "ExampleMeaning" = COALESCE("ExampleMeaning", NULLIF(LEFT(COALESCE(v_meta ->> 'exampleMeaning', ''), 500), '')),
            "TagsJson" = v_tags,
            "IsFavorite" = v_fav,
            "Note" = v_clean_note,
            "ModifiedDate" = COALESCE("ModifiedDate", "CreatedDate", NOW() AT TIME ZONE 'UTC')
        WHERE "Id" = ere."Id";
    END LOOP;
END
$$;

ALTER TABLE "SrsReviewSessions" ALTER COLUMN "RetryCardIdsJson" TYPE character varying(8000);

ALTER TABLE "SrsReviewSessions" ALTER COLUMN "QueueCardIdsJson" TYPE character varying(8000);

ALTER TABLE "SrsReviewSessions" ALTER COLUMN "CompletedCardIdsJson" TYPE character varying(8000);

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "DeadlineAtUtc" timestamp without time zone;

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "FilterLearningState" character varying(20);

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "ProcessedStepKeysJson" character varying(8000) NOT NULL DEFAULT '[]';

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "SelectionScope" character varying(30);

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "SessionKind" character varying(20) NOT NULL DEFAULT 'Scheduled';

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "ShuffleEnabled" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "SrsReviewSessions" ADD COLUMN IF NOT EXISTS "TimeLimitMinutes" integer;

CREATE INDEX IF NOT EXISTS "IX_SrsReviewSessions_UserId_SessionKind_Status_LastActivityAtU~" ON "SrsReviewSessions" ("UserId", "SessionKind", "Status", "LastActivityAtUtc");

DO $section_guard$
BEGIN
    IF (SELECT count(*) FROM "CertificateLevels" l JOIN "CertificateTypes" t ON t."Id" = l."CertificationId"
        WHERE t."Name" = 'JLPT' AND l."Code" IN ('N3', 'N2') AND NOT l."IsDeleted") <> 2 THEN
        RAISE EXCEPTION 'Expected exactly one active JLPT N3 and N2 level';
    END IF;
    IF EXISTS (SELECT 1 FROM "CertificateSections" WHERE NOT "IsDeleted"
        GROUP BY "CertificationLevelId", "Code" HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'Duplicate active CertificateSection codes';
    END IF;
    IF EXISTS (SELECT 1 FROM "CertificateSections" s JOIN "CertificateLevels" l
        ON l."Id" = s."CertificationLevelId" WHERE l."Code" IN ('N3', 'N2')
        AND s."Code" IN ('N3_VOCAB','N3_GRAMMAR','N3_READING','N3_LISTENING',
                         'N2_VOCAB','N2_GRAMMAR','N2_READING','N2_LISTENING') AND s."IsDeleted") THEN
        RAISE EXCEPTION 'A canonical N3/N2 section exists but is deleted; review it before migration';
    END IF;
    IF EXISTS (SELECT 1 FROM "MockTestSections" m JOIN "CertificateSections" s
        ON s."Id" = m."CertificationSectionId" LEFT JOIN "LanguageSkills" k ON k."Id" = m."SkillId"
        WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')
        AND (k."Name" IS NULL OR k."Name" NOT IN ('Vocabulary','Grammar','Reading','Listening'))) THEN
        RAISE EXCEPTION 'A GENERAL MockTestSection has an unmappable skill';
    END IF;
END $section_guard$;

WITH desired(level_code, suffix, section_name, sort_order) AS (VALUES
    ('N3','VOCAB','Kiến thức ngôn ngữ (Từ vựng)',1),
    ('N3','GRAMMAR','Kiến thức ngôn ngữ (Ngữ pháp)',2),
    ('N3','READING','Đọc hiểu',3),
    ('N3','LISTENING','Nghe hiểu',4),
    ('N2','VOCAB','Kiến thức ngôn ngữ (Từ vựng)',1),
    ('N2','GRAMMAR','Kiến thức ngôn ngữ (Ngữ pháp)',2),
    ('N2','READING','Đọc hiểu',3),
    ('N2','LISTENING','Nghe hiểu',4))
INSERT INTO "CertificateSections"
    ("CertificationLevelId", "Code", "Name", "SortOrder", "MaxScore", "PassingScore", "IsDeleted", "CreatedDate")
SELECT l."Id", d.level_code || '_' || d.suffix, d.section_name, d.sort_order,
    0, 0, false, now() AT TIME ZONE 'UTC'
FROM desired d JOIN "CertificateLevels" l ON l."Code" = d.level_code
JOIN "CertificateTypes" t ON t."Id" = l."CertificationId" AND t."Name" = 'JLPT'
WHERE NOT l."IsDeleted" AND NOT EXISTS (
    SELECT 1 FROM "CertificateSections" existing
    WHERE existing."CertificationLevelId" = l."Id" AND existing."Code" = d.level_code || '_' || d.suffix);

UPDATE "MockTestSections" m SET "CertificationSectionId" = target."Id"
FROM "CertificateSections" old, "CertificateSections" target,
     "CertificateLevels" level, "LanguageSkills" skill
WHERE old."Id" = m."CertificationSectionId"
  AND level."Id" = old."CertificationLevelId"
  AND skill."Id" = m."SkillId"
  AND old."Code" = level."Code" || '_GENERAL'
  AND level."Code" IN ('N3', 'N2')
  AND target."CertificationLevelId" = level."Id" AND NOT target."IsDeleted"
  AND target."Code" = level."Code" || '_' || CASE skill."Name"
      WHEN 'Vocabulary' THEN 'VOCAB' WHEN 'Grammar' THEN 'GRAMMAR'
      WHEN 'Reading' THEN 'READING' WHEN 'Listening' THEN 'LISTENING' END;

DO $section_guard$
BEGIN
    IF EXISTS (SELECT 1 FROM "MockTestSections" m JOIN "CertificateSections" s
        ON s."Id" = m."CertificationSectionId"
        WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')) THEN
        RAISE EXCEPTION 'GENERAL sections still have Mock Test references';
    END IF;
END $section_guard$;

UPDATE "CertificateSections" s SET "IsDeleted" = true
FROM "CertificateLevels" l WHERE l."Id" = s."CertificationLevelId"
    AND l."Code" IN ('N3','N2') AND s."Code" = l."Code" || '_GENERAL' AND NOT s."IsDeleted";

WITH ordering(code, position) AS (VALUES
    ('N5_VOCAB',1),('N5_GRAMMAR_READING',2),('N5_LISTENING',3),
    ('N4_VOCAB',1),('N4_GRAMMAR',2),('N4_READING',3),('N4_LISTENING',4),
    ('N3_VOCAB',1),('N3_GRAMMAR',2),('N3_READING',3),('N3_LISTENING',4),
    ('N2_VOCAB',1),('N2_GRAMMAR',2),('N2_READING',3),('N2_LISTENING',4))
UPDATE "CertificateSections" s SET "SortOrder" = o.position
FROM ordering o WHERE s."Code" = o.code AND NOT s."IsDeleted" AND s."SortOrder" = 0;

DROP INDEX IF EXISTS "IX_CertificateSections_CertificationLevelId_Code";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_CertificateSections_CertificationLevelId_Code" ON "CertificateSections" ("CertificationLevelId", "Code") WHERE "IsDeleted" = false;

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "AccessMode" character varying(32) NOT NULL DEFAULT 'FREE';

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "Category" character varying(40) NOT NULL DEFAULT 'general';

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "Code" character varying(80) NOT NULL DEFAULT '';

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "CreditFeatureKey" character varying(40);

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "IsCoreFree" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "RoutePrefixesJson" character varying(500);

ALTER TABLE "Features" ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0;

-- Controlled backfill & seed UPSERT for canonical Features 1..14 (works on 0, 8, or 14 rows)
INSERT INTO "Features" ("Id", "AccessMode", "Category", "Code", "CreditFeatureKey", "Description", "IsCoreFree", "IsDeleted", "Name", "RoutePrefixesJson", "SortOrder", "CreatedDate")
VALUES
    (1,  'PLAN_REQUIRED',   'self_study',    'LESSONS_CONTENT',     NULL,                    'Truy cập kho bài học JLPT đầy đủ theo cấp độ kèm nghe hiểu, đọc hiểu và lưu tiến độ.', FALSE, FALSE, 'Thư viện bài học JLPT',                    '["/lessons","/lesson-player","/riki/listening","/riki/reading"]', 10, TIMESTAMP '2026-11-23T00:00:00'),
    (2,  'FREE',            'core_tools',    'JAPANESE_LIBRARY',    NULL,                    'Tra cứu ngữ pháp, luyện viết Hiragana, Katakana và Kanji theo thứ tự nét miễn phí.', TRUE,  FALSE, 'Thư viện Kana, Kanji & Ngữ pháp',          '["/grammar","/kanji-writing","/vocabulary-detail"]',             3,  TIMESTAMP '2026-11-23T00:00:00'),
    (3,  'FREE',            'practice_exam', 'PRACTICE_EXERCISES',  NULL,                    'Làm bài luyện tập củng cố kỹ năng và bộ đề theo cấp độ JLPT.',                      FALSE, FALSE, 'Bài luyện tập theo kỹ năng',               '["/lesson-practice","/practice-sets"]',                          20, TIMESTAMP '2026-11-23T00:00:00'),
    (4,  'FREE',            'practice_exam', 'MOCK_TESTS',          NULL,                    'Luyện đề thi thử JLPT mô phỏng đầy đủ 4 kỹ năng và theo dõi kết quả.',               FALSE, FALSE, 'Đề thi thử JLPT',                          '["/mock-test","/mock-tests","/exam-result"]',                    21, TIMESTAMP '2026-11-23T00:00:00'),
    (5,  'FREE',            'self_study',    'STANDARD_ROADMAP',    NULL,                    'Xem lộ trình học JLPT tiêu chuẩn theo mục tiêu, kết quả đánh giá năng lực và tiến độ học.', FALSE, FALSE, 'Lộ trình tiêu chuẩn & Tiến độ học', '["/roadmap"]',                                                   11, TIMESTAMP '2026-11-23T00:00:00'),
    (6,  'CREDIT_REQUIRED', 'ai_services',   'AI_MOCK_EXPLANATION', 'mock_test_explanation', 'Nhận phân tích chi tiết đáp án đề thi thử từ trợ lý AI.',                             FALSE, FALSE, 'Giải thích câu hỏi bằng AI',               NULL,                                                             33, TIMESTAMP '2026-11-23T00:00:00'),
    (7,  'DISABLED',        'mentor_legacy', 'MENTOR_1ON1_LEGACY',  NULL,                    'Gói cũ bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor (đã thay bằng lớp học nhóm).', FALSE, FALSE, '12 buổi meeting 1-1 với Mentor',         NULL,                                                             90, TIMESTAMP '2026-11-23T00:00:00'),
    (8,  'DISABLED',        'mentor_legacy', 'MENTOR_ROOM_LEGACY',  NULL,                    'Cấu hình lịch 1-1 cũ (lớp học nhóm dùng cơ chế ghi danh khóa học riêng).',           FALSE, FALSE, 'Đặt lịch và phòng học trực tuyến',         NULL,                                                             91, TIMESTAMP '2026-11-23T00:00:00'),
    (9,  'FREE',            'core_tools',    'PERSONAL_NOTEBOOK',   NULL,                    'Tạo thư mục, tệp ghi chú, trang bài học, highlight và mục đã lưu cá nhân miễn phí.',  TRUE,  FALSE, 'Sổ tay cá nhân & Mini Notebook',           '["/kanji-notebook"]',                                            1,  TIMESTAMP '2026-11-23T00:00:00'),
    (10, 'FREE',            'core_tools',    'SRS_STUDY',           NULL,                    'Ôn thẻ đến hạn, 4 chế độ ôn tự do, quản lý thẻ cá nhân và thư viện thẻ mẫu miễn phí.', TRUE,  FALSE, 'Ôn tập SRS & Thư viện thẻ mẫu',            '["/daily-srs"]',                                                 2,  TIMESTAMP '2026-11-23T00:00:00'),
    (11, 'FREE',            'practice_exam', 'PLACEMENT_TEST',      NULL,                    'Làm bài đánh giá trình độ JLPT đầu vào để xác định cấp độ học phù hợp.',              FALSE, FALSE, 'Đánh giá năng lực đầu vào',                '["/placement"]',                                                 19, TIMESTAMP '2026-11-23T00:00:00'),
    (12, 'CREDIT_REQUIRED', 'ai_services',   'AI_LEARNING_PATH',    'learning_path',         'Tạo kế hoạch học tập chi tiết theo ngày bằng AI dựa trên điểm đánh giá và ngày thi.', FALSE, FALSE, 'Lộ trình học cá nhân hóa bằng AI',         NULL,                                                             30, TIMESTAMP '2026-11-23T00:00:00'),
    (13, 'CREDIT_REQUIRED', 'ai_services',   'AI_SPEAKING',         'speaking',              'Thu âm tiếng Nhật và nhận điểm phát âm, ngữ điệu cùng nhận xét chi tiết từ AI.',      FALSE, FALSE, 'Luyện nói & Chấm phát âm AI',              '["/riki/speaking","/speech"]',                                   31, TIMESTAMP '2026-11-23T00:00:00'),
    (14, 'CREDIT_REQUIRED', 'ai_services',   'AI_WRITING',          'writing',               'Viết câu và bài luận tiếng Nhật, nhận sửa lỗi ngữ pháp và chấm điểm từ AI.',          FALSE, FALSE, 'Luyện viết & Chấm bài luận AI',            '["/riki/essay","/sentence-studio"]',                             32, TIMESTAMP '2026-11-23T00:00:00')
ON CONFLICT ("Id") DO UPDATE SET
    "AccessMode"        = EXCLUDED."AccessMode",
    "Category"          = EXCLUDED."Category",
    "Code"              = EXCLUDED."Code",
    "CreditFeatureKey"  = EXCLUDED."CreditFeatureKey",
    "Description"       = EXCLUDED."Description",
    "IsCoreFree"        = EXCLUDED."IsCoreFree",
    "Name"              = EXCLUDED."Name",
    "RoutePrefixesJson" = EXCLUDED."RoutePrefixesJson",
    "SortOrder"         = EXCLUDED."SortOrder";

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Features_Code" ON "Features" ("Code");

SELECT setval(
    pg_get_serial_sequence('"Features"', 'Id'),
    GREATEST(
        (SELECT MAX("Id") FROM "Features") + 1,
        nextval(pg_get_serial_sequence('"Features"', 'Id'))),
    false);

-- ============================================================================
-- 4. Drop Superseded Single-Column Indexes ONLY AFTER Replacements Exist
-- ============================================================================
DO $drop_superseded$
BEGIN
    IF to_regclass('public."IX_ReviewHistories_ReviewItemId_ReviewedAt"') IS NOT NULL THEN
        DROP INDEX IF EXISTS "IX_ReviewHistories_ReviewItemId";
    END IF;
    IF to_regclass('public."IX_LearnerNoteEntries_VocabularyListId_IsDeleted"') IS NOT NULL THEN
        DROP INDEX IF EXISTS "IX_LearnerNoteEntries_VocabularyListId";
    END IF;
END $drop_superseded$;

-- ============================================================================
-- 5. Strict Structural & Invariant Postconditions BEFORE Recording History
-- ============================================================================
DO $postcheck$
DECLARE
    tbl_count int;
    col_count int;
    unvalidated_count int;
    active_sections int;
    zero_order_sections int;
    features_count int;
    bad_feature_codes int;
BEGIN
    SELECT COUNT(*) INTO tbl_count FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
    SELECT COUNT(*) INTO col_count FROM information_schema.columns WHERE table_schema = 'public';
    IF tbl_count <> 119 OR col_count <> 1790 THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: Expected 119 tables / 1790 columns, got % tables / % columns.', tbl_count, col_count;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist') THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: btree_gist not installed.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conrelid = 'public."MentorBookingReservations"'::regclass
          AND conname = 'EX_MentorBookingReservation_ActiveMentorRest' AND convalidated
    ) THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: EX_MentorBookingReservation_ActiveMentorRest missing or invalid.';
    END IF;

    SELECT COUNT(*) INTO unvalidated_count FROM pg_constraint c JOIN pg_class r ON r.oid=c.conrelid JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname='public' AND NOT c.convalidated;
    IF unvalidated_count > 0 THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: % unvalidated constraints found.', unvalidated_count;
    END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE s."SortOrder" <= 0)
    INTO active_sections, zero_order_sections
    FROM "CertificateSections" s
    JOIN "CertificateLevels" l ON l."Id" = s."CertificationLevelId"
    JOIN "CertificateTypes" t ON t."Id" = l."CertificationId"
    WHERE t."Name" = 'JLPT' AND l."Code" IN ('N5','N4','N3','N2') AND NOT l."IsDeleted" AND NOT s."IsDeleted";

    IF active_sections <> 15 OR zero_order_sections > 0 THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: Expected 15 active N5-N2 CertificateSections with SortOrder > 0 (got active=%, zero=%).', active_sections, zero_order_sections;
    END IF;

    IF EXISTS (SELECT 1 FROM "MockTestSections" m JOIN "CertificateSections" s ON s."Id"=m."CertificationSectionId" WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')) THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: MockTestSections still reference GENERAL sections.';
    END IF;

    SELECT COUNT(*), COUNT(*) FILTER (WHERE NULLIF(btrim("Code"), '') IS NULL)
    INTO features_count, bad_feature_codes
    FROM "Features";
    IF features_count < 14 OR bad_feature_codes > 0 THEN
        RAISE EXCEPTION 'ABORTED POSTCHECK: Expected >=14 Features with non-empty Code (got count=%, empty=%).', features_count, bad_feature_codes;
    END IF;
END $postcheck$;

-- ============================================================================
-- 6. Record Canonical EF Core Migration IDs (#01-#60) ONLY After Full Verification
-- ============================================================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
    ('20261027081217_InitalCreate', '8.0.8'),
    ('20261027094154_SeedCertificateSection', '8.0.8'),
    ('20261027131637_AddFlow1PlacementAndLearningMode', '8.0.8'),
    ('20261028151341_AddFieldIntoMockTestAttempt', '8.0.8'),
    ('20261030125610_AddMentorSchedulingPersistence', '8.0.8'),
    ('20261030153753_AddMentorProfileEntity', '8.0.8'),
    ('20261031082840_AddMentorCreditLedgerPersistence', '8.0.8'),
    ('20261031093906_AddMentorAttendancePersistence', '8.0.8'),
    ('20261031104216_AddMentorBookingCancellationMetadata', '8.0.8'),
    ('20261031111326_AddMentorReviewsAndDisputesPersistence', '8.0.8'),
    ('20261031235900_AddUserDeviceTokensPersistence', '8.0.8'),
    ('20261101083259_AddNotificationDeliveryPersistence', '8.0.8'),
    ('20261104103624_AddMentorCompensationFoundation', '8.0.8'),
    ('20261104104226_UseUtcForMentorCompensation', '8.0.8'),
    ('20261104115618_AddMentorCompensationLedgerAndDualPayout', '8.0.8'),
    ('20261105071236_AddContentAiReviewEntity', '8.0.8'),
    ('20261108141755_AddTopicsAndHomeworkAssignmentLessons', '8.0.8'),
    ('20261109120639_AddDailyStudyMinutesRange', '8.0.8'),
    ('20261111130000_AddMockQuestionOptionImageUrl', '8.0.8'),
    ('20261111150000_AddLearnerPlanOffers', '8.0.8'),
    ('20261112100000_AddDeclaredJlptCertificate', '8.0.8'),
    ('20261113150000_AddMentorHomeworkAndAttendance', '8.0.8'),
    ('20261113160000_AddGoogleSubject', '8.0.8'),
    ('20261113172409_AddStudyCoursesAndRetakePolicy', '8.0.8'),
    ('20261114041035_AddLearningGoalCertificateAndVersion', '8.0.8'),
    ('20261114083500_RepairMentorOverviewSchema', '8.0.8'),
    ('20261114133439_AddFlow2LessonAudioAndMentorReviews', '8.0.8'),
    ('20261114173931_MentorCohortsAndExamDrafts', '8.0.8'),
    ('20261116043614_AddMentorBookingHomeworkProgress', '8.0.8'),
    ('20261116054500_AddMentorPendingSlotOnSubscription', '8.0.8'),
    ('20261116061024_RetireLegacyStudyCourseModel', '8.0.8'),
    ('20261116072324_AddMentorGroupPolicyVersions', '8.0.8'),
    ('20261116072801_SnapshotMentorGroupPolicyOnSession', '8.0.8'),
    ('20261116073313_EnableGroupBookingSeatReservations', '8.0.8'),
    ('20261116073745_AddGroupEnrollmentAndRemediationPolicyRules', '8.0.8'),
    ('20261116143915_AddMentorAuthoredHomework', '8.0.8'),
    ('20261116171033_ReconcileCanonicalGroupCohortModel', '8.0.8'),
    ('20261116173741_AddCohortHomeworkAiDraftAndRevision', '8.0.8'),
    ('20261117021440_AddCohortEntitlementsAndRemediationSupport', '8.0.8'),
    ('20261117044503_AddCohortTransferAndMentorReplacement', '8.0.8'),
    ('20261117053348_AddCohortCheckoutPaymentIntegrity', '8.0.8'),
    ('20261117121238_AddLearnerFeedback', '8.0.8'),
    ('20261117122910_AddMentorWeeklySessionCap', '8.0.8'),
    ('20261117171728_AddGroupLearningRulesLifecycleFeedback', '8.0.8'),
    ('20261118054924_AddContentAudioTranscriptions', '8.0.8'),
    ('20261118091131_AddMockQuestionAudioTranscript', '8.0.8'),
    ('20261118100842_AddImportedContentImages', '8.0.8'),
    ('20261119125713_AdoptLearnerFeedback', '8.0.8'),
    ('20261119184931_EnforceCatalogDataIntegrityAndOrdering', '8.0.8'),
    ('20261119190751_AddAdminConfigAuditAndSubscriptionVersioning', '8.0.8'),
    ('20261119212046_AddUniversalProfileSecurityAndNotifications', '8.0.8'),
    ('20261120112045_SeedFullAccessLearnerDemo', '8.0.8'),
    ('20261121105758_AddMentorTeachingProposalsV4', '8.0.8'),
    ('20261122044025_AddMasterSrsSmartLearningV4', '8.0.8'),
    ('20261122050533_AddContentAuthoringExamBankV4', '8.0.8'),
    ('20261122051106_AddSrsReviewSessionsV41', '8.0.8'),
    ('20261122120731_AddPersonalNotebookV3', '8.0.8'),
    ('20261122130743_AddSrsFreePracticeV5', '8.0.8'),
    ('20261123030956_StandardizeN2N3CertificateSections', '8.0.8'),
    ('20261123033629_AddFeatureAccessPolicies', '8.0.8')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
