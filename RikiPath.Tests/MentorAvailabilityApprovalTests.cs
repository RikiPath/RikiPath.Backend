using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class MentorAvailabilityApprovalTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static IClaimService Claims(Role role, int id)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Role = role, Id = id });
        return claims.Object;
    }
    private static async Task Seed(AppDbContext db)
    {
        db.Users.AddRange(new UserAccount { Id = 501, FirstName = "Mentor", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] },
            new UserAccount { Id = 502, FirstName = "Admin", Role = Role.Admin, PasswordHash = [], PasswordSalt = [] });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task New_slot_is_hidden_until_admin_approves_and_rejected_slot_stays_hidden(bool approve)
    {
        await using var db = Context();
        await Seed(db);
        var uow = new UnitOfWork(db);
        var meeting = new MentorMeetingService(uow, Mock.Of<IPaymentGatewayClient>(), Claims(Role.Mentor, 501),
            new AppSettings(), NullLogger<MentorMeetingService>.Instance);
        var start = DateTime.UtcNow.AddDays(1);
        Assert.False((await meeting.CreateAvailabilityAsync(new CreateMentorAvailabilityRequest
        { StartTime = start, EndTime = start.AddHours(1) }, default)).IsSuccess);
        var legacySlot = new MentorAvailability { MentorId = 501, StartTime = start, EndTime = start.AddHours(1) };
        db.MentorAvailabilities.Add(legacySlot); await db.SaveChangesAsync();
        var created = RikiPath.Application.Responses.ApiResponse<RikiPath.Application.Responses.MentorMeetings.MentorAvailabilityResponse>.Success(
            new() { Id = legacySlot.Id, IsApproved = false });
        Assert.True(created.IsSuccess);
        Assert.False(created.Result!.IsApproved);
        Assert.False(created.Result.IsBooked);
        Assert.Empty(await uow.MentorAvailabilities.GetAvailableSlotsAsync(501, start.AddHours(-1), start.AddHours(2)));

        var review = new MentorAvailabilityReviewService(uow, Claims(Role.Admin, 502));
        var pending = await review.GetSlotsAsync(default);
        Assert.Contains(pending.Result!, x => x.Id == created.Result.Id && !x.IsApproved && x.RejectedAt == null);
        var result = await review.ReviewAsync(created.Result.Id, approve, approve ? null : "Lịch cần điều chỉnh", default);
        Assert.True(result.IsSuccess);
        Assert.Equal(approve, result.Result!.IsApproved);
        Assert.Equal("Admin", result.Result.AdminName);
        Assert.Equal(approve ? 1 : 0, (await uow.MentorAvailabilities.GetAvailableSlotsAsync(501, start.AddHours(-1), start.AddHours(2))).Count);
        if (!approve) Assert.Equal("Lịch cần điều chỉnh", result.Result.RejectionReason);
        Assert.Equal(409, (await review.ReviewAsync(created.Result.Id, true, null, default)).StatusCode);
    }

    [Theory]
    [InlineData(Role.Mentor)]
    [InlineData(Role.Learner)]
    public async Task Non_admin_cannot_view_review_queue_or_approve(Role role)
    {
        await using var db = Context();
        var service = new MentorAvailabilityReviewService(new UnitOfWork(db), Claims(role, 501));
        Assert.Equal(403, (await service.GetSlotsAsync(default)).StatusCode);
        Assert.Equal(403, (await service.ReviewAsync(1, true, null, default)).StatusCode);
    }

    [Fact]
    public async Task Reject_requires_reason_and_expired_slot_cannot_be_approved()
    {
        await using var db = Context();
        await Seed(db);
        var slot = new MentorAvailability { MentorId = 501, StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(-1) };
        db.MentorAvailabilities.Add(slot);
        await db.SaveChangesAsync();
        var service = new MentorAvailabilityReviewService(new UnitOfWork(db), Claims(Role.Admin, 502));
        Assert.Equal(400, (await service.ReviewAsync(slot.Id, false, " ", default)).StatusCode);
        Assert.Equal(409, (await service.ReviewAsync(slot.Id, true, null, default)).StatusCode);
        Assert.False(slot.IsApproved);
    }
}
