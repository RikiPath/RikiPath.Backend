-- Phase 4.1 Strict READ-ONLY Schema & Precondition Preflight
-- Safe for Shared Supabase read-only inspection: NEVER performs DDL, DML, or CREATE EXTENSION.
-- Execute with psql -v ON_ERROR_STOP=1.

BEGIN READ ONLY;
SET statement_timeout = '25s';

SELECT '01_target_db' AS check_key, current_database() AS observed_value
UNION ALL
SELECT '02_server_version', current_setting('server_version')
UNION ALL
SELECT '03_current_user', current_user
UNION ALL
SELECT '04_public_create_priv', has_schema_privilege(current_user, 'public', 'CREATE')::text
UNION ALL
SELECT '05_btree_gist_installed', EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'btree_gist')::text
UNION ALL
SELECT '06_btree_gist_available', EXISTS(SELECT 1 FROM pg_available_extensions WHERE name = 'btree_gist')::text
UNION ALL
SELECT '07_gist_int4_ops_visible', EXISTS(
    SELECT 1
    FROM pg_opclass opc
    JOIN pg_am am ON am.oid = opc.opcmethod
    JOIN pg_namespace n ON n.oid = opc.opcnamespace
    WHERE am.amname = 'gist'
      AND opc.opcname = 'gist_int4_ops'
      AND n.nspname = ANY (current_schemas(false))
)::text
UNION ALL
SELECT '08_ef_migrations_count', (SELECT COUNT(*)::text FROM "__EFMigrationsHistory")
UNION ALL
SELECT '09_ef_history_md5', md5(COALESCE((SELECT string_agg("MigrationId", ',' ORDER BY "MigrationId") FROM "__EFMigrationsHistory"), ''))
UNION ALL
SELECT '10_public_tables_count', (SELECT COUNT(*)::text FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE')
UNION ALL
SELECT '11_public_columns_count', (SELECT COUNT(*)::text FROM information_schema.columns WHERE table_schema = 'public')
UNION ALL
SELECT '12_public_columns_md5', md5(COALESCE((
    SELECT string_agg(
        table_name || '.' || column_name || ':' || data_type || ':' || COALESCE(character_maximum_length::text, '') || ':' || is_nullable || ':' || COALESCE(column_default, ''),
        E'\n' ORDER BY table_name, ordinal_position
    )
    FROM information_schema.columns
    WHERE table_schema = 'public'
), ''))
UNION ALL
SELECT '13_public_constraints_count', (
    SELECT COUNT(*)::text
    FROM pg_constraint c
    JOIN pg_class r ON r.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = r.relnamespace
    WHERE n.nspname = 'public'
)
UNION ALL
SELECT '14_public_constraints_md5', md5(COALESCE((
    SELECT string_agg(r.relname || '.' || c.conname || ':' || c.contype::text || ':' || pg_get_constraintdef(c.oid), E'\n' ORDER BY r.relname, c.conname)
    FROM pg_constraint c
    JOIN pg_class r ON r.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = r.relnamespace
    WHERE n.nspname = 'public'
), ''))
UNION ALL
SELECT '15_public_indexes_count', (SELECT COUNT(*)::text FROM pg_indexes WHERE schemaname = 'public')
UNION ALL
SELECT '16_unvalidated_constraints', (
    SELECT COUNT(*)::text
    FROM pg_constraint c
    JOIN pg_class r ON r.oid = c.conrelid
    JOIN pg_namespace n ON n.oid = r.relnamespace
    WHERE n.nspname = 'public' AND NOT c.convalidated
)
UNION ALL
SELECT '17_excl_constraint_present', (
    SELECT EXISTS(
        SELECT 1 FROM pg_constraint c
        JOIN pg_class r ON r.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = r.relnamespace
        WHERE n.nspname = 'public'
          AND r.relname = 'MentorBookingReservations'
          AND c.conname = 'EX_MentorBookingReservation_ActiveMentorRest'
          AND c.convalidated
    )::text
)
UNION ALL
SELECT '18_cert_types_dup_norm_name', (SELECT COUNT(*)::text FROM (SELECT lower(btrim("Name")) FROM "CertificateTypes" GROUP BY 1 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '19_cert_types_dup_sort_order', (SELECT COUNT(*)::text FROM (SELECT "SortOrder" FROM "CertificateTypes" GROUP BY 1 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '20_cert_levels_jlpt_active', (
    SELECT COUNT(*)::text
    FROM "CertificateLevels" l
    JOIN "CertificateTypes" t ON t."Id" = l."CertificationId"
    WHERE t."Name" = 'JLPT' AND l."Code" IN ('N5','N4','N3','N2','N1') AND NOT l."IsDeleted" AND NOT t."IsDeleted"
)
UNION ALL
SELECT '21_cert_levels_dup_norm_code', (SELECT COUNT(*)::text FROM (SELECT "CertificationId", upper(btrim("Code")) FROM "CertificateLevels" GROUP BY 1,2 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '22_cert_levels_dup_sort_order', (SELECT COUNT(*)::text FROM (SELECT "CertificationId", "SortOrder" FROM "CertificateLevels" GROUP BY 1,2 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '23_cert_sections_n5_n2_active', (
    SELECT COUNT(*)::text
    FROM "CertificateSections" s
    JOIN "CertificateLevels" l ON l."Id" = s."CertificationLevelId"
    JOIN "CertificateTypes" t ON t."Id" = l."CertificationId"
    WHERE t."Name" = 'JLPT' AND l."Code" IN ('N5','N4','N3','N2') AND NOT l."IsDeleted" AND NOT s."IsDeleted"
)
UNION ALL
SELECT '24_cert_sections_dup_active_code', (SELECT COUNT(*)::text FROM (SELECT "CertificationLevelId", "Code" FROM "CertificateSections" WHERE NOT "IsDeleted" GROUP BY 1,2 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '25_cert_sections_general_active', (SELECT COUNT(*)::text FROM "CertificateSections" WHERE NOT "IsDeleted" AND "Code" IN ('N3_GENERAL','N2_GENERAL'))
UNION ALL
SELECT '26_mocktestsections_general_refs', (
    SELECT COUNT(*)::text
    FROM "MockTestSections" m
    JOIN "CertificateSections" s ON s."Id" = m."CertificationSectionId"
    WHERE s."Code" IN ('N3_GENERAL','N2_GENERAL')
)
UNION ALL
SELECT '27_cert_sections_zero_sortorder', (SELECT COUNT(*)::text FROM "CertificateSections" WHERE NOT "IsDeleted" AND "SortOrder" <= 0)
UNION ALL
SELECT '28_features_count', (SELECT COUNT(*)::text FROM "Features")
UNION ALL
SELECT '29_features_has_code_col', EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Features' AND column_name='Code')::text
UNION ALL
SELECT '30_booking_res_active_overlaps', (
    SELECT COUNT(*)::text
    FROM "MentorBookingReservations" a
    JOIN "MentorBookingReservations" b
      ON a."Id" < b."Id"
     AND a."MentorId" = b."MentorId"
     AND a."MentorAvailabilityId" = b."MentorAvailabilityId"
     AND a."ReleasedAtUtc" IS NULL
     AND b."ReleasedAtUtc" IS NULL
     AND tstzrange(a."StartUtc", a."RestProtectedUntilUtc", '[)') && tstzrange(b."StartUtc", b."RestProtectedUntilUtc", '[)')
)
UNION ALL
SELECT '31_booking_res_invalid_range', (SELECT COUNT(*)::text FROM "MentorBookingReservations" WHERE "ReleasedAtUtc" IS NULL AND "RestProtectedUntilUtc" <= "StartUtc")
UNION ALL
SELECT '32_reviewcards_dup_kanji', (SELECT COUNT(*)::text FROM (SELECT "UserId", "KanjiEntryId", "Mode" FROM "ReviewCards" WHERE "KanjiEntryId" IS NOT NULL GROUP BY 1,2,3 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '33_reviewcards_dup_grammar', (SELECT COUNT(*)::text FROM (SELECT "UserId", "GrammarPointId", "Mode" FROM "ReviewCards" WHERE "GrammarPointId" IS NOT NULL GROUP BY 1,2,3 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '34_cohorts_invalid_capacity', (SELECT COUNT(*)::text FROM "StudyCohorts" WHERE "Capacity" <= 0)
UNION ALL
SELECT '35_studycourses_dup_active', (SELECT COUNT(*)::text FROM (SELECT "CohortId", "LearnerId" FROM "StudyCourses" WHERE "CohortId" IS NOT NULL AND "Status" = 'Active' GROUP BY 1,2 HAVING COUNT(*) > 1) s)
UNION ALL
SELECT '36_orphan_creditgrant_refs', (
    (SELECT COUNT(*) FROM "MentorBookings" b WHERE b."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = b."CreditGrantId"))
    + (SELECT COUNT(*) FROM "MentorCreditEntries" e WHERE e."CreditGrantId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "MentorCreditGrants" g WHERE g."Id" = e."CreditGrantId"))
)::text
UNION ALL
SELECT '37_avail_generated_null_rule', (SELECT COUNT(*)::text FROM "MentorAvailabilities" m WHERE m."GenerationKey" IS NOT NULL AND m."MentorScheduleRuleId" IS NULL AND NULLIF(to_jsonb(m) ->> 'TeachingProposalId', '') IS NULL)
ORDER BY check_key;

ROLLBACK;
