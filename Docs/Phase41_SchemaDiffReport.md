# Phase 4.1 — Full Object-Level Database Schema Diff Report

**Generated:** 2026-11-23  
**Mode:** Strictly READ-ONLY inspection (`BEGIN READ ONLY; ... ROLLBACK;` on Shared Supabase; no mutations to Shared).  
**Canonical Codebase Reference:** `AppDbContext` + `AppDbContextModelSnapshot` (60 EF Core migrations, 0 pending model changes via `verify-migration-readiness.ps1 -SkipDatabaseCheck`).

## 1. Executive Summary Matrix

| Environment / Target | Engine | EF Migrations | Base Tables | Columns | Constraints | Indexes | `btree_gist` | Unvalidated FKs | Active CertSections (N5–N2) | Status |
|---|---|---:|---:|---:|---:|---:|---|---:|---:|---|
| **Local (`127.0.0.1:5434/rikipath_local`)** | PG 16.16 | 60/60 | 119 | 1790 | 473 | 470 | `true` (`public`) | 0 | 15 active (+2 soft-deleted `GENERAL`, 0 refs) | Canonical Baseline |
| **QA (`127.0.0.1:5434/rikipath_phase2_qa_20261122`)** | PG 16.16 | 60/60 | 119 | 1790 | 473 | 470 | `true` (`public`) | 0 | 15 active (+2 soft-deleted `GENERAL`, 0 refs) | Canonical QA |
| **Remote Schema Clone (`rikipath_phase4_remote_schema_20261123`)** | PG 16.16 | 3 (non-canonical) | 109 | 1396 | 419 | 387 | `true` (`public`) | 0 | 15 active (`SortOrder=0`, already unique partial idx) | Outdated Clone (Schema-only, 0 users/features) |
| **Shared Supabase (`aws-0-ap-southeast-1.pooler.supabase.com/postgres`)** | PG 17.6 | 3 (non-canonical) | 109 | 1396 | 419 | 387 | `false` (available=`true`, `CREATE`=`true`) | 0 | 15 active (`SortOrder=0`, non-unique idx) | **DIVERGED — Production BLOCKED** |

### Structural Notes on Target Comparisons
- **Local vs QA:** `0` missing/extra tables, `0` column/type/nullability/default diffs, `0` missing/extra constraints, `0` index diffs. (16 CHECK constraints show purely cosmetic PostgreSQL 16 vs 17 `ARRAY[...]` cast syntax formatting differences from QA's dump origin, semantically identical.)
- **Clone vs Shared:** Identical tables/columns/defaults (`0` diffs), except the prior Phase 4 clone has `btree_gist` enabled locally and `CertificateSections.IX_CertificateSections_CertificationLevelId_Code` converted to a filtered unique index.
- **Local vs Shared Summary Diff:** `10` missing tables (`262` columns, `41` constraints, `53` indexes on new tables), `132` missing columns on `16` common tables, `22` missing column `DEFAULT` expressions on `7` common tables, `13` missing constraints + `1` conflicting CHECK constraint on common tables, `32` missing indexes + `2` extra superseded indexes + `2` conflicting indexes on common tables, missing `btree_gist` extension, and `59` missing canonical EF migration history rows (`2` squashed external history rows present).

## 2. Extension & Migration History Diff

| Object | Shared State | Canonical State | Classification | Risk | Notes / Remediation |
|---|---|---|---|---|---|
| Extension `btree_gist` | Not installed (`pg_available_extensions` = `true`, `public` CREATE = `true`) | Installed in `public` (`v1.7`) | `MISSING` | Low | Required before `EX_MentorBookingReservation_ActiveMentorRest`. On Supabase hosted projects, check/install in `extensions` or `public` ensuring `search_path` visibility. |
| Extension `pgcrypto`, `uuid-ossp` | Installed in `extensions` | Supabase platform default | `COMPATIBLE` | None | Supabase platform extensions; do not alter. |
| `__EFMigrationsHistory`: `20261120112045_SeedFullAccessLearnerDemo` | Present (8.0.8) | Present (Migration #52) | `COMPATIBLE` | None | Matches canonical #52. |
| `__EFMigrationsHistory`: `20261121010126_InitialCreate` | Present (8.0.8) | Absent (Canonical has #1–#51) | `CONFLICTING` | High | Squashed baseline representing #1–#51, omitting raw SQL objects (`btree_gist`, exclusion constraint, deferrable UQs, normalized expression indexes) and 22 column defaults. Never run `dotnet ef database update` without first reconciling schema & history. |
| `__EFMigrationsHistory`: `20261122074649_N5ToN2CertificateSection` | Present (8.0.8) | Absent (Canonical has #59 `20261123030956_StandardizeN2N3CertificateSections`) | `CONFLICTING` | High | Seeded 15 N5–N2 rows (`SortOrder=0`, non-unique index). Must backfill `SortOrder`, recreate unique filtered index, preserve all 15 IDs, and prove #59 parity. |
| `__EFMigrationsHistory`: Migrations #1–#51, #53–#60 (`59` IDs) | Absent | Present (60 total minus #52) | `MISSING` | High | Record canonical IDs ONLY inside transactional reconciliation AFTER all schema objects and data invariants pass strict postcondition assertions. Preserve existing 2 external rows for auditability. |

## 3. Missing Tables (10 Tables — All `MISSING`)

| # | Table | Migration Origin | Columns | Constraints | Indexes | Classification | Risk | Dependencies / Notes |
|---|---|---|---:|---:|---:|---|---|---|
| 1 | `ContentPassages` | #55 `AddContentAuthoringExamBankV4` | 36 | 3 | 5 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 2 | `LearnerHighlights` | #54 `AddMasterSrsSmartLearningV4` | 23 | 4 | 6 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 3 | `MentorTeachingProposalEvents` | #53 `AddMentorTeachingProposalsV4` | 11 | 3 | 2 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 4 | `MentorTeachingProposals` | #53 `AddMentorTeachingProposalsV4` | 25 | 10 | 8 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 5 | `NotebookFolders` | #57 `AddPersonalNotebookV3` | 12 | 2 | 4 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 6 | `NotebookPages` | #57 `AddPersonalNotebookV3` | 24 | 3 | 4 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 7 | `QuestionBankItems` | #55 `AddContentAuthoringExamBankV4` | 44 | 4 | 7 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 8 | `SrsCardTemplates` | #54 `AddMasterSrsSmartLearningV4` | 33 | 8 | 10 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 9 | `SrsReviewSessions` | #55 + #58 (`AddSrsFreePracticeV5` widens JSON cols to 8000) | 31 | 2 | 4 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |
| 10 | `UserSrsPreferences` | #54 `AddMasterSrsSmartLearningV4` | 23 | 2 | 3 | `MISSING` | Low (additive new table) | Empty upon creation; FK-ordered creation required (`ContentPassages` before `QuestionBankItems`, `NotebookFolders` before `LearnerNotes.FolderId`, `NotebookPages` after `LearnerNotes`). |

## 4. Missing Columns on Existing Common Tables (132 Columns)

| Table.Column | Data Type | Nullable | Default | Classification | Risk | Remediation / Backfill Action |
|---|---|---|---|---|---|---|
| `ContentApprovalRequests.AiProvenanceJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.CertificateLevelId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.IdempotencyKey` | `character varying(100)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.LanguageSkillId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.ReviewHistoryJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.RightsConfirmed` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.RightsStatus` | `character varying(100)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.SourceReference` | `character varying(300)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.SourceType` | `character varying(40)` | `NO` | `'ImportExcel'::character varying` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ContentApprovalRequests.ValidationSummaryJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Features.AccessMode` | `character varying(32)` | `NO` | `'FREE'::character varying` | `REQUIRES_BACKFILL` | Medium | Map IDs 1..8 to canonical AccessMode (PLAN_REQUIRED/FREE/CREDIT_REQUIRED/DISABLED) |
| `Features.Category` | `character varying(40)` | `NO` | `'general'::character varying` | `REQUIRES_BACKFILL` | Medium | Map IDs 1..8 to canonical Category |
| `Features.Code` | `character varying(80)` | `NO` | `''::character varying` | `REQUIRES_BACKFILL` | Medium | Populate canonical Code + metadata for IDs 1..8 and seed IDs 9..14 before UNIQUE IX_Features_Code |
| `Features.CreditFeatureKey` | `character varying(40)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Features.IsCoreFree` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Features.RoutePrefixesJson` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Features.SortOrder` | `integer` | `NO` | `0` | `REQUIRES_BACKFILL` | Medium | Assign canonical SortOrder (1..91) |
| `GrammarPatterns.Status` | `character varying(20)` | `NO` | `'Published'::character varying` | `COMPATIBLE` | Low | DEFAULT 'Published' applies cleanly to existing rows |
| `GrammarPatterns.UnpublishedAtUtc` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `GrammarPatterns.UnpublishedReason` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `GrammarPatterns.Version` | `integer` | `NO` | `1` | `COMPATIBLE` | Low | DEFAULT 1 applies cleanly |
| `Kanjis.Status` | `character varying(20)` | `NO` | `'Published'::character varying` | `COMPATIBLE` | Low | DEFAULT 'Published' applies cleanly to existing rows |
| `Kanjis.UnpublishedAtUtc` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Kanjis.UnpublishedReason` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Kanjis.Version` | `integer` | `NO` | `1` | `COMPATIBLE` | Low | DEFAULT 1 applies cleanly |
| `LearnerNoteEntries.ContextSnippet` | `character varying(600)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.ExampleMeaning` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.ExampleSentence` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.GrammarPatternId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.HighlightColor` | `character varying(24)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.IsFavorite` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.ItemKind` | `character varying(32)` | `NO` | `'manual'::character varying` | `REQUIRES_BACKFILL` | Medium | Classify ItemKind from VocabularyEntryId/KanjiEntryId or metadata |
| `LearnerNoteEntries.NotebookPageId` | `integer` | `YES` | (none) | `REQUIRES_BACKFILL` | Medium | Create/assign NotebookPage for active entries and migrate embedded metadata |
| `LearnerNoteEntries.Revision` | `integer` | `NO` | `1` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.SortOrder` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.SourceId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.SourcePath` | `character varying(300)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.SourceTitle` | `character varying(200)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.SourceType` | `character varying(40)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNoteEntries.TagsJson` | `jsonb` | `NO` | `'[]'::jsonb` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNotes.FolderId` | `integer` | `YES` | (none) | `REQUIRES_BACKFILL` | Medium | Create/assign default 'Sổ tay chung' NotebookFolder for active notes |
| `LearnerNotes.IsDefault` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNotes.Revision` | `integer` | `NO` | `1` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LearnerNotes.SortOrder` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LessonProgresses.CompletedAt` | `timestamp without time zone` | `YES` | (none) | `REQUIRES_BACKFILL` | Medium | Backfill CompletedAt where IsCompleted=true |
| `LessonProgresses.FirstViewedAt` | `timestamp without time zone` | `YES` | (none) | `REQUIRES_BACKFILL` | Medium | Backfill COALESCE(FirstViewedAt, CreatedDate, LastWatchedAt) for existing rows |
| `LessonProgresses.InteractionCount` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `LessonProgresses.LastInteractedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Lessons.Status` | `character varying(20)` | `NO` | `'Published'::character varying` | `COMPATIBLE` | Low | DEFAULT 'Published' applies cleanly to existing rows (version=1) |
| `Lessons.UnpublishedAtUtc` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Lessons.UnpublishedReason` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Lessons.Version` | `integer` | `NO` | `1` | `COMPATIBLE` | Low | DEFAULT 1 applies cleanly |
| `MentorAvailabilities.TeachingProposalId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.PassageId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.PlaybackPolicyJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.QuestionBankItemId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.QuestionGroupTitle` | `character varying(200)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.QuestionVersion` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.SkillMetadataJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockQuestions.TemplateKind` | `character varying(60)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTestSections.DurationMinutes` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTestSections.Instructions` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTestSections.SectionPolicyJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.AiProvenanceJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.AuthorUserId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.BlueprintPolicyJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ExamKind` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.FocusSkillId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.IncludedSkillsJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.IsArchivedVersion` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.IsManagedWorkflow` | `boolean` | `NO` | `false` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ParentExamId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.PublishStatus` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.PublishedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ReviewHistoryJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ReviewNote` | `text` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ReviewedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.ReviewedByName` | `text` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.RightsStatus` | `character varying(100)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.SourceReference` | `character varying(300)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.SourceType` | `character varying(40)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.SubmittedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.TagsJson` | `jsonb` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `MockTests.Version` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.AcceptedReadingsJson` | `character varying(2000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.ArchivedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.AudioUrl` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.BackText` | `character varying(1000)` | `NO` | `''::character varying` | `REQUIRES_BACKFILL` | Medium | Populate snapshot BackText from linked Vocabulary/Kanji/Grammar/NoteEntry |
| `ReviewCards.CardType` | `character varying(30)` | `NO` | `'Vocabulary'::character varying` | `REQUIRES_BACKFILL` | Medium | Derive CardType from VocabularyId/KanjiEntryId/GrammarPointId |
| `ReviewCards.ContextSnippet` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.CorrectCount` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.ExampleMeaning` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.ExampleSentence` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.FrontText` | `character varying(500)` | `NO` | `''::character varying` | `REQUIRES_BACKFILL` | Medium | Populate snapshot FrontText from linked Vocabulary/Kanji/Grammar/NoteEntry |
| `ReviewCards.IncorrectCount` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.JlptLevel` | `character varying(10)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.Lapses` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.LearningState` | `character varying(20)` | `NO` | `'New'::character varying` | `REQUIRES_BACKFILL` | Medium | Backfill LearningState from Repetitions/IntervalDays/EaseFactor |
| `ReviewCards.LessonId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.MasteredAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.Meaning` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.NormalizedContentKey` | `character varying(300)` | `NO` | `''::character varying` | `REQUIRES_BACKFILL` | Medium | Populate NormalizedContentKey and deduplicate before filtered unique index |
| `ReviewCards.PersonalNote` | `character varying(1000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.Reading` | `character varying(300)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.Revision` | `integer` | `NO` | `1` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SourceId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SourceLabel` | `character varying(300)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SourceLinksJson` | `character varying(4000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SourceType` | `character varying(40)` | `NO` | `'Manual'::character varying` | `REQUIRES_BACKFILL` | Medium | Derive SourceType from linked NoteEntry/Kanji/Grammar |
| `ReviewCards.SourceVersion` | `integer` | `NO` | `1` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SrsCardTemplateId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.Status` | `character varying(20)` | `NO` | `'Active'::character varying` | `REQUIRES_BACKFILL` | Medium | Backfill Active/Archived from IsDeleted |
| `ReviewCards.SuspendedAt` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.SynonymsJson` | `character varying(2000)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewCards.VocabularyId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.IdempotencyKey` | `character varying(120)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.IsCorrect` | `boolean` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.NewEaseFactor` | `double precision` | `NO` | `2.5` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.NewIntervalDays` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.NewState` | `character varying(20)` | `NO` | `'Learning'::character varying` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.PreviousEaseFactor` | `double precision` | `NO` | `2.5` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.PreviousIntervalDays` | `integer` | `NO` | `0` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.PreviousState` | `character varying(20)` | `NO` | `'New'::character varying` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.ResponseTimeMs` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.SessionReference` | `character varying(100)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.StudyMode` | `character varying(20)` | `NO` | `'flip'::character varying` | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `ReviewHistories.UserAnswer` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `StudyCohorts.SourceProposalId` | `integer` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Vocabularies.Status` | `character varying(20)` | `NO` | `'Published'::character varying` | `COMPATIBLE` | Low | DEFAULT 'Published' applies cleanly to existing rows |
| `Vocabularies.UnpublishedAtUtc` | `timestamp without time zone` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Vocabularies.UnpublishedReason` | `character varying(500)` | `YES` | (none) | `MISSING` | Low | Additive ALTER TABLE ADD COLUMN (safe; default value populated metadata-only or nullable). |
| `Vocabularies.Version` | `integer` | `NO` | `1` | `COMPATIBLE` | Low | DEFAULT 1 applies cleanly |

## 5. Missing Column Defaults on Common Tables (22 Columns)

| Table.Column | Canonical Default | Shared Default | Classification | Risk | Notes |
|---|---|---|---|---|---|
| `CourseRetakeRequests.Reason` | `''::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `HomeworkSubmissions.RevisionCount` | `0` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.AllowedSessionsPerWeekCsv` | `'2,3,4'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.CheckoutHoldMinutes` | `10` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.CourseDurationWeeks` | `10` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.DefaultSessionsPerWeek` | `3` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.EnrollmentCutoffMinutesBeforeFirstSession` | `0` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.LearnerTransferEnabled` | `true` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.MentorReplacementEnabled` | `true` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.MentorReplacementLeadHours` | `24` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.MinLearnersToStart` | `1` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.RemediationThresholdComparison` | `'>'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.RequireAdminRemediationReview` | `true` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.RequireOfficialExamEvidence` | `true` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.TransferAllowedAfterSourceStart` | `false` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `MentorGroupPolicies.WeeklySessionCap` | `0` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `RecommendedLearningPaths.Status` | `'Superseded'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `RecommendedLearningPaths.Version` | `0` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `StudyCohorts.CreatedAtUtc` | `'-infinity'::timestamp without time zone` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `StudyCourses.Currency` | `'VND'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `Users.LearningMode` | `'Standard'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |
| `Users.PlacementDecision` | `'NotTaken'::character varying` | *(none)* | `REQUIRES_BACKFILL` / `MISSING` default | Low | Omitted by Shared squashed `InitialCreate`. Apply non-destructive `ALTER TABLE ... ALTER COLUMN ... SET DEFAULT ...`. |

## 6. Constraint Differences on Common Tables (13 Missing + 1 Conflicting)

| Table.Constraint | Type | Canonical Definition | Shared State | Classification | Risk | Precondition & Remediation |
|---|---|---|---|---|---|---|
| `CertificateLevels.UQ_CertificateLevels_CertificationId_SortOrder` | UNIQUE | `UNIQUE ("CertificationId", "SortOrder") DEFERRABLE INITIALLY DEFERRED` | Absent | `MISSING` | Medium | Requires 0 duplicate SortOrder rows per CertificateType/Level (verified 0 on Shared). |
| `CertificateTypes.UQ_CertificateTypes_SortOrder` | UNIQUE | `UNIQUE ("SortOrder") DEFERRABLE INITIALLY DEFERRED` | Absent | `MISSING` | Medium | Requires 0 duplicate SortOrder rows per CertificateType/Level (verified 0 on Shared). |
| `LearnerNoteEntries.FK_LearnerNoteEntries_GrammarPatterns_GrammarPatternId` | FK | `FOREIGN KEY ("GrammarPatternId") REFERENCES "GrammarPatterns"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `LearnerNoteEntries.FK_LearnerNoteEntries_NotebookPages_NotebookPageId` | FK | `FOREIGN KEY ("NotebookPageId") REFERENCES "NotebookPages"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `LearnerNotes.FK_LearnerNotes_NotebookFolders_FolderId` | FK | `FOREIGN KEY ("FolderId") REFERENCES "NotebookFolders"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `MentorAvailabilities.FK_MentorAvailabilities_MentorTeachingProposals_TeachingPropos~` | FK | `FOREIGN KEY ("TeachingProposalId") REFERENCES "MentorTeachingProposals"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `MentorBookingReservations.EX_MentorBookingReservation_ActiveMentorRest` | EXCLUDE | `EXCLUDE USING gist ("MentorId" WITH =, "MentorAvailabilityId" WITH <>, tstzrange("StartUtc", "RestProtectedUntilUtc", '[)'::text) WITH &&) WHERE (("ReleasedAtUtc" IS NULL))` | Absent | `MISSING` | High (requires `btree_gist` & 0 active overlaps) | Requires `btree_gist` extension + 0 active overlapping/invalid reservations (verified 0 on Shared). |
| `MockQuestions.FK_MockQuestions_ContentPassages_PassageId` | FK | `FOREIGN KEY ("PassageId") REFERENCES "ContentPassages"("Id") ON DELETE SET NULL` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `MockQuestions.FK_MockQuestions_QuestionBankItems_QuestionBankItemId` | FK | `FOREIGN KEY ("QuestionBankItemId") REFERENCES "QuestionBankItems"("Id") ON DELETE SET NULL` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `ReviewCards.FK_ReviewCards_Lessons_LessonId` | FK | `FOREIGN KEY ("LessonId") REFERENCES "Lessons"("Id") ON DELETE SET NULL` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `ReviewCards.FK_ReviewCards_SrsCardTemplates_SrsCardTemplateId` | FK | `FOREIGN KEY ("SrsCardTemplateId") REFERENCES "SrsCardTemplates"("Id") ON DELETE SET NULL` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `ReviewCards.FK_ReviewCards_Vocabularies_VocabularyId` | FK | `FOREIGN KEY ("VocabularyId") REFERENCES "Vocabularies"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `StudyCohorts.FK_StudyCohorts_MentorTeachingProposals_SourceProposalId` | FK | `FOREIGN KEY ("SourceProposalId") REFERENCES "MentorTeachingProposals"("Id") ON DELETE RESTRICT` | Absent | `MISSING` | Low | Add after creating target tables/columns. |
| `MentorAvailabilities.CK_MentorAvailability_GeneratedSlot` | CHECK | Allows `MentorScheduleRuleId IS NOT NULL OR TeachingProposalId IS NOT NULL` | Requires `MentorScheduleRuleId IS NOT NULL` | `CONFLICTING` | Medium | Precondition verified: `0` existing generated rows have null rule. Drop old CHECK and add widened canonical CHECK inside single transaction after adding `TeachingProposalId`. |

> Note: On Shared, `MentorAvailabilities.CK_MentorAvailability_GeneratedSlot` vs Local differs semantically; 13 other CHECK constraints between PG16 Local and PG17 Shared differ only in internal PostgreSQL `pg_get_constraintdef` formatting of `ARRAY[...]` casts and are functionally `COMPATIBLE`.

## 7. Index Differences on Common Tables (32 Missing, 2 Conflicting, 2 Superseded)

| Table.Index | Canonical Definition | Shared State | Classification | Risk | Remediation / Order |
|---|---|---|---|---|---|
| `CertificateLevels.UQ_CertificateLevels_CertificationId_NormalizedCode` | `CREATE UNIQUE INDEX "UQ_CertificateLevels_CertificationId_NormalizedCode" ON public."CertificateLevels" USING btree ("CertificationId", upper(btrim(("Code")::text)))` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `CertificateLevels.UQ_CertificateLevels_CertificationId_SortOrder` | `CREATE UNIQUE INDEX "UQ_CertificateLevels_CertificationId_SortOrder" ON public."CertificateLevels" USING btree ("CertificationId", "SortOrder")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `CertificateTypes.UQ_CertificateTypes_NormalizedName` | `CREATE UNIQUE INDEX "UQ_CertificateTypes_NormalizedName" ON public."CertificateTypes" USING btree (lower(btrim(("Name")::text)))` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `CertificateTypes.UQ_CertificateTypes_SortOrder` | `CREATE UNIQUE INDEX "UQ_CertificateTypes_SortOrder" ON public."CertificateTypes" USING btree ("SortOrder")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ContentApprovalRequests.IX_ContentApprovalRequests_SubmittedByUserId_IdempotencyKey` | `CREATE UNIQUE INDEX "IX_ContentApprovalRequests_SubmittedByUserId_IdempotencyKey" ON public."ContentApprovalRequests" USING btree ("SubmittedByUserId", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL)` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `Features.IX_Features_Code` | `CREATE UNIQUE INDEX "IX_Features_Code" ON public."Features" USING btree ("Code")` | Absent | `MISSING` | High | Create UNIQUE index ONLY AFTER backfilling `Code` on IDs 1..8 and inserting 9..14. |
| `GrammarPatterns.IX_GrammarPatterns_Status` | `CREATE INDEX "IX_GrammarPatterns_Status" ON public."GrammarPatterns" USING btree ("Status")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `Kanjis.IX_Kanjis_Status` | `CREATE INDEX "IX_Kanjis_Status" ON public."Kanjis" USING btree ("Status")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNoteEntries.IX_LearnerNoteEntries_GrammarPatternId` | `CREATE INDEX "IX_LearnerNoteEntries_GrammarPatternId" ON public."LearnerNoteEntries" USING btree ("GrammarPatternId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNoteEntries.IX_LearnerNoteEntries_NotebookPageId_IsDeleted_SortOrder` | `CREATE INDEX "IX_LearnerNoteEntries_NotebookPageId_IsDeleted_SortOrder" ON public."LearnerNoteEntries" USING btree ("NotebookPageId", "IsDeleted", "SortOrder")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNoteEntries.IX_LearnerNoteEntries_VocabularyListId_IsDeleted` | `CREATE INDEX "IX_LearnerNoteEntries_VocabularyListId_IsDeleted" ON public."LearnerNoteEntries" USING btree ("VocabularyListId", "IsDeleted")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNotes.IX_LearnerNotes_FolderId` | `CREATE INDEX "IX_LearnerNotes_FolderId" ON public."LearnerNotes" USING btree ("FolderId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNotes.IX_LearnerNotes_UserId_FolderId_IsDeleted_SortOrder` | `CREATE INDEX "IX_LearnerNotes_UserId_FolderId_IsDeleted_SortOrder" ON public."LearnerNotes" USING btree ("UserId", "FolderId", "IsDeleted", "SortOrder")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `LearnerNotes.IX_LearnerNotes_UserId_IsDefault` | `CREATE UNIQUE INDEX "IX_LearnerNotes_UserId_IsDefault" ON public."LearnerNotes" USING btree ("UserId") WHERE (("IsDeleted" = false) AND ("IsDefault" = true))` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `Lessons.IX_Lessons_Status` | `CREATE INDEX "IX_Lessons_Status" ON public."Lessons" USING btree ("Status")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `MentorAvailabilities.IX_MentorAvailabilities_TeachingProposalId` | `CREATE INDEX "IX_MentorAvailabilities_TeachingProposalId" ON public."MentorAvailabilities" USING btree ("TeachingProposalId") WHERE ("TeachingProposalId" IS NOT NULL)` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `MentorBookingReservations.EX_MentorBookingReservation_ActiveMentorRest` | `CREATE INDEX "EX_MentorBookingReservation_ActiveMentorRest" ON public."MentorBookingReservations" USING gist ("MentorId", "MentorAvailabilityId", tstzrange("StartUtc", "RestProtectedUntilUtc", '[)'::text)) WHERE ("ReleasedAtUtc" IS NULL)` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `MentorOutboxEvents.IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtUt` | `CREATE INDEX "IX_MentorOutboxEvents_DeliveredAtUtc_AvailableAtUtc_CreatedAtUt" ON public."MentorOutboxEvents" USING btree ("DeliveredAtUtc", "AvailableAtUtc", "CreatedAtUtc")` | Absent | `COMPATIBLE` | Low | Duplicate of existing `...CreatedAtU~` index created by migration #26 (`RepairMentorOverviewSchema`); safe additive. |
| `MockQuestions.IX_MockQuestions_PassageId` | `CREATE INDEX "IX_MockQuestions_PassageId" ON public."MockQuestions" USING btree ("PassageId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `MockQuestions.IX_MockQuestions_QuestionBankItemId` | `CREATE INDEX "IX_MockQuestions_QuestionBankItemId" ON public."MockQuestions" USING btree ("QuestionBankItemId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewCards.IX_ReviewCards_LessonId` | `CREATE INDEX "IX_ReviewCards_LessonId" ON public."ReviewCards" USING btree ("LessonId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewCards.IX_ReviewCards_SrsCardTemplateId` | `CREATE INDEX "IX_ReviewCards_SrsCardTemplateId" ON public."ReviewCards" USING btree ("SrsCardTemplateId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewCards.IX_ReviewCards_UserId_GrammarPointId_Mode` | `CREATE UNIQUE INDEX "IX_ReviewCards_UserId_GrammarPointId_Mode" ON public."ReviewCards" USING btree ("UserId", "GrammarPointId", "Mode") WHERE ("GrammarPointId" IS NOT NULL)` | Absent | `MISSING` | High | Deduplicate/merge legacy ReviewCards per #54 SQL BEFORE creating filtered unique index (0 rows on Shared). |
| `ReviewCards.IX_ReviewCards_UserId_NormalizedContentKey_Mode` | `CREATE UNIQUE INDEX "IX_ReviewCards_UserId_NormalizedContentKey_Mode" ON public."ReviewCards" USING btree ("UserId", "NormalizedContentKey", "Mode") WHERE ((("NormalizedContentKey")::text <> ''::text) AND ("IsDeleted" = false))` | Absent | `MISSING` | High | Deduplicate/merge legacy ReviewCards per #54 SQL BEFORE creating filtered unique index (0 rows on Shared). |
| `ReviewCards.IX_ReviewCards_UserId_Status_LearningState_NextReviewDate` | `CREATE INDEX "IX_ReviewCards_UserId_Status_LearningState_NextReviewDate" ON public."ReviewCards" USING btree ("UserId", "Status", "LearningState", "NextReviewDate")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewCards.IX_ReviewCards_UserId_VocabularyId_Mode` | `CREATE UNIQUE INDEX "IX_ReviewCards_UserId_VocabularyId_Mode" ON public."ReviewCards" USING btree ("UserId", "VocabularyId", "Mode") WHERE ("VocabularyId" IS NOT NULL)` | Absent | `MISSING` | High | Deduplicate/merge legacy ReviewCards per #54 SQL BEFORE creating filtered unique index (0 rows on Shared). |
| `ReviewCards.IX_ReviewCards_VocabularyId` | `CREATE INDEX "IX_ReviewCards_VocabularyId" ON public."ReviewCards" USING btree ("VocabularyId")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewHistories.IX_ReviewHistories_ReviewItemId_IdempotencyKey` | `CREATE UNIQUE INDEX "IX_ReviewHistories_ReviewItemId_IdempotencyKey" ON public."ReviewHistories" USING btree ("ReviewItemId", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL)` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewHistories.IX_ReviewHistories_ReviewItemId_ReviewedAt` | `CREATE INDEX "IX_ReviewHistories_ReviewItemId_ReviewedAt" ON public."ReviewHistories" USING btree ("ReviewItemId", "ReviewedAt")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `ReviewHistories.IX_ReviewHistories_SessionReference` | `CREATE INDEX "IX_ReviewHistories_SessionReference" ON public."ReviewHistories" USING btree ("SessionReference")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `StudyCohorts.IX_StudyCohorts_SourceProposalId` | `CREATE INDEX "IX_StudyCohorts_SourceProposalId" ON public."StudyCohorts" USING btree ("SourceProposalId") WHERE ("SourceProposalId" IS NOT NULL)` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `Vocabularies.IX_Vocabularies_Status` | `CREATE INDEX "IX_Vocabularies_Status" ON public."Vocabularies" USING btree ("Status")` | Absent | `MISSING` | Low | Additive index; create in post-column step. |
| `CertificateSections.IX_CertificateSections_CertificationLevelId_Code` | `CREATE UNIQUE INDEX "IX_CertificateSections_CertificationLevelId_Code" ON public."CertificateSections" USING btree ("CertificationLevelId", "Code") WHERE ("IsDeleted" = false)` | `CREATE INDEX "IX_CertificateSections_CertificationLevelId_Code" ON public."CertificateSections" USING btree ("CertificationLevelId", "Code")` | `CONFLICTING` | High | Verify 0 active duplicate `(CertificationLevelId, Code)` (verified 0 on Shared), drop non-unique index, create UNIQUE filtered index `WHERE ("IsDeleted" = false)`. |
| `ReviewCards.IX_ReviewCards_UserId_KanjiEntryId_Mode` | `CREATE UNIQUE INDEX "IX_ReviewCards_UserId_KanjiEntryId_Mode" ON public."ReviewCards" USING btree ("UserId", "KanjiEntryId", "Mode") WHERE ("KanjiEntryId" IS NOT NULL)` | `CREATE UNIQUE INDEX "IX_ReviewCards_UserId_KanjiEntryId_Mode" ON public."ReviewCards" USING btree ("UserId", "KanjiEntryId", "Mode")` | `CONFLICTING` | High | Verify 0 duplicate `(UserId, KanjiEntryId, Mode)` WHERE `KanjiEntryId IS NOT NULL` (verified 0 on Shared), drop full unique index, create filtered unique index `WHERE ("KanjiEntryId" IS NOT NULL)`. |
| `LearnerNoteEntries.IX_LearnerNoteEntries_VocabularyListId` | Replaced by composite index (`VocabularyListId_IsDeleted` / `ReviewItemId_ReviewedAt`) | `CREATE INDEX "IX_LearnerNoteEntries_VocabularyListId" ON public."LearnerNoteEntries" USING btree ("VocabularyListId")` | `POTENTIALLY_DESTRUCTIVE` (Superseded) | Low | Drop ONLY AFTER replacement composite index exists so FK/query plans never lose index coverage. |
| `ReviewHistories.IX_ReviewHistories_ReviewItemId` | Replaced by composite index (`VocabularyListId_IsDeleted` / `ReviewItemId_ReviewedAt`) | `CREATE INDEX "IX_ReviewHistories_ReviewItemId" ON public."ReviewHistories" USING btree ("ReviewItemId")` | `POTENTIALLY_DESTRUCTIVE` (Superseded) | Low | Drop ONLY AFTER replacement composite index exists so FK/query plans never lose index coverage. |

## 8. Constraints & Indexes on the 10 Missing Tables (`41` Constraints, `53` Indexes — All `MISSING`)

| Table | Constraints (41) | Indexes (53) |
|---|---|---|
| `ContentPassages` | `FK_ContentPassages_CertificateLevels_CertificateLevelId`, `FK_ContentPassages_LanguageSkills_LanguageSkillId`, `PK_ContentPassages` | `IX_ContentPassages_CertificateLevelId_LanguageSkillId_PublishS~`, `IX_ContentPassages_ContentFingerprint`, `IX_ContentPassages_LanguageSkillId`, `IX_ContentPassages_PassageIdentity`, `PK_ContentPassages` |
| `LearnerHighlights` | `FK_LearnerHighlights_LearnerNoteEntries_LinkedNoteEntryId`, `FK_LearnerHighlights_ReviewCards_LinkedReviewCardId`, `FK_LearnerHighlights_Users_UserId`, `PK_LearnerHighlights` | `IX_LearnerHighlights_LinkedNoteEntryId`, `IX_LearnerHighlights_LinkedReviewCardId`, `IX_LearnerHighlights_UserId_CreatedDate`, `IX_LearnerHighlights_UserId_TargetKey`, `IX_LearnerHighlights_UserId_TargetType_TargetId`, `PK_LearnerHighlights` |
| `MentorTeachingProposalEvents` | `CK_MentorTeachingProposalEvent_RevisionAfter`, `FK_MentorTeachingProposalEvents_MentorTeachingProposals_Propos~`, `PK_MentorTeachingProposalEvents` | `IX_MentorTeachingProposalEvents_ProposalId_CreatedAtUtc`, `PK_MentorTeachingProposalEvents` |
| `MentorTeachingProposals` | `CK_MentorTeachingProposal_Duration`, `CK_MentorTeachingProposal_ProgramType`, `CK_MentorTeachingProposal_Revision`, `CK_MentorTeachingProposal_Status`, `FK_MentorTeachingProposals_CertificateLevels_CertificateLevelId`, `FK_MentorTeachingProposals_MentorGroupPolicies_GroupPolicyId`, `FK_MentorTeachingProposals_StudyCoursePlans_CoursePlanId`, `FK_MentorTeachingProposals_Users_MentorId`, `FK_MentorTeachingProposals_Users_ReviewedByAdminId`, `PK_MentorTeachingProposals` | `IX_MentorTeachingProposals_CertificateLevelId`, `IX_MentorTeachingProposals_CoursePlanId`, `IX_MentorTeachingProposals_GroupPolicyId`, `IX_MentorTeachingProposals_MentorId_Status_UpdatedAtUtc`, `IX_MentorTeachingProposals_MentorId_SubmissionIdempotencyKey`, `IX_MentorTeachingProposals_ReviewedByAdminId`, `IX_MentorTeachingProposals_Status_SubmittedAtUtc`, `PK_MentorTeachingProposals` |
| `NotebookFolders` | `FK_NotebookFolders_Users_UserId`, `PK_NotebookFolders` | `IX_NotebookFolders_UserId_IsDefault`, `IX_NotebookFolders_UserId_IsDeleted_SortOrder`, `IX_NotebookFolders_UserId_NormalizedName`, `PK_NotebookFolders` |
| `NotebookPages` | `FK_NotebookPages_LearnerNotes_LearnerNoteId`, `FK_NotebookPages_Users_UserId`, `PK_NotebookPages` | `IX_NotebookPages_LearnerNoteId_IsDeleted_SortOrder`, `IX_NotebookPages_UserId_IsDefault`, `IX_NotebookPages_UserId_IsDeleted_IsFavorite`, `PK_NotebookPages` |
| `QuestionBankItems` | `FK_QuestionBankItems_CertificateLevels_CertificateLevelId`, `FK_QuestionBankItems_ContentPassages_PassageId`, `FK_QuestionBankItems_LanguageSkills_LanguageSkillId`, `PK_QuestionBankItems` | `IX_QuestionBankItems_AuthorUserId`, `IX_QuestionBankItems_CertificateLevelId_LanguageSkillId_Publis~`, `IX_QuestionBankItems_ContentFingerprint`, `IX_QuestionBankItems_LanguageSkillId`, `IX_QuestionBankItems_PassageId`, `IX_QuestionBankItems_QuestionIdentity`, `PK_QuestionBankItems` |
| `SrsCardTemplates` | `FK_SrsCardTemplates_CertificateLevels_CertificateLevelId`, `FK_SrsCardTemplates_GrammarPatterns_GrammarPatternId`, `FK_SrsCardTemplates_Kanjis_KanjiId`, `FK_SrsCardTemplates_Lessons_LessonId`, `FK_SrsCardTemplates_Users_ReviewedByAdminId`, `FK_SrsCardTemplates_Users_SubmittedByUserId`, `FK_SrsCardTemplates_Vocabularies_VocabularyId`, `PK_SrsCardTemplates` | `IX_SrsCardTemplates_CertificateLevelId`, `IX_SrsCardTemplates_GrammarPatternId`, `IX_SrsCardTemplates_KanjiId`, `IX_SrsCardTemplates_LessonId`, `IX_SrsCardTemplates_NormalizedContentKey`, `IX_SrsCardTemplates_ReviewedByAdminId`, `IX_SrsCardTemplates_Status_JlptLevel_CardType`, `IX_SrsCardTemplates_SubmittedByUserId_Status`, `IX_SrsCardTemplates_VocabularyId`, `PK_SrsCardTemplates` |
| `SrsReviewSessions` | `FK_SrsReviewSessions_Users_UserId`, `PK_SrsReviewSessions` | `IX_SrsReviewSessions_SessionKey`, `IX_SrsReviewSessions_UserId_SessionKind_Status_LastActivityAtU~`, `IX_SrsReviewSessions_UserId_Status_LastActivityAtUtc`, `PK_SrsReviewSessions` |
| `UserSrsPreferences` | `FK_UserSrsPreferences_Users_UserId`, `PK_UserSrsPreferences` | `IX_UserSrsPreferences_DailyReminderEnabled_LastReminderSentLoc~`, `IX_UserSrsPreferences_UserId`, `PK_UserSrsPreferences` |

## 9. `CertificateSections` & `Features` Domain Parity

- **CertificateSections (Shared vs Local):** Shared has 15 active N5–N2 sections (`N5_VOCAB`, `N5_GRAMMAR_READING`, `N5_LISTENING`, `N4_*`, `N3_*`, `N2_*`) and 0 `N3_GENERAL`/`N2_GENERAL` rows, with `SortOrder = 0` on all 15 rows and primary key IDs shifted relative to Local (e.g., `N3_VOCAB` has `Id=8` on Shared vs `Id=10` on Local). **Mandatory rule:** Reconciliation MUST update `SortOrder` (1..3 for N5; 1..4 for N4, N3, N2) matching strictly by `("CertificationLevelId", "Code")`, NEVER by hardcoded `Id`, so existing `MockTestSections.CertificationSectionId` foreign keys (`640` rows on Shared) remain 100% intact.
- **Features (Shared vs Local):** Shared has IDs `1..8` matching pre-migration-#60 seeded features. Reconciliation must backfill `Code`, `AccessMode`, `Category`, `CreditFeatureKey`, `IsCoreFree`, `RoutePrefixesJson`, `SortOrder`, and updated `Name`/`Description` for IDs `1..8`, insert IDs `9..14`, reset the `Features` identity sequence, and enforce `UNIQUE IX_Features_Code`.

## 10. Verified Shared Data Preconditions (Read-Only on Shared, 2026-11-23)

- `Dup_CertificateSections_active = 0`
- `Dup_CertificateTypes_SortOrder = 0`, `Dup_CertificateTypes_NormName = 0`
- `Dup_CertificateLevels_SortOrder = 0`, `Dup_CertificateLevels_NormCode = 0`
- `Active_MentorBookingReservations_overlaps = 0`, `Active_MentorBookingReservations_bad_range = 0`
- `Dup_ReviewCards_Kanji_notnull = 0`, `Dup_ReviewCards_Grammar_notnull = 0` (`ReviewCards = 0`, `ReviewHistories = 0`)
- `StudyCohorts_invalid_capacity = 0`, `StudyCourses_dup_active_cohort = 0`, `Orphan_CreditGrantId_refs = 0`
- `MentorAvailabilities_generated_null_rule = 0`, `Unvalidated constraints = 0`
- **Non-zero Shared tables (verified via `pg_stat_user_tables` / exact counts):** `Users=11`, `Lessons=200`, `PracticeExercises=200`, `PracticeQuestions=1400`, `MockTests=160`, `MockTestSections=640`, `MockQuestions=3200`, `ContentApprovalRequests=760`, `UserSubscriptions=6`, `MentorBookings=4`, `MentorAvailabilities=4`, `MentorProfiles=2`. Note: `Users` PII rows were NOT dumped or cloned.
