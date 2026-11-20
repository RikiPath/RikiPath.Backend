# RikiPath.Backend — Permanent Engineering & Agent Rules

Before modifying any code in `RikiPath.Backend`, read and follow:

- `Docs/BACKEND_SECURITY_AND_ENGINEERING_STANDARDS.md`
- `README.md` (EF Core Migration & Database Safety Standards)

## Mandatory Rules

1. **Zero Test Projects / Zero Test Files Policy (Permanent):**
   - `RikiPath.Backend` intentionally does **not** maintain `RikiPath.Tests` or any Unit/Integration Test files, Fixtures, Mocks, or test-only scripts.
   - **NEVER create any test file** (`.cs` tests, test `.csproj`, mocks, test fixtures, or test runner scripts) for any Backend task.
   - **NEVER run `dotnet test`**.
   - Verify Backend work strictly via:
     1. Clean solution compilation (`dotnet build RikiPath.Backend.slnx`) and analyzer gates (`dotnet format analyzers RikiPath.Backend.slnx --severity error`),
     2. Read-only migration safety validation (`./tools/verify-migration-readiness.ps1`) when entities/configurations/migrations are modified,
     3. Direct live HTTP/API verification against the running local API (`http://localhost:5265`) when runtime verification is needed.
2. **Database & Migration Safety:**
   - Never apply EF migrations directly to Shared Supabase (`verify-migration-readiness.ps1` + explicit review required).
   - Local schema testing targets ONLY isolated local PostgreSQL (`rikipath_local`).
3. **Security & Business Integrity:**
   - Never expose secrets (PayOS keys, JWT keys, connection strings) in HTTP responses, audit logs, or git-tracked files.
   - Protect historical orders/subscriptions using snapshots (`AmountPaid`, `DurationDaysSnapshot`, `PlanNameSnapshot`, `OrderSnapshot`, `PolicyVersionSnapshot`) and optimistic concurrency tokens (`Revision`).
