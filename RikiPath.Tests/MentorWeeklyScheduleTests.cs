using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
namespace RikiPath.Tests;

public sealed class MentorWeeklyScheduleTests
{
    [Theory]
    [InlineData("2026-10-30T10:59:59Z", true)]
    [InlineData("2026-10-30T11:00:00Z", false)]
    [InlineData("2026-10-31T01:00:00Z", false)]
    public void Deadline_is_Friday_18_Vietnam_time(string utc, bool expected)
        => Assert.Equal(expected, MentorScheduleRules.CanRegister(new DateOnly(2026, 11, 2), DateTime.Parse(utc).ToUniversalTime()));
    [Theory]
    [InlineData("Morning", 1)] [InlineData("Afternoon", 6)] [InlineData("Evening", 11)]
    public void Fixed_slots_use_Vietnam_time_and_leave_ten_minutes(string period, int utcHour)
    {
        var start = MentorScheduleRules.SlotStart(new DateOnly(2026, 11, 2), period, 0);
        Assert.Equal(utcHour, start.Hour);
        Assert.Equal(TimeSpan.FromMinutes(10), MentorScheduleRules.SlotStart(new DateOnly(2026, 11, 2), period, 1) - start.AddMinutes(50));
    }
    private static IClaimService User(Role role, int id = 1)
    {
        var mock = new Mock<IClaimService>(); mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role }); return mock.Object;
    }
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static MentorScheduleService Service(AppDbContext db, Role role) => new(new UnitOfWork(db), User(role),
        Mock.Of<IEmailService>(), NullLogger<MentorScheduleService>.Instance);
    [Fact]
    public async Task Cannot_publish_less_than_five_slots_and_assignments_are_private_fifty_minute_drafts()
    {
        await using var db = Context();
        var week = MentorScheduleRules.Monday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))).AddDays(7);
        var mentor = new UserAccount { Id = 2, Role = Role.Mentor, FirstName = "Mentor", PasswordHash = [], PasswordSalt = [] };
        var morning = new MentorWeeklyAvailability { Mentor = mentor, Date = week, Period = "Morning" };
        var evening = new MentorWeeklyAvailability { Mentor = mentor, Date = week, Period = "Evening" };
        db.MentorWeeklyAvailabilities.AddRange(morning, evening); await db.SaveChangesAsync();
        var service = Service(db, Role.Admin);
        Assert.True((await service.AssignAsync(new(morning.Id, [0, 1, 2, 3]), default)).IsSuccess);
        Assert.False((await service.PublishAsync(week, default)).IsSuccess);
        Assert.All(await db.MentorAvailabilities.ToListAsync(), slot => {
            Assert.False(slot.IsApproved); Assert.Equal(1, slot.MaxLearners); Assert.Equal(50, (slot.EndTime - slot.StartTime).TotalMinutes);
        });
        Assert.False((await service.AssignAsync(new(morning.Id, [0]), default)).IsSuccess);
        Assert.True((await service.AssignAsync(new(evening.Id, [0]), default)).IsSuccess);
    }
    [Theory]
    [InlineData(23, false)] [InlineData(25, true)]
    public async Task Learner_cancellation_enforces_24_hours_and_refunds_only_once(int hours, bool success)
    {
        await using var db = Context();
        var slot = new MentorAvailability { MentorId = 2, StartTime = DateTime.UtcNow.AddHours(hours), EndTime = DateTime.UtcNow.AddHours(hours).AddMinutes(50), BookedCount = 1, IsApproved = true };
        var sub = new UserSubscription { UserId = 1, MentorId = 2, MeetingSessionsIncluded = 3, MeetingSessionsUsed = 1, PaymentStatus = PaymentStatus.Paid, EndDate = DateTime.UtcNow.AddDays(10) };
        var booking = new MentorBooking { MentorAvailability = slot, UserSubscription = sub, Status = MentorStatus.Assigned };
        db.MentorBookings.Add(booking); await db.SaveChangesAsync();
        var room = slot.RoomId;
        var result = await Service(db, Role.Learner).CancelBookingAsync(booking.Id, default);
        Assert.Equal(success, result.IsSuccess);
        Assert.Equal(success ? 0 : 1, sub.MeetingSessionsUsed);
        Assert.Equal(success ? 0 : 1, slot.BookedCount);
        if (success) {
            Assert.NotEqual(room, slot.RoomId);
            Assert.True((await Service(db, Role.Learner).CancelBookingAsync(booking.Id, default)).IsSuccess);
            Assert.Equal(0, sub.MeetingSessionsUsed);
        }
    }

    [Fact]
    public async Task Weekly_registration_requires_three_valid_periods_including_evening()
    {
        await using var db = Context();
        db.Users.Add(new UserAccount { Id = 2, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] });
        await db.SaveChangesAsync();
        var week = MentorScheduleRules.Monday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))).AddDays(7);
        var service = Service(db, Role.Admin);
        var periods = new List<WeeklyPeriodRequest> { new(week, "Morning"), new(week, "Afternoon") };
        Assert.False((await service.SaveWeekAsync(new(week, periods, 2), default)).IsSuccess);
        periods.Add(new(week.AddDays(1), "Invalid"));
        Assert.False((await service.SaveWeekAsync(new(week, periods, 2), default)).IsSuccess);
        periods[2] = new(week.AddDays(1), "Evening");
        Assert.True((await service.SaveWeekAsync(new(week, periods, 2), default)).IsSuccess);
        Assert.Equal(3, await db.MentorWeeklyAvailabilities.CountAsync());
    }
}
