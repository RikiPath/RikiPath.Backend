using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
namespace RikiPath.Tests;

public sealed class MentorConversationTests
{
    [Theory]
    [InlineData(Role.Learner, 1)] [InlineData(Role.Mentor, 2)]
    public async Task Multiple_bookings_keep_one_conversation_and_all_history_even_after_a_meeting_ends(Role role, int id)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var learner = new UserAccount { Id = 1, Role = Role.Learner, FirstName = "Learner", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 2, Role = Role.Mentor, FirstName = "Mentor", PasswordHash = [], PasswordSalt = [] };
        var plan = new SubscriptionPlan { Name = "Three sessions", MeetingSessionCount = 3 };
        var old = new MentorBooking { Id = 10, Status = MentorStatus.Cancelled,
            UserSubscription = new UserSubscription { UserAccount = learner, Mentor = mentor, SubscriptionPlan = plan,
                MeetingSessionsIncluded = 1, PaymentStatus = PaymentStatus.Paid, StartDate = DateTime.UtcNow.AddDays(-20), EndDate = DateTime.UtcNow.AddDays(-6) },
            MentorAvailability = new MentorAvailability { Mentor = mentor, StartTime = DateTime.UtcNow.AddDays(-7), EndTime = DateTime.UtcNow.AddDays(-7).AddMinutes(50) } };
        var recent = new MentorBooking { Id = 20, Status = MentorStatus.Completed,
            UserSubscription = new UserSubscription { UserAccount = learner, Mentor = mentor, SubscriptionPlan = plan,
                MeetingSessionsIncluded = 3, PaymentStatus = PaymentStatus.Paid, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(34) },
            MentorAvailability = new MentorAvailability { Mentor = mentor, StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(-1) } };
        db.MentorBookings.AddRange(old, recent);
        db.MentorChatMessages.AddRange(new MentorChatMessage { MentorBooking = old, Sender = learner, Text = "Earlier package", SentAt = DateTime.UtcNow.AddDays(-8) },
            new MentorChatMessage { MentorBooking = recent, Sender = mentor, Text = "Recent package", SentAt = DateTime.UtcNow.AddMinutes(-10) });
        await db.SaveChangesAsync();
        var claims = new Mock<IClaimService>(); claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role });
        var service = new MentorChatService(new UnitOfWork(db), claims.Object);
        var threads = await service.GetThreadsAsync(default);
        var thread = Assert.Single(threads.Result!);
        Assert.Equal(10, thread.BookingId);
        Assert.True(thread.CanSendMessages);
        Assert.True(thread.EndsAt > DateTime.UtcNow);
        Assert.Equal(10, (await service.GetThreadAsync(20, default)).Result!.BookingId);
        var history = (await service.GetMessagesAsync(20, null, 50, default)).Result!;
        Assert.Equal(2, history.Count); Assert.All(history, m => Assert.Equal(10, m.BookingId));
        var sent = await service.SendAsync(20, "After meeting, within package", default);
        Assert.True(sent.IsSuccess); Assert.Equal(10, sent.Result!.BookingId);
        Assert.Equal(3, await db.MentorChatMessages.CountAsync());
    }
}
