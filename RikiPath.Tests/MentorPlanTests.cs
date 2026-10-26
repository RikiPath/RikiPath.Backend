using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
namespace RikiPath.Tests;
public sealed class MentorPlanTests
{
    [Theory]
    [InlineData(1, 14)] [InlineData(3, 35)]
    public async Task Admin_sets_price_and_plan_duration_is_fixed(int sessions, int days)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var claims = new Mock<IClaimService>(); claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 1, Role = Role.Admin });
        var service = new MentorPlanService(new UnitOfWork(db), claims.Object);
        var result = await service.SaveAsync(null, new("Mentor plan", null, sessions, 150000, false), default);
        Assert.True(result.IsSuccess);
        var saved = await db.SubscriptionPlans.FindAsync(result.Result);
        Assert.Equal(days, saved!.DurationDays); Assert.Equal(150000, saved.Price); Assert.False(saved.IsActive);
        Assert.False((await service.SaveAsync(saved.Id, new("Invalid", null, 4, 150000, true), default)).IsSuccess);
        Assert.False((await service.SaveAsync(saved.Id, new("Invalid", null, sessions, 0, true), default)).IsSuccess);
    }
}
