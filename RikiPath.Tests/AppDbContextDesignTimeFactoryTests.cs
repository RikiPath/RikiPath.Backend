using Microsoft.EntityFrameworkCore;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class AppDbContextDesignTimeFactoryTests
{
    private static Func<string, string?> EnvMap(IReadOnlyDictionary<string, string> values) =>
        key => values.TryGetValue(key, out var value) ? value : null;

    [Fact]
    public void ResolveConnectionString_UsesCliConnectionArgument_First()
    {
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] = "Host=127.0.0.1;Database=env_app",
            ["ConnectionStrings__DefaultConnection"] = "Host=127.0.0.1;Database=env_conn"
        });

        var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
            args: ["--connection", "Host=127.0.0.1;Port=55439;Database=cli_override"],
            processArgs: Array.Empty<string>(),
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Equal("Host=127.0.0.1;Port=55439;Database=cli_override", resolved);
    }

    [Theory]
    [InlineData("AppSettings__ConnectionStrings__DefaultConnection")]
    [InlineData("AppSettings:ConnectionStrings:DefaultConnection")]
    [InlineData("ConnectionStrings__DefaultConnection")]
    [InlineData("ConnectionStrings:DefaultConnection")]
    public void ResolveConnectionString_ResolvesSupportedEnvironmentVariableKeys_OverConfigFiles(string envKey)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rikipath-ef-env-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "appsettings.json"),
                """
                {
                  "AppSettings": {
                    "ConnectionStrings": {
                      "DefaultConnection": "Host=127.0.0.1;Database=from_appsettings"
                    }
                  }
                }
                """);

            var env = EnvMap(new Dictionary<string, string>
            {
                [envKey] = "Host=127.0.0.1;Port=55439;Database=from_env"
            });

            var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
                args: Array.Empty<string>(),
                processArgs: Array.Empty<string>(),
                baseDirectoryOverride: tempDir,
                currentDirectoryOverride: tempDir,
                getEnvironmentVariable: env,
                includeDefaultConfigFiles: true);

            Assert.Equal("Host=127.0.0.1;Port=55439;Database=from_env", resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveConfiguredConnectionString_ReadsAppsettingsFromDirectoryOverride()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rikipath-ef-cfg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "appsettings.json"),
                """
                {
                  "AppSettings": {
                    "ConnectionStrings": {
                      "DefaultConnection": "Host=127.0.0.1;Port=55439;Database=from_base_json"
                    }
                  }
                }
                """);

            File.WriteAllText(
                Path.Combine(tempDir, "appsettings.Testing.json"),
                """
                {
                  "AppSettings": {
                    "ConnectionStrings": {
                      "DefaultConnection": "Host=127.0.0.1;Port=55439;Database=from_testing_json"
                    }
                  }
                }
                """);

            var env = EnvMap(new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing"
            });

            var resolved = AppDbContextDesignTimeFactory.ResolveConfiguredConnectionString(
                args: Array.Empty<string>(),
                processArgs: Array.Empty<string>(),
                baseDirectoryOverride: tempDir,
                currentDirectoryOverride: tempDir,
                getEnvironmentVariable: env,
                includeDefaultConfigFiles: true);

            Assert.Equal("Host=127.0.0.1;Port=55439;Database=from_testing_json", resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("Host=invalid;Database=rikipath_design_only")]
    [InlineData("Host=rikipath-design-only.invalid;Database=rikipath_design_only")]
    [InlineData("Host=INVALID;Database=something")]
    public void ResolveConfiguredConnectionString_IgnoresDesignOnlyPlaceholders(string placeholder)
    {
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] = placeholder
        });

        var resolved = AppDbContextDesignTimeFactory.ResolveConfiguredConnectionString(
            args: Array.Empty<string>(),
            processArgs: Array.Empty<string>(),
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Null(resolved);
    }

    [Theory]
    [InlineData("migrations", "add", "SampleMigration")]
    [InlineData("migrations", "remove")]
    [InlineData("migrations", "script")]
    [InlineData("migrations", "list")]
    [InlineData("migrations", "has-pending-model-changes")]
    [InlineData("dbcontext", "info")]
    [InlineData("dbcontext", "list")]
    [InlineData("dbcontext", "optimize")]
    [InlineData("--no-connect")]
    [InlineData("--design-only")]
    public void ResolveConnectionString_UsesDesignOnlyFallback_ForMetadataCommandsWhenUnconfigured(params string[] tokens)
    {
        var env = EnvMap(new Dictionary<string, string>());

        var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
            args: Array.Empty<string>(),
            processArgs: tokens,
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Equal(AppDbContextDesignTimeFactory.DesignOnlyFallbackConnectionString, resolved);
    }

    [Theory]
    [InlineData("database", "update")]
    [InlineData("database", "drop")]
    [InlineData("dbcontext", "scaffold")]
    [InlineData("--require-connection")]
    [InlineData]
    public void ResolveConnectionString_ThrowsHelpfulInvalidOperation_WhenLiveDatabaseRequiredAndUnconfigured(params string[] tokens)
    {
        var env = EnvMap(new Dictionary<string, string>());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AppDbContextDesignTimeFactory.ResolveConnectionString(
                args: Array.Empty<string>(),
                processArgs: tokens,
                getEnvironmentVariable: env,
                includeDefaultConfigFiles: false));

        Assert.Contains("AppSettings__ConnectionStrings__DefaultConnection", ex.Message);
        Assert.DoesNotContain("Host=invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveConnectionString_DoesNotMistakeShortConfigurationFlagForConnectionString()
    {
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] = "Host=127.0.0.1;Port=55439;Database=env_db"
        });

        var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
            args: ["-c", "Release"],
            processArgs: ["dotnet", "ef.dll", "database", "update", "-c", "Release"],
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Equal("Host=127.0.0.1;Port=55439;Database=env_db", resolved);
    }

    [Fact]
    public void ResolveConnectionString_WithNoConnectSwitchAndAppsettingsFallback_DoesNotThrowFormatException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rikipath-ef-nocfg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var env = EnvMap(new Dictionary<string, string>());
            var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
                args: ["--no-connect"],
                processArgs: Array.Empty<string>(),
                baseDirectoryOverride: tempDir,
                currentDirectoryOverride: tempDir,
                getEnvironmentVariable: env,
                includeDefaultConfigFiles: true);

            Assert.Equal(AppDbContextDesignTimeFactory.DesignOnlyFallbackConnectionString, resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CreateDbContext_WithExplicitConnectionArg_BuildsModelAndDiscoversMigrations()
    {
        var factory = new AppDbContextDesignTimeFactory();
        using var context = factory.CreateDbContext(["--connection", "Host=127.0.0.1;Port=55439;Database=design_test"]);

        Assert.NotNull(context);
        var migrations = context.Database.GetMigrations().ToList();
        Assert.NotEmpty(migrations);
    }

    [Fact]
    public void ResolveConnectionString_BlocksUnapprovedRemoteSupabaseUpdate_WithoutPrintingCredentials()
    {
        var secretPassword = "SUPER_SECRET_PASSWORD_DO_NOT_LEAK";
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] =
                $"Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.demo;Password={secretPassword}"
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AppDbContextDesignTimeFactory.ResolveConnectionString(
                args: Array.Empty<string>(),
                processArgs: ["dotnet", "ef.dll", "database", "update"],
                getEnvironmentVariable: env,
                includeDefaultConfigFiles: false));

        Assert.Contains("aws-0-ap-southeast-1.pooler.supabase.com", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RIKIPATH_ALLOW_REMOTE_DB_MIGRATION", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretPassword, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveConnectionString_AllowsRemoteSupabaseUpdate_WhenExplicitOptInEnvProvided()
    {
        var conn = "Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.demo;Password=qa_only";
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] = conn,
            ["RIKIPATH_ALLOW_REMOTE_DB_MIGRATION"] = "true"
        });

        var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
            args: Array.Empty<string>(),
            processArgs: ["dotnet", "ef.dll", "database", "update"],
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Equal(conn, resolved);
    }

    [Fact]
    public void ResolveConnectionString_AllowsRemoteSupabaseMetadataCommand_WithoutOptIn()
    {
        var conn = "Host=aws-0-ap-southeast-1.pooler.supabase.com;Port=5432;Database=postgres";
        var env = EnvMap(new Dictionary<string, string>
        {
            ["AppSettings__ConnectionStrings__DefaultConnection"] = conn
        });

        var resolved = AppDbContextDesignTimeFactory.ResolveConnectionString(
            args: Array.Empty<string>(),
            processArgs: ["dotnet", "ef.dll", "migrations", "list"],
            getEnvironmentVariable: env,
            includeDefaultConfigFiles: false);

        Assert.Equal(conn, resolved);
    }
}
