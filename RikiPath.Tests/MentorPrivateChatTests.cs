using Moq;
using Microsoft.EntityFrameworkCore;
using RikiPath.Infrastructure;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Tests;

public sealed class MentorPrivateChatTests
{
    private static (MentorChatService Service, Mock<IMentorChatMessageRepository> Messages) Create(
        int userId = 1, Role role = Role.Learner, PaymentStatus payment = PaymentStatus.Paid,
        bool expired = false)
    {
        var learner = new UserAccount { Id = 1, FirstName = "Learner", Role = Role.Learner, PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 2, FirstName = "Mentor", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        var booking = new MentorBooking
        {
            Id = 10, Status = MentorStatus.Assigned,
            UserSubscription = new UserSubscription { UserId = 1, MentorId = 2, PaymentStatus = payment,
                UserAccount = learner, Mentor = mentor, SubscriptionPlan = new SubscriptionPlan { Name = "Chat test", MeetingSessionCount = 1 },
                MeetingSessionsIncluded = 1, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(expired ? -1 : 1) },
            MentorAvailability = new MentorAvailability { Mentor = mentor, MentorId = 2, EndTime = DateTime.UtcNow.AddHours(expired ? -1 : 1) }
        };
        var bookings = new Mock<IMentorBookingRepository>();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.MentorBookings.Add(booking); db.SaveChanges();
        bookings.Setup(x => x.Query()).Returns(db.MentorBookings);
        var subscriptions = new Mock<IUserSubscriptionRepository>();
        subscriptions.Setup(x => x.Query()).Returns(db.UserSubscriptions);
        bookings.Setup(x => x.GetForChatAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        var messages = new Mock<IMentorChatMessageRepository>();
        messages.Setup(x => x.AddAsync(It.IsAny<MentorChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MentorChatMessage m, CancellationToken _) => { m.Id = 42; return m; });
        messages.Setup(x => x.GetWithSenderAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(new MentorChatMessage
        {
            Id = 42, MentorBookingId = 10, SenderUserId = userId, Text = "Xin chào",
            Sender = new UserAccount { FirstName = "Test", Role = role }
        });
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.MentorBookings).Returns(bookings.Object);
        uow.SetupGet(x => x.UserSubscriptions).Returns(subscriptions.Object);
        uow.SetupGet(x => x.MentorChatMessages).Returns(messages.Object);
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
        return (new MentorChatService(uow.Object, claims.Object), messages);
    }

    [Theory]
    [InlineData(1, Role.Learner)]
    [InlineData(2, Role.Mentor)]
    public async Task Participant_sends_and_returns_the_exact_saved_message(int userId, Role role)
    {
        var (service, messages) = Create(userId, role);
        var response = await service.SendAsync(10, "  Xin chào  ", default);
        Assert.True(response.IsSuccess);
        Assert.Equal(42, response.Result!.Id);
        messages.Verify(x => x.AddAsync(It.Is<MentorChatMessage>(m => m.Text == "Xin chào" && m.SenderUserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        messages.Verify(x => x.GetWithSenderAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        messages.Verify(x => x.GetPageAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(3, Role.Learner)]
    [InlineData(3, Role.Mentor)]
    [InlineData(1, Role.Admin)]
    public async Task Outsider_cannot_read_or_send(int userId, Role role)
    {
        var (service, messages) = Create(userId, role);
        Assert.Equal(404, (await service.GetThreadAsync(10, default)).StatusCode);
        Assert.Equal(404, (await service.SendAsync(10, "hello", default)).StatusCode);
        messages.Verify(x => x.AddAsync(It.IsAny<MentorChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unpaid_booking_cannot_be_accessed()
    {
        var (service, _) = Create(payment: PaymentStatus.Pending);
        Assert.Equal(404, (await service.GetThreadAsync(10, default)).StatusCode);
    }

    [Fact]
    public async Task Expired_booking_keeps_history_but_rejects_sending()
    {
        var (service, _) = Create(expired: true);
        var thread = await service.GetThreadAsync(10, default);
        Assert.True(thread.IsSuccess);
        Assert.False(thread.Result!.CanSendMessages);
        Assert.Equal(409, (await service.SendAsync(10, "hello", default)).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public async Task Invalid_text_is_rejected(int length)
    {
        var (service, messages) = Create();
        Assert.False((await service.SendAsync(10, new string('a', length), default)).IsSuccess);
        messages.Verify(x => x.AddAsync(It.IsAny<MentorChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
