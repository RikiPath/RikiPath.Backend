using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;

namespace RikiPath.Tests;

public sealed class CourseRetakePolicyTests
{
    public static StudyCourse Eligible()
    {
        var c = new StudyCourse { Status = "Completed", PaymentStatus = "Paid", TotalSessions = 10,
            StartsAt = DateTime.UtcNow.AddMonths(-3), EndsAt = DateTime.UtcNow.AddDays(-1) };
        for (var i = 1; i <= 10; i++)
        {
            c.Sessions.Add(new() { Number = i, Status = i <= 8 ? "Present" : "Absent" });
            c.Homework.Add(new() { Id = i, Required = true, Passed = i <= 9, DueAt = DateTime.UtcNow.AddDays(-2), SubmittedAt = DateTime.UtcNow.AddDays(-3) });
        }
        return c;
    }
    [Fact] public void Exactly_90_percent_homework_and_20_percent_absence_pass() => Assert.True(CourseRetakePolicy.Evaluate(Eligible(), false).CanClaim);
    [Fact] public void Below_90_percent_or_above_20_percent_fails()
    {
        var c = Eligible(); c.Homework[0].Passed = false;
        Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
        c = Eligible(); c.Sessions[0].Status = "ExcusedAbsent";
        Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
    }
    [Fact] public void Late_passing_homework_does_not_count_but_delayed_grading_does()
    {
        var c = Eligible(); c.Homework[0].ReviewedAt = DateTime.UtcNow;
        Assert.True(CourseRetakePolicy.Evaluate(c, false).CanClaim);
        c.Homework[0].SubmittedAt = c.Homework[0].DueAt.AddSeconds(1);
        Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
    }
    [Fact] public void Mentor_cancelled_is_excluded_and_makeup_is_counted_once()
    {
        var c = Eligible(); c.Sessions[9].Status = "CancelledByMentor";
        var result = CourseRetakePolicy.Evaluate(c, false);
        Assert.Equal(9, result.CountedSessions); Assert.Equal(1, result.AbsentSessions);
        c.Sessions.Add(new() { Number = 9, Attempt = 2, Status = "Present" });
        result = CourseRetakePolicy.Evaluate(c, false);
        Assert.Equal(9, result.CountedSessions); Assert.Equal(9, result.AttendedSessions); Assert.Equal(0, result.AbsentSessions);
        Assert.True(result.CanClaim);
    }
    [Fact] public void No_rounding_up_of_eligibility()
    {
        var c = Eligible(); c.Sessions.RemoveAt(0); c.TotalSessions = 9;
        Assert.False(CourseRetakePolicy.Evaluate(c, false).AttendanceMet); // 2/9 >20%
    }
    [Fact] public void Retake_cannot_chain_or_be_issued_twice()
    {
        var c = Eligible(); Assert.False(CourseRetakePolicy.Evaluate(c, true).CanClaim);
        c.RetakeOfId = 1; Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
    }
    [Fact] public void Missing_facts_or_open_sessions_never_qualify()
    {
        var c = Eligible(); c.Homework.Clear(); Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
        c = Eligible(); c.Sessions[9].Status = "Scheduled"; Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
        c = Eligible(); c.Sessions.Clear(); Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
    }
}

public sealed class StudyCourseServiceTests : IDisposable
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IUnitOfWork> uow = new();
    private readonly Mock<IStudyCourseRepository> repo = new();
    private readonly Mock<IClaimService> claims = new();
    private readonly Mock<IPaymentGatewayClient> gateway = new();
    private readonly Mock<IFileStorageService> storage = new();
    private ClaimDTO actor = new() { Id = 7, Role = Role.Admin };
    public StudyCourseServiceTests()
    {
        uow.SetupGet(x => x.StudyCourses).Returns(repo.Object);
        repo.Setup(x => x.Query()).Returns(() => db.Set<StudyCourse>());
        repo.Setup(x => x.LockAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns((int id, CancellationToken ct) => db.Set<StudyCourse>().SingleOrDefaultAsync(x => x.Id == id, ct));
        repo.Setup(x => x.AddAsync(It.IsAny<StudyCourse>(), It.IsAny<CancellationToken>())).ReturnsAsync((StudyCourse c, CancellationToken _) => { db.Add(c); return c; });
        uow.SetupGet(x => x.StudyCoursePlans).Returns(new GenericRepository<StudyCoursePlan>(db));
        uow.SetupGet(x => x.CertificateLevels).Returns(new CertificateLevelRepository(db));
        uow.SetupGet(x => x.CertificateTypes).Returns(new CertificateTypeRepository(db));
        uow.SetupGet(x => x.MentorAvailabilities).Returns(new MentorAvailabilityRepository(db));
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => db.SaveChangesAsync(ct));
        uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> fn, CancellationToken ct) => fn(ct));
        claims.Setup(x => x.GetUserClaim()).Returns(() => actor);
    }
    private StudyCourseService Service() => new(uow.Object, claims.Object, gateway.Object, storage.Object, new AppSettings { FrontendBaseUrl = "https://test.invalid" });
    private async Task<StudyCourse> Seed()
    {
        var c = CourseRetakePolicyTests.Eligible(); c.LearnerId = 10; c.MentorId = 20; c.Name = "Original";
        c.CertificateLevel = new() { Id = 81, Code = "N5", CertificateType = new() { Id = 80, Name = "JLPT" } };
        c.Learner = new() { Id = 10, Role = Role.Learner, PasswordHash = [], PasswordSalt = [] };
        c.Mentor = new() { Id = 20, Role = Role.Mentor, PasswordHash = [], PasswordSalt = [] };
        c.Plan = new() { Name = "Original", CertificateLevel = c.CertificateLevel, DurationMonths = 3, SessionsPerWeek = 3, SessionMinutes = 60, TotalSessions = 10, Price = 100, IsPublished = true };
        c.PurchaseKey = Guid.NewGuid(); c.SessionMinutes = 60; c.DurationMonths = 3; c.SessionsPerWeek = 3; c.Amount = 100;
        db.Add(c); await db.SaveChangesAsync(); return c;
    }
    [Fact] public async Task Other_learners_cannot_read_or_change_a_course()
    {
        var c = await Seed(); actor = new() { Id = 99, Role = Role.Learner };
        Assert.Equal(403, (await Service().DetailAsync(c.Id, default)).StatusCode);
        Assert.Equal(403, (await Service().SubmitHomeworkAsync(c.Id, c.Homework[0].Id, new("text"), default)).StatusCode);
    }
    [Fact] public async Task Completed_course_facts_are_locked()
    {
        var c = await Seed(); actor = new() { Id = 20, Role = Role.Mentor };
        Assert.False((await Service().GradeAsync(c.Id, c.Homework[0].Id, new(false, "Changed"), default)).IsSuccess);
        Assert.True(c.Homework[0].Passed);
    }
    [Fact] public async Task Approval_requires_verified_failure_and_grants_only_one_frozen_free_course()
    {
        var c = await Seed(); c.RetakeRequests.Add(new() { Status = "Pending", EvidenceUrl = "private://file" }); await db.SaveChangesAsync();
        var id = c.RetakeRequests.Single().Id;
        Assert.False((await Service().ReviewRetakeAsync(c.Id, id, new(true, false, "Check"), default)).IsSuccess);
        c.Plan.Name = "Changed plan"; c.Plan.Price = 999;
        Assert.True((await Service().ReviewRetakeAsync(c.Id, id, new(true, true, "JLPT verified"), default)).IsSuccess);
        var free = await db.Set<StudyCourse>().SingleAsync(x => x.RetakeOfId == c.Id);
        Assert.Equal("Original", free.Name); Assert.Equal(c.CertificateLevelId, free.CertificateLevelId);
        Assert.Equal(0, free.Amount); Assert.Equal("Paid", free.PaymentStatus); Assert.Equal("Ready", free.Status);
        Assert.False((await Service().ReviewRetakeAsync(c.Id, id, new(true, true, "Again"), default)).IsSuccess);
        Assert.Equal(1, await db.Set<StudyCourse>().CountAsync(x => x.RetakeOfId == c.Id));
    }
    [Fact] public async Task Incomplete_plan_cannot_be_published()
    {
        var result = await Service().SavePlanAsync(null, new("3 months", 1, 3, 3, null, null, null, 100, true), default);
        Assert.False(result.IsSuccess); Assert.Empty(db.Set<StudyCoursePlan>());
    }
    [Fact] public async Task Unverified_or_wrong_amount_payment_never_activates_course()
    {
        var c = await Seed(); c.Status = "PendingPayment"; c.PaymentStatus = "Pending"; c.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10);
        Assert.False((await Service().ConfirmPaymentAsync(c.Id, new(false, true, 700000000 + c.Id, 100, ""), null, default)).IsSuccess);
        Assert.False((await Service().ConfirmPaymentAsync(c.Id, new(true, true, 700000000 + c.Id, 99, ""), null, default)).IsSuccess);
        Assert.Equal("Pending", c.PaymentStatus);
        Assert.True((await Service().ConfirmPaymentAsync(c.Id, new(true, true, 700000000 + c.Id, 100, ""), "ref", default)).IsSuccess);
        Assert.Equal("Ready", c.Status);
    }
    [Fact] public async Task Delayed_paid_webhook_is_not_lost_and_repeated_callback_is_idempotent()
    {
        var c = await Seed(); c.Status = "PendingPayment"; c.PaymentStatus = "Pending"; c.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-10);
        var callback = new PaymentCallbackResult(true, true, 700000000 + c.Id, c.Amount, "");
        Assert.True((await Service().ConfirmPaymentAsync(c.Id, callback, "ref", default)).IsSuccess);
        Assert.True((await Service().ConfirmPaymentAsync(c.Id, callback, "ref", default)).IsSuccess);
        Assert.Equal("Paid", c.PaymentStatus); Assert.Equal("Ready", c.Status);
    }
    [Fact] public async Task Webhook_routes_course_orders_through_verified_course_handler()
    {
        var c = await Seed(); c.Status = "PendingPayment"; c.PaymentStatus = "Pending";
        var webhook = new PayOsWebhookRequest { Data = new() { Reference = "course-ref" } };
        gateway.Setup(x => x.VerifyWebhook(webhook)).Returns(new PaymentCallbackResult(true, true, 700000000 + c.Id, c.Amount, ""));
        var payment = new PaymentService(uow.Object, gateway.Object, new AppSettings(), Mock.Of<IMentorCreditLedgerService>(), Service());
        Assert.True((await payment.HandlePayOsWebhookAsync(webhook, default)).IsSuccess);
        Assert.Equal("Paid", c.PaymentStatus);
    }
    [Fact] public async Task Catalog_changes_can_disable_levels_without_deleting_history()
    {
        var c = await Seed(); var level = c.CertificateLevel;
        Assert.True((await Service().SaveLevelAsync(level.Id, new(level.CertificateTypeId, level.Code, "Hidden", 1, false), default)).IsSuccess);
        Assert.True(level.IsDeleted); Assert.Equal(level.Id, c.CertificateLevelId);
        Assert.False((await Service().SaveLevelAsync(level.Id, new(level.CertificateTypeId, "N1", "Rename", 1, true), default)).IsSuccess);
    }
    [Fact] public async Task Purchase_retry_reuses_order_and_keeps_original_price_after_plan_edit()
    {
        var original = await Seed(); actor = new() { Id = 10, Role = Role.Learner };
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), 100, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));
        var request = new PurchaseCourse(original.PlanId, Guid.NewGuid());
        Assert.True((await Service().PurchaseAsync(request, default)).IsSuccess);
        original.Plan.Price = 999;
        Assert.True((await Service().PurchaseAsync(request, default)).IsSuccess);
        var order = await db.Set<StudyCourse>().SingleAsync(x => x.PurchaseKey == request.IdempotencyKey);
        Assert.Equal(100, order.Amount);
        gateway.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact] public async Task Weekly_cadence_prevents_overbooking_original_sessions()
    {
        var c = await Seed(); c.Status = "Active"; c.Sessions.Clear();
        var start = DateTime.UtcNow.Date.AddDays(7);
        start = start.AddDays(-(((int)start.DayOfWeek + 6) % 7)).AddHours(2);
        c.StartsAt = start.AddDays(-1); c.EndsAt = start.AddMonths(3);
        for (int i = 0; i < 3; i++) c.Sessions.Add(new() { Number = i + 1, Attempt = 1, ScheduledAt = start.AddDays(i), Status = "Scheduled" });
        await db.SaveChangesAsync();
        var result = await Service().ScheduleAsync(c.Id, new(4, start.AddDays(3), null), default);
        Assert.False(result.IsSuccess); Assert.Contains("đủ số buổi", result.ErrorMessage);
    }
    public void Dispose() => db.Dispose();
}
