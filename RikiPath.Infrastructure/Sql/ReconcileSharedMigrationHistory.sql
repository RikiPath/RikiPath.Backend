-- RikiPath Shared PostgreSQL Reconciliation & Migration History Repair
-- Purpose:
--   Safely reconcile divergent migration history and partial schema objects on a shared PostgreSQL
--   database (e.g., shared Supabase) where objects from 20261030153753..20261104115618 already exist
--   without matching rows in "__EFMigrationsHistory", and where 20261030125610_AddMentorSchedulingPersistence
--   (8.0.0) ran before MentorSchedules / MentorScheduleRules / MentorBookingReservations existed.
--
-- Fail-Closed Guarantees:
--   1. Single atomic transaction (BEGIN; ... COMMIT;) — any validation error rolls back completely.
--   2. Additive only: never drops tables, drops columns, truncates rows, or deletes user data.
--   3. Rejects unexpected "__EFMigrationsHistory" IDs outside the canonical 45 + 6 audited Supabase IDs.
--   4. Verifies btree_gist creation privilege AND visibility in current_schemas(false).
--   5. Verifies constraint definitions (structural + check semantics) and fails closed on mismatch.
--   6. Verifies index target table, uniqueness, columns, and partial predicate — fails closed on mismatch.
--   7. Never records a migration ID unless its full expected schema/columns/constraints/indexes are verified.

BEGIN;

-- ============================================================================
-- 0. Baseline & Cross-Branch History Pre-Checks (Fail-Closed)
-- ============================================================================
DO $precheck$
DECLARE
    unexpected_migrations text;
    missing_study_tables text;
    mentor_reviews_cols integer;
    learner_feedback_cols integer;
    learner_feedback_constraint text;
    learner_feedback_index text;
    study_course_retake_exist boolean;
BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN
        RAISE EXCEPTION 'Aborted: "__EFMigrationsHistory" does not exist. Use standard EF Core migrations on empty databases.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20261030125610_AddMentorSchedulingPersistence'
    ) THEN
        RAISE EXCEPTION 'Aborted: baseline migration 20261030125610_AddMentorSchedulingPersistence is not present in "__EFMigrationsHistory".';
    END IF;

    IF to_regclass('public."MentorProfiles"') IS NULL THEN
        RAISE EXCEPTION 'Aborted: "MentorProfiles" does not exist. Reconcile only databases that already carry unrecorded MentorProfile schema.';
    END IF;

    -- Scenario 7: Reject unexpected migration history IDs outside canonical baseline (45) + audited cross-branch Supabase IDs (6).
    SELECT string_agg("MigrationId", ', ' ORDER BY "MigrationId")
    INTO unexpected_migrations
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" NOT IN (
        -- Canonical migrations in origin/main plus VNext forward migrations.
        '20261027081217_InitalCreate',
        '20261027094154_SeedCertificateSection',
        '20261027131637_AddFlow1PlacementAndLearningMode',
        '20261028151341_AddFieldIntoMockTestAttempt',
        '20261030125610_AddMentorSchedulingPersistence',
        '20261030153753_AddMentorProfileEntity',
        '20261031082840_AddMentorCreditLedgerPersistence',
        '20261031093906_AddMentorAttendancePersistence',
        '20261031104216_AddMentorBookingCancellationMetadata',
        '20261031111326_AddMentorReviewsAndDisputesPersistence',
        '20261031235900_AddUserDeviceTokensPersistence',
        '20261101083259_AddNotificationDeliveryPersistence',
        '20261104103624_AddMentorCompensationFoundation',
        '20261104104226_UseUtcForMentorCompensation',
        '20261104115618_AddMentorCompensationLedgerAndDualPayout',
        '20261105071236_AddContentAiReviewEntity',
        '20261108141755_AddTopicsAndHomeworkAssignmentLessons',
        '20261109120639_AddDailyStudyMinutesRange',
        '20261111130000_AddMockQuestionOptionImageUrl',
        '20261111150000_AddLearnerPlanOffers',
        '20261112100000_AddDeclaredJlptCertificate',
        '20261113150000_AddMentorHomeworkAndAttendance',
        '20261113160000_AddGoogleSubject',
        '20261113172409_AddStudyCoursesAndRetakePolicy',
        '20261114041035_AddLearningGoalCertificateAndVersion',
        '20261114083500_RepairMentorOverviewSchema',
        '20261114133439_AddFlow2LessonAudioAndMentorReviews',
        '20261114173931_MentorCohortsAndExamDrafts',
        '20261116043614_AddMentorBookingHomeworkProgress',
        '20261116054500_AddMentorPendingSlotOnSubscription',
        '20261116072324_AddMentorGroupPolicyVersions',
        '20261116072801_SnapshotMentorGroupPolicyOnSession',
        '20261116073313_EnableGroupBookingSeatReservations',
        '20261116073745_AddGroupEnrollmentAndRemediationPolicyRules',
        '20261116171033_ReconcileCanonicalGroupCohortModel',
        '20261116173741_AddCohortHomeworkAiDraftAndRevision',
        '20261117021440_AddCohortEntitlementsAndRemediationSupport',
        '20261117044503_AddCohortTransferAndMentorReplacement',
        '20261117053348_AddCohortCheckoutPaymentIntegrity',
        '20261117122910_AddMentorWeeklySessionCap',
        '20261117171728_AddGroupLearningRulesLifecycleFeedback',
        '20261119125713_AdoptLearnerFeedback',
        '20261119184931_EnforceCatalogDataIntegrityAndOrdering',
        '20261119190751_AddAdminConfigAuditAndSubscriptionVersioning',
        '20261119212046_AddUniversalProfileSecurityAndNotifications',
        -- Audited cross-branch Supabase migrations (PR #70 + unmerged Content/Homework/Feedback extensions)
        '20261116061024_RetireLegacyStudyCourseModel',
        '20261116143915_AddMentorAuthoredHomework',
        '20261117121238_AddLearnerFeedback',
        '20261118054924_AddContentAudioTranscriptions',
        '20261118091131_AddMockQuestionAudioTranscript',
        '20261118100842_AddImportedContentImages'
    );

    IF unexpected_migrations IS NOT NULL THEN
        RAISE EXCEPTION 'Aborted: unexpected migration history entries detected: [%]. Inspect cross-branch schema divergence before proceeding.', unexpected_migrations;
    END IF;

    -- Verify StudyCourses/StudyCohorts base tables still exist (including when dead 20261116061024_RetireLegacyStudyCourseModel is in history).
    SELECT string_agg(t, ', ')
    INTO missing_study_tables
    FROM unnest(ARRAY[
        'StudyCoursePlans', 'StudyCohorts', 'StudyCohortSessions', 'StudyCourses',
        'StudyCourseSessions', 'StudyCourseHomework', 'CourseScheduleRequests',
        'CourseRetakeRequests', 'CourseAuditEntries'
    ]) AS t
    WHERE to_regclass(format('public.%I', t)) IS NULL;

    IF missing_study_tables IS NOT NULL THEN
        RAISE EXCEPTION 'Aborted: required Group Learning base tables are missing: [%]. If 20261116061024_RetireLegacyStudyCourseModel dropped these tables, restore snapshot before deploying PR #71/#72.', missing_study_tables;
    END IF;

    -- Verify audited cross-branch additive artifacts when recorded in history.
    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116143915_AddMentorAuthoredHomework') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='HomeworkAssignments' AND column_name='AuthoredDefinitionJson')
           OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='HomeworkSubmissions' AND column_name='SelectedOptionsJson') THEN
            RAISE EXCEPTION 'Aborted: 20261116143915_AddMentorAuthoredHomework is recorded in history but expected columns are missing.';
        END IF;
    END IF;

    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118054924_AddContentAudioTranscriptions') THEN
        IF to_regclass('public."ContentAudioTranscriptions"') IS NULL THEN
            RAISE EXCEPTION 'Aborted: 20261118054924_AddContentAudioTranscriptions is recorded in history but table "ContentAudioTranscriptions" is missing.';
        END IF;
    END IF;

    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118091131_AddMockQuestionAudioTranscript') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='MockQuestions' AND column_name='AudioTranscript') THEN
            RAISE EXCEPTION 'Aborted: 20261118091131_AddMockQuestionAudioTranscript is recorded in history but column "AudioTranscript" is missing.';
        END IF;
    END IF;

    -- 20261117121238 source: origin/fix/import-file @ 1b9a904. Accept this
    -- externally applied ID only when its entire additive table shape matches.
    -- Its Up() also enabled RLS on MentorReviews and updated review/notification
    -- data; the QA audit must separately review those effects before release.
    IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261117121238_AddLearnerFeedback') THEN
        IF to_regclass('public."LearnerFeedback"') IS NULL THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback is recorded but its table is missing.';
        END IF;

        SELECT COUNT(*) INTO learner_feedback_cols
        FROM information_schema.columns
        WHERE table_schema='public' AND table_name='LearnerFeedback'
          AND (
            (column_name='Id' AND data_type='integer' AND is_nullable='NO' AND is_identity='YES' AND identity_generation='BY DEFAULT')
            OR (column_name='LearnerId' AND data_type='integer' AND is_nullable='NO')
            OR (column_name='TargetType' AND data_type='character varying' AND character_maximum_length=32 AND is_nullable='NO')
            OR (column_name='TargetId' AND data_type='integer' AND is_nullable='NO')
            OR (column_name='TargetTitle' AND data_type='text' AND is_nullable='NO')
            OR (column_name='Rating' AND data_type='integer' AND is_nullable='NO')
            OR (column_name='Comment' AND data_type='character varying' AND character_maximum_length=1000 AND is_nullable='YES')
            OR (column_name IN ('CreatedAtUtc','UpdatedAtUtc') AND data_type='timestamp without time zone' AND is_nullable='NO')
          );
        IF learner_feedback_cols <> 9
           OR (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='LearnerFeedback') <> 9 THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback column or nullability mismatch (% of 9).', learner_feedback_cols;
        END IF;

        SELECT pg_get_constraintdef(oid) INTO learner_feedback_constraint
        FROM pg_constraint WHERE conrelid='public."LearnerFeedback"'::regclass AND conname='PK_LearnerFeedback' AND contype='p' AND convalidated;
        IF learner_feedback_constraint <> 'PRIMARY KEY ("Id")' THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback primary key mismatch.';
        END IF;
        SELECT pg_get_constraintdef(oid) INTO learner_feedback_constraint
        FROM pg_constraint WHERE conrelid='public."LearnerFeedback"'::regclass AND conname='FK_LearnerFeedback_Users_LearnerId' AND contype='f' AND convalidated;
        IF learner_feedback_constraint <> 'FOREIGN KEY ("LearnerId") REFERENCES "Users"("Id") ON DELETE CASCADE' THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback learner FK mismatch (%).', learner_feedback_constraint;
        END IF;
        SELECT pg_get_constraintdef(oid) INTO learner_feedback_constraint
        FROM pg_constraint WHERE conrelid='public."LearnerFeedback"'::regclass AND conname='CK_LearnerFeedback_Rating' AND contype='c' AND convalidated;
        IF learner_feedback_constraint IS NULL
           OR learner_feedback_constraint NOT LIKE '%"Rating" >= 1%'
           OR learner_feedback_constraint NOT LIKE '%"Rating" <= 5%' THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback rating constraint mismatch (%).', learner_feedback_constraint;
        END IF;

        SELECT pg_get_indexdef(ix.indexrelid) INTO learner_feedback_index
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_index ix ON ix.indexrelid=c.oid
        WHERE n.nspname='public' AND c.relname='IX_LearnerFeedback_LearnerId_TargetType_TargetId'
          AND ix.indisunique AND ix.indrelid='public."LearnerFeedback"'::regclass;
        IF learner_feedback_index IS NULL
           OR learner_feedback_index NOT LIKE '%("LearnerId", "TargetType", "TargetId")%'
           OR learner_feedback_index LIKE '% WHERE %' THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback unique index mismatch (%).', learner_feedback_index;
        END IF;
        SELECT pg_get_indexdef(ix.indexrelid) INTO learner_feedback_index
        FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_index ix ON ix.indexrelid=c.oid
        WHERE n.nspname='public' AND c.relname='IX_LearnerFeedback_UpdatedAtUtc_Id'
          AND NOT ix.indisunique AND ix.indrelid='public."LearnerFeedback"'::regclass;
        IF learner_feedback_index IS NULL
           OR learner_feedback_index NOT LIKE '%("UpdatedAtUtc", "Id")%'
           OR learner_feedback_index LIKE '% WHERE %' THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback updated index mismatch (%).', learner_feedback_index;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_class WHERE oid='public."LearnerFeedback"'::regclass AND relrowsecurity)
           OR NOT EXISTS (SELECT 1 FROM pg_class WHERE oid='public."MentorReviews"'::regclass AND relrowsecurity) THEN
            RAISE EXCEPTION 'Aborted: AddLearnerFeedback RLS state mismatch.';
        END IF;
    END IF;

    -- If MentorReviews already exists prior to unapplied 20261031111326, assert full expected column parity.
    IF to_regclass('public."MentorReviews"') IS NOT NULL THEN
        SELECT COUNT(*) INTO mentor_reviews_cols
        FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MentorReviews'
          AND column_name IN (
            'Id','MentorBookingId','MentorId','LearnerId','Rating','Comment',
            'IsPublic','ModerationStatus','MentorReply','MentorRepliedAtUtc','CreatedAtUtc','UpdatedAtUtc'
          );
        IF mentor_reviews_cols <> 12 THEN
            RAISE EXCEPTION 'Aborted: pre-existing "MentorReviews" table does not match expected 12-column schema required by 20261031111326_AddMentorReviewsAndDisputesPersistence.';
        END IF;
    END IF;
END $precheck$;

-- ============================================================================
-- 1. Enable & Verify btree_gist Extension (Scenario 6: Fail-Closed Privilege Check)
-- ============================================================================
DO $ext$
DECLARE
    opclass_visible boolean;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist') THEN
        IF NOT EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'btree_gist') THEN
            RAISE EXCEPTION 'Aborted: PostgreSQL extension "btree_gist" is not available on this server.';
        END IF;

        BEGIN
            IF 'extensions' = ANY (current_schemas(false)) THEN
                EXECUTE 'CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA extensions';
            ELSE
                EXECUTE 'CREATE EXTENSION IF NOT EXISTS btree_gist';
            END IF;
        EXCEPTION
            WHEN insufficient_privilege THEN
                RAISE EXCEPTION 'Aborted: insufficient database privileges to create extension "btree_gist". Pre-enable "btree_gist" via Supabase Dashboard -> Database -> Extensions (or superuser role) before re-running.';
            WHEN OTHERS THEN
                RAISE EXCEPTION 'Aborted: failed to enable extension "btree_gist" (%): %', SQLSTATE, SQLERRM;
        END;
    END IF;

    SELECT EXISTS (
        SELECT 1
        FROM pg_opclass opc
        JOIN pg_am am ON am.oid = opc.opcmethod
        JOIN pg_namespace n ON n.oid = opc.opcnamespace
        WHERE am.amname = 'gist'
          AND opc.opcname = 'gist_int4_ops'
          AND n.nspname = ANY (current_schemas(false))
    ) INTO opclass_visible;

    IF NOT opclass_visible THEN
        RAISE EXCEPTION 'Aborted: extension "btree_gist" is installed in a schema not present in search_path (%). Include its schema in search_path.', current_setting('search_path');
    END IF;
END $ext$;

-- ============================================================================
-- 2. Complete Missing Objects from Divergent 20261030125610_AddMentorSchedulingPersistence
-- ============================================================================
ALTER TABLE "MentorAvailabilities" ADD COLUMN IF NOT EXISTS "GenerationKey" character varying(160);
ALTER TABLE "MentorAvailabilities" ADD COLUMN IF NOT EXISTS "LocalBusinessDate" date;
ALTER TABLE "MentorAvailabilities" ADD COLUMN IF NOT EXISTS "MentorScheduleRuleId" bigint;
ALTER TABLE "MentorAvailabilities" ADD COLUMN IF NOT EXISTS "PolicyVersion" integer;

DO $c_avail$
DECLARE
    def text;
BEGIN
    SELECT pg_get_constraintdef(c.oid) INTO def
    FROM pg_constraint c
    WHERE c.conrelid = 'public."MentorAvailabilities"'::regclass
      AND c.conname = 'AK_MentorAvailabilities_Id_MentorId';

    IF def IS NULL THEN
        ALTER TABLE "MentorAvailabilities"
            ADD CONSTRAINT "AK_MentorAvailabilities_Id_MentorId" UNIQUE ("Id", "MentorId");
    ELSIF def !~ '^UNIQUE \("Id", "MentorId"\)$' THEN
        RAISE EXCEPTION 'Aborted: constraint "AK_MentorAvailabilities_Id_MentorId" on "MentorAvailabilities" has incompatible definition: %', def;
    END IF;
END $c_avail$;

CREATE TABLE IF NOT EXISTS "MentorBookingDayGuards" (
    "MentorId" integer NOT NULL,
    "LocalBusinessDate" date NOT NULL,
    "ActiveBookingCount" integer NOT NULL,
    CONSTRAINT "PK_MentorBookingDayGuards" PRIMARY KEY ("MentorId", "LocalBusinessDate"),
    CONSTRAINT "CK_MentorBookingDayGuard_Count" CHECK ("ActiveBookingCount" >= 0),
    CONSTRAINT "FK_MentorBookingDayGuards_Users_MentorId" FOREIGN KEY ("MentorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorBookingIntents" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "LearnerId" integer NOT NULL,
    "IntentKey" character varying(128) NOT NULL,
    "RequestHash" character varying(64) NOT NULL,
    "MentorBookingId" integer,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorBookingIntents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MentorBookingIntents_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorBookingIntents_Users_LearnerId" FOREIGN KEY ("LearnerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorBookingReservations" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorId" integer NOT NULL,
    "MentorBookingId" integer NOT NULL,
    "MentorAvailabilityId" integer NOT NULL,
    "LocalBusinessDate" date NOT NULL,
    "StartUtc" timestamp with time zone NOT NULL,
    "EndUtc" timestamp with time zone NOT NULL,
    "RestProtectedUntilUtc" timestamp with time zone NOT NULL,
    "PolicyVersion" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ReleasedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MentorBookingReservations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorBookingReservation_Interval" CHECK ("EndUtc" > "StartUtc"),
    CONSTRAINT "CK_MentorBookingReservation_PolicyVersion" CHECK ("PolicyVersion" > 0),
    CONSTRAINT "CK_MentorBookingReservation_Rest" CHECK ("RestProtectedUntilUtc" = "EndUtc" + INTERVAL '15 minutes'),
    CONSTRAINT "FK_MentorBookingReservations_MentorAvailabilities_MentorAvaila~" FOREIGN KEY ("MentorAvailabilityId", "MentorId") REFERENCES "MentorAvailabilities" ("Id", "MentorId") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorBookingReservations_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorBookingReservations_Users_MentorId" FOREIGN KEY ("MentorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorBookingTerms" (
    "MentorBookingId" integer NOT NULL,
    "MentorAvailabilityId" integer NOT NULL,
    "PolicyVersion" integer NOT NULL,
    "ScheduledStartUtc" timestamp with time zone NOT NULL,
    "ScheduledEndUtc" timestamp with time zone NOT NULL,
    "ConfirmedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MentorBookingTerms" PRIMARY KEY ("MentorBookingId"),
    CONSTRAINT "CK_MentorBookingTerms_Interval" CHECK ("ScheduledEndUtc" > "ScheduledStartUtc"),
    CONSTRAINT "CK_MentorBookingTerms_PolicyVersion" CHECK ("PolicyVersion" > 0),
    CONSTRAINT "FK_MentorBookingTerms_MentorAvailabilities_MentorAvailabilityId" FOREIGN KEY ("MentorAvailabilityId") REFERENCES "MentorAvailabilities" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorBookingTerms_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorSchedules" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorId" integer NOT NULL,
    "PolicyVersion" integer NOT NULL,
    "TimeZoneId" character varying(64) NOT NULL,
    "AutoPublish" boolean NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "GeneratedThrough" date,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorSchedules" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorSchedule_PolicyVersion" CHECK ("PolicyVersion" > 0),
    CONSTRAINT "FK_MentorSchedules_Users_MentorId" FOREIGN KEY ("MentorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorScheduleRules" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "ScheduleId" bigint NOT NULL,
    "Weekday" integer NOT NULL,
    "DaypartCode" character varying(20) NOT NULL,
    "LocalStart" time without time zone NOT NULL,
    "LocalEnd" time without time zone NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "EffectiveTo" date,
    CONSTRAINT "PK_MentorScheduleRules" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorScheduleRule_Daypart" CHECK ("DaypartCode" IN ('Morning', 'Noon', 'Afternoon', 'Evening')),
    CONSTRAINT "CK_MentorScheduleRule_Time" CHECK ("LocalEnd" > "LocalStart"),
    CONSTRAINT "CK_MentorScheduleRule_Weekday" CHECK ("Weekday" BETWEEN 0 AND 6),
    CONSTRAINT "FK_MentorScheduleRules_MentorSchedules_ScheduleId" FOREIGN KEY ("ScheduleId") REFERENCES "MentorSchedules" ("Id") ON DELETE RESTRICT
);

-- Create indexes safely (Scenario 5 verification runs cleanly right below)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorAvailabilities_GenerationKey"
    ON "MentorAvailabilities" ("GenerationKey") WHERE "GenerationKey" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_MentorAvailabilities_MentorScheduleRuleId"
    ON "MentorAvailabilities" ("MentorScheduleRuleId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorBookingIntents_LearnerId_IntentKey"
    ON "MentorBookingIntents" ("LearnerId", "IntentKey");
CREATE INDEX IF NOT EXISTS "IX_MentorBookingIntents_MentorBookingId"
    ON "MentorBookingIntents" ("MentorBookingId");

DO $res_idx$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = 'IX_MentorBookingReservations_MentorAvailabilityId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'MentorBookings' AND column_name = 'LearnerId'
        ) THEN
            CREATE INDEX "IX_MentorBookingReservations_MentorAvailabilityId"
                ON "MentorBookingReservations" ("MentorAvailabilityId") WHERE "ReleasedAtUtc" IS NULL;
        ELSE
            CREATE UNIQUE INDEX "IX_MentorBookingReservations_MentorAvailabilityId"
                ON "MentorBookingReservations" ("MentorAvailabilityId") WHERE "ReleasedAtUtc" IS NULL;
        END IF;
    END IF;
END $res_idx$;

CREATE INDEX IF NOT EXISTS "IX_MentorBookingReservations_MentorAvailabilityId_MentorId"
    ON "MentorBookingReservations" ("MentorAvailabilityId", "MentorId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorBookingReservations_MentorBookingId"
    ON "MentorBookingReservations" ("MentorBookingId");
CREATE INDEX IF NOT EXISTS "IX_MentorBookingReservations_MentorId_LocalBusinessDate_Releas~"
    ON "MentorBookingReservations" ("MentorId", "LocalBusinessDate", "ReleasedAtUtc");

CREATE INDEX IF NOT EXISTS "IX_MentorBookingTerms_MentorAvailabilityId"
    ON "MentorBookingTerms" ("MentorAvailabilityId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorScheduleRules_ScheduleId_Weekday_DaypartCode_Effectiv~"
    ON "MentorScheduleRules" ("ScheduleId", "Weekday", "DaypartCode", "EffectiveFrom");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorSchedules_MentorId_PolicyVersion"
    ON "MentorSchedules" ("MentorId", "PolicyVersion");

DO $avail_checks$
DECLARE
    def text;
    has_group_policy_col boolean;
    has_learner_col boolean;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MentorAvailabilities' AND column_name = 'GroupPolicyId'
    ) INTO has_group_policy_col;

    SELECT pg_get_constraintdef(c.oid) INTO def
    FROM pg_constraint c
    WHERE c.conrelid = 'public."MentorAvailabilities"'::regclass
      AND c.conname = 'CK_MentorAvailability_GeneratedSlot';

    IF def IS NULL THEN
        IF NOT has_group_policy_col THEN
            ALTER TABLE "MentorAvailabilities"
                ADD CONSTRAINT "CK_MentorAvailability_GeneratedSlot"
                CHECK (("GenerationKey" IS NULL AND "PolicyVersion" IS NULL AND "MentorScheduleRuleId" IS NULL AND "LocalBusinessDate" IS NULL)
                    OR ("GenerationKey" IS NOT NULL AND "PolicyVersion" IS NOT NULL AND "PolicyVersion" > 0 AND "MentorScheduleRuleId" IS NOT NULL AND "LocalBusinessDate" IS NOT NULL AND "MaxLearners" = 1 AND "EndTime" = "StartTime" + INTERVAL '90 minutes'));
        ELSE
            RAISE EXCEPTION 'Aborted: "CK_MentorAvailability_GeneratedSlot" is missing while "GroupPolicyId" exists.';
        END IF;
    ELSIF def NOT LIKE '%"GenerationKey"%' OR def NOT LIKE '%"MentorScheduleRuleId"%' THEN
        RAISE EXCEPTION 'Aborted: constraint "CK_MentorAvailability_GeneratedSlot" has unexpected definition: %', def;
    END IF;

    SELECT pg_get_constraintdef(c.oid) INTO def
    FROM pg_constraint c
    WHERE c.conrelid = 'public."MentorAvailabilities"'::regclass
      AND c.conname = 'FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~';

    IF def IS NULL THEN
        ALTER TABLE "MentorAvailabilities"
            ADD CONSTRAINT "FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~"
            FOREIGN KEY ("MentorScheduleRuleId") REFERENCES "MentorScheduleRules" ("Id") ON DELETE RESTRICT;
    ELSIF def NOT LIKE 'FOREIGN KEY ("MentorScheduleRuleId") REFERENCES "MentorScheduleRules"("Id")%' THEN
        RAISE EXCEPTION 'Aborted: constraint "FK_MentorAvailabilities_MentorScheduleRules_MentorScheduleRule~" has unexpected definition: %', def;
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MentorBookings' AND column_name = 'LearnerId'
    ) INTO has_learner_col;

    SELECT pg_get_constraintdef(c.oid) INTO def
    FROM pg_constraint c
    WHERE c.conrelid = 'public."MentorBookingReservations"'::regclass
      AND c.conname = 'EX_MentorBookingReservation_ActiveMentorRest';

    IF def IS NULL THEN
        IF has_learner_col THEN
            ALTER TABLE "MentorBookingReservations"
                ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
                EXCLUDE USING gist (
                    "MentorId" WITH =,
                    "MentorAvailabilityId" WITH <>,
                    tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&
                ) WHERE ("ReleasedAtUtc" IS NULL);
        ELSE
            ALTER TABLE "MentorBookingReservations"
                ADD CONSTRAINT "EX_MentorBookingReservation_ActiveMentorRest"
                EXCLUDE USING gist (
                    "MentorId" WITH =,
                    tstzrange("StartUtc", "RestProtectedUntilUtc", '[)') WITH &&
                ) WHERE ("ReleasedAtUtc" IS NULL);
        END IF;
    ELSIF position('usinggist' in replace(lower(def), ' ', '')) = 0
       OR position('"mentorid"with=' in replace(lower(def), ' ', '')) = 0
       OR position('tstzrange("startutc","restprotecteduntilutc",''[)''' in replace(replace(lower(def), '::text', ''), ' ', '')) = 0
       OR position('with&&' in replace(lower(def), ' ', '')) = 0
       OR position('"releasedatutc"isnull' in replace(replace(replace(lower(def), '(', ''), ')', ''), ' ', '')) = 0 THEN
        RAISE EXCEPTION 'Aborted: constraint "EX_MentorBookingReservation_ActiveMentorRest" has unexpected definition: %', def;
    END IF;
END $avail_checks$;

-- ============================================================================
-- 3. Complete Missing Schema Objects from Skipped 20261104* Compensation Migrations
-- ============================================================================
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "CompensationFormulaVersion" integer;
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "CompensationLevelId" integer;
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "FrozenHourlyRate" numeric(18,2);
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "FrozenLevelCoefficient" numeric(10,4);
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "ScheduledCompensationAmount" numeric(18,2);
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "CompensationCurrency" character varying(10);
ALTER TABLE "MentorBookings" ADD COLUMN IF NOT EXISTS "CreditGrantId" bigint;
ALTER TABLE "UserSubscriptions" ADD COLUMN IF NOT EXISTS "CertificateLevelId" integer;
ALTER TABLE "SubscriptionPlans" ADD COLUMN IF NOT EXISTS "CertificateLevelId" integer;
ALTER TABLE "MentorCreditGrants" ADD COLUMN IF NOT EXISTS "CertificateLevelId" integer;

CREATE TABLE IF NOT EXISTS "JlptLevelPayCoefficients" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "CertificateLevelId" integer NOT NULL,
    "Coefficient" numeric(10,4) NOT NULL,
    "EffectiveFromUtc" timestamp with time zone NOT NULL,
    "EffectiveUntilUtc" timestamp with time zone,
    "ConfiguredByAdminId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_JlptLevelPayCoefficients" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_JlptLevelPayCoefficient_Positive" CHECK ("Coefficient" > 0),
    CONSTRAINT "CK_JlptLevelPayCoefficient_Range" CHECK ("EffectiveUntilUtc" IS NULL OR "EffectiveUntilUtc" > "EffectiveFromUtc"),
    CONSTRAINT "FK_JlptLevelPayCoefficients_CertificateLevels_CertificateLevel~" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_JlptLevelPayCoefficients_Users_ConfiguredByAdminId" FOREIGN KEY ("ConfiguredByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorHourlyRates" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "MentorUserId" integer NOT NULL,
    "AmountPerHour" numeric(18,2) NOT NULL,
    "EffectiveFromUtc" timestamp with time zone NOT NULL,
    "EffectiveUntilUtc" timestamp with time zone,
    "ConfiguredByAdminId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorHourlyRates" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorHourlyRate_Positive" CHECK ("AmountPerHour" > 0),
    CONSTRAINT "CK_MentorHourlyRate_Range" CHECK ("EffectiveUntilUtc" IS NULL OR "EffectiveUntilUtc" > "EffectiveFromUtc"),
    CONSTRAINT "FK_MentorHourlyRates_Users_ConfiguredByAdminId" FOREIGN KEY ("ConfiguredByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorHourlyRates_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorTeachingQualifications" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "MentorUserId" integer NOT NULL,
    "CertificateLevelId" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "VerifiedByAdminId" integer NOT NULL,
    "VerifiedAtUtc" timestamp with time zone NOT NULL,
    "RevokedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MentorTeachingQualifications" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorTeachingQualification_Revoked" CHECK ("RevokedAtUtc" IS NULL OR "RevokedAtUtc" >= "VerifiedAtUtc"),
    CONSTRAINT "FK_MentorTeachingQualifications_CertificateLevels_CertificateL~" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingQualifications_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorTeachingQualifications_Users_VerifiedByAdminId" FOREIGN KEY ("VerifiedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorAttendanceApprovals" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorBookingId" integer NOT NULL,
    "Status" character varying(40) NOT NULL,
    "RawMentorDurationSeconds" integer NOT NULL,
    "RawLearnerDurationSeconds" integer NOT NULL,
    "ApprovedEligibleMinutes" integer,
    "EvidenceSummary" character varying(2000) NOT NULL,
    "DecisionReason" character varying(1000) NOT NULL,
    "ReviewedByAdminId" integer,
    "ReviewedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorAttendanceApprovals" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorAttendanceApproval_Minutes" CHECK ("ApprovedEligibleMinutes" IS NULL OR ("ApprovedEligibleMinutes" > 0 AND "ApprovedEligibleMinutes" <= 90)),
    CONSTRAINT "CK_MentorAttendanceApproval_RawDuration" CHECK ("RawMentorDurationSeconds" >= 0 AND "RawLearnerDurationSeconds" >= 0),
    CONSTRAINT "FK_MentorAttendanceApprovals_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorAttendanceApprovals_Users_ReviewedByAdminId" FOREIGN KEY ("ReviewedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorBankAccounts" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorUserId" integer NOT NULL,
    "AccountVersion" integer NOT NULL,
    "BankCode" character varying(50) NOT NULL,
    "BankName" character varying(200) NOT NULL,
    "AccountHolderName" character varying(200) NOT NULL,
    "EncryptedAccountNumber" character varying(1000) NOT NULL,
    "MaskedAccountNumber" character varying(50) NOT NULL,
    "AccountNumberHash" character varying(128) NOT NULL,
    "VerificationStatus" character varying(30) NOT NULL,
    "IsCurrent" boolean NOT NULL,
    "VerifiedByAdminId" integer,
    "VerifiedAtUtc" timestamp with time zone,
    "VerificationNotes" character varying(1000),
    "EffectiveFromUtc" timestamp with time zone NOT NULL,
    "EffectiveUntilUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorBankAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorBankAccount_Version" CHECK ("AccountVersion" >= 1),
    CONSTRAINT "FK_MentorBankAccounts_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorBankAccounts_Users_VerifiedByAdminId" FOREIGN KEY ("VerifiedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorPayrollPeriods" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorUserId" integer NOT NULL,
    "Year" integer NOT NULL,
    "Month" integer NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "SupplementalReason" character varying(1000),
    "Currency" character varying(10) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "TotalEarningsAmount" numeric(18,2) NOT NULL,
    "TotalAdjustmentsAmount" numeric(18,2) NOT NULL,
    "GrossPayableAmount" numeric(18,2) NOT NULL,
    "PreparedByAdminId" integer NOT NULL,
    "PreparedAtUtc" timestamp with time zone NOT NULL,
    "ApprovedByAdminId" integer,
    "ApprovedAtUtc" timestamp with time zone,
    "ApprovalNotes" character varying(1000),
    "ClosedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MentorPayrollPeriods" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorPayrollPeriod_Payable" CHECK ("TotalEarningsAmount" >= 0 AND "GrossPayableAmount" >= 0),
    CONSTRAINT "CK_MentorPayrollPeriod_Period" CHECK ("Year" >= 2020 AND "Year" <= 2100 AND "Month" >= 1 AND "Month" <= 12 AND "SequenceNumber" >= 1),
    CONSTRAINT "CK_MentorPayrollPeriod_Supplemental" CHECK ("SequenceNumber" = 1 OR ("SupplementalReason" IS NOT NULL AND length(trim("SupplementalReason")) > 0)),
    CONSTRAINT "FK_MentorPayrollPeriods_Users_ApprovedByAdminId" FOREIGN KEY ("ApprovedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayrollPeriods_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayrollPeriods_Users_PreparedByAdminId" FOREIGN KEY ("PreparedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "OrganizationPayoutAccounts" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "ChannelCode" character varying(100) NOT NULL,
    "ProviderType" character varying(30) NOT NULL,
    "BankCode" character varying(50) NOT NULL,
    "BankName" character varying(200) NOT NULL,
    "AccountHolderName" character varying(200) NOT NULL,
    "MaskedAccountNumber" character varying(50) NOT NULL,
    "ProviderAccountReference" character varying(200),
    "IsActive" boolean NOT NULL,
    "IsVerified" boolean NOT NULL,
    "ConfiguredByAdminId" integer NOT NULL,
    "VerifiedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrganizationPayoutAccounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OrganizationPayoutAccounts_Users_ConfiguredByAdminId" FOREIGN KEY ("ConfiguredByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorEarningEntries" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "MentorBookingId" integer NOT NULL,
    "AttendanceApprovalId" bigint NOT NULL,
    "MentorUserId" integer NOT NULL,
    "CertificateLevelId" integer NOT NULL,
    "JlptLevelCode" character varying(10) NOT NULL,
    "FrozenHourlyRate" numeric(18,2) NOT NULL,
    "FrozenLevelCoefficient" numeric(10,4) NOT NULL,
    "ApprovedEligibleMinutes" integer NOT NULL,
    "GrossAmount" numeric(18,2) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "FormulaVersion" integer NOT NULL,
    "Status" character varying(30) NOT NULL,
    "PayrollPeriodId" bigint,
    "OperationKey" character varying(150) NOT NULL,
    "CreatedByAdminId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorEarningEntries" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorEarningEntry_Positive" CHECK ("FrozenHourlyRate" > 0 AND "FrozenLevelCoefficient" > 0 AND "ApprovedEligibleMinutes" > 0 AND "ApprovedEligibleMinutes" <= 90 AND "GrossAmount" > 0),
    CONSTRAINT "FK_MentorEarningEntries_CertificateLevels_CertificateLevelId" FOREIGN KEY ("CertificateLevelId") REFERENCES "CertificateLevels" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorEarningEntries_MentorAttendanceApprovals_AttendanceAp~" FOREIGN KEY ("AttendanceApprovalId") REFERENCES "MentorAttendanceApprovals" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorEarningEntries_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorEarningEntries_MentorPayrollPeriods_PayrollPeriodId" FOREIGN KEY ("PayrollPeriodId") REFERENCES "MentorPayrollPeriods" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorEarningEntries_Users_CreatedByAdminId" FOREIGN KEY ("CreatedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorEarningEntries_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorPayrollAdjustments" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "PayrollPeriodId" bigint NOT NULL,
    "RelatedEarningEntryId" bigint,
    "RelatedBookingId" integer,
    "SignedAmount" numeric(18,2) NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "OperationKey" character varying(150) NOT NULL,
    "ApprovedByAdminId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorPayrollAdjustments" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorPayrollAdjustment_NonZero" CHECK ("SignedAmount" <> 0),
    CONSTRAINT "FK_MentorPayrollAdjustments_MentorPayrollPeriods_PayrollPeriod~" FOREIGN KEY ("PayrollPeriodId") REFERENCES "MentorPayrollPeriods" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayrollAdjustments_Users_ApprovedByAdminId" FOREIGN KEY ("ApprovedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorPayoutObligations" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "PayrollPeriodId" bigint NOT NULL,
    "MentorUserId" integer NOT NULL,
    "LockedBankAccountId" bigint,
    "OrganizationPayoutAccountId" bigint,
    "PayableAmount" numeric(18,2) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "SelectedMethod" character varying(30) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "ActiveAttemptId" bigint,
    "SettledAttemptId" bigint,
    "SettledReference" character varying(200),
    "SettledAtUtc" timestamp with time zone,
    "ApprovedByAdminId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorPayoutObligations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorPayoutObligation_Positive" CHECK ("PayableAmount" > 0),
    CONSTRAINT "CK_MentorPayoutObligation_Settlement" CHECK (("Status" <> 'Paid') OR ("SettledAttemptId" IS NOT NULL AND "SettledReference" IS NOT NULL AND "SettledAtUtc" IS NOT NULL)),
    CONSTRAINT "FK_MentorPayoutObligations_MentorBankAccounts_LockedBankAccoun~" FOREIGN KEY ("LockedBankAccountId") REFERENCES "MentorBankAccounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutObligations_MentorPayrollPeriods_PayrollPeriodId" FOREIGN KEY ("PayrollPeriodId") REFERENCES "MentorPayrollPeriods" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutObligations_OrganizationPayoutAccounts_Organiza~" FOREIGN KEY ("OrganizationPayoutAccountId") REFERENCES "OrganizationPayoutAccounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutObligations_Users_ApprovedByAdminId" FOREIGN KEY ("ApprovedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutObligations_Users_MentorUserId" FOREIGN KEY ("MentorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorPayrollLines" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "PayrollPeriodId" bigint NOT NULL,
    "EarningEntryId" bigint NOT NULL,
    "MentorBookingId" integer NOT NULL,
    "FrozenHourlyRate" numeric(18,2) NOT NULL,
    "FrozenLevelCoefficient" numeric(10,4) NOT NULL,
    "ApprovedEligibleMinutes" integer NOT NULL,
    "LineAmount" numeric(18,2) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MentorPayrollLines" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorPayrollLine_Positive" CHECK ("LineAmount" > 0 AND "ApprovedEligibleMinutes" > 0),
    CONSTRAINT "FK_MentorPayrollLines_MentorBookings_MentorBookingId" FOREIGN KEY ("MentorBookingId") REFERENCES "MentorBookings" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayrollLines_MentorEarningEntries_EarningEntryId" FOREIGN KEY ("EarningEntryId") REFERENCES "MentorEarningEntries" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayrollLines_MentorPayrollPeriods_PayrollPeriodId" FOREIGN KEY ("PayrollPeriodId") REFERENCES "MentorPayrollPeriods" ("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "MentorPayoutAttempts" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
    "PayoutObligationId" bigint NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "ExecutionMethod" character varying(30) NOT NULL,
    "IdempotencyKey" character varying(150) NOT NULL,
    "BatchId" character varying(150),
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(10) NOT NULL,
    "LockedBankAccountId" bigint,
    "InternalStatus" character varying(40) NOT NULL,
    "ProviderStatus" character varying(100),
    "ProviderPayoutId" character varying(200),
    "TransferReference" character varying(200),
    "EvidenceStorageKey" character varying(500),
    "EvidenceNotes" character varying(1000),
    "ErrorCode" character varying(100),
    "ErrorMessage" character varying(1000),
    "InitiatedByAdminId" integer NOT NULL,
    "ConfirmedByAdminId" integer,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "CompletedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MentorPayoutAttempts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MentorPayoutAttempt_ManualEvidence" CHECK (("ExecutionMethod" <> 'ManualBankTransfer' OR "InternalStatus" <> 'Succeeded') OR ("TransferReference" IS NOT NULL AND "EvidenceStorageKey" IS NOT NULL)),
    CONSTRAINT "CK_MentorPayoutAttempt_Positive" CHECK ("Amount" > 0 AND "AttemptNumber" >= 1),
    CONSTRAINT "FK_MentorPayoutAttempts_MentorBankAccounts_LockedBankAccountId" FOREIGN KEY ("LockedBankAccountId") REFERENCES "MentorBankAccounts" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutAttempts_MentorPayoutObligations_PayoutObligati~" FOREIGN KEY ("PayoutObligationId") REFERENCES "MentorPayoutObligations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutAttempts_Users_ConfirmedByAdminId" FOREIGN KEY ("ConfirmedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MentorPayoutAttempts_Users_InitiatedByAdminId" FOREIGN KEY ("InitiatedByAdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_MentorBookings_CompensationLevelId" ON "MentorBookings" ("CompensationLevelId");
CREATE INDEX IF NOT EXISTS "IX_MentorBookings_CreditGrantId" ON "MentorBookings" ("CreditGrantId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_JlptLevelPayCoefficients_CertificateLevelId_EffectiveFromUtc" ON "JlptLevelPayCoefficients" ("CertificateLevelId", "EffectiveFromUtc");
CREATE INDEX IF NOT EXISTS "IX_JlptLevelPayCoefficients_ConfiguredByAdminId" ON "JlptLevelPayCoefficients" ("ConfiguredByAdminId");
CREATE INDEX IF NOT EXISTS "IX_MentorHourlyRates_ConfiguredByAdminId" ON "MentorHourlyRates" ("ConfiguredByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorHourlyRates_MentorUserId_EffectiveFromUtc" ON "MentorHourlyRates" ("MentorUserId", "EffectiveFromUtc");
CREATE INDEX IF NOT EXISTS "IX_MentorTeachingQualifications_CertificateLevelId" ON "MentorTeachingQualifications" ("CertificateLevelId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorTeachingQualifications_MentorUserId_CertificateLevelId" ON "MentorTeachingQualifications" ("MentorUserId", "CertificateLevelId") WHERE "IsActive" = TRUE;
CREATE INDEX IF NOT EXISTS "IX_MentorTeachingQualifications_VerifiedByAdminId" ON "MentorTeachingQualifications" ("VerifiedByAdminId");
CREATE INDEX IF NOT EXISTS "IX_UserSubscriptions_CertificateLevelId" ON "UserSubscriptions" ("CertificateLevelId");
CREATE INDEX IF NOT EXISTS "IX_SubscriptionPlans_CertificateLevelId" ON "SubscriptionPlans" ("CertificateLevelId");
CREATE INDEX IF NOT EXISTS "IX_MentorCreditGrants_CertificateLevelId" ON "MentorCreditGrants" ("CertificateLevelId");
CREATE INDEX IF NOT EXISTS "IX_MentorCreditGrants_LearnerId_CertificateLevelId_ExpiresAtUtc" ON "MentorCreditGrants" ("LearnerId", "CertificateLevelId", "ExpiresAtUtc");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorAttendanceApprovals_MentorBookingId" ON "MentorAttendanceApprovals" ("MentorBookingId");
CREATE INDEX IF NOT EXISTS "IX_MentorAttendanceApprovals_ReviewedByAdminId" ON "MentorAttendanceApprovals" ("ReviewedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorBankAccounts_MentorUserId_AccountVersion" ON "MentorBankAccounts" ("MentorUserId", "AccountVersion");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorBankAccounts_MentorUserId_Current" ON "MentorBankAccounts" ("MentorUserId") WHERE "IsCurrent" = TRUE;
CREATE INDEX IF NOT EXISTS "IX_MentorBankAccounts_VerifiedByAdminId" ON "MentorBankAccounts" ("VerifiedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorEarningEntries_AttendanceApprovalId" ON "MentorEarningEntries" ("AttendanceApprovalId");
CREATE INDEX IF NOT EXISTS "IX_MentorEarningEntries_CertificateLevelId" ON "MentorEarningEntries" ("CertificateLevelId");
CREATE INDEX IF NOT EXISTS "IX_MentorEarningEntries_CreatedByAdminId" ON "MentorEarningEntries" ("CreatedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorEarningEntries_MentorBookingId" ON "MentorEarningEntries" ("MentorBookingId");
CREATE INDEX IF NOT EXISTS "IX_MentorEarningEntries_MentorUserId_Status" ON "MentorEarningEntries" ("MentorUserId", "Status");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorEarningEntries_OperationKey" ON "MentorEarningEntries" ("OperationKey");
CREATE INDEX IF NOT EXISTS "IX_MentorEarningEntries_PayrollPeriodId" ON "MentorEarningEntries" ("PayrollPeriodId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_ConfirmedByAdminId" ON "MentorPayoutAttempts" ("ConfirmedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_IdempotencyKey" ON "MentorPayoutAttempts" ("IdempotencyKey");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_InitiatedByAdminId" ON "MentorPayoutAttempts" ("InitiatedByAdminId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_LockedBankAccountId" ON "MentorPayoutAttempts" ("LockedBankAccountId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_OneSucceededPerObligation" ON "MentorPayoutAttempts" ("PayoutObligationId") WHERE "InternalStatus" = 'Succeeded';
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_PayoutObligationId_AttemptNumber" ON "MentorPayoutAttempts" ("PayoutObligationId", "AttemptNumber");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayoutAttempts_SucceededTransferReference" ON "MentorPayoutAttempts" ("TransferReference") WHERE "TransferReference" IS NOT NULL AND "InternalStatus" = 'Succeeded';
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutObligations_ApprovedByAdminId" ON "MentorPayoutObligations" ("ApprovedByAdminId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutObligations_LockedBankAccountId" ON "MentorPayoutObligations" ("LockedBankAccountId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutObligations_MentorUserId" ON "MentorPayoutObligations" ("MentorUserId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayoutObligations_OrganizationPayoutAccountId" ON "MentorPayoutObligations" ("OrganizationPayoutAccountId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayoutObligations_PayrollPeriodId" ON "MentorPayoutObligations" ("PayrollPeriodId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayrollAdjustments_ApprovedByAdminId" ON "MentorPayrollAdjustments" ("ApprovedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayrollAdjustments_OperationKey" ON "MentorPayrollAdjustments" ("OperationKey");
CREATE INDEX IF NOT EXISTS "IX_MentorPayrollAdjustments_PayrollPeriodId" ON "MentorPayrollAdjustments" ("PayrollPeriodId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayrollLines_EarningEntryId" ON "MentorPayrollLines" ("EarningEntryId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayrollLines_MentorBookingId" ON "MentorPayrollLines" ("MentorBookingId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayrollLines_PayrollPeriodId" ON "MentorPayrollLines" ("PayrollPeriodId");
CREATE INDEX IF NOT EXISTS "IX_MentorPayrollPeriods_ApprovedByAdminId" ON "MentorPayrollPeriods" ("ApprovedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MentorPayrollPeriods_MentorUserId_Year_Month_SequenceNumber~" ON "MentorPayrollPeriods" ("MentorUserId", "Year", "Month", "SequenceNumber", "Currency");
CREATE INDEX IF NOT EXISTS "IX_MentorPayrollPeriods_PreparedByAdminId" ON "MentorPayrollPeriods" ("PreparedByAdminId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrganizationPayoutAccounts_ChannelCode" ON "OrganizationPayoutAccounts" ("ChannelCode");
CREATE INDEX IF NOT EXISTS "IX_OrganizationPayoutAccounts_ConfiguredByAdminId" ON "OrganizationPayoutAccounts" ("ConfiguredByAdminId");

-- Add compensation FKs only if NO equivalent FK on the same source/target column already exists (avoids duplicate _fkey vs FK_*)
DO $comp_fks$
DECLARE
    fk_rec record;
    has_named boolean;
    has_equiv boolean;
    named_def text;
BEGIN
    FOR fk_rec IN
        SELECT * FROM (VALUES
            ('MentorBookings', 'FK_MentorBookings_CertificateLevels_CompensationLevelId', 'CompensationLevelId', 'CertificateLevels', 'Id'),
            ('MentorBookings', 'FK_MentorBookings_MentorCreditGrants_CreditGrantId', 'CreditGrantId', 'MentorCreditGrants', 'Id'),
            ('MentorCreditGrants', 'FK_MentorCreditGrants_CertificateLevels_CertificateLevelId', 'CertificateLevelId', 'CertificateLevels', 'Id'),
            ('SubscriptionPlans', 'FK_SubscriptionPlans_CertificateLevels_CertificateLevelId', 'CertificateLevelId', 'CertificateLevels', 'Id'),
            ('UserSubscriptions', 'FK_UserSubscriptions_CertificateLevels_CertificateLevelId', 'CertificateLevelId', 'CertificateLevels', 'Id')
        ) AS t(tbl, con_name, col_name, ref_tbl, ref_col)
    LOOP
        SELECT pg_get_constraintdef(c.oid) INTO named_def
        FROM pg_constraint c
        WHERE c.conrelid = format('public.%I', fk_rec.tbl)::regclass
          AND c.conname = fk_rec.con_name;

        IF named_def IS NOT NULL THEN
            IF named_def NOT LIKE format('FOREIGN KEY (%I) REFERENCES %I(%I)%%', fk_rec.col_name, fk_rec.ref_tbl, fk_rec.ref_col) THEN
                RAISE EXCEPTION 'Aborted: constraint "%" on "%" has incompatible definition: %', fk_rec.con_name, fk_rec.tbl, named_def;
            END IF;
            CONTINUE;
        END IF;

        -- Check structural equivalence (e.g., MentorCreditGrants_CertificateLevelId_fkey, SubscriptionPlans_CertificateLevelId_fkey)
        SELECT EXISTS (
            SELECT 1
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            JOIN pg_class ref ON ref.oid = c.confrelid
            JOIN pg_attribute ra ON ra.attrelid = c.confrelid AND ra.attnum = c.confkey[1]
            WHERE c.conrelid = format('public.%I', fk_rec.tbl)::regclass
              AND c.contype = 'f'
              AND array_length(c.conkey, 1) = 1
              AND a.attname = fk_rec.col_name
              AND ref.relname = fk_rec.ref_tbl
              AND ra.attname = fk_rec.ref_col
        ) INTO has_equiv;

        IF NOT has_equiv THEN
            EXECUTE format(
                'ALTER TABLE %I ADD CONSTRAINT %I FOREIGN KEY (%I) REFERENCES %I (%I) ON DELETE RESTRICT',
                fk_rec.tbl, fk_rec.con_name, fk_rec.col_name, fk_rec.ref_tbl, fk_rec.ref_col
            );
        END IF;
    END LOOP;
END $comp_fks$;

-- Validate previously NOT VALID credit-ledger FKs (aborts cleanly if orphan references exist)
DO $validate_not_valid_fks$
DECLARE
    nv record;
BEGIN
    FOR nv IN
        SELECT cls.relname AS tbl, con.conname
        FROM pg_constraint con
        JOIN pg_class cls ON cls.oid = con.conrelid
        JOIN pg_namespace ns ON ns.oid = cls.relnamespace
        WHERE ns.nspname = 'public'
          AND con.contype = 'f'
          AND NOT con.convalidated
          AND cls.relname IN ('MentorBookings', 'MentorCreditEntries')
          AND con.conname IN (
              'FK_MentorBookings_MentorCreditGrants_CreditGrantId',
              'FK_MentorCreditEntries_MentorCreditGrants_CreditGrantId'
          )
        ORDER BY cls.relname, con.conname
    LOOP
        EXECUTE format('ALTER TABLE %I VALIDATE CONSTRAINT %I', nv.tbl, nv.conname);
    END LOOP;
END $validate_not_valid_fks$;

-- ============================================================================
-- 4. Strict Structure Verification of ALL Critical Indexes & Constraints (Scenarios 4 & 5)
-- ============================================================================
DO $verify_objects$
DECLARE
    idx_rec record;
    con_rec record;
    actual_tbl text;
    actual_unique boolean;
    actual_def text;
    expected_unique boolean;
    has_learner_col boolean;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MentorBookings' AND column_name = 'LearnerId'
    ) INTO has_learner_col;

    -- Scenario 5: Fail closed if any named index exists on the wrong table, wrong uniqueness, wrong columns, or wrong predicate.
    FOR idx_rec IN
        SELECT * FROM (VALUES
            ('MentorAvailabilities', 'IX_MentorAvailabilities_GenerationKey', true, ' ("GenerationKey")', '"GenerationKey" IS NOT NULL'),
            ('MentorAvailabilities', 'IX_MentorAvailabilities_MentorScheduleRuleId', false, ' ("MentorScheduleRuleId")', NULL),
            ('MentorBookingIntents', 'IX_MentorBookingIntents_LearnerId_IntentKey', true, ' ("LearnerId", "IntentKey")', NULL),
            ('MentorBookingIntents', 'IX_MentorBookingIntents_MentorBookingId', false, ' ("MentorBookingId")', NULL),
            ('MentorBookingReservations', 'IX_MentorBookingReservations_MentorAvailabilityId_MentorId', false, ' ("MentorAvailabilityId", "MentorId")', NULL),
            ('MentorBookingReservations', 'IX_MentorBookingReservations_MentorBookingId', true, ' ("MentorBookingId")', NULL),
            ('MentorBookingReservations', 'IX_MentorBookingReservations_MentorId_LocalBusinessDate_Releas~', false, ' ("MentorId", "LocalBusinessDate", "ReleasedAtUtc")', NULL),
            ('MentorBookingTerms', 'IX_MentorBookingTerms_MentorAvailabilityId', false, ' ("MentorAvailabilityId")', NULL),
            ('MentorScheduleRules', 'IX_MentorScheduleRules_ScheduleId_Weekday_DaypartCode_Effectiv~', true, ' ("ScheduleId", "Weekday", "DaypartCode", "EffectiveFrom")', NULL),
            ('MentorSchedules', 'IX_MentorSchedules_MentorId_PolicyVersion', true, ' ("MentorId", "PolicyVersion")', NULL),
            ('MentorBookings', 'IX_MentorBookings_CompensationLevelId', false, ' ("CompensationLevelId")', NULL),
            ('MentorBookings', 'IX_MentorBookings_CreditGrantId', false, ' ("CreditGrantId")', NULL),
            ('JlptLevelPayCoefficients', 'IX_JlptLevelPayCoefficients_CertificateLevelId_EffectiveFromUtc', true, ' ("CertificateLevelId", "EffectiveFromUtc")', NULL),
            ('MentorHourlyRates', 'IX_MentorHourlyRates_MentorUserId_EffectiveFromUtc', true, ' ("MentorUserId", "EffectiveFromUtc")', NULL),
            ('MentorTeachingQualifications', 'IX_MentorTeachingQualifications_MentorUserId_CertificateLevelId', true, ' ("MentorUserId", "CertificateLevelId")', '"IsActive" = true'),
            ('MentorAttendanceApprovals', 'IX_MentorAttendanceApprovals_MentorBookingId', true, ' ("MentorBookingId")', NULL),
            ('MentorBankAccounts', 'IX_MentorBankAccounts_MentorUserId_AccountVersion', true, ' ("MentorUserId", "AccountVersion")', NULL),
            ('MentorBankAccounts', 'IX_MentorBankAccounts_MentorUserId_Current', true, ' ("MentorUserId")', '"IsCurrent" = true'),
            ('MentorEarningEntries', 'IX_MentorEarningEntries_OperationKey', true, ' ("OperationKey")', NULL),
            ('MentorEarningEntries', 'IX_MentorEarningEntries_MentorBookingId', true, ' ("MentorBookingId")', NULL),
            ('MentorPayoutAttempts', 'IX_MentorPayoutAttempts_IdempotencyKey', true, ' ("IdempotencyKey")', NULL),
            ('MentorPayoutAttempts', 'IX_MentorPayoutAttempts_OneSucceededPerObligation', true, ' ("PayoutObligationId")', '"InternalStatus"'),
            ('MentorPayoutObligations', 'IX_MentorPayoutObligations_PayrollPeriodId', true, ' ("PayrollPeriodId")', NULL),
            ('MentorPayrollAdjustments', 'IX_MentorPayrollAdjustments_OperationKey', true, ' ("OperationKey")', NULL),
            ('MentorPayrollLines', 'IX_MentorPayrollLines_MentorBookingId', true, ' ("MentorBookingId")', NULL),
            ('MentorPayrollPeriods', 'IX_MentorPayrollPeriods_MentorUserId_Year_Month_SequenceNumber~', true, ' ("MentorUserId", "Year", "Month", "SequenceNumber", "Currency")', NULL),
            ('OrganizationPayoutAccounts', 'IX_OrganizationPayoutAccounts_ChannelCode', true, ' ("ChannelCode")', NULL)
        ) AS t(tbl, idx_name, exp_unique, exp_cols, exp_where)
    LOOP
        SELECT tbl_cls.relname, ix.indisunique, pg_get_indexdef(ix.indexrelid)
        INTO actual_tbl, actual_unique, actual_def
        FROM pg_class idx_cls
        JOIN pg_namespace n ON n.oid = idx_cls.relnamespace
        JOIN pg_index ix ON ix.indexrelid = idx_cls.oid
        JOIN pg_class tbl_cls ON tbl_cls.oid = ix.indrelid
        WHERE n.nspname = 'public' AND idx_cls.relname = idx_rec.idx_name;

        IF actual_def IS NULL THEN
            RAISE EXCEPTION 'Aborted: expected index "%" on "%" is missing.', idx_rec.idx_name, idx_rec.tbl;
        END IF;
        IF actual_tbl <> idx_rec.tbl OR actual_unique <> idx_rec.exp_unique OR actual_def NOT LIKE '%' || idx_rec.exp_cols || '%' THEN
            RAISE EXCEPTION 'Aborted: index "%" has incompatible structure (table=%, unique=%, def=%).', idx_rec.idx_name, actual_tbl, actual_unique, actual_def;
        END IF;
        IF idx_rec.exp_where IS NOT NULL AND position(lower(idx_rec.exp_where) in lower(actual_def)) = 0 THEN
            RAISE EXCEPTION 'Aborted: index "%" predicate mismatch (expected "%", actual "%").', idx_rec.idx_name, idx_rec.exp_where, actual_def;
        END IF;
        IF idx_rec.exp_where IS NULL AND position(' WHERE ' in actual_def) > 0 THEN
            RAISE EXCEPTION 'Aborted: index "%" has unexpected WHERE predicate: %', idx_rec.idx_name, actual_def;
        END IF;
    END LOOP;

    -- Special pre/post Group Learning index check on IX_MentorBookingReservations_MentorAvailabilityId
    expected_unique := NOT has_learner_col;
    SELECT tbl_cls.relname, ix.indisunique, pg_get_indexdef(ix.indexrelid)
    INTO actual_tbl, actual_unique, actual_def
    FROM pg_class idx_cls
    JOIN pg_namespace n ON n.oid = idx_cls.relnamespace
    JOIN pg_index ix ON ix.indexrelid = idx_cls.oid
    JOIN pg_class tbl_cls ON tbl_cls.oid = ix.indrelid
    WHERE n.nspname = 'public' AND idx_cls.relname = 'IX_MentorBookingReservations_MentorAvailabilityId';

    IF actual_def IS NULL OR actual_tbl <> 'MentorBookingReservations'
       OR actual_unique <> expected_unique
       OR actual_def NOT LIKE '%("MentorAvailabilityId")%'
       OR position('releasedatutc" is null' in lower(actual_def)) = 0 THEN
        RAISE EXCEPTION 'Aborted: index "IX_MentorBookingReservations_MentorAvailabilityId" has incompatible definition (expected unique=%, actual=%).', expected_unique, actual_def;
    END IF;

    -- Scenario 4: Verify check constraints on pre-existing or newly ensured tables match expected semantics.
    FOR con_rec IN
        SELECT * FROM (VALUES
            ('MentorBookingDayGuards', 'CK_MentorBookingDayGuard_Count', '"ActiveBookingCount" >= 0'),
            ('MentorBookingReservations', 'CK_MentorBookingReservation_Interval', '"EndUtc" > "StartUtc"'),
            ('MentorBookingReservations', 'CK_MentorBookingReservation_PolicyVersion', '"PolicyVersion" > 0'),
            ('MentorBookingTerms', 'CK_MentorBookingTerms_Interval', '"ScheduledEndUtc" > "ScheduledStartUtc"'),
            ('MentorSchedules', 'CK_MentorSchedule_PolicyVersion', '"PolicyVersion" > 0'),
            ('MentorScheduleRules', 'CK_MentorScheduleRule_Weekday', '"Weekday" >= 0 AND "Weekday" <= 6'),
            ('MentorScheduleRules', 'CK_MentorScheduleRule_Time', '"LocalEnd" > "LocalStart"'),
            ('JlptLevelPayCoefficients', 'CK_JlptLevelPayCoefficient_Positive', '"Coefficient" > 0'),
            ('MentorHourlyRates', 'CK_MentorHourlyRate_Positive', '"AmountPerHour" > 0'),
            ('MentorAttendanceApprovals', 'CK_MentorAttendanceApproval_Minutes', '90'),
            ('MentorBankAccounts', 'CK_MentorBankAccount_Version', '"AccountVersion" >= 1'),
            ('MentorPayrollPeriods', 'CK_MentorPayrollPeriod_Payable', '"GrossPayableAmount" >= 0'),
            ('MentorEarningEntries', 'CK_MentorEarningEntry_Positive', '"GrossAmount" > 0'),
            ('MentorPayrollAdjustments', 'CK_MentorPayrollAdjustment_NonZero', '"SignedAmount" <> 0'),
            ('MentorPayoutObligations', 'CK_MentorPayoutObligation_Positive', '"PayableAmount" > 0'),
            ('MentorPayrollLines', 'CK_MentorPayrollLine_Positive', '"LineAmount" > 0'),
            ('MentorPayoutAttempts', 'CK_MentorPayoutAttempt_Positive', '"Amount" > 0')
        ) AS t(tbl, con_name, expected_fragment)
    LOOP
        SELECT pg_get_constraintdef(c.oid) INTO actual_def
        FROM pg_constraint c
        WHERE c.conrelid = format('public.%I', con_rec.tbl)::regclass
          AND c.conname = con_rec.con_name;

        IF actual_def IS NULL OR position(replace(replace(replace(con_rec.expected_fragment, '(', ''), ')', ''), ' ', '') in replace(replace(replace(actual_def, '(', ''), ')', ''), ' ', '')) = 0 THEN
            RAISE EXCEPTION 'Aborted: check constraint "%" on "%" mismatch (expected fragment "%", actual "%").', con_rec.con_name, con_rec.tbl, con_rec.expected_fragment, actual_def;
        END IF;
    END LOOP;

    SELECT pg_get_constraintdef(c.oid) INTO actual_def
    FROM pg_constraint c
    WHERE c.conrelid = 'public."MentorBookingReservations"'::regclass
      AND c.conname = 'CK_MentorBookingReservation_Rest';
    IF actual_def IS NULL OR (position('RestProtectedUntilUtc' in actual_def) = 0 OR (position('15 minutes' in actual_def) = 0 AND position('00:15:00' in actual_def) = 0 AND position('>=' in actual_def) = 0)) THEN
        RAISE EXCEPTION 'Aborted: check constraint "CK_MentorBookingReservation_Rest" mismatch: %', actual_def;
    END IF;
END $verify_objects$;

-- ============================================================================
-- 5. Schema-Verified History Reconciliation (Scenario 3: Full Parity Required)
-- ============================================================================
DO $reconcile_history$
DECLARE
    col_count integer;
    actual_def text;
    idx_unique boolean;
    idx_def text;
BEGIN
    -- 5a. 20261030153753_AddMentorProfileEntity
    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'MentorProfiles'
      AND column_name IN (
        'MentorId','Bio','Expertise','JlptLevel','YearsOfExperience','Workplace',
        'VerificationDocumentUrl','IntroductionVideoUrl','VerificationStatus',
        'SubmittedAt','ReviewedAt','ReviewedByAdminName','RejectionReason',
        'ReviewNotes','CreatedDate','ModifiedDate','CreatedBy','ModifiedBy','IsDeleted'
      );
    IF col_count <> 19 OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'public."MentorProfiles"'::regclass AND c.contype = 'p'
    ) OR NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        WHERE c.conrelid = 'public."MentorProfiles"'::regclass AND c.contype = 'f' AND c.conname = 'FK_MentorProfiles_Users_MentorId'
    ) THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261030153753_AddMentorProfileEntity ("MentorProfiles").';
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030153753_AddMentorProfileEntity', '8.0.8')
    ON CONFLICT ("MigrationId") DO NOTHING;

    -- 5b. 20261031082840_AddMentorCreditLedgerPersistence (full column + constraint + index checks)
    IF to_regclass('public."MentorCreditGrants"') IS NULL OR to_regclass('public."MentorCreditEntries"') IS NULL THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261031082840_AddMentorCreditLedgerPersistence (missing tables).';
    END IF;

    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'MentorCreditGrants'
      AND column_name IN (
        'Id','LearnerId','UserSubscriptionId','PolicyVersion','InitialQuantity',
        'AvailableQuantity','EffectiveFromUtc','ExpiresAtUtc','CreatedAtUtc','CertificateLevelId'
      );
    IF col_count <> 10 THEN
        RAISE EXCEPTION 'Aborted: "MentorCreditGrants" partial column mismatch (found % of 10 expected columns).', col_count;
    END IF;

    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'MentorCreditEntries'
      AND column_name IN (
        'Id','CreditGrantId','LearnerId','EntryType','Amount','OperationKey',
        'MentorBookingId','Reason','CreatedAtUtc'
      );
    IF col_count <> 9 THEN
        RAISE EXCEPTION 'Aborted: "MentorCreditEntries" partial column mismatch (found % of 9 expected columns).', col_count;
    END IF;

    SELECT ix.indisunique, pg_get_indexdef(ix.indexrelid)
    INTO idx_unique, idx_def
    FROM pg_class ic JOIN pg_namespace n ON n.oid = ic.relnamespace JOIN pg_index ix ON ix.indexrelid = ic.oid
    WHERE n.nspname = 'public' AND ic.relname = 'IX_MentorCreditEntries_OperationKey';
    IF NOT COALESCE(idx_unique, false) OR idx_def NOT LIKE '%("OperationKey")%' THEN
        RAISE EXCEPTION 'Aborted: index "IX_MentorCreditEntries_OperationKey" missing or non-unique (%).', idx_def;
    END IF;

    SELECT ix.indisunique, pg_get_indexdef(ix.indexrelid)
    INTO idx_unique, idx_def
    FROM pg_class ic JOIN pg_namespace n ON n.oid = ic.relnamespace JOIN pg_index ix ON ix.indexrelid = ic.oid
    WHERE n.nspname = 'public' AND ic.relname = 'IX_MentorCreditGrants_UserSubscriptionId';
    IF NOT COALESCE(idx_unique, false) OR idx_def NOT LIKE '%("UserSubscriptionId")%' THEN
        RAISE EXCEPTION 'Aborted: index "IX_MentorCreditGrants_UserSubscriptionId" missing or non-unique (%).', idx_def;
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031082840_AddMentorCreditLedgerPersistence', '8.0.8')
    ON CONFLICT ("MigrationId") DO NOTHING;

    -- 5c. 20261031104216_AddMentorBookingCancellationMetadata
    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'MentorBookings'
      AND column_name IN (
        'CancellationReason','CancelledAtUtc','CancelledByRole','CancelledByUserId',
        'ExceptionApproved','ExceptionReason','ExceptionRequested'
      );
    IF col_count <> 7 OR NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MentorBookings'
          AND column_name = 'ExceptionRequested' AND is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261031104216_AddMentorBookingCancellationMetadata.';
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031104216_AddMentorBookingCancellationMetadata', '8.0.8')
    ON CONFLICT ("MigrationId") DO NOTHING;

    -- 5d. 20261101083259_AddNotificationDeliveryPersistence
    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'Notifications'
      AND column_name IN ('DeliveredTokens', 'IdempotencyKey');
    SELECT ix.indisunique, pg_get_indexdef(ix.indexrelid)
    INTO idx_unique, idx_def
    FROM pg_class ic JOIN pg_namespace n ON n.oid = ic.relnamespace JOIN pg_index ix ON ix.indexrelid = ic.oid
    WHERE n.nspname = 'public' AND ic.relname = 'IX_Notifications_IdempotencyKey';

    IF col_count <> 2 OR NOT COALESCE(idx_unique, false)
       OR idx_def NOT LIKE '%("IdempotencyKey")%'
       OR position('idempotencykey" is not null' in lower(idx_def)) = 0 THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261101083259_AddNotificationDeliveryPersistence (%).', idx_def;
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101083259_AddNotificationDeliveryPersistence', '8.0.8')
    ON CONFLICT ("MigrationId") DO NOTHING;

    -- 5e. 20261104103624, 20261104104226 (UTC column types), 20261104115618 (all 12 tables + added columns)
    IF to_regclass('public."JlptLevelPayCoefficients"') IS NULL
       OR to_regclass('public."MentorHourlyRates"') IS NULL
       OR to_regclass('public."MentorTeachingQualifications"') IS NULL
       OR to_regclass('public."MentorAttendanceApprovals"') IS NULL
       OR to_regclass('public."MentorBankAccounts"') IS NULL
       OR to_regclass('public."MentorPayrollPeriods"') IS NULL
       OR to_regclass('public."OrganizationPayoutAccounts"') IS NULL
       OR to_regclass('public."MentorEarningEntries"') IS NULL
       OR to_regclass('public."MentorPayrollAdjustments"') IS NULL
       OR to_regclass('public."MentorPayoutObligations"') IS NULL
       OR to_regclass('public."MentorPayrollLines"') IS NULL
       OR to_regclass('public."MentorPayoutAttempts"') IS NULL THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261104* Compensation tables.';
    END IF;

    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'MentorBookings'
      AND column_name IN (
        'CompensationFormulaVersion','CompensationLevelId','FrozenHourlyRate',
        'FrozenLevelCoefficient','ScheduledCompensationAmount','CompensationCurrency','CreditGrantId'
      );
    IF col_count <> 7 THEN
        RAISE EXCEPTION 'Aborted: "MentorBookings" missing compensation columns from 20261104* (% of 7 found).', col_count;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='UserSubscriptions' AND column_name='CertificateLevelId')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='SubscriptionPlans' AND column_name='CertificateLevelId') THEN
        RAISE EXCEPTION 'Aborted: "UserSubscriptions" or "SubscriptionPlans" missing "CertificateLevelId".';
    END IF;

    -- Verify all 8 UTC timestamptz columns required by 20261104104226_UseUtcForMentorCompensation
    SELECT COUNT(*) INTO col_count
    FROM information_schema.columns
    WHERE table_schema = 'public' AND data_type = 'timestamp with time zone'
      AND (
        (table_name = 'MentorTeachingQualifications' AND column_name IN ('VerifiedAtUtc', 'RevokedAtUtc'))
        OR (table_name = 'MentorHourlyRates' AND column_name IN ('EffectiveFromUtc', 'EffectiveUntilUtc', 'CreatedAtUtc'))
        OR (table_name = 'JlptLevelPayCoefficients' AND column_name IN ('EffectiveFromUtc', 'EffectiveUntilUtc', 'CreatedAtUtc'))
      );
    IF col_count <> 8 THEN
        RAISE EXCEPTION 'Aborted: schema verification failed for 20261104104226_UseUtcForMentorCompensation (found % of 8 timestamptz columns).', col_count;
    END IF;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
        ('20261104103624_AddMentorCompensationFoundation', '8.0.8'),
        ('20261104104226_UseUtcForMentorCompensation', '8.0.8'),
        ('20261104115618_AddMentorCompensationLedgerAndDualPayout', '8.0.8')
    ON CONFLICT ("MigrationId") DO NOTHING;
END $reconcile_history$;

COMMIT;
