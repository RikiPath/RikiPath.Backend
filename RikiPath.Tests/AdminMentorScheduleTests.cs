using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
namespace RikiPath.Tests;
public sealed class AdminMentorScheduleTests
{
    private static MentorAvailabilityReviewService Service(AppDbContext db, Role role = Role.Admin)
    {
        var claims = new Mock<IClaimService>(); claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = 9, Role = role });
        return new(new UnitOfWork(db), claims.Object);
    }
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [Fact]
    public async Task Actual_schedule_is_scoped_to_mentor_and_range_with_learner_and_question()
    {
        await using var db = Context();
        var mentor = new UserAccount { Id = 2, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        var other = new UserAccount { Id = 3, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 1, Role = Role.Learner, FirstName = "Actual", LastName = "Learner", PasswordHash = [], PasswordSalt = [] };
        var start = new DateTime(2030, 1, 7, 1, 0, 0, DateTimeKind.Utc);
        var slot = new MentorAvailability { Mentor = mentor, StartTime = start, EndTime = start.AddMinutes(50), IsApproved = true, BookedCount = 1 };
        var booking = new MentorBooking { Status = MentorStatus.Assigned, MentorAvailability = slot,
            Question = "Actual saved question", UserSubscription = new UserSubscription { UserAccount = learner, Mentor = mentor,
                SubscriptionPlan = new SubscriptionPlan { Name = "One session" }, PaymentStatus = PaymentStatus.Paid } };
        db.MentorBookings.Add(booking);
        db.MentorAvailabilities.Add(new() { Mentor = other, StartTime = start, EndTime = start.AddMinutes(50), IsApproved = true });
        db.MentorAvailabilities.Add(new() { Mentor = mentor, StartTime = start.AddDays(7), EndTime = start.AddDays(7).AddMinutes(50), IsApproved = true });
        await db.SaveChangesAsync();
        var response = await Service(db).GetScheduleAsync(2, start, start.AddDays(7), default);
        Assert.True(response.IsSuccess);
        var actual = Assert.Single(response.Result!); Assert.Equal(slot.Id, actual.Id); Assert.Equal("Booked", actual.Status);
        var saved = Assert.Single(actual.Bookings); Assert.Equal("Actual Learner", saved.LearnerName); Assert.Equal("Actual saved question", saved.Question);
        Assert.Empty((await Service(db).GetScheduleAsync(2, start.AddDays(14), start.AddDays(21), default)).Result!);
        Assert.Equal(403, (await Service(db, Role.Mentor).GetScheduleAsync(2, start, start.AddDays(7), default)).StatusCode);
        Assert.Equal(400, (await Service(db).GetScheduleAsync(2, start, start.AddDays(32), default)).StatusCode);
    }
    [Fact]
    public async Task Cancelled_and_draft_slots_are_not_reported_as_free_published_slots()
    {
        await using var db = Context();
        var mentor = new UserAccount { Id = 2, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        var start = DateTime.UtcNow.AddDays(1);
        db.MentorAvailabilities.AddRange(new MentorAvailability { Mentor = mentor, StartTime = start, EndTime = start.AddMinutes(50) },
            new MentorAvailability { Mentor = mentor, StartTime = start.AddHours(1), EndTime = start.AddHours(1).AddMinutes(50), IsDeleted = true, IsApproved = true });
        await db.SaveChangesAsync();
        var response = await Service(db).GetScheduleAsync(2, start, start.AddDays(7), default);
        Assert.Equal(new[] { "Draft", "Cancelled" }, response.Result!.Select(s => s.Status));
    }
}
