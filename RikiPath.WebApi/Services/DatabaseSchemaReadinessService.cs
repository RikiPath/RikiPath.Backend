using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using RikiPath.Infrastructure;

namespace RikiPath.WebApi.Services;

public sealed record SchemaReadinessResult(
    bool IsReady,
    string Status,
    string Reason,
    int? MissingRequiredTables = null,
    int? PendingMigrationCount = null);

/// <summary>
/// Strict read-only runtime readiness verifier for `/health/ready`.
/// Verifies database connectivity and critical table/migration parity WITHOUT ever executing DDL,
/// calling Database.Migrate(), or leaking connection strings / raw exceptions.
/// </summary>
public sealed class DatabaseSchemaReadinessService(
    AppDbContext dbContext,
    ILogger<DatabaseSchemaReadinessService> logger)
{
    private static readonly string[] CriticalRuntimeTables =
    [
        "__EFMigrationsHistory",
        "Users",
        "MentorAvailabilities",
        "MentorGroupPolicies",
        "LearnerNotes",
        "LearnerNoteEntries",
        "StudyCohorts",
    ];

    public async Task<SchemaReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!dbContext.Database.IsRelational())
            {
                return new SchemaReadinessResult(true, "Ready", "Ok");
            }

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                logger.LogError("Health readiness check failed: database connectivity unavailable.");
                return new SchemaReadinessResult(false, "NotReady", "DATABASE_UNREACHABLE");
            }

            var conn = dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(cancellationToken);

            // Verify critical physical tables exist in PostgreSQL public schema
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*)
                FROM pg_catalog.pg_tables
                WHERE schemaname = 'public'
                  AND tablename IN (
                    '__EFMigrationsHistory',
                    'Users',
                    'MentorAvailabilities',
                    'MentorGroupPolicies',
                    'LearnerNotes',
                    'LearnerNoteEntries',
                    'StudyCohorts'
                  );
                """;

            var presentCountObj = await cmd.ExecuteScalarAsync(cancellationToken);
            var presentCount = Convert.ToInt32(presentCountObj, System.Globalization.CultureInfo.InvariantCulture);
            var missingTables = Math.Max(0, CriticalRuntimeTables.Length - presentCount);
            if (missingTables > 0)
            {
                logger.LogError(
                    "Health readiness check blocked traffic: {MissingCount} critical schema table(s) absent.",
                    missingTables);
                return new SchemaReadinessResult(
                    false,
                    "NotReady",
                    "SCHEMA_INCOMPATIBLE",
                    MissingRequiredTables: missingTables);
            }

            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                logger.LogError(
                    "Health readiness check blocked traffic: {PendingCount} pending EF migration(s) unapplied on target DB.",
                    pending.Count);
                return new SchemaReadinessResult(
                    false,
                    "NotReady",
                    "PENDING_MIGRATIONS",
                    MissingRequiredTables: 0,
                    PendingMigrationCount: pending.Count);
            }

            return new SchemaReadinessResult(true, "Ready", "Ok", 0, 0);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Health readiness DB verification failed safely.");
            return new SchemaReadinessResult(false, "NotReady", "DATABASE_CHECK_ERROR");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health readiness unexpected verification error.");
            return new SchemaReadinessResult(false, "NotReady", "READINESS_CHECK_FAILED");
        }
    }
}
