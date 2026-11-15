using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RikiPath.Infrastructure;

/// <summary>
/// Chỉ dùng để sinh migration ở design time; không kết nối hoặc cập nhật cơ sở dữ liệu.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=invalid;Database=rikipath_design_only")
            .Options;
        return new AppDbContext(options);
    }
}
