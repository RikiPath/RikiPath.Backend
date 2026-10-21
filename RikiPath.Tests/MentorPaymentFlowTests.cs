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

namespace RikiPath.Tests;

public sealed class MentorPaymentFlowTests
{
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
        => new(new UnitOfWork(db), gateway.Object, Claims(id, role), Settings, NullLogger<MentorMeetingService>.Instance);
    private static async Task<int> Purchase(AppDbContext db, Mock<IPaymentGatewayClient> gateway)
    {
        var mentor = new UserAccount { Id = 2, FirstName = "Mentor", Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(mentor, new UserAccount { Id = 1, FirstName = "Learner", Role = Role.Learner, PasswordHash = [], PasswordSalt = [] });
        var slot = new MentorAvailability { Mentor = mentor, StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1), IsApproved = true };
        var plan = new SubscriptionPlan { Name = "Test meeting plan", Price = 1000, DurationDays = 30, MeetingSessionCount = 3, IsActive = true };
        db.MentorAvailabilities.Add(slot); db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), 1000, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.payos.vn/test-checkout", "test-payment-link", "test-qr"));
        var result = await Service(db, gateway).PurchaseAsync(new PurchaseMentorPlanRequest
        { SubscriptionPlanId = plan.Id, MentorAvailabilityId = slot.Id, Question = "Câu hỏi gửi tới Mentor" }, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        gateway.Verify(x => x.CreatePaymentLinkAsync(result.Result!.SubscriptionId, 1000, It.IsAny<string>(),
            It.Is<string>(url => url.Contains($"subscriptionId={result.Result.SubscriptionId}")),
            It.Is<string>(url => url.Contains($"subscriptionId={result.Result.SubscriptionId}")), It.IsAny<CancellationToken>()), Times.Once);
        return result.Result!.SubscriptionId;
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
        Assert.Equal(MentorStatus.AwaitingPayment, Assert.Single(pending.Result.Bookings).Status);
        Assert.Empty((await Service(db, gateway, 2, Role.Mentor).GetMyMeetingsAsync(default)).Result!);

        gateway.Setup(x => x.GetPaymentInformationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkInformation(id, 1000, 1000, "PAID", "test-payment-link"));
        var paid = await Service(db, gateway).SyncPurchaseAsync(id, default);
        Assert.Equal(PaymentStatus.Paid, paid.Result!.PaymentStatus);
        db.ChangeTracker.Clear();
        var booking = Assert.Single((await Service(db, gateway, 2, Role.Mentor).GetMyMeetingsAsync(default)).Result!);
        Assert.Equal(MentorStatus.Assigned, booking.Status);
        Assert.Equal("Câu hỏi gửi tới Mentor", booking.Question);
        Assert.Contains("/consultation-room/", booking.MeetingLink);
        Assert.True((await Service(db, gateway).SyncPurchaseAsync(id, default)).IsSuccess);
        Assert.Single(db.MentorBookings);
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
        var service = new PaymentService(new UnitOfWork(db), gateway.Object, Settings);
        Assert.True((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.True((await service.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal(PaymentStatus.Paid, (await db.UserSubscriptions.FindAsync(id))!.PaymentStatus);
        Assert.Equal(MentorStatus.Assigned, Assert.Single(db.MentorBookings).Status);
        Assert.Equal("bank-ref", (await db.UserSubscriptions.FindAsync(id))!.PaymentTransactionId);
    }

    [Fact]
    public async Task Invalid_signature_or_wrong_webhook_amount_does_not_confirm_payment()
    {
        await using var db = Context(); var gateway = new Mock<IPaymentGatewayClient>();
        var id = await Purchase(db, gateway);
        var webhook = new PayOsWebhookRequest();
        var service = new PaymentService(new UnitOfWork(db), gateway.Object, Settings);
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
        Assert.Equal(MentorStatus.Cancelled, Assert.Single(db.MentorBookings).Status);
        Assert.Empty((await Service(db, gateway, 2, Role.Mentor).GetMyMeetingsAsync(default)).Result!);
    }
}
