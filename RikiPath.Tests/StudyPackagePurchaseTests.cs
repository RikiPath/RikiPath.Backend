using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class StudyPackagePurchaseTests
{
    [Fact]
    public async Task Paid_roadmap_order_grants_one_roadmap_use_without_opening_other_features()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Users.Add(new UserAccount { Id = 45, Email = "study@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = SubscriptionAccessService.LearningPathCredit, Name = "Lượt lộ trình", Price = 1000, AiGradingQuota = 1
        });
        await db.SaveChangesAsync();
        var settings = new AppSettings
        {
            PayOs = new PayOsSettings
            {
                WebReturnUrl = "http://localhost:5173/payment/success",
                WebCancelUrl = "http://localhost:5173/payment/cancel",
            }
        };
        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 45, Role = Role.Learner });
        var gateway = new Mock<IPaymentGatewayClient>();
        gateway.Setup(g => g.CreatePaymentLinkAsync(It.IsAny<long>(), 1000, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentLinkResult("https://example.test/pay", "link", ""));
        var uow = new UnitOfWork(db);
        var access = new SubscriptionAccessService(uow, claims.Object, gateway.Object, settings);
        var purchase = await access.PurchaseAsync(SubscriptionAccessService.LearningPathCredit, default);
        Assert.True(purchase.IsSuccess);
        var id = purchase.Result!.PurchaseId;
        gateway.Verify(g => g.CreatePaymentLinkAsync(SubscriptionAccessService.OrderPrefix + id, 1000,
            It.IsAny<string>(), $"http://localhost:5173/payment/success?purchaseId={id}",
            $"http://localhost:5173/payment/cancel?purchaseId={id}", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, (await access.GetAccessAsync(default)).LearningPathUsesRemaining);
        var payment = new PaymentService(uow, gateway.Object, settings, Mock.Of<IMentorCreditLedgerService>());
        var webhook = new PayOsWebhookRequest { Data = new PayOsWebhookData { PaymentLinkId = "link" } };
        gateway.Setup(g => g.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(true, true,
            SubscriptionAccessService.OrderPrefix + id, 999, ""));
        Assert.False((await payment.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal(0, (await access.GetAccessAsync(default)).LearningPathUsesRemaining);
        gateway.Setup(g => g.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(true, true,
            SubscriptionAccessService.OrderPrefix + id, 1000, ""));
        Assert.True((await payment.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);

        var paid = await access.GetAccessAsync(default);
        Assert.Equal(1, paid.LearningPathUsesRemaining);
        Assert.False(paid.Active);
        Assert.False(paid.Speaking);
        Assert.False(paid.Writing);
        Assert.Equal(0, paid.MentorSessionsRemaining);
        Assert.True((await access.UseAiAsync("learning_path", () => Task.FromResult(ApiResponse<string>.Success("ok")), default)).IsSuccess);
        Assert.True((await payment.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal(0, (await access.GetAccessAsync(default)).LearningPathUsesRemaining);
        var blocked = await access.UseAiAsync<string>("learning_path", () => throw new InvalidOperationException(), default);
        Assert.Equal("AI_CREDIT_REQUIRED", blocked.ErrorCode);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(30);
        db.SubscriptionPlans.AddRange(Enumerable.Range(1, 3)
            .Select(id => new SubscriptionPlan { Id = id, Name = $"Gói tự học {id}" }));
        db.UserSubscriptions.AddRange(
            new UserSubscription { UserId = 45, SubscriptionPlanId = 1, PaymentStatus = PaymentStatus.Paid,
                EndDate = expiresAt, AiGradingQuota = 5, AiGradingUsedCount = 2 },
            new UserSubscription { UserId = 45, SubscriptionPlanId = 2, PaymentStatus = PaymentStatus.Paid,
                EndDate = now.AddHours(1), AiGradingQuota = 1 },
            new UserSubscription { UserId = 45, SubscriptionPlanId = 3, PaymentStatus = PaymentStatus.Paid,
                EndDate = now.AddDays(-1), AiGradingQuota = 10 });
        await db.SaveChangesAsync();
        var study = await access.GetAccessAsync(default);
        Assert.True(study.Active);
        Assert.Equal(expiresAt, study.SelfStudyExpiresAt);
        Assert.Equal(2, study.SelfStudyDaysRemaining);
        Assert.Equal(4, study.AiGradingUsesRemaining);
    }

    [Theory]
    [InlineData("writing")]
    [InlineData("speaking")]
    public async Task Grading_uses_one_learner_credit_and_mentor_can_grade_without_a_plan(string feature)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.Users.Add(new UserAccount { Id = 45, Email = "learner@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = SubscriptionAccessService.GradingCredit, Name = "Lượt chấm", AiGradingQuota = 1
        });
        var order = new UserSubscription
        {
            UserId = 45, SubscriptionPlanId = SubscriptionAccessService.GradingCredit,
            PaymentStatus = PaymentStatus.Paid, AiGradingQuota = 1
        };
        db.UserSubscriptions.Add(order);
        await db.SaveChangesAsync();

        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 45, Role = Role.Learner });
        var access = new SubscriptionAccessService(new UnitOfWork(db), claims.Object,
            Mock.Of<IPaymentGatewayClient>(), new AppSettings());
        Task<ApiResponse<string>> Grade() => Task.FromResult(ApiResponse<string>.Success("graded"));

        Assert.True((await access.UseAiAsync(feature, Grade, default)).IsSuccess);
        Assert.Equal(1, order.AiGradingUsedCount);
        var rejected = await access.UseAiAsync<string>(feature,
            () => throw new InvalidOperationException("AI must not be called without a credit"), default);
        Assert.Equal("AI_CREDIT_REQUIRED", rejected.ErrorCode);

        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 46, Role = Role.Mentor });
        Assert.True((await access.UseAiAsync(feature, Grade, default)).IsSuccess);
        Assert.Equal(1, order.AiGradingUsedCount);
    }
}
