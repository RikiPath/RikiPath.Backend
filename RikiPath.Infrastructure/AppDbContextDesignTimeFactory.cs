using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace RikiPath.Infrastructure;

/// <summary>
/// Tạo <see cref="AppDbContext"/> tại design time cho EF Core CLI (<c>dotnet ef</c>).
/// Ưu tiên chuỗi kết nối thật từ CLI, biến môi trường, User Secrets và cấu hình khởi động;
/// chỉ dùng chuỗi giữ chỗ không kết nối cho các thao tác chỉ đọc/sinh metadata khi chưa cấu hình DB.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    internal const string DesignOnlyFallbackConnectionString =
        "Host=rikipath-design-only.invalid;Database=rikipath_design_only";

    private static readonly string[] EnvConnectionKeys =
    [
        "AppSettings__ConnectionStrings__DefaultConnection",
        "AppSettings:ConnectionStrings:DefaultConnection",
        "ConnectionStrings__DefaultConnection",
        "ConnectionStrings:DefaultConnection",
    ];

    public AppDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var connectionString = ResolveConnectionString(args ?? []);
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgOptions => npgOptions.EnableRetryOnFailure());
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));

        return new AppDbContext(optionsBuilder.Options);
    }

    internal static string ResolveConnectionString(
        string[]? args,
        string[]? processArgs = null,
        string? baseDirectoryOverride = null,
        string? currentDirectoryOverride = null,
        Func<string, string?>? getEnvironmentVariable = null,
        bool includeDefaultConfigFiles = true)
    {
        var cliArgs = args ?? [];
        var effectiveProcessArgs = processArgs ?? Environment.GetCommandLineArgs();
        var envGetter = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

        var configured = ResolveConfiguredConnectionString(
            cliArgs,
            effectiveProcessArgs,
            baseDirectoryOverride,
            currentDirectoryOverride,
            envGetter,
            includeDefaultConfigFiles);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (AllowsDesignOnlyFallback(cliArgs, effectiveProcessArgs))
        {
            return DesignOnlyFallbackConnectionString;
        }

        throw new InvalidOperationException(
            "Database connection string is not configured for EF Core design-time execution. " +
            "Configure 'AppSettings:ConnectionStrings:DefaultConnection' or 'ConnectionStrings:DefaultConnection' " +
            "(via appsettings, user secrets, environment variables 'AppSettings__ConnectionStrings__DefaultConnection' / " +
            "'ConnectionStrings__DefaultConnection'), or pass '--connection \"<connection-string>\"'.");
    }

    internal static string? ResolveConfiguredConnectionString(
        string[]? args,
        string[]? processArgs = null,
        string? baseDirectoryOverride = null,
        string? currentDirectoryOverride = null,
        Func<string, string?>? getEnvironmentVariable = null,
        bool includeDefaultConfigFiles = true)
    {
        var cliArgs = args ?? [];
        var effectiveProcessArgs = processArgs ?? Environment.GetCommandLineArgs();
        var envGetter = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

        var explicitCliConnection = ExtractCliConnection(cliArgs) ?? ExtractCliConnection(effectiveProcessArgs);
        if (IsUsableConnectionString(explicitCliConnection))
        {
            return explicitCliConnection!.Trim();
        }

        foreach (var envKey in EnvConnectionKeys)
        {
            var envValue = envGetter(envKey);
            if (IsUsableConnectionString(envValue))
            {
                return envValue!.Trim();
            }
        }

        if (!includeDefaultConfigFiles)
        {
            return null;
        }

        var configuration = BuildDesignTimeConfiguration(
            baseDirectoryOverride ?? AppContext.BaseDirectory,
            currentDirectoryOverride ?? Directory.GetCurrentDirectory(),
            envGetter);

        var appSettingsConn = configuration["AppSettings:ConnectionStrings:DefaultConnection"];
        if (IsUsableConnectionString(appSettingsConn))
        {
            return appSettingsConn!.Trim();
        }

        var defaultConn = configuration.GetConnectionString("DefaultConnection");
        if (IsUsableConnectionString(defaultConn))
        {
            return defaultConn!.Trim();
        }

        return null;
    }

    internal static bool AllowsDesignOnlyFallback(string[] args, string[] processArgs)
    {
        var combined = args.Concat(processArgs).Where(static s => !string.IsNullOrWhiteSpace(s)).ToArray();

        if (combined.Any(static a =>
                string.Equals(a, "--require-connection", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (combined.Any(static a =>
                string.Equals(a, "--no-connect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--design-only", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (ContainsSubcommand(combined, "database", "update") ||
            ContainsSubcommand(combined, "database", "drop") ||
            ContainsSubcommand(combined, "dbcontext", "scaffold"))
        {
            return false;
        }

        return ContainsSubcommand(combined, "migrations", "add") ||
               ContainsSubcommand(combined, "migrations", "remove") ||
               ContainsSubcommand(combined, "migrations", "script") ||
               ContainsSubcommand(combined, "migrations", "list") ||
               ContainsSubcommand(combined, "migrations", "has-pending-model-changes") ||
               ContainsSubcommand(combined, "dbcontext", "info") ||
               ContainsSubcommand(combined, "dbcontext", "list") ||
               ContainsSubcommand(combined, "dbcontext", "optimize");
    }

    private static bool ContainsSubcommand(string[] tokens, string command, string subcommand)
    {
        var positional = new List<string>(4);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.StartsWith('-'))
            {
                if (!token.Contains('=') &&
                    (string.Equals(token, "--project", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-p", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--startup-project", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-s", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--context", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--configuration", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-c", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--framework", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-f", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--connection", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--output-dir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-o", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--output", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--namespace", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "-n", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--working-dir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--assembly", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--startup-assembly", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--project-dir", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--root-namespace", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--language", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--data-dir", StringComparison.OrdinalIgnoreCase)))
                {
                    i++;
                }

                continue;
            }

            // Bỏ qua đường dẫn dotnet / ef.dll ở vị trí đầu tiến trình.
            if (token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(token), "dotnet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "ef", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            positional.Add(token);
        }

        for (var i = 0; i + 1 < positional.Count; i++)
        {
            if (string.Equals(positional[i], command, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(positional[i + 1], subcommand, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractCliConnection(string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
            {
                return token["--connection=".Length..];
            }

            if (string.Equals(token, "--connection", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < tokens.Length)
            {
                return tokens[i + 1];
            }
        }

        return null;
    }

    private static bool IsUsableConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.Contains("rikipath_design_only", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("Host=invalid", StringComparison.OrdinalIgnoreCase);
    }

    private static IConfigurationRoot BuildDesignTimeConfiguration(
        string baseDirectory,
        string currentDirectory,
        Func<string, string?> envGetter)
    {
        var environment = envGetter("ASPNETCORE_ENVIRONMENT")
                          ?? envGetter("DOTNET_ENVIRONMENT")
                          ?? "Development";

        var builder = new ConfigurationBuilder();

        foreach (var dir in ResolveCandidateDirectories(baseDirectory, currentDirectory))
        {
            builder.AddJsonFile(Path.Combine(dir, "appsettings.json"), optional: true, reloadOnChange: false);
            builder.AddJsonFile(Path.Combine(dir, $"appsettings.{environment}.json"), optional: true, reloadOnChange: false);
        }

        foreach (var assembly in FindUserSecretAssemblies(baseDirectory))
        {
            builder.AddUserSecrets(assembly, optional: true);
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    private static IEnumerable<string> ResolveCandidateDirectories(string baseDirectory, string currentDirectory)
    {
        var candidates = new[]
        {
            baseDirectory,
            currentDirectory,
            Path.Combine(currentDirectory, "RikiPath.WebApi"),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "RikiPath.WebApi")),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var full = Path.GetFullPath(raw);
            if (seen.Add(full) && Directory.Exists(full))
            {
                yield return full;
            }
        }
    }

    private static IEnumerable<Assembly> FindUserSecretAssemblies(string baseDirectory)
    {
        var yielded = new HashSet<Assembly>();

        var entry = Assembly.GetEntryAssembly();
        if (entry is not null &&
            entry.GetCustomAttribute<UserSecretsIdAttribute>() is not null &&
            yielded.Add(entry))
        {
            yield return entry;
        }

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(loaded.GetName().Name, "RikiPath.WebApi", StringComparison.OrdinalIgnoreCase) &&
                loaded.GetCustomAttribute<UserSecretsIdAttribute>() is not null &&
                yielded.Add(loaded))
            {
                yield return loaded;
            }
        }

        if (yielded.Count == 0 && !string.IsNullOrWhiteSpace(baseDirectory))
        {
            var webApiDll = Path.Combine(baseDirectory, "RikiPath.WebApi.dll");
            if (File.Exists(webApiDll))
            {
                Assembly? loaded = null;
                try
                {
                    loaded = Assembly.LoadFrom(webApiDll);
                }
                catch
                {
                    // Không cản trở luồng design-time nếu không nạp được startup assembly.
                }

                if (loaded is not null &&
                    loaded.GetCustomAttribute<UserSecretsIdAttribute>() is not null &&
                    yielded.Add(loaded))
                {
                    yield return loaded;
                }
            }
        }
    }
}
