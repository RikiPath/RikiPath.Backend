using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class MigrationGovernanceTests
{
    private static string ResolveBackendRoot([CallerFilePath] string callerFile = "")
    {
        var testsDir = Path.GetDirectoryName(callerFile);
        if (!string.IsNullOrWhiteSpace(testsDir))
        {
            var root = Path.GetFullPath(Path.Combine(testsDir, ".."));
            if (Directory.Exists(Path.Combine(root, "RikiPath.Infrastructure", "Migrations")))
            {
                return root;
            }
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "RikiPath.Infrastructure", "Migrations")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate RikiPath.Backend root directory.");
    }

    [Fact]
    public void MigrationsDirectory_HasNoDuplicateIds_AndMatchesCompiledMigrations()
    {
        var root = ResolveBackendRoot();
        var migrationsDir = Path.Combine(root, "RikiPath.Infrastructure", "Migrations");

        var migrationFiles = Directory.GetFiles(migrationsDir, "*.cs")
            .Where(static p =>
                !p.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Path.GetFileName(p), "AppDbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(migrationFiles);

        // Guard against orphan *.Designer.cs without its matching migration *.cs class file.
        foreach (var designerPath in Directory.GetFiles(migrationsDir, "*.Designer.cs"))
        {
            var mainPath = designerPath[..^".Designer.cs".Length] + ".cs";
            Assert.True(File.Exists(mainPath), $"Orphan migration designer '{Path.GetFileName(designerPath)}' is missing its .cs class.");
        }

        var fileIds = new List<string>(migrationFiles.Length);
        var migrationAttrRegex = new Regex(@"\[Migration\(""([^""]+)""\)\]", RegexOptions.Compiled);
        var dbContextAttrRegex = new Regex(@"\[DbContext\(typeof\(AppDbContext\)\)\]", RegexOptions.Compiled);

        foreach (var mainPath in migrationFiles)
        {
            var designerPath = mainPath[..^".cs".Length] + ".Designer.cs";
            var combinedText = File.ReadAllText(mainPath) +
                               (File.Exists(designerPath) ? Environment.NewLine + File.ReadAllText(designerPath) : string.Empty);

            Assert.Matches(dbContextAttrRegex, combinedText);

            var match = migrationAttrRegex.Match(combinedText);
            Assert.True(match.Success, $"Missing [Migration] attribute for '{Path.GetFileName(mainPath)}'.");
            fileIds.Add(match.Groups[1].Value);
        }

        var duplicates = fileIds.GroupBy(static id => id, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToArray();
        Assert.Empty(duplicates);

        var factory = new AppDbContextDesignTimeFactory();
        using var context = factory.CreateDbContext(["--no-connect"]);
        var compiledIds = context.Database.GetMigrations().ToArray();

        Assert.Equal(
            fileIds.OrderBy(static id => id, StringComparer.Ordinal),
            compiledIds.OrderBy(static id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void AppDbContext_HasZeroModelSnapshotDrift()
    {
        var factory = new AppDbContextDesignTimeFactory();
        using var context = factory.CreateDbContext(["--no-connect"]);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void ReconciliationSql_EnforcesStrictAdditiveAndFailClosedGuards_ForCanonicalMigrations()
    {
        var root = ResolveBackendRoot();
        var sqlPath = Path.Combine(root, "RikiPath.Infrastructure", "Sql", "ReconcileSharedMigrationHistory.sql");
        Assert.True(File.Exists(sqlPath));

        var sql = File.ReadAllText(sqlPath);

        Assert.Contains("BEGIN;", sql, StringComparison.Ordinal);
        Assert.Contains("COMMIT;", sql, StringComparison.Ordinal);

        Assert.DoesNotMatch(new Regex(@"\bDROP\s+TABLE\b", RegexOptions.IgnoreCase), sql);
        Assert.DoesNotMatch(new Regex(@"\bTRUNCATE\b", RegexOptions.IgnoreCase), sql);
        Assert.DoesNotMatch(new Regex(@"\bDELETE\s+FROM\b", RegexOptions.IgnoreCase), sql);

        Assert.Contains("unexpected migration history entries", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("insufficient_privilege", sql, StringComparison.Ordinal);
        Assert.Contains("pg_get_indexdef", sql, StringComparison.Ordinal);
        Assert.Contains("pg_get_constraintdef", sql, StringComparison.Ordinal);

        var factory = new AppDbContextDesignTimeFactory();
        using var context = factory.CreateDbContext(["--no-connect"]);
        foreach (var migrationId in context.Database.GetMigrations())
        {
            Assert.Contains($"'{migrationId}'", sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeploymentWorkflow_RequiresReadOnlySchemaPreflight_BeforePublishOrTransfer()
    {
        var root = ResolveBackendRoot();
        var deploy = File.ReadAllText(Path.Combine(root, ".github", "workflows", "deploy.yml"));
        var prGate = File.ReadAllText(Path.Combine(root, ".github", "workflows", "migration-governance.yml"));

        var preflight = deploy.IndexOf("Block deployment when shared database schema diverges", StringComparison.Ordinal);
        var publish = deploy.IndexOf("Publish Web API", StringComparison.Ordinal);
        var transfer = deploy.IndexOf("Transfer files to VPS", StringComparison.Ordinal);
        Assert.True(preflight >= 0 && preflight < publish && publish < transfer,
            "Read-only schema preflight must run before publishing or transferring the API binary.");
        Assert.Contains("verify-migration-readiness.ps1 -NoBuild -RequireSharedRemote", deploy, StringComparison.Ordinal);
        Assert.DoesNotContain("RIKIPATH_ALLOW_REMOTE_DB_MIGRATION", deploy, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet ef database update", deploy, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("pull_request:", prGate, StringComparison.Ordinal);
        Assert.Contains("verify-migration-readiness.ps1 -SkipDatabaseCheck -NoBuild", prGate, StringComparison.Ordinal);
    }
}
