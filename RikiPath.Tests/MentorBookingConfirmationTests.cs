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

namespace RikiPath.Tests;

public sealed class MentorBookingConfirmationTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MentorMeetingService Service(AppDbContext db, int id = 1, Role role = Role.Mentor)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role });
        return new MentorMeetingService(new UnitOfWork(db), Mock.Of<IPaymentGatewayClient>(), claims.Object,
            new AppSettings(), NullLogger<MentorMeetingService>.Instance);
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
    public async Task Assigned_mentor_accepts_and_state_survives_reload_without_duplicate_action()
    {
        await using var db = Context();
        var booking = await Seed(db);
        var result = await Service(db).AcceptMentorBookingAsync(booking.Id, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(MentorStatus.Accepted, result.Result!.Status);
        Assert.Equal("Real Learner", result.Result.LearnerName);
        Assert.Equal("Câu hỏi thật của học viên", result.Result.Question);
        Assert.Equal(booking.MentorAvailability!.RoomId, result.Result.RoomId);
        db.ChangeTracker.Clear();
        Assert.Equal(MentorStatus.Accepted, (await Service(db).GetMentorBookingAsync(booking.Id, default)).Result!.Status);
        Assert.True((await Service(db).AcceptMentorBookingAsync(booking.Id, default)).IsSuccess);
        Assert.Single(db.MentorBookings);
    }

    [Theory]
    [InlineData(3, Role.Mentor, 404)]
    [InlineData(2, Role.Learner, 403)]
    [InlineData(1, Role.Admin, 403)]
    public async Task Other_users_cannot_read_or_accept(int id, Role role, int expected)
    {
        await using var db = Context();
        var booking = await Seed(db);
        Assert.Equal(expected, (await Service(db, id, role).GetMentorBookingAsync(booking.Id, default)).StatusCode);
        Assert.Equal(expected, (await Service(db, id, role).AcceptMentorBookingAsync(booking.Id, default)).StatusCode);
        Assert.Equal(MentorStatus.Assigned, booking.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, MentorStatus.Assigned, false, true, 404)]
    [InlineData(PaymentStatus.Paid, MentorStatus.Completed, false, true, 409)]
    [InlineData(PaymentStatus.Paid, MentorStatus.Cancelled, false, true, 409)]
    [InlineData(PaymentStatus.Paid, MentorStatus.Assigned, true, true, 409)]
    [InlineData(PaymentStatus.Paid, MentorStatus.Assigned, false, false, 409)]
    public async Task Ineligible_booking_is_not_confirmed(PaymentStatus payment, MentorStatus status, bool expired, bool approved, int expected)
    {
        await using var db = Context();
        var booking = await Seed(db, payment, status, expired, approved);
        Assert.Equal(expected, (await Service(db).AcceptMentorBookingAsync(booking.Id, default)).StatusCode);
        Assert.Equal(status, booking.Status);
    }

    [Fact]
    public async Task Queue_contains_only_own_paid_bookings_and_real_details()
    {
        await using var db = Context();
        var booking = await Seed(db);
        var result = await Service(db).GetMyMeetingsAsync(default);
        Assert.Equal(booking.Id, Assert.Single(result.Result!).Id);
        Assert.Empty((await Service(db, 3).GetMyMeetingsAsync(default)).Result!);
        booking.UserSubscription.PaymentStatus = PaymentStatus.Pending;
        await db.SaveChangesAsync();
        Assert.Empty((await Service(db).GetMyMeetingsAsync(default)).Result!);
    }
}
