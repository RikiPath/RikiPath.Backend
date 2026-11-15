using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

public sealed class MentorBookingReadTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MentorBookingService Service(AppDbContext db, int id = 1, Role role = Role.Mentor)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role });
        var unitOfWork = new UnitOfWork(db);
        var ledger = Mock.Of<IMentorCreditLedgerService>();
        return new MentorBookingService(db, unitOfWork, claims.Object, ledger, new AppSettings(), NullLogger<MentorBookingService>.Instance);
    }
    private static async Task<MentorBooking> Seed(AppDbContext db, PaymentStatus payment = PaymentStatus.Paid,
        MentorStatus status = MentorStatus.Assigned, bool expired = false, bool approved = true)
    {
        var learner = new UserAccount { Id = 2, FirstName = "Real", LastName = "Learner", Role = Role.Learner, PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 1, FirstName = "Real", LastName = "Mentor", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        var slot = new MentorAvailability { Mentor = mentor, IsApproved = approved,
            StartTime = DateTime.UtcNow.AddHours(expired ? -2 : 1), EndTime = DateTime.UtcNow.AddHours(expired ? -1 : 2) };
        var plan = new SubscriptionPlan { Name = "Real plan" };
        var subscription = new UserSubscription { UserAccount = learner, SubscriptionPlan = plan, PaymentStatus = payment };
        var booking = new MentorBooking { MentorAvailability = slot, UserSubscription = subscription, Status = status,
            ScheduledAt = slot.StartTime, Question = "Câu hỏi thật của học viên" };
        db.MentorBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    [Fact]
    public async Task Mentor_queue_reads_only_own_paid_bookings_and_preserves_historical_status()
    {
        await using var db = Context();
        var booking = await Seed(db);
        var result = await Service(db).GetMyMentorBookingsAsync(default);
        Assert.Equal(booking.Id, Assert.Single(result.Result!).Id);
        Assert.Equal(MentorStatus.Assigned, result.Result![0].Status);
        Assert.Empty((await Service(db, 3).GetMyMentorBookingsAsync(default)).Result!);
        booking.UserSubscription.PaymentStatus = PaymentStatus.Pending;
        await db.SaveChangesAsync();
        Assert.Empty((await Service(db).GetMyMentorBookingsAsync(default)).Result!);
    }

    [Fact]
    public async Task Mentor_booking_detail_is_limited_to_its_owner()
    {
        await using var db = Context();
        var booking = await Seed(db);

        var ownerResult = await Service(db).GetMentorBookingAsync(booking.Id, default);
        Assert.True(ownerResult.IsSuccess);
        Assert.Equal(booking.Id, ownerResult.Result!.Id);
        Assert.Equal(booking.UserSubscription.UserId, ownerResult.Result.LearnerId);
        Assert.Equal(MentorStatus.Assigned, ownerResult.Result.Status);

        Assert.Equal(404, (await Service(db, id: 3).GetMentorBookingAsync(booking.Id, default)).StatusCode);
        Assert.Equal(403, (await Service(db, id: 2, role: Role.Learner).GetMentorBookingAsync(booking.Id, default)).StatusCode);
    }
}
