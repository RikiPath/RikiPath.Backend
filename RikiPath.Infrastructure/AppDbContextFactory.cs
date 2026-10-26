using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace RikiPath.Infrastructure;

/// <summary>Migration scaffolding does not boot the API or access a configured production database.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var connection = Environment.GetEnvironmentVariable("RIKIPATH_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=rikipath_migration_design;Username=postgres;Password=design-only";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
}
