using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Controllers;
using Xunit;

namespace RikiPath.Tests;

public sealed class MentorGroupPolicyTests
{
    [Fact]
    public async Task Versioned_policy_preserves_prior_effective_values_and_rejects_stale_writes()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.MentorGroupPolicies.Add(new MentorGroupPolicy
        {
            Id = 1,
            Version = 1,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxLearners = 1,
            SessionMinutes = 90,
            RestMinutes = 15,
            DailySessionCap = 4,
            PublicationHorizonDays = 30,
            CancellationLeadHours = 24,
            ReschedulingLeadHours = 24,
            HomeworkDeadlineDays = 7,
            PlacementAttemptLimit = 2,
            RemediationCompletionThresholdPercent = 90,
            KpiReportingPeriodDays = 30,
            RecurringWeekdayPatternsJson = "[[1,3,5]]",
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new MentorGroupPolicyService(context);
        var nextDate = DateTime.UtcNow.Date.AddDays(5).AddHours(17);
        var request = ValidRequest(nextDate);
        var created = await service.CreateVersionAsync(request, adminId: 900001);

        Assert.True(created.IsSuccess);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(2, created.Result!.Version);
        Assert.Equal(4, created.Result.MaxLearners);
        Assert.Equal(1, (await service.GetEffectiveAsync(nextDate.AddTicks(-1)))!.Version);
        Assert.Equal(2, (await service.GetEffectiveAsync(nextDate))!.Version);
        Assert.Equal(2, (await service.GetHistoryAsync()).Count);

        var stale = await service.CreateVersionAsync(request, adminId: 900001);
        Assert.False(stale.IsSuccess);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(2, (await service.GetHistoryAsync()).Count);
    }

    [Fact]
    public async Task Capacity_above_approved_maximum_is_rejected()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new MentorGroupPolicyService(context);
        var request = ValidRequest(DateTime.UtcNow.Date.AddDays(5).AddHours(17), maxLearners: 5);

        var result = await service.CreateVersionAsync(request, adminId: 900001);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(await service.GetHistoryAsync());
    }

    [Fact]
    public void Policy_api_requires_admin_role()
    {
        var authorize = Assert.Single(typeof(AdminMentorGroupPolicyController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", authorize.Roles);
    }

    private static CreateMentorGroupPolicyRequest ValidRequest(DateTime effectiveFromUtc, int maxLearners = 4) => new()
    {
        ExpectedCurrentVersion = 1,
        EffectiveFromUtc = effectiveFromUtc,
        MaxLearners = maxLearners,
        SessionMinutes = 90,
        RestMinutes = 15,
        DailySessionCap = 4,
        PublicationHorizonDays = 30,
        CancellationLeadHours = 24,
        ReschedulingLeadHours = 24,
        HomeworkDeadlineDays = 7,
        PlacementAttemptLimit = 2,
        RemediationCompletionThresholdPercent = 90,
        KpiReportingPeriodDays = 30,
        RecurringWeekdayPatterns = [[1, 3, 5], [2, 4, 6]]
    };
}
