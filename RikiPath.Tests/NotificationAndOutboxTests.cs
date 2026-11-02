using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application;
using RikiPath.Application.Common;
using RikiPath.Application.Hubs;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses.Notifications;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;
using RikiPath.Infrastructure.Services;
using Xunit;

namespace RikiPath.Tests;

// Bộ kiểm thử hệ thống Thông báo Thời gian thực và Điều phối Outbox (Stage 13 Post-Workstream A)
public sealed class NotificationAndOutboxTests
{
    private static AppDbContext CreateInMemoryDb(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IClaimService MockClaims(Role role, int userId)
    {
        var mock = new Mock<IClaimService>();
        mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });
        return mock.Object;
    }

    private static IServiceScopeFactory CreateScopeFactory(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimService>(_ => MockClaims(Role.Learner, 101));
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped(_ => NullLogger<MentorOutboxDispatcherService>.Instance);
        var sp = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(() => sp.CreateScope());
        return scopeFactoryMock.Object;
    }

    private static (IUnitOfWork uow, NotificationService service, Mock<IHubClients> hubClientsMock) CreateNotificationService(
        AppDbContext db, int userId = 101, Role role = Role.Learner)
    {
        var uow = new UnitOfWork(db);
        var claims = MockClaims(role, userId);

        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubClientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        hubContextMock.Setup(h => h.Clients).Returns(hubClientsMock.Object);

        var service = new NotificationService(uow, claims, hubContextMock.Object);
        return (uow, service, hubClientsMock);
    }

    [Fact]
    public async Task SendNotificationToUserAsync_PersistsNotification_AndBroadcastsRealtime()
    {
        using var db = CreateInMemoryDb();
        var (_, service, hubClientsMock) = CreateNotificationService(db, userId: 101);

        var result = await service.SendNotificationToUserAsync(
            targetUserId: 101,
            title: "Lịch tư vấn mới",
            message: "Bạn có lịch hẹn tư vấn mới #10.",
            type: "MentorBooking",
            linkUrl: "/mentor/bookings/10",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result);
        Assert.Equal("Lịch tư vấn mới", result.Result.Title);
        Assert.Equal("/mentor/bookings/10", result.Result.LinkUrl);
        Assert.False(result.Result.IsRead);

        var persisted = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == 101);
        Assert.NotNull(persisted);
        Assert.Equal("MentorBooking", persisted.Type);

        hubClientsMock.Verify(c => c.Group("user_101"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesState_AndRejectsUnauthorizedUser()
    {
        using var db = CreateInMemoryDb();
        var notif = new Notification
        {
            UserId = 101,
            Title = "Thông báo test",
            Message = "Nội dung",
            Type = "System",
            IsRead = false,
            CreatedDate = DateTime.UtcNow
        };
        db.Notifications.Add(notif);
        await db.SaveChangesAsync();

        // 1. Thử cập nhật với user khác (userId = 999) -> Forbidden
        var (_, otherUserService, _) = CreateNotificationService(db, userId: 999);
        var forbiddenRes = await otherUserService.MarkAsReadAsync(notif.Id, CancellationToken.None);
        Assert.False(forbiddenRes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // 2. Cập nhật với chính chủ (userId = 101) -> Success
        var (_, ownerService, _) = CreateNotificationService(db, userId: 101);
        var okRes = await ownerService.MarkAsReadAsync(notif.Id, CancellationToken.None);
        Assert.True(okRes.IsSuccess);

        var updated = await db.Notifications.FindAsync(notif.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_UpdatesAllUnreadNotifications()
    {
        using var db = CreateInMemoryDb();
        db.Notifications.AddRange(
            new Notification { UserId = 101, Title = "T1", Message = "M1", Type = "System", IsRead = false },
            new Notification { UserId = 101, Title = "T2", Message = "M2", Type = "System", IsRead = false },
            new Notification { UserId = 102, Title = "T3", Message = "M3", Type = "System", IsRead = false }
        );
        await db.SaveChangesAsync();

        var (_, service, _) = CreateNotificationService(db, userId: 101);
        var res = await service.MarkAllAsReadAsync(CancellationToken.None);
        Assert.True(res.IsSuccess);

        var count101 = await db.Notifications.CountAsync(n => n.UserId == 101 && !n.IsRead);
        var count102 = await db.Notifications.CountAsync(n => n.UserId == 102 && !n.IsRead);

        Assert.Equal(0, count101);
        Assert.Equal(1, count102); // User 102 không bị ảnh hưởng
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchesBookingCreated_ToLearnerAndMentor()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName);
        var nowUtc = DateTime.UtcNow;

        var outboxEvt = new MentorOutboxEvent
        {
            EventKey = "booking:created:1001",
            AggregateType = "MentorBooking",
            AggregateId = "1001",
            EventType = "BookingCreated",
            EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new
            {
                BookingId = 1001,
                MentorId = 201,
                LearnerId = 101,
                SlotId = 55,
                ScheduledAtUtc = nowUtc.AddDays(2),
                EndTimeUtc = nowUtc.AddDays(2).AddMinutes(90),
                MeetingLink = "https://rikipath.vn/consultation-room/abc",
                TimestampUtc = nowUtc
            }),
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            Attempts = 0
        };
        db.MentorOutboxEvents.Add(outboxEvt);
        await db.SaveChangesAsync();

        var scopeFactory = CreateScopeFactory(dbName);
        var dispatcher = new MentorOutboxDispatcherService(scopeFactory, NullLogger<MentorOutboxDispatcherService>.Instance);
        var count = await dispatcher.ProcessPendingOutboxEventsAsync(CancellationToken.None);

        Assert.Equal(1, count);

        using var verifyDb = CreateInMemoryDb(dbName);
        var updatedEvt = await verifyDb.MentorOutboxEvents.FindAsync(outboxEvt.Id);
        Assert.NotNull(updatedEvt);
        Assert.NotNull(updatedEvt.DeliveredAtUtc);
        Assert.Equal(1, updatedEvt.Attempts);

        var learnerNotif = await verifyDb.Notifications.FirstOrDefaultAsync(n => n.UserId == 101);
        var mentorNotif = await verifyDb.Notifications.FirstOrDefaultAsync(n => n.UserId == 201);

        Assert.NotNull(learnerNotif);
        Assert.Contains("Đặt lịch tư vấn thành công", learnerNotif.Title);
        Assert.Equal("Lịch tư vấn của bạn đã được xác nhận.", learnerNotif.Message);
        Assert.Equal("/mentor/bookings/1001", learnerNotif.LinkUrl);

        Assert.NotNull(mentorNotif);
        Assert.Contains("Lịch tư vấn mới", mentorNotif.Title);
        Assert.Equal("Bạn có lịch tư vấn mới từ học viên.", mentorNotif.Message);
        Assert.Equal("/mentor/meetings/1001", mentorNotif.LinkUrl);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchesBookingCancelled_WithRefundNotice()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName);
        var nowUtc = DateTime.UtcNow;

        var outboxEvt = new MentorOutboxEvent
        {
            EventKey = "booking:cancelled:2001",
            AggregateType = "MentorBooking",
            AggregateId = "2001",
            EventType = "BookingCancelled",
            EventVersion = 1,
            PayloadJson = JsonSerializer.Serialize(new
            {
                BookingId = 2001,
                LearnerId = 101,
                MentorId = 201,
                CancelledBy = "Learner",
                Reason = "Bận việc cá nhân",
                IsLate = false,
                IsRefunded = true,
                TimestampUtc = nowUtc
            }),
            CreatedAtUtc = nowUtc,
            AvailableAtUtc = nowUtc,
            Attempts = 0
        };
        db.MentorOutboxEvents.Add(outboxEvt);
        await db.SaveChangesAsync();

        var scopeFactory = CreateScopeFactory(dbName);
        var dispatcher = new MentorOutboxDispatcherService(scopeFactory, NullLogger<MentorOutboxDispatcherService>.Instance);
        var count = await dispatcher.ProcessPendingOutboxEventsAsync(CancellationToken.None);

        Assert.Equal(1, count);

        using var verifyDb = CreateInMemoryDb(dbName);
        var learnerNotif = await verifyDb.Notifications.FirstOrDefaultAsync(n => n.UserId == 101);
        Assert.NotNull(learnerNotif);
        Assert.Contains("Xác nhận hủy lịch tư vấn", learnerNotif.Title);
        Assert.Contains("vé tư vấn đã được hoàn", learnerNotif.Message);

        var mentorNotif = await verifyDb.Notifications.FirstOrDefaultAsync(n => n.UserId == 201);
        Assert.NotNull(mentorNotif);
        Assert.Contains("Lịch tư vấn đã bị hủy", mentorNotif.Title);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchesReviewAndDisputeEvents()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName);
        var nowUtc = DateTime.UtcNow;

        db.MentorOutboxEvents.AddRange(
            new MentorOutboxEvent
            {
                EventKey = "review:1",
                AggregateType = "MentorReview",
                AggregateId = "3001",
                EventType = "ReviewSubmitted",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { BookingId = 3001, MentorId = 201, Rating = 5 }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            },
            new MentorOutboxEvent
            {
                EventKey = "dispute:1",
                AggregateType = "MentorDispute",
                AggregateId = "4001",
                EventType = "DisputeSubmitted",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { BookingId = 3001, MentorId = 201, LearnerId = 101 }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            },
            new MentorOutboxEvent
            {
                EventKey = "dispute:resolved:1",
                AggregateType = "MentorDispute",
                AggregateId = "4001",
                EventType = "DisputeResolved",
                EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { DisputeId = 4001, BookingId = 3001, LearnerId = 101, MentorId = 201 }),
                CreatedAtUtc = nowUtc,
                AvailableAtUtc = nowUtc
            }
        );
        await db.SaveChangesAsync();

        var scopeFactory = CreateScopeFactory(dbName);
        var dispatcher = new MentorOutboxDispatcherService(scopeFactory, NullLogger<MentorOutboxDispatcherService>.Instance);
        var count = await dispatcher.ProcessPendingOutboxEventsAsync(CancellationToken.None);

        Assert.Equal(3, count);

        using var verifyDb = CreateInMemoryDb(dbName);
        var allDelivered = await verifyDb.MentorOutboxEvents.AllAsync(e => e.DeliveredAtUtc != null);
        Assert.True(allDelivered);

        var mentorNotifs = await verifyDb.Notifications.Where(n => n.UserId == 201).ToListAsync();
        Assert.Contains(mentorNotifs, n => n.Title.Contains("Đánh giá mới"));
        Assert.Contains(mentorNotifs, n => n.Title.Contains("Khiếu nại về buổi tư vấn"));
    }

    [Fact]
    public async Task MentorBookingService_EmitsOutboxEvents_OnBookAndRelease()
    {
        using var db = CreateInMemoryDb();
        var nowUtc = DateTime.UtcNow;

        var mentor = new UserAccount
        {
            Id = 201,
            FirstName = "Yamada",
            LastName = "Kenji",
            Email = "mentor201@rikipath.vn",
            Role = Role.Mentor,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(mentor);

        var profile = new MentorProfile
        {
            MentorId = 201,
            Bio = "10 năm kinh nghiệm",
            Expertise = "JLPT N1",
            JlptLevel = "N1",
            VerificationStatus = MentorVerificationStatus.Approved
        };
        db.MentorProfiles.Add(profile);

        var slot = new MentorAvailability
        {
            Id = 801,
            MentorId = 201,
            Mentor = mentor,
            StartTime = nowUtc.AddHours(48),
            EndTime = nowUtc.AddHours(49.5),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = true,
            RoomId = Guid.NewGuid(),
            PolicyVersion = 3
        };
        db.MentorAvailabilities.Add(slot);

        var learner = new UserAccount
        {
            Id = 101,
            FirstName = "Minh",
            LastName = "Tran",
            Email = "minh101@learner.vn",
            Role = Role.Learner,
            PasswordHash = [],
            PasswordSalt = []
        };
        db.Users.Add(learner);

        var sub = new UserSubscription
        {
            Id = 501,
            UserId = 101,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 3,
            MeetingSessionsUsed = 0,
            PurchasedAt = nowUtc,
            StartDate = nowUtc.AddDays(-1),
            EndDate = nowUtc.AddDays(30)
        };
        db.UserSubscriptions.Add(sub);

        var grant = new MentorCreditGrant
        {
            Id = 701,
            LearnerId = 101,
            UserSubscriptionId = 501,
            InitialQuantity = 3,
            AvailableQuantity = 3,
            ExpiresAtUtc = nowUtc.AddDays(30),
            CreatedAtUtc = nowUtc
        };
        db.MentorCreditGrants.Add(grant);

        var entry = new MentorCreditEntry
        {
            CreditGrantId = grant.Id,
            LearnerId = 101,
            EntryType = CreditEntryType.Grant,
            Amount = 3,
            OperationKey = "grant:sub:501",
            Reason = "Cấp lượt ban đầu",
            CreatedAtUtc = nowUtc
        };
        db.MentorCreditEntries.Add(entry);
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var claims = MockClaims(Role.Learner, 101);
        var ledgerService = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        var bookingService = new MentorBookingService(
            db,
            uow,
            claims,
            ledgerService,
            new AppSettings { FrontendBaseUrl = "https://rikipath.vn" },
            NullLogger<MentorBookingService>.Instance);

        // 1. Đặt lịch -> kiểm tra sinh BookingCreated outbox event
        var bookRes = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot.Id,
            Question = "Hỏi về JLPT N2",
            OperationKey = "test:booking:outbox:1"
        });

        Assert.True(bookRes.IsSuccess);
        var bookingId = bookRes.Result!.Id;

        var createdOutbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.EventType == "BookingCreated" && e.AggregateId == bookingId.ToString());
        Assert.NotNull(createdOutbox);
        Assert.Contains(bookingId.ToString(), createdOutbox.PayloadJson);

        // 2. Hủy / Giải phóng giữ chỗ -> kiểm tra sinh BookingReleased outbox event
        var releaseRes = await bookingService.ReleaseBookingHoldAsync(bookingId, "Đổi kế hoạch học tập");
        Assert.True(releaseRes.IsSuccess);

        var releasedOutbox = await db.MentorOutboxEvents.FirstOrDefaultAsync(e => e.EventType == "BookingReleased" && e.AggregateId == bookingId.ToString());
        Assert.NotNull(releasedOutbox);
        using var doc = JsonDocument.Parse(releasedOutbox.PayloadJson);
        Assert.Equal(bookingId, doc.RootElement.GetProperty("BookingId").GetInt32());
        Assert.Equal("Đổi kế hoạch học tập", doc.RootElement.GetProperty("Reason").GetString());
    }
    [Fact]
    public async Task MentorVerificationEvents_NotifyOnlyIntendedAdminsAndMentor()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateInMemoryDb(dbName);
        var nowUtc = DateTime.UtcNow;
        db.MentorOutboxEvents.AddRange(
            new MentorOutboxEvent
            {
                EventKey = "mentor:submit:301", AggregateType = "MentorProfile", AggregateId = "301",
                EventType = "MentorVerificationSubmitted", EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { MentorId = 301, AdminIds = new[] { 401, 402 } }),
                CreatedAtUtc = nowUtc, AvailableAtUtc = nowUtc
            },
            new MentorOutboxEvent
            {
                EventKey = "mentor:review:301", AggregateType = "MentorProfile", AggregateId = "301",
                EventType = "MentorVerificationReviewed", EventVersion = 1,
                PayloadJson = JsonSerializer.Serialize(new { MentorId = 301, AdminId = 401, AdminIds = new[] { 402 }, Decision = "Approve" }),
                CreatedAtUtc = nowUtc, AvailableAtUtc = nowUtc
            });
        await db.SaveChangesAsync();

        var dispatcher = new MentorOutboxDispatcherService(CreateScopeFactory(dbName),
            NullLogger<MentorOutboxDispatcherService>.Instance);
        Assert.Equal(2, await dispatcher.ProcessPendingOutboxEventsAsync());

        using var verified = CreateInMemoryDb(dbName);
        Assert.Equal(1, await verified.Notifications.CountAsync(n => n.UserId == 401));
        Assert.Equal(2, await verified.Notifications.CountAsync(n => n.UserId == 402));
        var mentorNotification = Assert.Single(await verified.Notifications.Where(n => n.UserId == 301).ToListAsync());
        Assert.Equal("MentorVerificationResult", mentorNotification.Type);
        Assert.Equal("/mentor/profile", mentorNotification.LinkUrl);
        Assert.Equal(0, await verified.Notifications.CountAsync(n => n.UserId == 403));
        Assert.Equal(0, await dispatcher.ProcessPendingOutboxEventsAsync());
    }
}
