# RikiPath.Backend — Mandatory Engineering and Delivery Rules

These rules apply permanently to every agent and contributor working inside
`RikiPath.Backend`. They apply even when an individual task does not repeat them.

Before changing Backend code, read and follow:

- `CLAUDE.md`
- `Docs/BACKEND_SECURITY_AND_ENGINEERING_STANDARDS.md`
- `README.md`

## 1. Development principles

- Prefer code-first fixes to the actual defect. Do not add unrelated work, long reports,
  speculative abstractions, fake data, hard-coded production behavior, or temporary
  workarounds that hide a Backend defect.
- Explicitly requested development seed data is allowed only when clearly marked as demo
  data and must not weaken production authorization or business rules.
- Do not create test projects, test files, mocks, fixtures, or test-only scripts. Never run
  `dotnet test`. This repository permanently uses build, analyzers, migration gates, and
  targeted live API verification instead.
- Avoid repeating full builds or expensive checks without a reason. Run targeted checks
  while developing, then run the applicable mandatory gate once before delivery.
- Never skip compilation, security, migration, API-contract, authorization, or data-integrity
  checks merely to save time or tokens.

## 2. Mandatory pre-push gate

Before every Backend commit, push, merge, or deployment preparation:

1. Inspect the current branch, working tree, staged files, and untracked files. Never reset,
   stash, clean, overwrite, or delete another contributor's work.
2. Confirm the affected Backend projects compile with zero C# errors.
3. Review affected API endpoints, DTOs, services, repositories, and DI registrations.
4. Review authorization, ownership/IDOR protection, validation, exception handling, and
   transaction/concurrency behavior. Do not allow avoidable HTTP 500 responses.
5. Check API-contract compatibility with the Web and Mobile clients.
6. Run security analyzers at error severity. Do not disable analyzers, authentication,
   authorization, or validation to obtain a passing result.
7. Run `dotnet ef migrations has-pending-model-changes` whenever entities, EF configuration,
   `AppDbContext`, provider packages, or migrations are affected. The canonical wrapper is:

   ```powershell
   .\tools\verify-migration-readiness.ps1 -SkipDatabaseCheck -NoBuild
   ```

8. For schema/migration changes, inspect migration pairs, IDs, model snapshot, table/column/
   index/constraint names, destructive operations, and cross-branch migration history.
9. Apply migrations only to an explicitly verified local or QA database, then rerun the
   read-only live preflight. Never let a hook apply migrations automatically.
10. Perform targeted live API verification when runtime behavior or contracts changed.

If any applicable gate fails or has not been verified, stop. Do not commit, push, merge,
deploy, or report PASS.

## 3. Database and migration protection

- Never run `dotnet ef database update` blindly. First resolve and report only the masked
  host, port, database, and environment; never print credentials.
- Automated migration application is allowed only on approved isolated local PostgreSQL
  (`rikipath_local`) or the documented QA clone (`127.0.0.1:55439`).
- Never automatically migrate Shared Supabase, staging, or production. Production requires
  explicit approval, verified backup/restore, a reviewed preflight, and the release runbook.
- Never edit `__EFMigrationsHistory`, fabricate migrations, silently accept schema drift,
  reset a database, drop data, or remove constraints to bypass a failure.
- Every entity or Fluent-configuration model change requires a reviewed migration and an
  updated snapshot. Do not rewrite, delete, or squash previously deployed migrations.
- Do not commit a migration until it has been reviewed for clean-database ordering and
  verified against the corresponding allowed local/QA state.

## 4. Quality and security

- Check affected code for secret exposure, SQL injection, unsafe uploads, input validation,
  RBAC/ownership gaps, race conditions, optimistic concurrency, and transaction safety.
- Use parameterized EF Core/SQL access and preserve immutable financial/order snapshots.
- Dependency changes require compatibility review and relevant NuGet vulnerability output.
- Never claim the whole Backend is error-free based on partial verification.

## 5. Git delivery

- Commit or push only when the task or user explicitly authorizes it.
- Never push directly to `main`; use the protected pull-request workflow.
- Branch protection for `main` must require the existing CI contexts: `Build, Analyzers &
  NuGet Vulnerability Audit`, `Secret Scan (Gitleaks)`, `Analyze C# (security-extended)`, and
  the migration-governance check. Required reviews and up-to-date branches must remain enabled.
- Never commit passwords, tokens, private connection strings, database dumps, `.pgpass`, or
  local/QA secret files.
- The tracked pre-push hook is enabled per clone with:

  ```powershell
  git config core.hooksPath .githooks
  ```

- Preserve `.githooks/pre-push` as an executable file. When it is first added from
  Windows, stage it with `git add --chmod=+x .githooks/pre-push` as part of the
  explicitly authorized commit.

- The hook supplements CI and branch protection; it does not replace either one.

## 6. Required final report

End each Backend task with this concise status block:

```text
Code: PASS / FAIL
Build: PASS / FAIL / NOT RUN
API: VERIFIED / PARTIAL / NOT RUN
Migration: NOT REQUIRED / VERIFIED ON LOCAL / VERIFIED ON QA / BLOCKED
Security: PASS / ISSUES FOUND / NOT VERIFIED
Git: LOCAL ONLY / COMMITTED / PUSHED
Shared Supabase: UNTOUCHED / APPROVED DEPLOYMENT
```

When blocked, state the exact reason and do not bypass the gate.

**Final rule: no verified build, no migration readiness, no safe push.**
