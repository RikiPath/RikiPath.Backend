# RikiPath.Backend — EF Core Migration & Database Safety Standards

Canonical operational rules for schema changes, EF Core migrations, QA verification, and shared database reconciliation across all `RikiPath.Backend` branches.

## Core Safety Rules

1. **Never run migrations against an unverified database target.**
   - Always verify the resolved `Host` and `Database` via [`tools/verify-migration-readiness.ps1`](tools/verify-migration-readiness.ps1) before running `dotnet ef database update`.
   - Keep **Local**, **QA (`127.0.0.1:55439`)**, **Staging**, and **Production / Shared Supabase** strictly separated.
   - `AppDbContextDesignTimeFactory` blocks unapproved `database update` / `database drop` commands targeting remote Supabase hosts (`*.supabase.co`, `*.supabase.com`) unless `RIKIPATH_ALLOW_REMOTE_DB_MIGRATION=true` (or `-- --allow-remote-database`) is explicitly provided after review.
2. **Never store credentials in source code, scripts, logs, or reports.**
   - Supply connection strings via environment variables (`AppSettings__ConnectionStrings__DefaultConnection` / `ConnectionStrings__DefaultConnection`), User Secrets, or CI secret stores. Never print secrets in preflight or CI logs.
3. **Every domain entity / Fluent configuration change requires a reviewed migration (zero drift).**
   - CI & pre-merge must verify clean compilation (`dotnet build RikiPath.Backend.slnx`) and offline migration parity via `./tools/verify-migration-readiness.ps1 -SkipDatabaseCheck` (`HasPendingModelChanges() == false`, zero duplicate IDs, matching `.Designer.cs` files).
4. **Never arbitrarily rewrite, delete, or squash previously deployed migrations, nor mix independent branches directly on shared DBs.**
   - Do not run `dotnet ef database update` from unmerged feature branches against Shared Supabase.
   - Every migration requires an explicit PR owner; cross-branch migrations must be integrated into the canonical branch before shared deployment.
5. **No destructive migration without an approved data-migration strategy, and no blind history tampering.**
   - Never insert rows into `"__EFMigrationsHistory"` unless schema columns, constraints, and indexes are structurally verified ([`ReconcileSharedMigrationHistory.sql`](RikiPath.Infrastructure/Sql/ReconcileSharedMigrationHistory.sql)).
   - Never assume `Down()` is lossless on production data.
6. **Always rehearse on an isolated QA clone (`127.0.0.1:55439`) first.**
   - Verify database identity, pre-check, reconciliation, pending migrations, schema parity, idempotency, existing data preservation, and Backend API smoke checks before requesting backup & shared deployment approval.
7. **One migration writer per shared database.**
   - Require the `Migration governance` pull-request check and a named migration owner in branch protection; never use a feature-branch connection string to mutate Shared Supabase.
   - Before an approved release, the owner records the exact source SHA, read-only history/schema fingerprint, tested backup, a team-wide write freeze, and the deployment window. Re-run the fingerprint under the freeze; any change invalidates approval and requires a new QA clone.
   - Keep the remote migration override disabled outside that approved window. CI and routine Web API startup must not run schema migrations.

## Automated Read-Only Tooling & Release Package

- **Offline CI build & migration checks:**
  ```powershell
  dotnet build RikiPath.Backend.slnx -c Release
  .\tools\verify-migration-readiness.ps1 -SkipDatabaseCheck -NoBuild
  ```
- **Live target read-only preflight:**
  ```powershell
  .\tools\verify-migration-readiness.ps1
  ```
- **Full Deployment, Post-Deployment & Rollback Runbook:**
  See [`Docs/MigrationReleaseReadiness.md`](Docs/MigrationReleaseReadiness.md).
- **Security, Code Quality, CI/CD & Branch Protection Standards:**
  See [`Docs/BACKEND_SECURITY_AND_ENGINEERING_STANDARDS.md`](Docs/BACKEND_SECURITY_AND_ENGINEERING_STANDARDS.md).
