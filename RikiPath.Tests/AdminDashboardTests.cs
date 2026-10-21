using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class AdminDashboardTests
{
    private static IClaimService Claims(Role role)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 1, Role = role });
        return claims.Object;
    }

    [Fact]
    public async Task Dashboard_counts_live_data_and_excludes_deleted_or_processed_records()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.AddRange(
            new UserAccount { Id = 1, Role = Role.Admin, PasswordHash = [], PasswordSalt = [] },
            new UserAccount { Id = 2, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] },
            new UserAccount { Id = 3, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] },
            new UserAccount { Id = 4, Role = Role.Mentor, IsDeleted = true, PasswordHash = [], PasswordSalt = [] });
        db.ContentApprovalRequests.AddRange(
            new ContentApprovalRequest { Status = ContentApprovalStatus.Draft, SubmittedByUserId = 1 },
            new ContentApprovalRequest { Status = ContentApprovalStatus.Draft, SubmittedByUserId = 1, IsDeleted = true },
            new ContentApprovalRequest { Status = ContentApprovalStatus.Approved, SubmittedByUserId = 1 });
        db.MentorAvailabilities.AddRange(
            new MentorAvailability { MentorId = 3, StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1) },
            new MentorAvailability { MentorId = 3, IsApproved = true, StartTime = DateTime.UtcNow.AddDays(2) },
            new MentorAvailability { MentorId = 3, StartTime = DateTime.UtcNow.AddDays(-1) },
            new MentorAvailability { MentorId = 3, StartTime = DateTime.UtcNow.AddDays(3), RejectedAt = DateTime.UtcNow });
        db.SubscriptionPlans.AddRange(new SubscriptionPlan { Id = 1, Name = "Open", IsActive = true },
            new SubscriptionPlan { Id = 2, Name = "Closed", IsActive = false });
        db.UserSubscriptions.AddRange(new UserSubscription { Id = 1, UserId = 2, SubscriptionPlanId = 1, PaymentStatus = PaymentStatus.Paid },
            new UserSubscription { Id = 2, UserId = 2, SubscriptionPlanId = 1, PaymentStatus = PaymentStatus.Pending });
        db.MentorBookings.AddRange(new MentorBooking { UserSubscriptionId = 1 },
            new MentorBooking { UserSubscriptionId = 2, IsDeleted = true });
        await db.SaveChangesAsync();

        var result = await new AdminDashboardService(new UnitOfWork(db), Claims(Role.Admin)).GetAsync(default);
        Assert.True(result.IsSuccess);
        var data = result.Result!;
        Assert.Equal(3, data.TotalUsers);
        Assert.Equal(1, data.Learners);
        Assert.Equal(1, data.Mentors);
        Assert.Equal(1, data.PendingContent);
        Assert.Equal(1, data.PendingMentorSchedules);
        Assert.Equal(1, data.MentorBookings);
        Assert.Equal(1, data.ActivePlans);
        Assert.Equal(1, data.PaidSubscriptions);
    }

    [Theory]
    [InlineData(Role.Learner)]
    [InlineData(Role.Mentor)]
    public async Task Non_admin_cannot_read_dashboard(Role role)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Assert.Equal(403, (await new AdminDashboardService(new UnitOfWork(db), Claims(role)).GetAsync(default)).StatusCode);
    }
}
