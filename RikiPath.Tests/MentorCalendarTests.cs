using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

public sealed class MentorCalendarTests
{
    [Fact] public async Task Missing_configuration_does_not_access_external_services_or_require_calendar_tables()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var http = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var service = new MentorCalendarService(db, http.Object, Mock.Of<IDataProtectionProvider>(), new ConfigurationBuilder().Build(), new AppSettings());
        Assert.False(service.IsConfigured);
        await service.SyncAsync(1, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConnectAsync(1, default));
        http.VerifyNoOtherCalls();
    }

    [Fact] public async Task OAuth_state_cannot_link_a_different_mentor_account()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var state = "unpredictable-state-value";
        db.Add(new MentorCalendarConnection { MentorId = 2,
            StateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state))).ToLowerInvariant(), StateExpiresAt = DateTime.UtcNow.AddMinutes(5) });
        await db.SaveChangesAsync();
        var http = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["GoogleCalendar:ClientId"] = "test", ["GoogleCalendar:ClientSecret"] = "test-only", ["GoogleCalendar:RedirectUri"] = "https://test.invalid/mentor-availability/" }).Build();
        var service = new MentorCalendarService(db, http.Object, Mock.Of<IDataProtectionProvider>(), config, new AppSettings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(1, state, "code", default));
        Assert.Null((await db.Set<MentorCalendarConnection>().SingleAsync()).ProtectedRefreshToken);
        http.VerifyNoOtherCalls();
    }
}
