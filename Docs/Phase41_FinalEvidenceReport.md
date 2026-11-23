# RikiPath — Phase 4.1 Final Evidence & Release Readiness Report

- **Date:** 2026-11-23
- **Scope:** `RikiPath.Backend` & `RikiPath.Web`
- **QA Release Decision:** **`GO FOR QA`**
- **Production Release Decision:** **`BLOCKED / NO-GO FOR PRODUCTION`** (pending maintenance window, production snapshot/restore check, PII-safe full-data clone rehearsal, and large official JLPT Mock Test authoring)

---

## 1. Master Status Matrix (`PASS / FAIL / BLOCKED / NOT VERIFIED`)

| Priority | Area / Flow | Status | Evidence Summary |
|---|---|---|---|
| **Priority 1** | Shared Supabase Read-Only Schema Diff | **`PASS`** | [`Phase41_SchemaDiffReport.md`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/Docs/Phase41_SchemaDiffReport.md): 119 vs 109 tables (`10` missing), `132` missing cols on `16` common tables, `22` defaults, `13` missing + `1` conflicting constraints, `32` missing + `2` conflicting + `2` superseded indexes, missing `btree_gist`, shifted `CertificateSections` IDs (`SortOrder=0`). |
| **Priority 1** | Read-Only Preflight SQL (`Shared` / `Local` / `QA` / `Clone`) | **`PASS`** | [`PreflightSharedSchemaReadOnly_Phase41.sql`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Infrastructure/Sql/PreflightSharedSchemaReadOnly_Phase41.sql) (`BEGIN READ ONLY; ... ROLLBACK;`): `0` violations across all 13 Shared data preconditions; zero mutations on Shared Supabase. |
| **Priority 1** | Forward-Only Reconciliation Rehearsal & Idempotency | **`PASS`** | [`ReconcileSharedCloneRehearsal_Phase41.sql`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Infrastructure/Sql/ReconcileSharedCloneRehearsal_Phase41.sql) run twice on `rikipath_phase41_rehearsal_20261123`: Run 1 (`INSERT 0 59`, `COMMIT`), Run 2 (`INSERT 0 0`, `COMMIT`), `0` schema diffs vs `rikipath_local` (`119` tables, `1790` cols, `470` indexes, `473` constraints, `btree_gist=true`, `15` CertSections `SortOrder>0`, `14` Features, `dotnet ef database update` -> `"already up to date"`), multi-role API smoke on `127.0.0.1:5279` `HTTP 200`. |
| **Priority 1** | Pre-Reconcile Backup & Restore Verification | **`PASS`** | Native PG16 `pg_dump -Fc` (`rehearsal_pre_reconcile.dump`, `467,754` bytes, SHA-256 `DD74F2365B3E3972BE37EFC4F7E62F99CD7D060B0A1F4BEDDCC22927F01AD1A0`) restored via `pg_restore` into `rikipath_phase41_restore_verify_20261123` with `100%` row & schema integrity. |
| **Priority 1** | **Production Database Reconciliation Execution** | **`BLOCKED`** | Shared Supabase kept strictly read-only (`0` DDL/DML writes). Blocked on live PII governance/full-data clone rehearsal, Supabase Dashboard PITR/restore-to-new-project drill, extension schema `search_path` check (`btree_gist`), and approved write-freeze maintenance window ([`Phase41_DatabaseReconciliationPackage.md`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/Docs/Phase41_DatabaseReconciliationPackage.md)). |
| **Priority 2** | Author & Admin Approval of QA Lesson PracticeExercises | **`PASS`** | `author1@rikipath.local` authored & submitted 2 `PracticeExercises` (`draftId=87,88`, 3 N5 vocab questions each) for Published N5 Lessons `2` & `3`; `admin@rikipath.local` approved both (`HTTP 200`), persisted as active `PracticeExercises` `Id=1,2` (`6` active `PracticeQuestions`). |
| **Priority 2** | Placement -> Roadmap -> Lesson -> Practice -> Unlock Progression (QA) | **`PASS`** | `learner.meeting@rikipath.local` (`Id=900006`): set N5 goal + `2026-12-06` (`HTTP 200`), completed canonical `/api/PlacementTest/start` -> `submit` (`attemptId=19`, `20/20`, `100%`), `/api/StandardRoadmap/assessment` (`200`, initial: Lesson `2` unlocked, Lesson `3` locked) -> Lesson `2` complete & Practice `pe2Id=2` passed (`100%`) -> Lesson `3` unlocked -> Lesson `3` complete & Practice `pe3Id=1` passed (`100%`) -> final roadmap `completedLessons=2, totalLessons=2`. |
| **Priority 3** | Google OAuth Config & Token Rejection Validation | **`PASS`** | `GET /api/auth/google-config` (`HTTP 200`, `enabled=true`, matches `RikiPath.Web/.env` `VITE_GOOGLE_CLIENT_ID`); `POST /api/auth/google` rejects `{}` (`400`), malformed token (`401`), and fake 3-part JWT (`401`). [`GoogleSignIn.jsx`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Web/src/pages/auth/GoogleSignIn.jsx) verified against GIS popup + fallback + linking flow. |
| **Priority 3** | **Real Interactive Google OAuth Popup Login E2E** | **`NOT VERIFIED`** | Requires real user Google account consent inside an interactive browser popup; not runnable in headless automated API verification. |
| **Priority 4** | CertificateSections Standardization Verification | **`PASS`** | `GET /api/admin/certificate-sections` (`HTTP 200`): `15` active N5–N2 sections (`N5`: 3, `N4`: 4, `N3`: 4, `N2`: 4), `0` active `GENERAL` rows, `0` zero `sortOrder` values. |
| **Priority 4** | Listening Audio Media URL Integration Fix & Security Check | **`PASS`** | Fixed [`AllowedMediaUrlPolicy.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Application/Helpers/AllowedMediaUrlPolicy.cs) and [`Program.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.WebApi/Program.cs#L66) so `/api/content-import/listening-audio` URLs under `AwsAudio:PublicBaseUrl` (`/api/audio/content/{ownerId}/{32-hex}.{ext}`) pass validation while external URLs (`https://evil.example/audio.mp3`) and raw disk paths (`D:\Capstone\listening.mp3`) stay rejected (`HTTP 400`). |
| **Priority 4** | Author/Admin Clean QA N5 MockTest & Learner MockTest E2E | **`PASS`** | Uploaded `MP3/listening.mp3` (`HTTP 200`), authored/approved clean N5 `MockTest Id=14` (`12` questions across `N5_VOCAB`, `N5_GRAMMAR_READING`, `N5_LISTENING`, `PublishStatus=Published`); Learner `GET /api/MockTest/14/info` (`200`) -> `POST /14/start` (`201`, `attemptId=20`) -> `PUT /answer` (`200`) -> `POST /submit` (`200`, `100%` across all 4 skills) -> `GET /attempts/20` (`200`). |
| **Priority 4** | Bulk Workbook Dry-Run Preview & Audit | **`PASS`** | Dry-run `POST /api/content-import/preview` on `Data Import Bulk - MockTest đa dạng.xlsx` (`HTTP 200`, `6` sheets: Lesson/Kanji/Vocab/Grammar `0` errors; `Mock Test` `77` rows / `2` error blocks: N5 `11` errors, N4 `156` errors incl. missing Listening & `SectionCode`). Inspected `Data Import Bulk - TEST 5000.xlsx.inspect.ndjson` (`98,529` lines, `[IMPORT-TEST-5000]` synthetic rows, missing `D:\Capstone\...` media, `0` official media ZIPs in repo). |
| **Priority 4** | **Large Official JLPT Mock Test Import** | **`BLOCKED`** | No valid uncopyrighted official JLPT N5–N2 dataset + uploaded audio/image media assets exist in repository; existing bulk workbooks fail validation (`MockTest đa dạng.xlsx`) or are synthetic stress fixtures (`TEST 5000.xlsx`). |
| **Priority 5** | SRS & Personal Notebook API Regression (QA) | **`PASS`** | `GET /api/Review/summary` (`200`, `totalCards=36, dueTodayCount=30`), `GET /api/Review/daily-queue` (`200`, `30` items), `GET /api/Notebook/tree` (`200`), `POST /folders` (`201`, `Id=28`), `POST /files` (`201`, `Id=27`), `POST /pages` (`201`, `Id=43`), `GET /api/Notebook/tree` (`200`). |
| **Priority 5** | **Live Browser UI Verification (SRS & Notebook Flows)** | **`NOT VERIFIED`** | Built-in `browser` subagent previously failed with host error `unknown model key MODEL_PLACEHOLDER_M530: model not found`; per agent policy, model was not switched autonomously and live browser UI E2E remains unverified. |
| **Priority 5** | Backend Release Build, Analyzers & Migration Readiness | **`PASS`** | Isolated Release build (`scratch/phase41/rel-build`): `0 Error(s)` (`REL_BUILD_EXIT=0`, `00:00:31.62`); `dotnet format analyzers --verify-no-changes --severity error` (`exit 0`); `verify-migration-readiness.ps1 -NoBuild` passed (`exit 0`) on Offline, `rikipath_local` (`60/60`), and `rikipath_phase2_qa_20261122` (`60/60`); `0` test projects/files in `RikiPath.Backend` (`4` `.csproj` files only). *(Note: default `Debug` build in `_codex-build` is locked by pre-existing user processes on ports `5265`/`5299`, which were preserved untouched.)* |
| **Priority 5** | Frontend Quality Gates (`RikiPath.Web`) | **`PASS`** | `node scripts/check-locales.mjs --strict` (`9236` JA keys, `0` missing, `0` JA duplicates, `exit 0`); `npm run lint` (`0` errors, `exit 0`); `npm test` (`382/382` passed across `80` suites, `0` failed, `0` skipped, `exit 0`); `npm run build` (`1.28s`, `exit 0`). |

---

## 2. Backend Code Change Implemented in Phase 4.1

### Root Cause Found During QA MockTest Verification
When Author (`author1@rikipath.local`) uploaded `RikiPath.Backend\MP3\listening.mp3` via `POST /api/content-import/listening-audio`, `AwsAudioFileStorageService.ContentReference` returned `https://<AwsAudio:PublicBaseUrl>/api/audio/content/{ownerId}/{32-hex}.mp3` (served via [`AudioController.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.WebApi/Controllers/AudioController.cs)). However, `AllowedMediaUrlPolicy.IsApprovedMediaUrl` only permitted `/api/FileStorage/local/...`, `*.supabase.co`, and `*.amazonaws.com`, causing `POST /api/content-author/manual/items/MockTest` (and all bulk-import/authoring validators using `AllowedMediaUrlPolicy`) to reject uploaded audio URLs with `HTTP 400`: `"URL âm thanh câu 10 phải là URL media hợp lệ trên storage được phép."`

### Minimal Security-Hardened Fix Applied
- **[`AllowedMediaUrlPolicy.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Application/Helpers/AllowedMediaUrlPolicy.cs):**
  - Added `ContentAudioPathRegex` matching strictly `/api/audio/content/{ownerId}/{32-hex}.{audioExt}` (`mp3|wav|m4a|mp4|webm|ogg|flac|amr`).
  - Added `IsSafeContentAudioPath` (blocking `..`, `\`, `%`, `?`, `#`) and `ConfigureApprovedAudioBaseUrl(string? publicBaseUrl)` validating absolute `http`/`https` origins without userinfo/query/fragment.
  - Updated `IsApprovedMediaUrl` to accept relative or loopback/`localhost` `/api/audio/content/...` paths and HTTPS/HTTP URLs whose scheme, host, and port strictly match `_configuredAudioPublicOrigin` while keeping existing `supabase.co`/`amazonaws.com` HTTPS rules and SSRF rejections intact.
- **[`Program.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.WebApi/Program.cs#L66):**
  - Invoked `AllowedMediaUrlPolicy.ConfigureApprovedAudioBaseUrl(appSettings.AwsAudio.PublicBaseUrl);` right after `AwsAudio` settings bind.
- **Verification of Fix:**
  - `POST /api/content-import/listening-audio` -> `HTTP 200` + `POST /api/content-author/manual/items/MockTest` (`MockTest Id=14`) -> `HTTP 200` (`draftId=89`, approved -> published `Id=14`).
  - Negative checks confirmed external URL `https://evil.example/audio.mp3` and local path `D:\Capstone\listening.mp3` still fail with `HTTP 400`.

---

## 3. Detailed QA Verification Evidence (`rikipath_phase2_qa_20261122`, Isolated Port `127.0.0.1:5278`)

### 3.1 Learning Path Architecture Findings & Full E2E Trace
- **Pre-existing QA DB findings:**
  - `Lessons Id=2` ("Phase2 QA Batch Isolation Lesson") and `Id=3` ("Phase2 QA Full Flow Lesson") were `Published` N5 Vocabulary lessons with `0` `PracticeExercises` prior to Phase 4.1 QA authoring.
  - Legacy `MockTests Id=1..4` (`Đề đánh giá N5..N2`) had `PublishStatus=0` (`Draft`), sample `"Câu hỏi mẫu #"` prompts with `"Đáp án A (Chính xác)"` option labels, and `0` Listening audio URLs — causing `/api/MockTest/placement/start`, `/api/MockTest/1/info`, and `/api/MockTest/1/start` to return `409`/`404` under `MockTestService.IsLearnerReady` & `IsApprovedForLearnersAsync`.
  - Coexistence observation: `RikiPath.Web` uses `/api/PlacementTest/*` ([`PlacementTestController.cs`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.WebApi/Controllers/PlacementTestController.cs)) on onboarding and [`StandardAssessmentRoadmap.jsx`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Web/src/pages/learner/StandardAssessmentRoadmap.jsx#L247) (`/app/placement-test`) to generate the `QuestionSnapshotJson` (`CompletedResult`) read by `StandardRoadmapService.GetAssessmentRoadmapAsync`; `/api/MockTest/placement/*` writes `MockTestAttempt` without that snapshot format.
  - **Note on Placement Audio Evidence in QA:** `PracticeTestService` drew the 20 N5 placement questions for `attemptId=19` from the lowest-ID N5 MockTest (`Id=1`, before `Id=14`), so `Listening` reported `hasAudioEvidence=false` / `hasSufficientEvidence=false` while `Vocabulary`, `Grammar`, and `Reading` passed `100%` (`5/5` each) and unlocked the assessment roadmap cleanly. (Direct Learner MockTest E2E on `Id=14` verified uploaded listening audio question delivery and `3/3` `100%` Listening scoring.)
- **End-to-End Flow Executed (`learner.meeting@rikipath.local`, `Id=900006`):**
  1. Author + Admin published QA `PracticeExercises` `Id=1` (`LessonId=3`, 3 questions) and `Id=2` (`LessonId=2`, 3 questions) -> `6` active `PracticeQuestions`.
  2. Learner set goal to `N5` (`targetExamDate=2026-12-06`, `learningGoalVersion` updated `3 -> 4`, `HTTP 200`).
  3. Canonical Placement: `POST /api/PlacementTest/start` (`201`, `attemptId=19`, `20` questions) -> `POST /api/PlacementTest/attempts/19/submit` (`200`, `totalScore=100`, `correctCount=20/20`, `recommendedTargetJlptLevelName="N5"`) -> `GET /api/PlacementTest/attempts/19` (`200`).
  4. Initial Standard Assessment Roadmap (`GET /api/StandardRoadmap/assessment`): `200`, `totalLessons=2`, `completedLessons=0`, Lesson `2` `isLocked=false`, Lesson `3` `isLocked=true`, both `hasPracticeExercise=true`.
  5. Lesson `2` + Practice `2`: `GET /lessons/2` (`200`) -> `POST /lessons/2/complete` (`200`) -> `POST /practice-exercises/2/assessment-roadmap/attempts` (`200`) -> `submit` (`200`, `3/3`, `100%`, `passed=true`). Subsequent `GET /api/StandardRoadmap/assessment` (`200`): `completedLessons=1`, Lesson `2` `isCompleted=true, practicePassed=true, progressPercent=100`, Lesson `3` **`isLocked=false`**.
  6. Lesson `3` + Practice `1`: `GET /lessons/3` (`200`) -> `POST /lessons/3/complete` (`200`) -> `POST /practice-exercises/1/assessment-roadmap/attempts` (`200`) -> `submit` (`200`, `3/3`, `100%`, `passed=true`). Final `GET /api/StandardRoadmap/assessment` (`200`): **`completedLessons=2, totalLessons=2`**. (Also verified directly via PG `LessonProgresses`: Lessons `2` & `3` both `IsCompleted=t, ProgressPercent=100`.)

### 3.2 Google OAuth, CertificateSections, MockTest & SRS/Notebook Smoke
- **Google OAuth:** `GET /api/auth/google-config` -> `200` (`enabled=true`, `clientId` matches `RikiPath.Web/.env` `VITE_GOOGLE_CLIENT_ID`); `POST /api/auth/google` `{}` -> `400`; invalid string -> `401`; unsigned fake 3-part JWT -> `401`.
- **CertificateSections & Clean QA MockTest E2E:** `GET /api/admin/certificate-sections` -> `200` (`15` sections, `0` `GENERAL`, `0` zero `SortOrder`). On newly published QA N5 `MockTest Id=14` (`12` questions across `N5_VOCAB`, `N5_GRAMMAR_READING`, `N5_LISTENING` with uploaded `.mp3`): `GET /api/MockTest/14/info` -> `200`, `POST /14/start` -> `201` (`attemptId=20`), `PUT /attempts/20/answer` -> `200`, `POST /attempts/20/submit` -> `200` (`totalScore=100`, `Vocabulary 3/3`, `Grammar 3/3`, `Reading 3/3`, `Listening 3/3`), `GET /attempts/20` -> `200`.
- **Workbook Preview Audits:**
  - `Data Import Bulk - MockTest đa dạng.xlsx`: `POST /api/content-import/preview` -> `200` (`6` sheets: `Lesson=5/0 errors`, `Kanji=50/0`, `Vocabulary=25/0`, `Grammar Pattern=25/0`, `Mock Test=77 rows/2 error blocks`, `Practice Exercise=5 rows/1 error`); `?entityType=4` confirmed `Mock Test` Row 2 N5 (`5` questions, `11` errors: missing `SectionCode`, invalid `AudioUrl`/`ImageUrl`, missing N5 Reading) and Row 7 N4 (`72` questions, `156` errors: inconsistent `MockTestDescription`, duplicate sort orders, missing `SectionCode`, un-uploaded media URLs, missing N4 Listening).
  - `Data Import Bulk - TEST 5000.xlsx.inspect.ndjson`: `98,529` lines (`Lesson` 201 rows, `Kanji` 51, `Grammar Pattern` 51, `Vocabulary` 101, `Mock Test` `A1:Y3201` 3201 rows, `Practice Exercise` `A1:T1401` 1401 rows), synthetic `[IMPORT-TEST-5000]` prefix and `D:\Capstone\...` non-storage media references; zero official JLPT media `.zip` archives exist in repository.
- **Post-Final-Edit `rel-build` Smoke Check (`127.0.0.1:5278`):**
  - Confirmed cleanly stopped `5278` after verifying `google-config=200`, `google-empty=400`, `google-bad=401`, `roadmap-assess=200 total=2 completed=2`, `cert-sections=200 count=15`, `mocktest14-info=200`, `review-summary=200`, `notebook-tree=200`.

---

## 4. Final Quality Gates Summary

- **Backend (`RikiPath.Backend`):**
  - Isolated Release Build (`dotnet build RikiPath.Backend.slnx -c Release -p:OutDir=...`): **`0 Error(s)`**, exit `0` (`1001` pre-existing warnings, `00:00:31.62`).
  - `dotnet format analyzers RikiPath.Backend.slnx --verify-no-changes --severity error`: **exit `0`**.
  - `.\tools\verify-migration-readiness.ps1 -NoBuild -SkipDatabaseCheck`: **exit `0`** (`60` canonical migrations, `0` missing/invalid pairs, `0` duplicates, `0` model drift).
  - `.\tools\verify-migration-readiness.ps1 -NoBuild -ConnectionString <local>`: **exit `0`** (`rikipath_local`: `60` applied, `0` pending, `0` unexpected, `btree_gist=true`, `0` unvalidated FKs, `18/18` tables).
  - `.\tools\verify-migration-readiness.ps1 -NoBuild -ConnectionString <qa>`: **exit `0`** (`rikipath_phase2_qa_20261122`: `60` applied, `0` pending, `0` unexpected, `btree_gist=true`, `0` unvalidated FKs, `18/18` tables).
  - Permanent rule compliance: **`0` test projects and `0` test files** in `RikiPath.Backend` (`dotnet test` never executed).
- **Frontend (`RikiPath.Web`):**
  - `node scripts/check-locales.mjs --strict`: **exit `0`** (`JA keys: 9236 · missing JA: 0 · duplicate JA keys: 0`).
  - `npm run lint`: **exit `0`**.
  - `npm test`: **exit `0`** (`382` passed, `80` suites, `0` failed, `0` skipped).
  - `npm run build`: **exit `0`** (`1.28s`).
- **Git & Cleanliness:** Preserved all pre-existing dirty changes in both `RikiPath.Backend` and `RikiPath.Web`; added Phase 4.1 artifacts/fixes cleanly without secrets, passwords, tokens, or connection credentials.

---

## 5. Next Required Operational Actions Before Any Production Release

1. Obtain formal maintenance window approval and freeze Shared Supabase writes.
2. Perform a Supabase Platform Backup / PITR checkpoint and complete a **Restore to a New Project** drill on Supabase Dashboard, then run [`ReconcileSharedCloneRehearsal_Phase41.sql`](file:///d:/FPT%20University/SEP491-new/rikipath/RikiPath.Backend/RikiPath.Infrastructure/Sql/ReconcileSharedCloneRehearsal_Phase41.sql) against a masked full-data clone of that restored project.
3. Verify `btree_gist` schema/role visibility (`public` vs `extensions` in `search_path`) on the restored clone and align `$AuditedExternalMigrations` / CI secrets if preserving Shared's two non-canonical history IDs.
4. Author/curate real uncopyrighted official JLPT Mock Tests and upload their `.mp3`/image assets via official media storage before running live placement/mock-exam release sign-off.
5. Run interactive browser manual/QA verification for Google Sign-In popup login and SRS/Personal Notebook UI flows.
