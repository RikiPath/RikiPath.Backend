# RikiPath.Backend — Migration Release Readiness, Deployment & Recovery Package

> Current decision and latest evidence: [TASK 05C release package](MigrationTask05CReleasePackage.md). Sections below preserve the earlier TASK 05/05B audit and may describe a superseded baseline; do not execute them without the TASK 05C gates.

**Branch Baseline:** `feat/single-full-name-field` (`f3694c5`) / `origin/main` (`1100de5`, PR #74 — identical tree)  
**Canonical Source Migrations:** `41` (`20261027081217_InitalCreate` → `20261117171728_AddGroupLearningRulesLifecycleFeedback`)  
**Audited Shared Supabase Cross-Branch Migrations:** `6` including the structurally verified `20261117121238_AddLearnerFeedback`; canonical source integration remains pending  
**Deployment Gate Status (TASK 05 — 2026-11-19):** **`BLOCKED`** — **DO NOT apply to Shared Supabase.** Live `"__EFMigrationsHistory"` drifted from the `27`-row TASK 04 baseline to `36` rows (`verify-migration-readiness.ps1` exits `2`; `ReconcileSharedMigrationHistory.sql` aborts fail-closed).

---

## 1. Cross-Branch Migration Reconciliation Summary & Live Drift Finding

**Expected TASK 04 Baseline vs Live Shared Supabase Reality (`aws-0-ap-southeast-1.pooler.supabase.com:5432`, DB `postgres`, PG 17.6):**
- **TASK 04 Baseline (`27` rows):** `22` canonical repo IDs + `5` audited cross-branch IDs (`19` pending: `6` historical reconciliation + `13` EF `20261031111326`..`20261117171728`).
- **Live Shared Supabase State Observed Now (`36` rows, `80` tables in `public`):** Between TASK 04 and TASK 05, uncoordinated external writes applied migrations directly to Shared Supabase:
  1. **Cross-Branch Migration (source now located):** `20261117121238_AddLearnerFeedback` (`ProductVersion = 8.0.8`) was applied directly to Shared Supabase, creating table `"LearnerFeedback"` (`0` rows, FK `Users ON DELETE CASCADE`, rating check `1..5`, unique index on `("LearnerId", "TargetType", "TargetId")`). Its source is on `origin/fix/import-file` at `1b9a904`, not on canonical `origin/main`; TASK 05C details the independent integration design.
  2. **Applied Canonical Dispute & Device Token Migrations (`8.0.8`):** `20261031111326_AddMentorReviewsAndDisputesPersistence` (`MentorAbuseReports`, `MentorDisputes`, `MentorDisputeHistories`, `0` rows) and `20261031235900_AddUserDeviceTokensPersistence` (`UserDeviceTokens`, `0` rows) now exist in Shared Supabase.
  3. **False-Applied History Stamps (`8.0.0`) WITHOUT Underlying Tables:** All `6` historical IDs (`20261031082840`, `20261031104216`, `20261101083259`, `20261104103624`, `20261104104226`, `20261104115618`) now appear in `"__EFMigrationsHistory"` with `ProductVersion = 8.0.0`, **YET** all `12` Mentor Compensation tables (`JlptLevelPayCoefficients`..`OrganizationPayoutAccounts`) and all `6` `20261030125610` Scheduling tables (`MentorBookingDayGuards`..`MentorSchedules`) **STILL DO NOT EXIST** in `public` (`80` tables actual vs `107` expected in QA rehearsal minus `10` pending Group tables + `1` `LearnerFeedback`). Running `dotnet ef database update` alone would silently skip creating those `18` tables; `ReconcileSharedMigrationHistory.sql` remains mandatory because its `CREATE TABLE IF NOT EXISTS` repairs them idempotently before inserting history (`ON CONFLICT DO NOTHING`).
  4. **Pending Canonical Migrations Now Remaining on Supabase (`11`):** `20261116072324_AddMentorGroupPolicyVersions` through `20261117171728_AddGroupLearningRulesLifecycleFeedback` (`10` new Group tables remain absent). Extensions/FKs remain unchanged: `btree_gist = false` (`postgres` role `usesuper = f`), `2` `NOT VALID` FKs with `0` orphan rows.

> **Required Unblock Conditions Before Re-Reviewing Deployment:**
> 1. Locate the developer branch / PR that authored `20261117121238_AddLearnerFeedback`, verify schema & code parity, and either integrate it cleanly into `origin/main` OR formally audit/whitelist it in both `ReconcileSharedMigrationHistory.sql` and `tools/verify-migration-readiness.ps1` (after isolated QA rehearsal).
> 2. Enforce an immediate write freeze on Shared Supabase from unmerged developer machines/branches (`git`/team coordination).
> 3. The local full logical backup/public-schema restore test is complete. A Supabase-native restore-to-new-project, write freeze and final-source QA rehearsal remain required. Release preflight exit `0` is expected only **after** reconciliation and the approved pending migration queue; do not suppress the current exit `2`.

| Supabase Migration ID | Origin | Live DB Objects Found | Compatibility With Current `origin/main` | Action |
|---|---|---|---|---|
| `20261116061024_RetireLegacyStudyCourseModel` | Commit `443d3f2` (PR #70, superseded by PR #71/#72) | History row ONLY — `StudyCoursePlans`, `StudyCohorts`, `StudyCourses`, `CourseRetakeRequests`, `MentorAttendanceRecords.CourseSessionId`, etc. are still intact (`StudyCohorts` has 1 row, `StudyCoursePlans` has 2 rows) | **CONFLICTING if executed (`Up()` drops Group Learning tables)**, SAFE in current Supabase because tables remain intact | Never add to repo; fail-closed precheck in `ReconcileSharedMigrationHistory.sql` aborts if Group tables are missing |
| `20261116143915_AddMentorAuthoredHomework` | External unmerged branch | Nullable `HomeworkAssignments.AuthoredDefinitionJson`, `HomeworkSubmissions.SelectedOptionsJson`, `HomeworkSubmissions.PrivateAudioReference` | Compatible / additive (`NULL`-allowed, disjoint from `20261116173741`) | Keep intact on Supabase; verify via read-only preflight |
| `20261118054924_AddContentAudioTranscriptions` | External unmerged branch | Standalone table `ContentAudioTranscriptions` (`ON DELETE SET NULL` to `Users`, RLS enabled, 2 rows) | Compatible / disjoint | Keep intact on Supabase; coordinate source PR integration separately |
| `20261118091131_AddMockQuestionAudioTranscript` | External unmerged branch | Nullable `MockQuestions.AudioTranscript` (`text`) | Compatible / additive | Keep intact on Supabase; coordinate source PR integration separately |
| `20261118100842_AddImportedContentImages` | External unmerged branch | Nullable `ImageUrl` (`text`) across `Lessons`, `Vocabularies`, `Kanjis`, `GrammarPatterns`, `MockQuestions`, `MockQuestionOptions`, `PracticeExercises`, `PracticeQuestions`, `PracticeQuestionOptions`, `PracticeSubmissions` | Compatible / additive | Keep intact on Supabase; coordinate source PR integration separately |

Also verified on Live Supabase (read-only):
- **`MentorReviews` already exists** cleanly (12 columns, indexes, FK, check constraint). Migration `20261031111326_AddMentorReviewsAndDisputesPersistence` uses `CREATE TABLE IF NOT EXISTS "MentorReviews"` + `CREATE INDEX IF NOT EXISTS`, so applying it adds `MentorAbuseReports`, `MentorDisputes`, and `MentorDisputeHistories` cleanly.
- **`MentorCreditGrants` & `MentorCreditEntries`** use PostgreSQL-generated constraint names (`MentorCreditGrants_pkey`, `MentorCreditEntries_pkey`, `MentorCreditEntries_Amount_check`, `MentorCreditGrants_PolicyVersion_check`, `MentorCreditGrants_CertificateLevelId_fkey`, `SubscriptionPlans_CertificateLevelId_fkey`, etc.) structurally identical to canonical EF names; `ReconcileSharedMigrationHistory.sql` skips duplicate FKs when an equivalent structural FK already exists and runs `VALIDATE CONSTRAINT` on `FK_MentorBookings_MentorCreditGrants_CreditGrantId` & `FK_MentorCreditEntries_MentorCreditGrants_CreditGrantId` (currently `NOT VALID` on Supabase with `0` orphan rows).
- **`MentorOutboxEvents`** already holds `MentorOutboxEvents_pkey` and `"IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtUt"` (63-byte PostgreSQL truncation; avoids the duplicate `...U~` index created when `20261030125610` + `20261114083500` both run from scratch).
- **`Users.FullName` Design Contract:** There is **no** `Users.FullName` DB column or pending rename migration. `UserAccount` / PostgreSQL intentionally keep `FirstName` (`varchar(100)`) and `LastName` (`varchar(100)`), while `UserNameHelper` (`SplitFullName` / `FormatFullName`, max 200 chars) exposes unified `fullName` across Auth, UserProfile, Mentor, and Admin DTOs.
- **`btree_gist` (`1.7`)** is available on Supabase (`pg_available_extensions`) in `extensions` schema (`postgres` role has `CREATE` on `extensions` + DB; `search_path = "$user", public, extensions`), not yet enabled.

### Cross-Branch PR Integration Plan (for Future Merges onto `origin/main`)

1. **`20261116061024_RetireLegacyStudyCourseModel` (`443d3f2`, PR #70):** Superseded by PR #71/#72 Group Learning model. Any stale branch still containing it must delete/exclude that migration and rebase cleanly onto `origin/main` (`1100de5+`). Do not add to canonical history.
2. **`20261116143915_AddMentorAuthoredHomework`:** When its feature branch merges to `main`, rebase onto `20261117171728_AddGroupLearningRulesLifecycleFeedback`, regenerate `AppDbContextModelSnapshot.cs`, and ensure the migration keeps its exact `MigrationId` (`20261116143915_AddMentorAuthoredHomework`) with idempotent `IF NOT EXISTS` DDL (`ADD COLUMN IF NOT EXISTS`) so Shared Supabase skips re-execution (`__EFMigrationsHistory` already has it) while fresh environments receive the 3 nullable homework columns cleanly.
3. **`20261118054924_AddContentAudioTranscriptions`:** When its feature branch merges, rebase entities/DbContext onto `origin/main`, regenerate snapshot cleanly, and wrap `CREATE TABLE IF NOT EXISTS "ContentAudioTranscriptions"` / indexes / FK (`FK_ContentAudioTranscriptions_Users_CreatedByUserId` guard) under its recorded `MigrationId` so Shared Supabase (`2` existing rows) is untouched and fresh builds stay aligned.
4. **`20261118091131_AddMockQuestionAudioTranscript`:** When merging, rebase onto `origin/main` and use `ADD COLUMN IF NOT EXISTS "AudioTranscript" text;` under `20261118091131_AddMockQuestionAudioTranscript`.
5. **`20261118100842_AddImportedContentImages`:** When merging, rebase onto `origin/main` and use idempotent `ADD COLUMN IF NOT EXISTS "ImageUrl" text;` across `Lessons`, `Vocabularies`, `Kanjis`, `GrammarPatterns`, `MockQuestions`, `MockQuestionOptions`, `PracticeExercises`, `PracticeQuestions`, `PracticeQuestionOptions`, `PracticeSubmissions` under `20261118100842_AddImportedContentImages`.
6. **`20261117121238_AddLearnerFeedback` (UNMERGED):** Source found on `origin/fix/import-file` (`1b9a90462e8bf9b205e406f96bc663f620dcdbb3`); the Shared artifact was structurally verified and rehearsed on QA. Do not merge the whole branch because it includes a destructive legacy-study migration. Review a small canonical integration separately. See `MigrationTask05BCheckpoint.md` for current evidence and remaining approval blockers.
7. **Legacy Unmerged Remote Branches (`origin/*`):** Among fetched remote branches whose tips are not merged into `origin/main`, only `origin/fix/Update_Writting` (`49ac70b`, legacy 2026-10-06 dictionary migration chain) remains unmerged — archive or recreate work on top of `origin/main` without merging stale migrations. Add a GitHub Actions PR gate running `dotnet test --filter "FullyQualifiedName~MigrationGovernanceTests|FullyQualifiedName~AppDbContextDesignTimeFactoryTests"` and `.\tools\verify-migration-readiness.ps1 -SkipDatabaseCheck` (note `.github/workflows/deploy.yml` currently publishes to VPS on `main` without running migration governance or `dotnet ef database update`).

---

## 2. Pre-Deployment Checklist

1. **Confirm Approved Migration Baseline (`41` canonical + optional `5` audited Supabase IDs):**
   Run offline CI & read-only preflight before any deployment window:
   ```powershell
   dotnet test RikiPath.Tests/RikiPath.Tests.csproj --filter "FullyQualifiedName~MigrationGovernanceTests|FullyQualifiedName~AppDbContextDesignTimeFactoryTests"
   .\tools\verify-migration-readiness.ps1
   ```
2. **Mandatory Backup & Restore Verification (Shared Supabase):**
   - Trigger a Supabase Point-in-Time Recovery (PITR) / manual snapshot AND capture a full logical backup:
     ```powershell
     pg_dump -Fc --no-owner --no-acl -h <host> -p 5432 -U <user> -d postgres -f rikipath_pre_deploy.dump
     ```
   - Verify backup readability (`pg_restore -l rikipath_pre_deploy.dump | Select-Object -First 20`) and test restoring into an isolated clone (`rikipath_restore_verify`) before touching production/shared Supabase.
3. **Database Permission Prerequisites:**
   - Target operator role must hold `CREATE` on `public` and `extensions` (or enable `btree_gist` beforehand in Supabase Dashboard → Database → Extensions).
4. **Preflight SQL (SELECT / READ-ONLY):**
   ```sql
   BEGIN READ ONLY;
   SELECT current_database(), current_user, current_setting('search_path');
   SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
   SELECT extname FROM pg_extension WHERE extname = 'btree_gist';
   SELECT COUNT(*) FROM "StudyCohorts" WHERE "Capacity" <= 0;
   SELECT COUNT(*) FROM (
       SELECT "CohortId", "LearnerId"
       FROM "StudyCourses"
       WHERE "CohortId" IS NOT NULL AND "Status" = 'Active'
       GROUP BY 1, 2 HAVING COUNT(*) > 1
   ) d;
   ROLLBACK;
   ```
   **Stop immediately** if any conflict count `> 0` or unexpected migration ID appears.

---

## 3. Deployment Procedure (Strict Execution Order)

> Do **not** skip Step 1 (backup) or run Step 3 before Step 2 commits cleanly. Keep `RIKIPATH_ALLOW_REMOTE_DB_MIGRATION` **unset** except during the explicit approved Step 3 window.

1. **Step 1 — Backup, Freeze & Approval Gate:** Confirm restore-tested snapshot, maintenance window freeze on external branches/tools, and independent review of `20261117121238_AddLearnerFeedback`. The read-only **release** preflight is expected to return exit `2` before reconciliation because 11 migrations and 18 tables remain pending; record those exact expected gaps, and stop for any new divergence. Exit `0` is required **after** reconciliation and all pending migrations, before declaring the release complete.
2. **Step 2 — Run Reconciliation Script (Atomic Transaction):**
   Execute [`RikiPath.Infrastructure/Sql/ReconcileSharedMigrationHistory.sql`](../RikiPath.Infrastructure/Sql/ReconcileSharedMigrationHistory.sql) using `psql -v ON_ERROR_STOP=1` (supply credentials via `.pgpass` / prompt — do not store secrets in repo):
   ```powershell
   & "C:\Program Files\PostgreSQL\17\bin\psql.exe" `
     -h <supabase-host> -p 5432 -U <db-user> -d postgres `
     -v ON_ERROR_STOP=1 `
     -f .\RikiPath.Infrastructure\Sql\ReconcileSharedMigrationHistory.sql
   ```
   - Idempotently ensures `20261030153753`, `20261031082840`, `20261031104216`, `20261101083259`, `20261104103624`, `20261104104226`, `20261104115618` via `INSERT ... ON CONFLICT ("MigrationId") DO NOTHING` after verifying schema parity.
   - Enables `btree_gist` (or aborts with explicit privilege guidance).
   - Creates the missing `6` `20261030125610` scheduling/reservation tables (`MentorBookingDayGuards`..`MentorScheduleRules`) and `12` `20261104*` compensation tables (`JlptLevelPayCoefficients`..`OrganizationPayoutAccounts`) + indexes/checks, and runs `VALIDATE CONSTRAINT` on the `2` `NOT VALID` `CreditGrantId` FKs.
   - Fails closed (`ROLLBACK`) if any schema gap, constraint/index conflict, missing privilege, or unexpected migration ID exists.
3. **Step 3 — Apply Pending Canonical Migrations (`11` currently remaining on Supabase, or `13` from 27-row baseline):**
   Preview pending migrations first, then execute with explicit `--connection` from secret store inside `try / finally`:
   ```powershell
   $conn = "<postgres-connection-string-from-secret-store>"
   dotnet ef migrations list --project RikiPath.Infrastructure --startup-project RikiPath.WebApi --connection $conn
   $env:RIKIPATH_ALLOW_REMOTE_DB_MIGRATION = "true"
   try {
       dotnet ef database update --project RikiPath.Infrastructure --startup-project RikiPath.WebApi --connection $conn
   } finally {
       Remove-Item Env:\RIKIPATH_ALLOW_REMOTE_DB_MIGRATION -ErrorAction SilentlyContinue
   }
   ```
   **Canonical Migration Queue (`#1–#2` already applied in current `36`-row Supabase; `#3–#13` pending):**
   1. `20261031111326_AddMentorReviewsAndDisputesPersistence`
   2. `20261031235900_AddUserDeviceTokensPersistence`
   3. `20261116072324_AddMentorGroupPolicyVersions` (seeds `MentorGroupPolicies` `Id=1`)
   4. `20261116072801_SnapshotMentorGroupPolicyOnSession`
   5. `20261116073313_EnableGroupBookingSeatReservations`
   6. `20261116073745_AddGroupEnrollmentAndRemediationPolicyRules`
   7. `20261116171033_ReconcileCanonicalGroupCohortModel`
   8. `20261116173741_AddCohortHomeworkAiDraftAndRevision`
   9. `20261117021440_AddCohortEntitlementsAndRemediationSupport`
   10. `20261117044503_AddCohortTransferAndMentorReplacement`
   11. `20261117053348_AddCohortCheckoutPaymentIntegrity`
   12. `20261117122910_AddMentorWeeklySessionCap`
   13. `20261117171728_AddGroupLearningRulesLifecycleFeedback`

---

## 4. Post-Deployment Verification

Run immediately after Step 3 (`BEGIN READ ONLY;`):
1. **Migration History Count:**
   - Re-run `.\tools\verify-migration-readiness.ps1` and expect **Exit Code `0`**, `Pending canonical : 0`, `UNEXPECTED history rows : 0`, `btree_gist installed : true`, `Unvalidated (NOT VALID) FK: 0` (`46` rows on `27`-row baseline, or `47` once the blocking `20261117121238_AddLearnerFeedback` migration is audited/resolved). `dotnet ef migrations list` must show `0` `(Pending)` entries.
2. **Critical Objects Present:**
   - Extension `btree_gist` active & usable.
   - `6` Scheduling tables (`MentorBookingDayGuards`, `MentorBookingIntents`, `MentorBookingReservations`, `MentorBookingTerms`, `MentorSchedules`, `MentorScheduleRules`) and `12` Compensation tables (`JlptLevelPayCoefficients`..`OrganizationPayoutAccounts`) restored and present (`107` total QA tables, or `+1` if `LearnerFeedback` is retained).
   - `MentorGroupPolicies` row `Id = 1` present (`AllowedSessionsPerWeekCsv = '2,3,4'`, `CheckoutHoldMinutes = 10`, `WeeklySessionCap = 0`).
   - Exclusion constraint `EX_MentorBookingReservation_ActiveMentorRest` present with `"MentorAvailabilityId" WITH <>`.
   - New Group Learning & Governance tables present: `CohortComplaints`, `CohortComplaintEvents`, `CohortMentorReviews`, `CohortScheduleRequests`, `CohortScheduleRequestEvents`, `CohortTransferRequests`, `CohortTransferRequestEvents`, `CohortEnrollmentEntitlements`, `StudyCohortMentorAssignments`, `UserDeviceTokens`, `MentorDisputes`, `MentorDisputeHistories`, `MentorAbuseReports`.
3. **Data Integrity & Smoke Checks:**
   - Pre-existing `Users` (`12`), `MentorProfiles` (`2`), `MentorAvailabilities` (`48`), `MentorBookings` (`4`), `MentorCreditGrants` (`1`), `StudyCoursePlans`, `StudyCohorts` (`1`), `ContentAudioTranscriptions`, `MockQuestions` counts preserved cleanly.
   - Verify `NOT VALID` FK count in `public` is `0` (`FK_MentorBookings_MentorCreditGrants_CreditGrantId` & `FK_MentorCreditEntries_MentorCreditGrants_CreditGrantId` validated).
   - Run smoke requests (`POST /api/auth/login` + authenticated `GET`s) across all roles: `/api/userprofile/me`, `/api/subscriptions/plans`, `/api/mentor/profile`, `/api/study-courses/plans`, `/api/study-courses/cohorts`, `/api/admin/mentor-group-policies/current`, `/api/content-author/manual/lookups`, `/api/content-approvals/mine`, and `/api/Notification` (plus subscribed learner `/api/lessonprogress/lessons`).

---

## 5. Recovery & Rollback Plan

> **Important:** Do **NOT** blindly run `dotnet ef database update <previous>` / `Down()` in production! Several `Down()` methods call `DropTable` / `DropColumn` (destroying data), and `20261116073313` (`EnableGroupBookingSeatReservations`) `Down()` explicitly aborts once multiple learners share a group reservation.

1. **Failure during `ReconcileSharedMigrationHistory.sql`:**
   - Single-transaction rollback leaves DB 100% unchanged. Inspect the exact exception (`missing columns`, `incompatible index`, `insufficient privileges`, or `unexpected migration ID`), resolve cleanly, and re-run on QA first.
2. **Partial Failure during `dotnet ef database update`:**
   - EF Core commits each migration in its own transaction, so earlier migrations in the 13-step queue remain applied while the failing migration rolls back cleanly.
   - **Stop immediately** — do not drop tables manually. Compare `"__EFMigrationsHistory"` against the 13-step list:
     - **Option A (Preferred when additive forward-fix is clear):** Fix data/permission blocker (or author a forward-fix migration tested in QA) and re-run `dotnet ef database update`.
     - **Option B (If full revert required):** Restore `rikipath_pre_deploy.dump` (or Supabase PITR snapshot) to revert schema and data atomically.
3. **N-1 & Running Application Binary Compatibility:**
   - **`Program.cs` & `.github/workflows/deploy.yml`:** `RikiPath.WebApi` does **NOT** run `Database.Migrate()` on startup, and pushing to `main` publishes & restarts `/var/www/rikipath-api` (`rikipath-api.service`) via `APPSETTINGS_PRODUCTION` without migrating Supabase. Therefore DB reconciliation/migration must always be coordinated cleanly with application deployment.
   - **Schema vs Running N-1 Binary:** The `13` pending canonical migrations (`11` remaining on Supabase) are additive: new tables, `nullable: true` columns (`MentorAvailabilities.GroupPolicyId` / `*Snapshot`, `MentorBookings.LearnerId`, `StudyCourses.OrderSnapshot` / `Payment*` / `Transferred*`, `HomeworkAssignments.AiDraft*`), or explicit `defaultValue`-backed non-null columns (`MentorGroupPolicies.*`, `StudyCohorts.CreatedAtUtc`, `HomeworkSubmissions.RevisionCount = 0`, `HomeworkAssignments.AssignmentScope = 'Booking1on1'`, `CourseRetakeRequests.Reason = ''`, `StudyCourses.Currency = 'VND'`). Existing N-1 inserts succeed cleanly; `20261116072801`/`20261116073313` relax `CK_MentorAvailability_GeneratedSlot` (`MaxLearners BETWEEN 1 AND 4`) and `EX_MentorBookingReservation_ActiveMentorRest` (`"MentorAvailabilityId" WITH <>`, requiring `btree_gist`).
   - **Critical Warning on Current Live Supabase vs Current Backend Binary:** Currently live Shared Supabase is missing all `6` scheduling tables (`20261030125610`) and all `12` compensation tables (`20261104*`), plus `10` Group Learning tables (`20261116072324`–`20261117171728`). Deploy the reconciled DB schema and matching `origin/main` (+ Task V6.2 scheduling/review fixes) backend binary together during a brief maintenance window.
