using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

public sealed class MentorPaymentFlowTests
{
    [Fact]
    public async Task Short_consultation_cannot_overlap_a_long_course_session()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        var subscription = await db.UserSubscriptions.FindAsync(id); subscription!.PaymentStatus = PaymentStatus.Paid;
        var slot = await db.MentorAvailabilities.SingleAsync();
        db.StudyCourses.Add(new StudyCourse { Name = "Long course QA", LearnerId = 1, MentorId = 2, Status = "Active", PaymentStatus = "Paid",
            SessionMinutes = 60, Sessions = [new() { Number = 1, ScheduledAt = slot.StartTime, Status = "Scheduled" }] });
        await db.SaveChangesAsync();
        var result = await BookingService(db).BookSlotAsync(new() { MentorAvailabilityId = slot.Id }, default);
        Assert.False(result.IsSuccess); Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, await db.MentorBookings.CountAsync()); Assert.Equal(0, subscription.MeetingSessionsUsed);
    }
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly AppSettings Settings = new()
    {
        FrontendBaseUrl = "http://localhost:5173",
        PayOs = new PayOsSettings { WebReturnUrl = "http://localhost:5173/payment/success", WebCancelUrl = "http://localhost:5173/payment/cancel" }
    };
    private static IClaimService Claims(int id, Role role)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role });
        return claims.Object;
    }
    private static MentorMeetingService Service(AppDbContext db, Mock<IPaymentGatewayClient> gateway, int id = 1, Role role = Role.Learner)
    {
        var uow = new UnitOfWork(db);
        var claims = Claims(id, role);
        var ledger = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        return new MentorMeetingService(uow, gateway.Object, claims, Settings, NullLogger<MentorMeetingService>.Instance, ledger);
    }
    private static MentorBookingService BookingService(AppDbContext db, int id = 1, Role role = Role.Learner)
    {
        var uow = new UnitOfWork(db);
        var claims = Claims(id, role);
        var ledger = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        return new MentorBookingService(db, uow, claims, ledger, Settings, NullLogger<MentorBookingService>.Instance);
    }
    private static async Task<int> Purchase(AppDbContext db, Mock<IPaymentGatewayClient> gateway, int sessions = 3)
    {
        var mentor = new UserAccount { Id = 2, FirstName = "Mentor", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [],
            MentorProfile = new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved } };
        db.Users.AddRange(mentor, new UserAccount { Id = 1, FirstName = "Learner", Role = Role.Learner, PasswordHash = [], PasswordSalt = [] });
        var slot = new MentorAvailability { Mentor = mentor, StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddMinutes(90), IsApproved = true };
        var plan = new SubscriptionPlan { Name = "Test meeting plan", Price = 1000, DurationDays = 30, MeetingSessionCount = sessions, IsActive = true };
        db.MentorAvailabilities.Add(slot); db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), 1000, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.payos.vn/test-checkout", "test-payment-link", "test-qr"));
        var result = await Service(db, gateway).PurchaseAsync(new PurchaseMentorPlanRequest
        { SubscriptionPlanId = plan.Id }, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        gateway.Verify(x => x.CreatePaymentLinkAsync(result.Result!.SubscriptionId, 1000, It.IsAny<string>(),
            It.Is<string>(url => url.Contains($"subscriptionId={result.Result.SubscriptionId}")),
            It.Is<string>(url => url.Contains($"subscriptionId={result.Result.SubscriptionId}")), It.IsAny<CancellationToken>()), Times.Once);
        return result.Result!.SubscriptionId;
    }

    [Fact]
    public async Task One_session_new_purchase_expires_after_30_days()
    {
        await using var db = Context();
        var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway, sessions: 1);
        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id, 1000, 1000, "PAID", "test-payment-link"));

        var paid = await Service(db, gateway).SyncPurchaseAsync(id, default);

        Assert.True(paid.IsSuccess, paid.ErrorMessage);
        var subscription = (await db.UserSubscriptions.FindAsync(id))!;
        Assert.InRange((subscription.EndDate!.Value - subscription.StartDate!.Value).TotalDays, 29.999, 30.001);
        Assert.Equal(subscription.EndDate, Assert.Single(db.MentorCreditGrants).ExpiresAtUtc);
    }

    [Fact]
    public async Task Real_purchase_stays_pending_until_gateway_confirms_then_appears_in_mentor_queue()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id, 1000, 0, "PENDING", "test-payment-link"));
        var pending = await Service(db, gateway).SyncPurchaseAsync(id, default);
        Assert.Equal(PaymentStatus.Pending, pending.Result!.PaymentStatus);
        Assert.Empty(pending.Result.Bookings);
        Assert.Equal(0, pending.Result.MeetingSessionsUsed);
        Assert.Empty(db.MentorCreditGrants);
        Assert.Equal(0, (await db.MentorAvailabilities.SingleAsync()).BookedCount);
        Assert.Empty((await BookingService(db, 2, Role.Mentor).GetMyMentorBookingsAsync(default)).Result!);

        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id, 1000, 1000, "PAID", "test-payment-link"));
        var paid = await Service(db, gateway).SyncPurchaseAsync(id, default);
        Assert.Equal(PaymentStatus.Paid, paid.Result!.PaymentStatus);
        var paidSubscription = (await db.UserSubscriptions.FindAsync(id))!;
        Assert.InRange((paidSubscription.EndDate!.Value - paidSubscription.StartDate!.Value).TotalDays, 89.999, 90.001);
        Assert.Equal(3, Assert.Single(db.MentorCreditGrants).AvailableQuantity);
        Assert.Equal((await db.UserSubscriptions.FindAsync(id))!.EndDate,
            Assert.Single(db.MentorCreditGrants).ExpiresAtUtc);
        Assert.Empty(paid.Result.Bookings);
        Assert.Equal(3, paid.Result.MeetingSessionsIncluded - paid.Result.MeetingSessionsUsed);
        var booked = await BookingService(db).BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = (await db.MentorAvailabilities.SingleAsync()).Id, Question = "Câu hỏi gửi tới Mentor" }, default);
        Assert.True(booked.IsSuccess, booked.ErrorMessage);
        db.ChangeTracker.Clear();
        var booking = Assert.Single((await BookingService(db, 2, Role.Mentor).GetMyMentorBookingsAsync(default)).Result!);
        Assert.Equal(MentorStatus.Accepted, booking.Status);
        Assert.Equal("Câu hỏi gửi tới Mentor", booking.Question);
        Assert.Contains("/consultation-room/", booking.MeetingLink);
        Assert.True((await Service(db, gateway).SyncPurchaseAsync(id, default)).IsSuccess);
        Assert.Single(db.MentorBookings);
        Assert.Single(db.MentorCreditGrants);
    }

    [Theory]
    [InlineData(1001, "test-payment-link", 0)]
    [InlineData(1000, "wrong-link", 0)]
    [InlineData(1000, "test-payment-link", 1)]
    public async Task Mismatched_payment_information_never_marks_order_paid(int amount, string link, int offset)
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id + offset, amount, amount, "PAID", link));
        Assert.Equal(409, (await Service(db, gateway).SyncPurchaseAsync(id, default)).StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await db.UserSubscriptions.FindAsync(id))!.PaymentStatus);
    }

    [Fact]
    public async Task Other_learner_cannot_sync_or_see_order_and_mentor_cannot_sync()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        Assert.Equal(404, (await Service(db, gateway, 3).SyncPurchaseAsync(id, default)).StatusCode);
        Assert.Equal(404, (await Service(db, gateway, 3).GetPurchaseStatusAsync(id, default)).StatusCode);
        Assert.Equal(403, (await Service(db, gateway, 2, Role.Mentor).SyncPurchaseAsync(id, default)).StatusCode);
        gateway.Verify(x => x.GetPaymentInformationAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Valid_webhook_confirms_booking_and_duplicate_webhook_does_not_duplicate_booking()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        var webhook = new PayOsWebhookRequest { Data = new PayOsWebhookData { Reference = "bank-ref" } };
        gateway.Setup(x => x.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(true, true, id, 1000, ""));
        var uow = new UnitOfWork(db);
        var ledger = new MentorCreditLedgerService(uow, Claims(1, Role.Learner), NullLogger<MentorCreditLedgerService>.Instance);
        var service = new PaymentService(uow, gateway.Object, Settings, ledger);
        Assert.True((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.True((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal(PaymentStatus.Paid, (await db.UserSubscriptions.FindAsync(id))!.PaymentStatus);
        Assert.Empty(db.MentorBookings);
        Assert.Equal(0, (await db.UserSubscriptions.FindAsync(id))!.MeetingSessionsUsed);
        Assert.Equal("bank-ref", (await db.UserSubscriptions.FindAsync(id))!.PaymentTransactionId);
        Assert.Equal(3, Assert.Single(db.MentorCreditGrants).AvailableQuantity);
        Assert.Single(db.MentorCreditEntries);
        Assert.Equal((await db.UserSubscriptions.FindAsync(id))!.EndDate,
            Assert.Single(db.MentorCreditGrants).ExpiresAtUtc);
    }

    [Fact]
    public async Task Invalid_signature_or_wrong_webhook_amount_does_not_confirm_payment()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        var webhook = new PayOsWebhookRequest();
        var uow = new UnitOfWork(db);
        var ledger = new MentorCreditLedgerService(uow, Claims(1, Role.Learner), NullLogger<MentorCreditLedgerService>.Instance);
        var service = new PaymentService(uow, gateway.Object, Settings, ledger);
        gateway.Setup(x => x.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(false, true, id, 1000, ""));
        Assert.False((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        gateway.Setup(x => x.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(true, true, id, 999, ""));
        Assert.False((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal(PaymentStatus.Pending, (await db.UserSubscriptions.FindAsync(id))!.PaymentStatus);
    }

    [Fact]
    public async Task Cancelled_gateway_order_is_not_added_to_queue()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id, 1000, 0, "CANCELLED", "test-payment-link"));
        var result = await Service(db, gateway).SyncPurchaseAsync(id, default);
        Assert.Equal(PaymentStatus.Failed, result.Result!.PaymentStatus);
        Assert.Empty(db.MentorBookings);
        Assert.Empty((await BookingService(db, 2, Role.Mentor).GetMyMentorBookingsAsync(default)).Result!);
    }

    [Fact]
    public async Task Account_credits_can_book_different_mentors_and_retry_is_idempotent()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        var first = await db.MentorAvailabilities.SingleAsync();
        Assert.False((await BookingService(db).BookSlotAsync(new() { MentorAvailabilityId = first.Id }, default)).IsSuccess);
        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id,1000,1000,"PAID","test-payment-link"));
        await Service(db,gateway).SyncPurchaseAsync(id,default);
        Assert.True((await BookingService(db).BookSlotAsync(new(){MentorAvailabilityId=first.Id},default)).IsSuccess);
        db.ChangeTracker.Clear();
        var other = new UserAccount { Id=3, Role=Role.Mentor, PasswordHash=[], PasswordSalt=[] };
        other.MentorProfile = new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved };
        var second = new MentorAvailability { Mentor=other, IsApproved=true, StartTime=DateTime.UtcNow.AddDays(2), EndTime=DateTime.UtcNow.AddDays(2).AddMinutes(90) };
        db.Users.Add(other); db.MentorAvailabilities.Add(second); await db.SaveChangesAsync();
        var secondBooking = await BookingService(db).BookSlotAsync(new(){MentorAvailabilityId=second.Id},default);
        Assert.True(secondBooking.IsSuccess);
        db.ChangeTracker.Clear();
        var retry = await BookingService(db).BookSlotAsync(new(){MentorAvailabilityId=second.Id},default);
        Assert.True(retry.IsSuccess);
        Assert.Equal(secondBooking.Result!.Id, retry.Result!.Id);
        Assert.Equal(2,(await db.UserSubscriptions.FindAsync(id))!.MeetingSessionsUsed);
        Assert.Null((await db.UserSubscriptions.FindAsync(id))!.MentorId);
        Assert.Equal(2,await db.MentorBookings.CountAsync());
        Assert.Equal(1,(await Service(db,gateway).GetMyPurchasesAsync(default)).Result!.Single().MeetingSessionsIncluded - (await db.UserSubscriptions.FindAsync(id))!.MeetingSessionsUsed);
    }

    [Fact]
    public async Task Booking_spends_earliest_expiring_eligible_package_and_excludes_expired_or_unpaid()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id=await Purchase(db,gateway);
        var plan=await db.SubscriptionPlans.SingleAsync();
        var original=await db.UserSubscriptions.FindAsync(id);
        original!.PaymentStatus=PaymentStatus.Paid; original.EndDate=DateTime.UtcNow.AddDays(35);
        var sooner=new UserSubscription {UserId=1,SubscriptionPlan=plan,PaymentStatus=PaymentStatus.Paid,MeetingSessionsIncluded=1,EndDate=DateTime.UtcNow.AddDays(3)};
        var expired=new UserSubscription {UserId=1,SubscriptionPlan=plan,PaymentStatus=PaymentStatus.Paid,MeetingSessionsIncluded=3,EndDate=DateTime.UtcNow.AddDays(-1)};
        db.UserSubscriptions.AddRange(sooner,expired); await db.SaveChangesAsync();
        Assert.True((await BookingService(db).BookSlotAsync(new(){MentorAvailabilityId=(await db.MentorAvailabilities.SingleAsync()).Id},default)).IsSuccess);
        Assert.Equal(1,sooner.MeetingSessionsUsed); Assert.Equal(0,original.MeetingSessionsUsed); Assert.Equal(0,expired.MeetingSessionsUsed);
        Assert.Empty((await Service(db,gateway,99).GetMyPurchasesAsync(default)).Result!);
    }
}
