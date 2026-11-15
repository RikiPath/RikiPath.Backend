using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;

namespace RikiPath.Tests;

public sealed class MentorPlanIntegrityTests
{
    [Fact]
    public async Task Meeting_plans_never_masquerade_as_other_subscription_plans()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var selfStudy = new SubscriptionPlan { Name = "Self-study", MeetingSessionCount = 0, Price = 100 };
        var twelveSession = new SubscriptionPlan { Name = "Twelve sessions", MeetingSessionCount = 12, Price = 1200 };
        db.SubscriptionPlans.AddRange(selfStudy, twelveSession);
        await db.SaveChangesAsync();

        var repository = new SubscriptionPlanRepository(db);
        Assert.Empty(await repository.GetMeetingPlansAsync());
        Assert.Null(await repository.GetMeetingPlanAsync(selfStudy.Id));
        Assert.Null(await repository.GetMeetingPlanAsync(twelveSession.Id));
        Assert.Equal(0, selfStudy.MeetingSessionCount);
        Assert.Equal(12, twelveSession.MeetingSessionCount);
        Assert.Equal("Self-study", selfStudy.Name);
        Assert.Equal("Twelve sessions", twelveSession.Name);

        var oneSession = new SubscriptionPlan { Name = "One session", MeetingSessionCount = 1, Price = 200, SortOrder = 1 };
        var threeSessions = new SubscriptionPlan { Name = "Three sessions", MeetingSessionCount = 3, Price = 500, SortOrder = 2 };
        db.SubscriptionPlans.AddRange(oneSession, threeSessions);
        await db.SaveChangesAsync();

        var actual = await repository.GetMeetingPlansAsync();
        Assert.Equal([oneSession.Id, threeSessions.Id], actual.Select(x => x.Id).ToArray());
        Assert.Equal(oneSession.Id, (await repository.GetMeetingPlanAsync(oneSession.Id))?.Id);
        Assert.Equal(threeSessions.Id, (await repository.GetMeetingPlanAsync(threeSessions.Id))?.Id);
    }
}
