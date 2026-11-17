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
            c.Homework.Add(new() { Id = i, Required = true, Passed = i <= 9, DueAt = DateTime.UtcNow.AddDays(-2), SubmittedAt = i <= 9 ? DateTime.UtcNow.AddDays(-3) : DateTime.UtcNow });
        }
        return c;
    }
    [Fact] public void Exactly_90_percent_homework_and_20_percent_absence_pass() => Assert.True(CourseRetakePolicy.Evaluate(Eligible(), false).CanClaim);
    [Fact] public void Below_90_percent_fails_but_absence_does_not_block()
    {
        var c = Eligible(); c.Homework[0].SubmittedAt = c.Homework[0].DueAt.AddSeconds(1);
        Assert.False(CourseRetakePolicy.Evaluate(c, false).CanClaim);
        c = Eligible(); c.Sessions[0].Status = "ExcusedAbsent";
        Assert.True(CourseRetakePolicy.Evaluate(c, false).CanClaim);
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
        Assert.True(CourseRetakePolicy.Evaluate(c, false).AttendanceMet); // Attendance no longer gates retakes.
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
        uow.SetupGet(x => x.StudyCohorts).Returns(new GenericRepository<StudyCohort>(db));
        uow.SetupGet(x => x.CourseScheduleRequests).Returns(new GenericRepository<CourseScheduleRequest>(db));
        uow.SetupGet(x => x.UserAccounts).Returns(new UserAccountRepository(db));
        uow.SetupGet(x => x.MentorBookings).Returns(new MentorBookingRepository(db));
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
        Assert.True((await Service().ReviewRetakeAsync(c.Id, id, new(true, true, "JLPT verified", OnTimeHomeworkEvidenceVerified: true), default)).IsSuccess);
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
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), 100, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));
        var request = new PurchaseCourse(original.PlanId, Guid.NewGuid());
        Assert.True((await Service().PurchaseAsync(request, default)).IsSuccess);
        original.Plan.Price = 999;
        Assert.True((await Service().PurchaseAsync(request, default)).IsSuccess);
        var order = await db.Set<StudyCourse>().SingleAsync(x => x.PurchaseKey == request.IdempotencyKey);
        Assert.Equal(100, order.Amount);
        gateway.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact] public async Task Paid_individual_package_books_once_and_reuses_existing_room()
    {
        var c = await Seed(); c.Status = "Ready"; c.MentorId = null; c.Sessions.Clear();
        actor = new() { Id = c.LearnerId, Role = Role.Learner };
        var start = DateTime.UtcNow.AddDays(10);
        var slot = new MentorAvailability { MentorId = 20, StartTime = start, EndTime = start.AddMinutes(60), IsApproved = true, MaxLearners = 1 };
        db.Add(slot); await db.SaveChangesAsync();
        Assert.True((await Service().BookSlotAsync(c.Id, new(slot.Id), default)).IsSuccess);
        Assert.Equal(1, slot.BookedCount); Assert.Equal(20, c.MentorId);
        Assert.Contains(slot.RoomId.ToString(), c.Sessions.Single().MeetingUrl);
        Assert.False((await Service().BookSlotAsync(c.Id, new(slot.Id), default)).IsSuccess);
        Assert.Single(c.Sessions);
    }
    [Fact] public async Task Group_last_seat_counts_holds_but_releases_expired_holds()
    {
        var original = await Seed(); var p = original.Plan; p.SessionType = "Group"; p.TotalSessions = 1;
        var start = DateTime.UtcNow.AddDays(10);
        var slot = new MentorAvailability { MentorId = 20, StartTime = start, EndTime = start.AddMinutes(60), IsApproved = true, MaxLearners = 2 };
        var cohort = new StudyCohort { Plan = p, MentorId = 20, Name = "Group", Capacity = 2, Status = "Published", StartsAt = start, EndsAt = slot.EndTime,
            Sessions = [new() { Number = 1, Availability = slot }] };
        db.Add(cohort);
        for (int i = 0; i < 2; i++) cohort.Enrollments.Add(new StudyCourse { LearnerId = 50 + i, Plan = p, PurchaseKey = Guid.NewGuid(), PaymentStatus = "Pending", PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        await db.SaveChangesAsync(); actor = new() { Id = 10, Role = Role.Learner };
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));
        Assert.False((await Service().PurchaseAsync(new(p.Id, Guid.NewGuid(), cohort.Id), default)).IsSuccess);
        cohort.Enrollments[0].PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        Assert.True((await Service().PurchaseAsync(new(p.Id, Guid.NewGuid(), cohort.Id), default)).IsSuccess);
    }

    [Fact] public async Task Group_enrollment_closes_at_first_approved_session_policy_cutoff()
    {
        var original = await Seed(); var plan = original.Plan; plan.SessionType = "Group"; plan.TotalSessions = 2;
        var firstStart = DateTime.UtcNow.AddHours(2);
        var policy = new MentorGroupPolicy { Version = 1, MaxLearners = 4,
            EnrollmentCutoffMinutesBeforeFirstSession = 180, SessionMinutes = 60 };
        var first = new MentorAvailability { MentorId = 20, StartTime = firstStart,
            EndTime = firstStart.AddMinutes(60), IsApproved = true, MaxLearners = 4, GroupPolicy = policy };
        var second = new MentorAvailability { MentorId = 20, StartTime = firstStart.AddDays(2),
            EndTime = firstStart.AddDays(2).AddMinutes(60), IsApproved = true, MaxLearners = 4, GroupPolicy = policy };
        var cohort = new StudyCohort { Plan = plan, MentorId = 20, Name = "Lớp định kỳ", Capacity = 4,
            Status = "Published", StartsAt = second.StartTime, EndsAt = second.EndTime,
            Sessions = [new() { Number = 1, Availability = first }, new() { Number = 2, Availability = second }] };
        db.Add(cohort); await db.SaveChangesAsync(); actor = new() { Id = 10, Role = Role.Learner };

        var result = await Service().PurchaseAsync(new(plan.Id, Guid.NewGuid(), cohort.Id), default);

        Assert.False(result.IsSuccess);
        Assert.Contains("khóa đăng ký", result.ErrorMessage);
        gateway.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gateway.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact] public async Task New_policy_group_class_opens_paid_checkout_at_published_price_without_free_enrollment()
    {
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/x", "plink", ""));
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/x", "plink", ""));
        var original = await Seed(); var plan = original.Plan; plan.SessionType = "Group"; plan.TotalSessions = 1;
        var start = DateTime.UtcNow.AddDays(4);
        var policy = new MentorGroupPolicy { Version = 1, MaxLearners = 4, SessionMinutes = 60 };
        var slot = new MentorAvailability { MentorId = 20, StartTime = start, EndTime = start.AddMinutes(60),
            IsApproved = true, MaxLearners = 4, GroupPolicy = policy };
        var cohort = new StudyCohort { Plan = plan, MentorId = 20, Name = "Lớp mới", Capacity = 4,
            Status = "Published", StartsAt = start, EndsAt = slot.EndTime,
            Sessions = [new() { Number = 1, Availability = slot }] };
        db.Add(cohort); await db.SaveChangesAsync(); actor = new() { Id = 10, Role = Role.Learner };

        var result = await Service().PurchaseAsync(new(plan.Id, Guid.NewGuid(), cohort.Id), default);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var order = await db.Set<StudyCourse>().SingleAsync(x => x.CohortId == cohort.Id);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Equal("PendingPayment", order.Status);
        Assert.Equal(plan.Price, order.Amount);
        Assert.Empty(order.Sessions);
        Assert.Empty(db.Set<CohortEnrollmentEntitlement>());
        // Link PayOS hết hạn đúng lúc giữ chỗ hết hạn.
        gateway.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), (int)plan.Price, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), order.PaymentExpiresAt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact] public async Task New_group_class_uses_approved_policy_capacity_without_overwriting_slot_snapshot()
    {
        var original = await Seed(); var plan = original.Plan; plan.SessionType = "Group"; plan.TotalSessions = 2;
        var policy = new MentorGroupPolicy { Version = 1, MaxLearners = 4, SessionMinutes = 60 };
        var start = DateTime.UtcNow.AddDays(8);
        var slots = new[] { 0, 2 }.Select(days => new MentorAvailability
        {
            MentorId = 20, StartTime = start.AddDays(days), EndTime = start.AddDays(days).AddMinutes(60),
            IsApproved = true, MaxLearners = 4, GroupPolicy = policy, SessionMinutesSnapshot = 60
        }).ToArray();
        db.AddRange(slots); await db.SaveChangesAsync();

        // Sĩ số lớp 1..MaxLearners của chính sách; vượt sức chứa chính sách bị từ chối.
        var wrongCapacity = await Service().SaveCohortAsync(null,
            new SaveStudyCohort(plan.Id, "Lớp định kỳ", 20, 5, slots.Select(x => x.Id).ToList(), true), default);
        var approvedCapacity = await Service().SaveCohortAsync(null,
            new SaveStudyCohort(plan.Id, "Lớp định kỳ", 20, 4, slots.Select(x => x.Id).ToList(), true), default);

        Assert.False(wrongCapacity.IsSuccess);
        Assert.Equal("COURSE_PLAN_INVALID_CAPACITY", wrongCapacity.ErrorCode);
        Assert.True(approvedCapacity.IsSuccess, approvedCapacity.ErrorMessage);
        Assert.All(slots, slot => Assert.Equal(4, slot.MaxLearners));
        Assert.Equal(4, (await db.StudyCohorts.SingleAsync()).Capacity);
    }

    [Fact] public async Task Mentor_cannot_publish_two_group_classes_at_overlapping_times()
    {
        var original = await Seed(); var plan = original.Plan; plan.SessionType = "Group"; plan.TotalSessions = 1;
        var policy = new MentorGroupPolicy { Version = 1, MaxLearners = 4, SessionMinutes = 60 };
        var start = DateTime.UtcNow.AddDays(9);
        var slots = new[] { start, start.AddMinutes(30) }.Select(time => new MentorAvailability
        {
            MentorId = 20, StartTime = time, EndTime = time.AddMinutes(60), IsApproved = true,
            MaxLearners = 4, GroupPolicy = policy, SessionMinutesSnapshot = 60
        }).ToArray();
        db.AddRange(slots); await db.SaveChangesAsync();

        var first = await Service().SaveCohortAsync(null,
            new SaveStudyCohort(plan.Id, "Lớp A", 20, 4, [slots[0].Id], true), default);
        var second = await Service().SaveCohortAsync(null,
            new SaveStudyCohort(plan.Id, "Lớp B", 20, 4, [slots[1].Id], true), default);

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.False(second.IsSuccess);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal("MENTOR_SCHEDULE_CONFLICT", second.ErrorCode);
        Assert.Contains("Lớp A", second.ErrorMessage);
        Assert.Single(await db.StudyCohorts.ToListAsync());
    }

    [Fact] public async Task Cancelling_unpaid_enrollment_releases_one_seat_and_late_payment_needs_review()
    {
        var original = await Seed(); var plan = original.Plan; plan.SessionType = "Group"; plan.TotalSessions = 1;
        var start = DateTime.UtcNow.AddDays(8);
        var slot = new MentorAvailability { MentorId = 20, StartTime = start, EndTime = start.AddMinutes(60),
            IsApproved = true, MaxLearners = 1 };
        var cohort = new StudyCohort { Plan = plan, MentorId = 20, Name = "Lớp nhỏ", Capacity = 1,
            Status = "Published", StartsAt = start, EndsAt = slot.EndTime,
            Sessions = [new() { Number = 1, Availability = slot }] };
        var pending = new StudyCourse { LearnerId = 10, Plan = plan, Cohort = cohort,
            PurchaseKey = Guid.NewGuid(), Amount = 100, PaymentStatus = "Pending", Status = "PendingPayment",
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        db.Add(pending); await db.SaveChangesAsync(); actor = new() { Id = 10, Role = Role.Learner };
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));
        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/checkout", "test", ""));

        Assert.True((await Service().CancelPendingEnrollmentAsync(pending.Id, default)).IsSuccess);
        Assert.Equal("Cancelled", pending.PaymentStatus);
        Assert.True((await Service().CancelPendingEnrollmentAsync(pending.Id, default)).IsSuccess);
        Assert.False((await Service().PurchaseAsync(new(plan.Id, pending.PurchaseKey, cohort.Id), default)).IsSuccess);
        Assert.True((await Service().PurchaseAsync(new(plan.Id, Guid.NewGuid(), cohort.Id), default)).IsSuccess);

        var late = await Service().ConfirmPaymentAsync(pending.Id,
            new PaymentCallbackResult(true, true, 700000000 + pending.Id, pending.Amount, ""), "late", default);
        Assert.True(late.IsSuccess, late.ErrorMessage);
        Assert.Equal("PaymentReview", pending.Status);
        Assert.Empty(pending.Sessions);
    }
    [Fact] public async Task Late_paid_group_order_requires_review_when_seat_was_reused()
    {
        var c = await Seed(); c.PaymentStatus = "Pending"; c.Status = "PendingPayment"; c.Sessions.Clear();
        var cohort = new StudyCohort { Plan = c.Plan, MentorId = 20, Name = "Full", Capacity = 1, Status = "Published",
            StartsAt = DateTime.UtcNow.AddDays(1), EndsAt = DateTime.UtcNow.AddDays(2) };
        c.Cohort = cohort; cohort.Enrollments.Add(new StudyCourse { LearnerId = 90, Plan = c.Plan, PaymentStatus = "Paid", PurchaseKey = Guid.NewGuid() });
        db.Add(cohort); await db.SaveChangesAsync();
        Assert.True((await Service().ConfirmPaymentAsync(c.Id, new(true, true, 700000000 + c.Id, c.Amount, ""), "paid", default)).IsSuccess);
        Assert.Equal("Paid", c.PaymentStatus); Assert.Equal("PaymentReview", c.Status); Assert.Empty(c.Sessions);
    }
    [Fact] public async Task Learner_cannot_buy_group_after_start_or_create_class()
    {
        var c = await Seed(); c.Plan.SessionType = "Group"; actor = new() { Id = 10, Role = Role.Learner };
        var cohort = new StudyCohort { Name = "Started", Plan = c.Plan, MentorId = 20, Capacity = 2, Status = "Published",
            StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddDays(1) };
        db.Add(cohort); await db.SaveChangesAsync();
        Assert.False((await Service().PurchaseAsync(new(c.PlanId, Guid.NewGuid(), cohort.Id), default)).IsSuccess);
        Assert.Equal(403, (await Service().SaveCohortAsync(null, new(c.PlanId, "X", 20, 3, [], true), default)).StatusCode);
    }
    [Fact] public void Homework_grade_is_not_a_retake_requirement()
    {
        var c = CourseRetakePolicyTests.Eligible(); foreach (var h in c.Homework) h.Passed = false;
        Assert.True(CourseRetakePolicy.Evaluate(c, false).CanClaim);
    }

    [Fact] public async Task Cancellation_requires_admin_review_and_can_restore_the_session_credit()
    {
        var c = await Seed(); c.Status = "Active"; c.Sessions.Clear();
        c.Sessions.Add(new() { Number = 1, ScheduledAt = DateTime.UtcNow.AddDays(4) }); await db.SaveChangesAsync();
        actor = new() { Id = c.LearnerId, Role = Role.Learner };
        Assert.True((await Service().RequestChangeAsync(c.Id, new(c.Sessions[0].Id, "Cancel", "Change plans"), default)).IsSuccess);
        var request = await db.CourseScheduleRequests.SingleAsync();
        Assert.Equal(403, (await Service().ReviewChangeAsync(c.Id, request.Id, new(true, "approve", RestoreSessionCredit: true), default)).StatusCode);
        actor = new() { Id = 7, Role = Role.Admin };
        Assert.True((await Service().ReviewChangeAsync(c.Id, request.Id, new(true, "Approve credit", RestoreSessionCredit: true), default)).IsSuccess);
        Assert.Equal("CancelledByMentor", c.Sessions.Single().Status);
        Assert.False((await Service().ReviewChangeAsync(c.Id, request.Id, new(true, "Again"), default)).IsSuccess);
    }
    public void Dispose() => db.Dispose();
}
