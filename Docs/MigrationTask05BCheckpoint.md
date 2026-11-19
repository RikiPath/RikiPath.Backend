# TASK 05B — Migration deployment checkpoint (2026-11-19)

**Status: BLOCKED for Shared Supabase deployment.** No remote DDL/DML, migration-history edits, commit or push were performed.

## Source and live baseline

- `20261117121238_AddLearnerFeedback` is authored on `origin/fix/import-file` at `1b9a90462e8bf9b205e406f96bc663f620dcdbb3`, not on canonical `origin/main`. Do not merge that entire branch: it also carries the destructive `RetireLegacyStudyCourseModel` path. The migration's table, nine columns, PK, cascade FK, rating CHECK, two secondary indexes, and RLS state match the live table. The table, `MentorReviews`, and affected review notifications each held zero rows at inspection, so the migration's historical DML had no current rows to affect. The source still needs an independently reviewed canonical integration before a normal future deploy.
- Shared Supabase read-only preflight: 36 history rows, 11 canonical pending, all six externally applied IDs now structurally audited, 80 public tables, 18 missing Scheduling/Compensation tables, `btree_gist` not installed, two credit FKs `NOT VALID`. The six Scheduling and twelve Compensation tables are absent even though their source migration IDs are already recorded; history alone is not proof of deployed schema.
- `btree_gist` version 1.7 is available and the current role reports database/schema `CREATE` privileges. Actual creation was **not** attempted on Shared. It succeeded on isolated QA.

## QA rehearsal and backup evidence

- Full logical dump of Shared was taken read-only into `%LOCALAPPDATA%\RikiPath\qa\task05b-20261119\shared_full_restore_test.dump`; SHA-256 `04FDB64B1462ECFBFFA5CC6390FC1031CAF35FD3A3C57A669ED292028AFFB694`. Windows ACL permits only `ADMIN` and `SYSTEM`. This contains real data: treat as sensitive and delete under the retention policy after approval/cancellation.
- Full Supabase-specific schemas/extensions are not portable to ordinary local PostgreSQL; the **public application schema and all its data** were restored from that full dump into QA `127.0.0.1:55439/rikipath_task05b_full_restore_retry_20261119`. Baseline: 36 migration rows, 80 tables, 12 users, 4 mentor bookings, 1 study cohort. A separate schema+history clone was also rehearsed.
- Reconciliation succeeded twice on the schema clone with unchanged second-run counts. On the data-bearing restore, reconciliation and all 11 pending canonical EF migrations succeeded. Final: 47 migration rows, 108 tables, 12 users, 4 mentor bookings, 1 cohort, 18/18 repaired Scheduling/Compensation tables, `btree_gist` installed, zero unvalidated FKs, active booking-rest exclusion and group policy row. Read-only preflight PASS on QA and BLOCKED (exit 2) on Shared.
- Focused governance/design-time tests: 33 PASS, 0 FAIL, isolated build output. QA Web API smoke: Swagger 200, subscription plans 200, protected study-plan and admin-policy endpoints 401 without a token (expected authorization). Authenticated role-specific APIs were **not** exercised. API process was stopped after the smoke check.

## Changes and release gates

- `ReconcileSharedMigrationHistory.sql` now accepts AddLearnerFeedback only after full structural/RLS checks; it does not recreate or restamp this external migration. It remains atomic and additive.
- `verify-migration-readiness.ps1` checks the feedback artifact, all 18 repaired tables, `btree_gist`, and unvalidated FKs; missing prerequisites now fail closed.
- Migration governance tests no longer pin the valid migration count to 41. A PR workflow and a pre-publish deploy gate run focused offline governance tests. Repository branch protection must make the PR check required; a YAML file alone cannot enforce that setting.
- Before any Shared deployment: obtain a Supabase-native PITR/snapshot restore test for non-public platform schemas, arrange and attest a team-wide migration write freeze, recheck the latest Shared history/schema fingerprint and backup, independently review the external migration's canonical integration and the complete release diff, then obtain explicit Product Owner approval for a named operator/window. An approval based on this checkpoint becomes invalid if Shared changes.
