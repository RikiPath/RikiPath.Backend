# Phase 4.1 — Database Reconciliation, Recovery Strategy & Rehearsal Evidence Package

- **Date:** 2026-11-23
- **Production Release Reconciliation Status:** **`BLOCKED`**
- **Shared Supabase Mutation Performed:** **NO** (0 writes, 0 DDL, 0 `dotnet ef database update` calls; all Shared inspection executed strictly under `BEGIN READ ONLY; ... ROLLBACK;`).

---

## 1. Executive Summary & Deliverables

| Deliverable | Path / Location | Verification State |
|---|---|---|
| Complete Object-Level Schema Diff | [`Phase41_SchemaDiffReport.md`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/Docs/Phase41_SchemaDiffReport.md) | Complete across Local (60), QA (60), Clone (3), and Shared Supabase (READ-ONLY, 3) |
| Read-Only Preflight SQL | [`PreflightSharedSchemaReadOnly_Phase41.sql`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Infrastructure/Sql/PreflightSharedSchemaReadOnly_Phase41.sql) | Executed READ-ONLY against Local, QA, Clone, Shared Supabase, and Rehearsal DB |
| Forward-Only Clone Reconciliation SQL | [`ReconcileSharedCloneRehearsal_Phase41.sql`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Infrastructure/Sql/ReconcileSharedCloneRehearsal_Phase41.sql) | Rehearsed twice (idempotent `COMMIT`), 100% schema parity vs `rikipath_local` |
| Pre-Reconcile Logical Dump & Restore Check | `%LOCALAPPDATA%\RikiPath\qa\phase41-20261123\rehearsal_pre_reconcile.dump` | Verified via PG16 `pg_restore` into `rikipath_phase41_restore_verify_20261123` |
| Reconciled Clone API Smoke | Isolated `RikiPath.WebApi` on `127.0.0.1:5279` (`MentorQA` mode, stopped cleanly) | Multi-role Auth/Admin/Mentor/Author/Learner HTTP 200 pass; 15 CertSections verified |

---

## 2. Database Reconciliation Strategy (Forward-Only, Zero Data Loss)

### 2.1 Core Principles
1. **Forward-only & Additive:** Zero `DROP TABLE`, zero `DROP COLUMN`, zero row truncation. Only adds missing extensions (`btree_gist`), missing tables (`10`), missing columns (`132`), missing defaults (`22`), missing constraints (`54`), missing/composite indexes (`85`), widens `CK_MentorAvailability_GeneratedSlot` cleanly after null-rule verification, and drops 2 superseded single-column indexes (`IX_ReviewHistories_ReviewItemId`, `IX_LearnerNoteEntries_VocabularyListId`) strictly **after** their replacement composite indexes exist.
2. **Preserve Migrations #59 & #60 and Historical Auditability:** Preserves `20261123030956_StandardizeN2N3CertificateSections` (#59) and `20261123033629_AddFeatureAccessPolicies` (#60). Never creates synthetic "fix-history" migrations or deletes Shared's historical records (`20261121010126_InitialCreate`, `20261122074649_N5ToN2CertificateSection`). Canonical IDs #01–#60 are inserted into `"__EFMigrationsHistory"` strictly **at the end of the single transaction** only after all structural and invariant postconditions pass.
3. **Code-Based `CertificateSections` Resolution (Never Hardcoded `Id`):** Live Shared Supabase stores shifted PK IDs for `CertificateSections` (e.g., `N3_VOCAB` is `Id=8` on Shared vs `Id=10` on Local) with `SortOrder=0` across all 15 active N5–N2 sections. Reconciliation matches strictly by `("CertificationLevelId", "Code")` so all `MockTestSections.CertificationSectionId` foreign keys (`640` rows on Shared) stay intact without data loss.
4. **Privilege & Extension Visibility Check:** Checks `pg_available_extensions` and verifies `gist_int4_ops` appears in `current_schemas(false)` before adding `EX_MentorBookingReservation_ActiveMentorRest`. On managed Supabase, if non-superuser roles place extensions in `extensions` rather than `public`, verify `search_path` includes `extensions` or pre-enable `btree_gist` via Supabase Dashboard before maintenance.

### 2.2 Exact Safe Execution Ordering (Inside Single Atomic Transaction)
1. **Preconditions (`DO $safety$`):** Fail closed on unvalidated constraints, duplicate active `CertificateSections`, duplicate `CertificateTypes`/`Levels` names/codes/sort orders, active `MentorBookingReservations` range overlaps or invalid ranges, `StudyCohorts.Capacity <= 0`, duplicate active cohort enrollments, orphan `CreditGrantId` references, or generated `MentorAvailabilities` lacking both `MentorScheduleRuleId` and `TeachingProposalId`.
2. **Extension:** Enable `btree_gist` and assert `gist_int4_ops` visibility.
3. **Metadata-Only Defaults & Pre-#53 Constraints:** Set the 22 missing `COLUMN DEFAULT` values (zero table rewrite in PG 11+ for constant/volatile-free defaults) and add deferrable `UQ_CertificateTypes_SortOrder`, `UQ_CertificateLevels_CertificationId_SortOrder`, expression unique indexes, GiST exclusion constraint `EX_MentorBookingReservation_ActiveMentorRest`, and `IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtUt`.
4. **Migration #53 (`AddMentorTeachingProposalsV4`):** Add `StudyCohorts.SourceProposalId` & `MentorAvailabilities.TeachingProposalId`, create `MentorTeachingProposals` + `MentorTeachingProposalEvents`, swap `CK_MentorAvailability_GeneratedSlot` cleanly to permit `MentorScheduleRuleId IS NOT NULL OR TeachingProposalId IS NOT NULL`.
5. **Migration #54 (`AddMasterSrsSmartLearningV4`):** Add additive status/SRS snapshot/history columns, create `LearnerHighlights`/`SrsCardTemplates`/`UserSrsPreferences`, deduplicate any legacy `ReviewCards` (reassigning `ReviewHistories` before deleting duplicate rows), backfill snapshot `FrontText`/`BackText`/`NormalizedContentKey`/`LearningState`/`Status` and `LessonProgresses`, swap `IX_ReviewCards_UserId_KanjiEntryId_Mode` to filtered `WHERE "KanjiEntryId" IS NOT NULL`, create new unique/lookup indexes & FKs.
6. **Migrations #55 + #56 + #58 (`AddContentAuthoringExamBankV4`, `AddSrsReviewSessionsV41`, `AddSrsFreePracticeV5`):** Add authoring/question-bank/attempt columns, create `ContentPassages`/`QuestionBankItems`/`SrsReviewSessions` (with final `varchar(8000)` lengths and free-practice fields), add FKs & indexes.
7. **Migration #57 (`AddPersonalNotebookV3`):** Add `LearnerNotes`/`LearnerNoteEntries` V3 columns, create `NotebookFolders`/`NotebookPages`, run idempotent folder/page backfill guarded by `WHERE NOT "IsDeleted" AND "FolderId" IS NULL` and `WHERE NOT "IsDeleted" AND "NotebookPageId" IS NULL`.
8. **Migration #59 (`StandardizeN2N3CertificateSections`):** Assert zero `GENERAL` refs and zero duplicates, normalize `SortOrder` (1..3 for N5; 1..4 for N4/N3/N2) matching by `Code`, swap `IX_CertificateSections_CertificationLevelId_Code` to `UNIQUE ... WHERE ("IsDeleted" = false)`.
9. **Migration #60 (`AddFeatureAccessPolicies`):** Add 7 policy columns to `Features`, idempotently `INSERT ... ON CONFLICT ("Id") DO UPDATE` all 14 feature policies (`1..14`), reset `"Features"` identity sequence via `setval`, create `UNIQUE INDEX "IX_Features_Code"`.
10. **Drop Superseded Indexes:** Drop `IX_ReviewHistories_ReviewItemId` and `IX_LearnerNoteEntries_VocabularyListId` only after confirming replacement composite indexes exist.
11. **Postconditions & History Recording:** Assert 119 base tables, 1790 columns, `btree_gist`, valid GiST exclusion constraint, 0 unvalidated constraints, 15 active N5–N2 `CertificateSections` with `SortOrder > 0`, 0 `GENERAL` MockTestSection refs, >= 14 `Features` with non-empty `Code`. Only then insert missing canonical IDs #01–#60 into `"__EFMigrationsHistory"`.

### 2.3 Invalid Data Handling, Lock & Downtime Estimate
- **Invalid/Conflicting Data Handling:** Fail closed before mutating if preconditions fail. Wait-free rollback leaves DB untouched because everything runs inside a single transaction. If preflight detects duplicate `ReviewCards` or active `CertificateSections`, remediate in an explicit reviewed SQL step on a cloned snapshot before retrying.
- **Locking & Estimated Downtime:** At current Shared data scale (total ~18 MB across all tables; largest table `MockQuestionOptions` ~12k rows), full single-transaction execution completed in **< 1 second** in rehearsal with brief `ACCESS EXCLUSIVE` locks on `Features`, `MentorAvailabilities`, `CertificateSections`, `ReviewCards`, `LearnerNotes`, `LearnerNoteEntries`, and `MockTests`/`MockQuestions`. Recommend a **5–15 minute write-freeze maintenance window** on production to drain active sessions/workers, take pre-change logical + Supabase physical snapshot/PITR checkpoint, run read-only preflight, execute reconciliation (`ON_ERROR_STOP=1`), verify post-flight + smoke, and lift freeze.

---

## 3. Isolated Synthetic Clone Rehearsal & Data Integrity Report

Because dumping production PII from Shared Supabase was not authorized in Phase 4.1, rehearsal was executed on an isolated local clone (`rikipath_phase41_rehearsal_20261123`) templated from `rikipath_phase4_remote_schema_20261123` and reset to exact pre-reconcile Shared Supabase state (`btree_gist` dropped, non-unique `IX_CertificateSections_CertificationLevelId_Code`, `SortOrder = 0`), seeded with representative **synthetic non-PII domain data**.

| Domain / Metric | Pre-Reconcile State | Post-Reconcile Run 1 | Post-Reconcile Run 2 (Idempotency) | Status |
|---|---:|---:|---:|---|
| Base Tables (`public`) | 109 | 119 | 119 | `PASS` (0 diff vs `rikipath_local`) |
| Columns (`public`) | 1396 | 1790 | 1790 | `PASS` (0 diff vs `rikipath_local`) |
| Indexes (`public`) | 387 | 470 | 470 | `PASS` (0 diff vs `rikipath_local`) |
| Constraints (`public`, excl. PG16/17 array cast formatting) | 419 | 473 | 473 | `PASS` (0 name/type diff vs `rikipath_local`) |
| Unvalidated Constraints | 0 | 0 | 0 | `PASS` |
| `btree_gist` Installed | `false` | `true` | `true` | `PASS` |
| `EX_MentorBookingReservation_ActiveMentorRest` | `false` | `true` (GiST valid) | `true` | `PASS` |
| Synthetic Users / MentorProfiles / Bookings / Reservations | 4 / 1 / 1 / 1 | 4 / 1 / 1 / 1 | 4 / 1 / 1 / 1 | `PASS` (100% preserved) |
| Synthetic StudyCohorts / StudyCourses / UserSubscriptions | 1 / 1 / 1 | 1 / 1 / 1 | 1 / 1 / 1 | `PASS` (100% preserved) |
| Synthetic Lessons / PracticeExercises / MockTests / Sections | 1 / 1 / 1 / 1 | 1 / 1 / 1 / 1 | 1 / 1 / 1 / 1 | `PASS` (`N3_VOCAB` FK preserved) |
| `CertificateSections` N5–N2 Active / `SortOrder=0` | 15 / 15 | 15 / 0 | 15 / 0 | `PASS` |
| `Features` Total / With Non-Empty `Code` | 8 / 0 | 14 / 14 | 14 / 14 | `PASS` |
| Legacy `LearnerNotes` -> `NotebookFolders` / `NotebookPages` | 1 / 0 / 0 | 1 / 1 / 1 | 1 / 1 / 1 | `PASS` (no folder dup on re-run) |
| `LearnerNoteEntries` Backfilled with `NotebookPageId` | 0 / 1 | 1 / 1 | 1 / 1 | `PASS` |
| `ReviewCards` Backfilled (`FrontText` + `NormalizedContentKey`) | 0 / 3 | 3 / 3 | 3 / 3 | `PASS` (`Kanji=Review`, `Grammar=Mastered`) |
| EF Core `dotnet ef database update` Post-Check | Fails (`CertificateTypes` exists) | `"The database is already up to date."` | `"The database is already up to date."` | `PASS` |
| Isolated Rehearsal API Smoke (`127.0.0.1:5279`, 4 roles) | N/A | HTTP 200 on Auth/Admin/Mentor/Author/Learner + 15 sections | N/A | `PASS` |

---

## 4. Backup & Restore Rollback Rehearsal Evidence

- **Dump Artifact:** `%LOCALAPPDATA%\RikiPath\qa\phase41-20261123\rehearsal_pre_reconcile.dump`
- **Format & Size:** PostgreSQL custom archive (`pg_dump -Fc --no-owner --no-privileges`), `467,754` bytes
- **SHA-256:** `DD74F2365B3E3972BE37EFC4F7E62F99CD7D060B0A1F4BEDDCC22927F01AD1A0`
- **Restore Target:** Isolated database `rikipath_phase41_restore_verify_20261123` via PostgreSQL 16 `pg_restore --no-owner --no-privileges`.
- **Verified Restored Counts:** `Migrations=3`, `Tables=109`, `Users=4`, `Features=8`, `CertSections_active=15`, `MentorProfiles=1`, `MentorAvailabilities=1`, `MentorBookings=1`, `MentorBookingReservations=1`, `StudyCohorts=1`, `StudyCourses=1`, `UserSubscriptions=1`, `Lessons=1`, `PracticeExercises=1`, `MockTests=1`, `MockTestSections=1`, `LearnerNotes=1`, `LearnerNoteEntries=1`, `ReviewCards=3`, `ReviewHistories=1`, `ContentApprovalRequests=1`.

---

## 5. Unproven Risks & Mandatory Production Release Blockers (`BLOCKED`)

Even though synthetic clone rehearsal achieved 100% structural and data-invariant parity, **Production Release & Production DB Reconciliation remain `BLOCKED`** for the following concrete reasons:

1. **No Authorized Full-Data Clone of Shared Supabase:** Shared Supabase holds live rows (`Users=11`, `Lessons=200`, `PracticeExercises=200`, `PracticeQuestions=1400`, `MockTests=160`, `MockTestSections=640`, `MockQuestions=3200`, `ContentApprovalRequests=760`, `UserSubscriptions=6`, `MentorBookings=4`, `MentorProfiles=2`, `Notifications=38`) that were **not** dumped/cloned due to PII governance. Unknown edge cases in those real production rows (e.g., dirty strings, legacy `ReviewHistories` or `UserSubscriptions` constraints not present in empty tables, or sequence drift beyond `Features`) are not yet tested on a real masked clone.
2. **Supabase Platform-Native Recovery & Extensions Verification Missing:**
   - Supabase Dashboard **Database -> Backups -> Restore to a New Project** has not yet been executed (Supabase logical `pg_dump` does not verify platform `auth`, `storage` object bytes, Vault keys, or RLS role grants).
   - `btree_gist` extension is currently `false` on Shared (`pg_available_extensions = true`, `public` CREATE = `true`); managed Supabase projects may require installing into `extensions` schema and verifying `search_path` visibility under the production connection role.
3. **Human Governance Gates Still Open:**
   - Written maintenance window approval, named DBA/release operator, and team-wide write freeze attestation are not yet recorded.
   - `verify-migration-readiness.ps1` still lists only the 6 older cross-branch IDs in `$AuditedExternalMigrations`; if Shared's 2 squashed history IDs (`20261121010126_InitialCreate`, `20261122074649_N5ToN2CertificateSection`) are preserved for auditability, `$AuditedExternalMigrations` in `tools/verify-migration-readiness.ps1` and CI secrets (`MIGRATION_GATE_HOST`, `MIGRATION_GATE_DATABASE`, `MIGRATION_GATE_USERNAME`) must be reviewed/updated by the release owner so `deploy.yml` preflight accepts them only after schema parity is proven.
