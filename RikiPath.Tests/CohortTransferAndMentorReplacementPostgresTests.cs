using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notifications;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

// PostgreSQL integration tests for controlled Learner transfer and Admin primary-Mentor replacement.
// They run against an isolated schema in the database named by RIKIPATH_COURSE_TEST_DB (never a shared/production DB).
public sealed class CohortTransferAndMentorReplacementPostgresTests
{
    static CohortTransferAndMentorReplacementPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private const int AdminId = 7001, MentorA = 7002, MentorB = 7003, MentorUnqualified = 7004, MentorBusy = 7005;
    private static readonly int[] Learners = [7011, 7012, 7013, 7014, 7015, 7016];

    private sealed class World
    {
        public required string Schema { get; init; }
        public required string Cs { get; init; }
        public int LevelN3 { get; set; }
        public int LevelN4 { get; set; }
        public int PlanN3 { get; set; }
        public int PlanN4 { get; set; }
        public int PlanN3Premium { get; set; }
        public int PolicyId { get; set; }
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs).Options);
    }

    private static StudyCourseService Service(AppDbContext db, int userId, Role role, IFileStorageService? storage = null, AppSettings? settings = null)
        => new(new UnitOfWork(db), Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role }),
            Mock.Of<IPaymentGatewayClient>(MockBehavior.Strict), storage ?? Mock.Of<IFileStorageService>(),
            settings ?? new AppSettings { FrontendBaseUrl = "https://test.invalid" }, groupPolicies: new MentorGroupPolicyService(db, settings));

    private static AppSettings PostStartApprovedSettings() => new()
    {
        FrontendBaseUrl = "https://test.invalid",
        GroupLearning = new() { PostStartTransferProductApproved = true }
    };

    private static RikiPath.Application.Requests.MentorScheduling.CreateMentorGroupPolicyRequest ValidPolicyRequest(bool transferAllowedAfterSourceStart) => new()
    {
        TransferAllowedAfterSourceStart = transferAllowedAfterSourceStart,
        ExpectedCurrentVersion = 1, EffectiveFromUtc = DateTime.UtcNow.Date.AddDays(3).AddHours(-7), MaxLearners = 4, SessionMinutes = 90,
        DailySessionCap = 4, PublicationHorizonDays = 30, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, KpiReportingPeriodDays = 30,
        RemediationCompletionThresholdPercent = 90, RecurringWeekdayPatterns = [[1, 3, 5]], MentorReplacementLeadHours = 24,
        LearnerTransferEnabled = true, MentorReplacementEnabled = true
    };

    private static DateTime Utc(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);

    private static async Task<World> CreateWorldAsync(bool allowAfterSourceStart = false)
    {
        var baseCs = Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!;
        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(baseCs);
        var world = new World { Schema = schema, Cs = cs };
        await using var db = world.Db();
        UserAccount User(int id, Role role, string name, MentorVerificationStatus? status = null) => new()
        {
            Id = id, Role = role, FirstName = name, LastName = id.ToString(), Email = $"u{id}@test.invalid", PasswordHash = [], PasswordSalt = [],
            MentorProfile = status is null ? null : new MentorProfile { VerificationStatus = status.Value, JlptLevel = "N3" }
        };
        db.Users.AddRange(User(AdminId, Role.Admin, "Admin"),
            User(MentorA, Role.Mentor, "MentorA", MentorVerificationStatus.Approved), User(MentorB, Role.Mentor, "MentorB", MentorVerificationStatus.Approved),
            User(MentorUnqualified, Role.Mentor, "MentorC", MentorVerificationStatus.Approved), User(MentorBusy, Role.Mentor, "MentorD", MentorVerificationStatus.Approved));
        db.Users.AddRange(Learners.Select(id => User(id, Role.Learner, "Learner")));
        var type = new CertificateType { Name = "JLPT", IsActive = true };
        var n3 = new CertificateLevel { Code = "N3", CertificateType = type, SortOrder = 3 };
        var n4 = new CertificateLevel { Code = "N4", CertificateType = type, SortOrder = 2 };
        StudyCoursePlan Plan(CertificateLevel level, decimal price, string name) => new()
        {
            Name = name, CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90, TotalSessions = 3,
            DurationMonths = 2, SessionsPerWeek = 3, Price = price, IsPublished = true
        };
        var p3 = Plan(n3, 500_000, "N3 Group"); var p4 = Plan(n4, 500_000, "N4 Group"); var p3x = Plan(n3, 650_000, "N3 Premium");
        var policy = new MentorGroupPolicy
        {
            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60,
            SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24,
            ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90m,
            RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30, RecurringWeekdayPatternsJson = "[[1,3,5]]",
            LearnerTransferEnabled = true, TransferAllowedAfterSourceStart = allowAfterSourceStart, MentorReplacementEnabled = true,
            MentorReplacementLeadHours = 24, CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        db.AddRange(n3, n4, p3, p4, p3x, policy);
        await db.SaveChangesAsync();
        foreach (var mentorId in new[] { MentorA, MentorB, MentorBusy })
            db.MentorTeachingQualifications.Add(new MentorTeachingQualification
            {
                MentorUserId = mentorId, CertificateLevelId = n3.Id, IsActive = true, VerifiedByAdminId = AdminId, VerifiedAtUtc = DateTime.UtcNow.AddDays(-60)
            });
        await db.SaveChangesAsync();
        world.LevelN3 = n3.Id; world.LevelN4 = n4.Id; world.PlanN3 = p3.Id; world.PlanN4 = p4.Id; world.PlanN3Premium = p3x.Id; world.PolicyId = policy.Id;
        return world;
    }

    private static async Task<int> CreateCohortAsync(World w, int mentorId, int planId, DateTime[] starts, int capacity = 4, string name = "Cohort")
    {
        await using var db = w.Db();
        var cohort = new StudyCohort
        {
            PlanId = planId, Name = name, MentorId = mentorId, Capacity = capacity, CapacitySnapshot = capacity, Status = "Published",
            StartsAt = starts.Min(), EndsAt = starts.Max().AddMinutes(90)
        };
        for (var i = 0; i < starts.Length; i++)
            cohort.Sessions.Add(new StudyCohortSession
            {
                Number = i + 1,
                Availability = new MentorAvailability
                {
                    MentorId = mentorId, StartTime = starts[i], EndTime = starts[i].AddMinutes(90), MaxLearners = capacity,
                    IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
                }
            });
        db.StudyCohorts.Add(cohort);
        await db.SaveChangesAsync();
        return cohort.Id;
    }

    private static async Task<int> EnrollAsync(World w, int learnerId, int cohortId, DateTime? now = null)
    {
        await using var db = w.Db();
        var cohort = await db.StudyCohorts.Include(c => c.Plan).Include(c => c.Sessions).ThenInclude(s => s.Availability).SingleAsync(c => c.Id == cohortId);
        var course = new StudyCourse
        {
            CohortId = cohort.Id, MentorId = cohort.MentorId, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt, SessionType = "Group",
            LearnerId = learnerId, PurchaseKey = Guid.NewGuid(), PlanId = cohort.PlanId, Name = cohort.Plan.Name,
            CertificateLevelId = cohort.Plan.CertificateLevelId, PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3,
            TotalSessions = 3, SessionMinutes = 90, Amount = cohort.Plan.Price, PaymentStatus = "Paid", Status = "Active", PaymentReference = $"PAID-{learnerId}-{cohortId}"
        };
        foreach (var s in cohort.Sessions)
            course.Sessions.Add(new StudyCourseSession
            {
                Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}"
            });
        db.StudyCourses.Add(course);
        await db.SaveChangesAsync();
        return course.Id;
    }

    private static async Task<int> RequestAsync(World w, int learnerId, int courseId, int? targetCohortId, Guid? key = null)
    {
        await using var db = w.Db();
        var res = await Service(db, learnerId, Role.Learner).RequestTransferAsync(courseId, targetCohortId,
            "Lịch làm việc thay đổi nên cần lớp khác", key ?? Guid.NewGuid(), null, default);
        Assert.True(res.IsSuccess, res.ErrorMessage);
        return JsonSerializer.SerializeToElement(res.Result).GetProperty("Id").GetInt32();
    }

    // Yêu cầu chờ duyệt tạo trước khi có quy tắc "không chuyển lớp sau khai giảng" (dữ liệu cũ) — ghi thẳng vào DB.
    private static async Task<int> SeedLegacyPendingRequestAsync(World w, int learnerId, int courseId, int sourceCohortId, int? targetCohortId)
    {
        await using var db = w.Db();
        var request = new CohortTransferRequest
        {
            LearnerId = learnerId, SourceCourseId = courseId, SourceCohortId = sourceCohortId, RequestedTargetCohortId = targetCohortId,
            Reason = "Yêu cầu cũ trước quy tắc chính thức", IdempotencyKey = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.CohortTransferRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    private static async Task DropAsync(World w) => await CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

    private static DateTime[] Future(double firstDays, double gapDays = 1) =>
        [Utc(DateTime.UtcNow.AddDays(firstDays)), Utc(DateTime.UtcNow.AddDays(firstDays + gapDays)), Utc(DateTime.UtcNow.AddDays(firstDays + 2 * gapDays))];

    [CoursePostgresFact]
    public async Task Valid_request_approval_is_atomic_idempotent_releases_source_seat_once_and_creates_no_payment_or_1on1_booking()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3), name: "Source");
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(10), capacity: 2, name: "Target");
            var sourceCourse = await EnrollAsync(w, Learners[0], source);
            var key = Guid.NewGuid();
            var requestId = await RequestAsync(w, Learners[0], sourceCourse, target, key);
            // Retrying the same submission returns the same request instead of creating a duplicate.
            Assert.Equal(requestId, await RequestAsync(w, Learners[0], sourceCourse, target, key));
            await using (var db = w.Db())
            {
                var dup = await Service(db, Learners[0], Role.Learner).RequestTransferAsync(sourceCourse, target, "Một lý do khác đủ dài", Guid.NewGuid(), null, default);
                Assert.Equal(409, dup.StatusCode); Assert.Equal("TRANSFER_REQUEST_ALREADY_PENDING", dup.ErrorCode);
                // Another learner can neither submit for nor read this enrollment/request.
                var foreign = await Service(db, Learners[1], Role.Learner).RequestTransferAsync(sourceCourse, target, "Không phải ghi danh của tôi", Guid.NewGuid(), null, default);
                Assert.Equal(403, foreign.StatusCode);
                Assert.Equal(404, (await Service(db, Learners[1], Role.Learner).TransferRequestDetailAsync(requestId, default)).StatusCode);
                Assert.Equal(403, (await Service(db, MentorA, Role.Mentor).TransferRequestsAsync(null, default)).StatusCode);
                // Learners cannot approve (no direct enrollment changes).
                Assert.Equal(403, (await Service(db, Learners[0], Role.Learner).ApproveTransferAsync(requestId, new(), default)).StatusCode);
            }
            int bookingsBefore, subscriptionsBefore;
            await using (var db = w.Db()) { bookingsBefore = await db.MentorBookings.CountAsync(); subscriptionsBefore = await db.UserSubscriptions.CountAsync(); }

            await using (var db = w.Db())
            {
                var preview = await Service(db, AdminId, Role.Admin).TransferEligibilityAsync(requestId, target, default);
                Assert.True(preview.IsSuccess, preview.ErrorMessage);
                Assert.True(JsonSerializer.SerializeToElement(preview.Result).GetProperty("Eligible").GetBoolean());
                var approved = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new ApproveCohortTransfer(Note: "Đồng ý"), default);
                Assert.True(approved.IsSuccess, approved.ErrorMessage);
            }
            await using (var db = w.Db())
            {
                var again = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new ApproveCohortTransfer(), default);
                Assert.True(again.IsSuccess);
                Assert.True(JsonSerializer.SerializeToElement(again.Result).GetProperty("Idempotent").GetBoolean());
            }
            await using (var db = w.Db())
            {
                var req = await db.CohortTransferRequests.Include(r => r.History).SingleAsync(r => r.Id == requestId);
                Assert.Equal("Approved", req.Status); Assert.Equal(AdminId, req.ReviewedById); Assert.NotNull(req.DestinationCourseId);
                Assert.Equal(new[] { "Requested", "Approved" }, req.History.OrderBy(h => h.Id).Select(h => h.Action).ToArray());
                var src = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == sourceCourse);
                Assert.Equal("Transferred", src.Status); Assert.Equal(source, src.CohortId);
                Assert.Equal(3, src.Sessions.Count); Assert.All(src.Sessions, s => Assert.Equal("TransferredOut", s.Status));
                var dest = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == req.DestinationCourseId);
                Assert.Equal(target, dest.CohortId); Assert.Equal(MentorB, dest.MentorId); Assert.Equal("Active", dest.Status);
                Assert.Equal(0m, dest.Amount); Assert.Equal(sourceCourse, dest.TransferredFromCourseId); Assert.Equal(3, dest.Sessions.Count);
                Assert.Equal(1, await db.StudyCourses.CountAsync(c => c.CohortId == target));
                Assert.Equal(1, await db.CohortTransferRequests.CountAsync(r => r.DestinationCourseId == dest.Id));
                Assert.Equal(bookingsBefore, await db.MentorBookings.CountAsync());
                Assert.Equal(subscriptionsBefore, await db.UserSubscriptions.CountAsync());
                Assert.Equal(1, await db.MentorOutboxEvents.CountAsync(e => e.EventKey == $"cohort-transfer:{requestId}:approved"));
                // Source seat released exactly once: the source cohort lists one fewer reserved seat.
                var cohorts = await Service(db, AdminId, Role.Admin).CohortsAsync(default);
                var sourceRow = JsonSerializer.SerializeToElement(cohorts.Result).EnumerateArray().Single(x => x.GetProperty("Id").GetInt32() == source);
                Assert.Equal(0, sourceRow.GetProperty("EnrolledCount").GetInt32());
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Rejection_requires_reason_and_is_visible_to_learner_without_touching_enrollment()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3));
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(10));
            var course = await EnrollAsync(w, Learners[0], source);
            var requestId = await RequestAsync(w, Learners[0], course, target);
            await using (var db = w.Db())
            {
                var missing = await Service(db, AdminId, Role.Admin).RejectTransferAsync(requestId, new RejectCohortTransfer("   "), default);
                Assert.Equal(422, missing.StatusCode); Assert.Equal("TRANSFER_REJECTION_REASON_REQUIRED", missing.ErrorCode);
                var ok = await Service(db, AdminId, Role.Admin).RejectTransferAsync(requestId, new RejectCohortTransfer("Lớp đích không phù hợp lộ trình"), default);
                Assert.True(ok.IsSuccess, ok.ErrorMessage);
                var approveAfter = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new(), default);
                Assert.Equal(409, approveAfter.StatusCode);
            }
            await using (var db = w.Db())
            {
                var mine = await Service(db, Learners[0], Role.Learner).TransferRequestsAsync(null, default);
                var row = JsonSerializer.SerializeToElement(mine.Result).EnumerateArray().Single();
                Assert.Equal("Rejected", row.GetProperty("Status").GetString());
                Assert.Equal("Lớp đích không phù hợp lộ trình", row.GetProperty("DecisionNote").GetString());
                var src = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == course);
                Assert.Equal("Active", src.Status); Assert.All(src.Sessions, s => Assert.Equal("Scheduled", s.Status));
                Assert.Equal(0, await db.StudyCourses.CountAsync(c => c.CohortId == target));
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Two_concurrent_approvals_for_the_last_seat_admit_exactly_one_and_keep_the_loser_source_intact()
    {
        var w = await CreateWorldAsync();
        try
        {
            var sourceA = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3), name: "SrcA");
            var sourceB = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3.3), name: "SrcB");
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(10), capacity: 2, name: "Target");
            await EnrollAsync(w, Learners[5], target); // one of two seats already taken
            var c1 = await EnrollAsync(w, Learners[0], sourceA);
            var c2 = await EnrollAsync(w, Learners[1], sourceB);
            var r1 = await RequestAsync(w, Learners[0], c1, target);
            var r2 = await RequestAsync(w, Learners[1], c2, target);
            using var gate = new SemaphoreSlim(0, 2);
            async Task<ApiResponse<object>> Approve(int requestId)
            {
                await gate.WaitAsync();
                await using var db = w.Db();
                return await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new(), default);
            }
            var t1 = Task.Run(() => Approve(r1)); var t2 = Task.Run(() => Approve(r2));
            gate.Release(2);
            var results = await Task.WhenAll(t1, t2);
            Assert.Single(results, r => r.IsSuccess);
            var loser = Assert.Single(results, r => !r.IsSuccess);
            Assert.Equal(409, loser.StatusCode); Assert.Equal("TRANSFER_TARGET_FULL", loser.ErrorCode);
            await using var check = w.Db();
            Assert.Equal(2, await check.StudyCourses.CountAsync(c => c.CohortId == target && c.Status == "Active"));
            var statuses = await check.CohortTransferRequests.Where(r => r.Id == r1 || r.Id == r2).Select(r => r.Status).ToListAsync();
            Assert.Equal(new[] { "Approved", "Pending" }, statuses.OrderBy(s => s).ToArray());
            // The losing learner keeps the source enrollment and every scheduled session (no partial write).
            var loserCourseId = results[0].IsSuccess ? c2 : c1;
            var loserCourse = await check.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == loserCourseId);
            Assert.Equal("Active", loserCourse.Status); Assert.All(loserCourse.Sessions, s => Assert.Equal("Scheduled", s.Status));
            Assert.Equal(0, await check.StudyCourses.CountAsync(c => c.TransferredFromCourseId == loserCourseId));
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Ineligible_targets_are_rejected_with_stable_codes_and_never_modify_the_source()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3), name: "Source");
            var n4 = await CreateCohortAsync(w, MentorB, w.PlanN4, Future(10), name: "N4");
            var premium = await CreateCohortAsync(w, MentorB, w.PlanN3Premium, Future(12), name: "Premium");
            var started = await CreateCohortAsync(w, MentorB, w.PlanN3,
                [Utc(DateTime.UtcNow.AddHours(-2)), Utc(DateTime.UtcNow.AddDays(2)), Utc(DateTime.UtcNow.AddDays(4))], name: "Started");
            var pastCutoff = await CreateCohortAsync(w, MentorB, w.PlanN3,
                [DateTime.UtcNow.AddMinutes(30), Utc(DateTime.UtcNow.AddDays(6)), Utc(DateTime.UtcNow.AddDays(7))], name: "Cutoff");
            var clashTimes = Future(20);
            var clash = await CreateCohortAsync(w, MentorB, w.PlanN3, clashTimes, name: "Clash");
            var other = await CreateCohortAsync(w, MentorBusy, w.PlanN3, [clashTimes[0].AddMinutes(30), Utc(DateTime.UtcNow.AddDays(30)), Utc(DateTime.UtcNow.AddDays(31))], name: "Other");
            await using (var db = w.Db())
            {
                // Policy cutoff for legacy-style slots comes from the slot policy; give the cutoff cohort a policy snapshot.
                var slot = await db.Set<StudyCohortSession>().Where(s => s.CohortId == pastCutoff && s.Number == 1).Select(s => s.Availability).SingleAsync();
                var rule = new MentorScheduleRule
                {
                    Schedule = new MentorSchedule { MentorId = MentorB, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date), CreatedAtUtc = DateTime.UtcNow },
                    Weekday = 1, DaypartCode = "Evening", LocalStart = new TimeOnly(19, 0), LocalEnd = new TimeOnly(22, 0), EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
                };
                db.Add(rule); await db.SaveChangesAsync();
                slot.GroupPolicyId = w.PolicyId; slot.PolicyVersion = 1; slot.SessionMinutesSnapshot = 90; slot.RestMinutesSnapshot = 15;
                slot.MentorScheduleRuleId = rule.Id; slot.GenerationKey = $"cutoff-{Guid.NewGuid():N}"; slot.LocalBusinessDate = DateOnly.FromDateTime(slot.StartTime);
                await db.SaveChangesAsync();
            }
            var course = await EnrollAsync(w, Learners[0], source);
            await EnrollAsync(w, Learners[0], other); // creates a schedule conflict with "Clash"
            var requestId = await RequestAsync(w, Learners[0], course, null);
            async Task Expect(int targetId, int status, string code)
            {
                await using var db = w.Db();
                var res = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new ApproveCohortTransfer(targetId), default);
                Assert.False(res.IsSuccess);
                Assert.Equal(code, res.ErrorCode); Assert.Equal(status, res.StatusCode);
            }
            await Expect(n4, 422, "TRANSFER_LEVEL_MISMATCH");
            await Expect(premium, 422, "TRANSFER_PAYMENT_POLICY_REQUIRED");
            await Expect(started, 422, "TRANSFER_TARGET_STARTED");
            await Expect(pastCutoff, 422, "TRANSFER_TARGET_CUTOFF");
            await Expect(clash, 422, "TRANSFER_SCHEDULE_CONFLICT");
            await Expect(source, 422, "TRANSFER_TARGET_SAME_COHORT");
            await using (var db = w.Db())
            {
                var noTarget = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new(), default);
                Assert.Equal("TRANSFER_TARGET_REQUIRED", noTarget.ErrorCode);
                var src = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == course);
                Assert.Equal("Active", src.Status); Assert.All(src.Sessions, s => Assert.Equal("Scheduled", s.Status));
                Assert.Equal("Pending", (await db.CohortTransferRequests.SingleAsync(r => r.Id == requestId)).Status);
                Assert.Equal(0, await db.StudyCourses.CountAsync(c => c.TransferredFromCourseId == course));
            }
            // A started source is always blocked (official Product Owner rule): new requests and legacy pending ones.
            var startedSource = await CreateCohortAsync(w, MentorA, w.PlanN3,
                [Utc(DateTime.UtcNow.AddDays(-3)), Utc(DateTime.UtcNow.AddDays(14)), Utc(DateTime.UtcNow.AddDays(15))], name: "StartedSource");
            var learnerCourse = await EnrollAsync(w, Learners[2], startedSource);
            var cleanTarget = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(40), name: "Clean");
            await using (var db = w.Db())
            {
                var denied = await Service(db, Learners[2], Role.Learner).RequestTransferAsync(learnerCourse, cleanTarget,
                    "Lịch làm việc thay đổi nên cần lớp khác", Guid.NewGuid(), null, default);
                Assert.Equal(422, denied.StatusCode); Assert.Equal("TRANSFER_SOURCE_STARTED", denied.ErrorCode);
            }
            var startedRequest = await SeedLegacyPendingRequestAsync(w, Learners[2], learnerCourse, startedSource, cleanTarget);
            await using (var db = w.Db())
            {
                var res = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(startedRequest, new(), default);
                Assert.Equal("TRANSFER_SOURCE_STARTED", res.ErrorCode);
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Post_start_transfer_is_rejected_even_when_policy_flag_and_approval_setting_are_on_and_source_history_stays_untouched()
    {
        var w = await CreateWorldAsync(allowAfterSourceStart: true);
        try
        {
            var past = Utc(DateTime.UtcNow.AddDays(-2));
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, [past, Utc(DateTime.UtcNow.AddDays(5)), Utc(DateTime.UtcNow.AddDays(6))], name: "Started");
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(12), name: "Next");
            var course = await EnrollAsync(w, Learners[0], source);
            int sessionId, attendanceId, homeworkId;
            Guid pastRoom;
            await using (var db = w.Db())
            {
                var s1 = await db.Set<StudyCourseSession>().Include(s => s.Availability).SingleAsync(s => s.CourseId == course && s.Number == 1);
                s1.Status = "Present"; s1.ConfirmedById = MentorA; s1.ConfirmedAt = past.AddMinutes(95); s1.Note = "Có mặt";
                pastRoom = s1.Availability!.RoomId; sessionId = s1.Id;
                var att = new MentorAttendanceRecord { CourseSessionId = s1.Id, RoomId = pastRoom, UserId = Learners[0], Role = "Learner", Status = MentorAttendanceStatus.Left, TotalDurationSeconds = 5000 };
                var homework = new HomeworkAssignment
                {
                    LearnerId = Learners[0], MentorId = MentorA, CohortId = source, StudyCourseId = course, AssignmentScope = "CohortAll", Title = "Bài 1",
                    DueAt = past.AddDays(1), Status = "Graded", MentorFeedback = "Tốt", MentorScore = 9, Passed = true, PublishedFeedbackAtUtc = past.AddDays(1), PublishedByMentorId = MentorA,
                    Submission = new HomeworkSubmission { LearnerId = Learners[0], TextContent = "答え", SubmittedAt = past.AddHours(5) }
                };
                db.AddRange(att, homework); await db.SaveChangesAsync();
                attendanceId = att.Id; homeworkId = homework.Id;
            }
            // Learner cannot even submit a request once the source course started (policy flag + approval setting on).
            await using (var db = w.Db())
            {
                var denied = await Service(db, Learners[0], Role.Learner, settings: PostStartApprovedSettings()).RequestTransferAsync(course, target,
                    "Lịch làm việc thay đổi nên cần lớp khác", Guid.NewGuid(), null, default);
                Assert.Equal(422, denied.StatusCode);
                Assert.Equal("TRANSFER_SOURCE_STARTED", denied.ErrorCode);
                Assert.Equal(0, await db.CohortTransferRequests.CountAsync());
            }
            // A legacy pending request (created before the official rule) is rejected on approval too.
            var requestId = await SeedLegacyPendingRequestAsync(w, Learners[0], course, source, target);
            await using (var db = w.Db())
            {
                var blocked = await Service(db, AdminId, Role.Admin, settings: PostStartApprovedSettings()).ApproveTransferAsync(requestId, new(), default);
                Assert.False(blocked.IsSuccess);
                Assert.Equal(422, blocked.StatusCode);
                Assert.Equal("TRANSFER_SOURCE_STARTED", blocked.ErrorCode);
            }
            await using (var db = w.Db())
            {
                var s1 = await db.Set<StudyCourseSession>().SingleAsync(s => s.Id == sessionId);
                Assert.Equal("Present", s1.Status); Assert.Equal(MentorA, s1.ConfirmedById); Assert.Equal(course, s1.CourseId);
                var att = await db.MentorAttendanceRecords.SingleAsync(a => a.Id == attendanceId);
                Assert.Equal(sessionId, att.CourseSessionId); Assert.Equal(pastRoom, att.RoomId); Assert.Equal(5000, att.TotalDurationSeconds);
                var hw = await db.HomeworkAssignments.Include(h => h.Submission).SingleAsync(h => h.Id == homeworkId);
                Assert.Equal(source, hw.CohortId); Assert.Equal(course, hw.StudyCourseId); Assert.Equal(MentorA, hw.MentorId);
                Assert.Equal("Tốt", hw.MentorFeedback); Assert.Equal(9, hw.MentorScore); Assert.NotNull(hw.Submission);
                var futureSource = await db.Set<StudyCourseSession>().Where(s => s.CourseId == course && s.Number > 1).Select(s => s.Status).ToListAsync();
                Assert.All(futureSource, s => Assert.Equal("Scheduled", s));
                var src = await db.StudyCourses.SingleAsync(c => c.Id == course);
                Assert.Equal("Active", src.Status);
                Assert.Equal(0, await db.StudyCourses.CountAsync(c => c.TransferredFromCourseId == course));
                Assert.Equal("Pending", (await db.CohortTransferRequests.SingleAsync(r => r.Id == requestId)).Status);
            }
            await using (var db = w.Db())
            {
                // Mentor A still reads the enrollment (roster/feedback) unchanged.
                var detail = await Service(db, MentorA, Role.Mentor).DetailAsync(course, default);
                Assert.True(detail.IsSuccess, detail.ErrorMessage);
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Post_start_transfer_is_blocked_and_new_policy_versions_enabling_it_are_rejected_with_or_without_approval()
    {
        var w = await CreateWorldAsync(allowAfterSourceStart: true);
        try
        {
            var past = Utc(DateTime.UtcNow.AddDays(-2));
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, [past, Utc(DateTime.UtcNow.AddDays(5)), Utc(DateTime.UtcNow.AddDays(6))], name: "Started");
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(12), name: "Next");
            var course = await EnrollAsync(w, Learners[0], source);
            var requestId = await SeedLegacyPendingRequestAsync(w, Learners[0], course, source, target);
            MentorGroupPolicy storedBefore;
            await using (var db = w.Db())
                storedBefore = await db.MentorGroupPolicies.AsNoTracking().SingleAsync(p => p.Id == w.PolicyId);

            await using (var db = w.Db())
            {
                // Stored policy flag is on; default settings (no approval switch): still the official TRANSFER_SOURCE_STARTED.
                var blocked = await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new(), default);
                Assert.False(blocked.IsSuccess);
                Assert.Equal("TRANSFER_SOURCE_STARTED", blocked.ErrorCode);
            }
            await using (var db = w.Db())
            {
                var src = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == course);
                Assert.Equal("Active", src.Status);
                Assert.DoesNotContain(src.Sessions, s => s.Status == "TransferredOut");
                Assert.Equal(0, await db.StudyCourses.CountAsync(c => c.TransferredFromCourseId == course));
                Assert.Equal("Pending", (await db.CohortTransferRequests.SingleAsync(r => r.Id == requestId)).Status);

                // New policy versions enabling post-start transfer are rejected without approval settings...
                var rejected = await new MentorGroupPolicyService(db).CreateVersionAsync(ValidPolicyRequest(transferAllowedAfterSourceStart: true), AdminId);
                Assert.Equal(400, rejected.StatusCode);
                Assert.Equal(1, await db.MentorGroupPolicies.CountAsync());
            }
            await using (var db = w.Db())
            {
                // ...and also with the approval switch configured (official rule supersedes it).
                var rejectedWithApproval = await new MentorGroupPolicyService(db, PostStartApprovedSettings())
                    .CreateVersionAsync(ValidPolicyRequest(transferAllowedAfterSourceStart: true), AdminId);
                Assert.Equal(400, rejectedWithApproval.StatusCode);
                Assert.Equal(1, await db.MentorGroupPolicies.CountAsync());
            }
            await using (var db = w.Db())
            {
                // The already-stored policy row is never rewritten by the gate.
                var storedAfter = await db.MentorGroupPolicies.AsNoTracking().SingleAsync(p => p.Id == w.PolicyId);
                Assert.Equal(storedBefore.Version, storedAfter.Version);
                Assert.Equal(storedBefore.TransferAllowedAfterSourceStart, storedAfter.TransferAllowedAfterSourceStart);
                Assert.Equal(storedBefore.LearnerTransferEnabled, storedAfter.LearnerTransferEnabled);
                Assert.Equal(storedBefore.EffectiveFromUtc, storedAfter.EffectiveFromUtc);
                Assert.Equal(storedBefore.MaxLearners, storedAfter.MaxLearners);
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Mentor_replacement_moves_only_future_sessions_preserves_history_kpi_and_blocks_conflicts()
    {
        var w = await CreateWorldAsync();
        try
        {
            var s1 = Utc(DateTime.UtcNow.AddDays(-4)); var s2 = Utc(DateTime.UtcNow.AddDays(-2)); var s3 = Utc(DateTime.UtcNow.AddDays(3));
            await using (var db = w.Db())
            {
                Assert.Equal(3, (await db.StudyCoursePlans.SingleAsync(p => p.Id == w.PlanN3)).TotalSessions);
            }
            var cohort = await CreateCohortAsync(w, MentorA, w.PlanN3, [s1, s2, s3], name: "Replace");
            var courses = new[] { await EnrollAsync(w, Learners[0], cohort), await EnrollAsync(w, Learners[1], cohort) };
            List<int> pastSlotIds;
            await using (var db = w.Db())
            {
                foreach (var cs in await db.Set<StudyCourseSession>().Include(s => s.Availability).Include(s => s.Course).Where(s => courses.Contains(s.CourseId) && s.Number <= 2).ToListAsync())
                {
                    cs.Status = "Present"; cs.ConfirmedById = MentorA; cs.ConfirmedAt = cs.ScheduledAt.AddMinutes(95); cs.Note = "Có mặt";
                    db.MentorAttendanceRecords.Add(new MentorAttendanceRecord { CourseSessionId = cs.Id, RoomId = cs.Availability!.RoomId, UserId = cs.Course.LearnerId, Role = "Learner", TotalDurationSeconds = 5400 });
                }
                pastSlotIds = await db.Set<StudyCohortSession>().Where(s => s.CohortId == cohort && s.Number <= 2).Select(s => s.AvailabilityId).ToListAsync();
                foreach (var slot in await db.MentorAvailabilities.Where(a => pastSlotIds.Contains(a.Id)).ToListAsync())
                    db.MentorAttendanceRecords.Add(new MentorAttendanceRecord { RoomId = slot.RoomId, UserId = MentorA, Role = "Mentor", TotalDurationSeconds = 5400 });
                await db.SaveChangesAsync();
            }
            // Mentor D already teaches a published class at the time of session 3 -> schedule conflict
            // (an open, unbooked availability slot is no longer treated as a teaching commitment).
            await CreateCohortAsync(w, MentorBusy, w.PlanN3, [s3.AddMinutes(30), s3.AddDays(2), s3.AddDays(4)], name: "Busy class");
            int revision;
            await using (var db = w.Db())
            {
                var svc = Service(db, AdminId, Role.Admin);
                Assert.Equal("REPLACEMENT_LEVEL_NOT_QUALIFIED", (await svc.ReplaceCohortMentorAsync(cohort, new(MentorUnqualified, "Cố vấn A nghỉ dài hạn", Guid.NewGuid(), 1), default)).ErrorCode);
                var busyPreview = await svc.PreviewMentorReplacementAsync(cohort, MentorBusy, null, default);
                var busy = JsonSerializer.SerializeToElement(busyPreview.Result);
                Assert.False(busy.GetProperty("Eligible").GetBoolean());
                Assert.NotEmpty(busy.GetProperty("Conflicts").EnumerateArray());
                var busyConfirm = await svc.ReplaceCohortMentorAsync(cohort, new(MentorBusy, "Cố vấn A nghỉ dài hạn", Guid.NewGuid(), 1), default);
                Assert.Equal(409, busyConfirm.StatusCode); Assert.Equal("REPLACEMENT_MENTOR_SCHEDULE_CONFLICT", busyConfirm.ErrorCode);
                Assert.Equal("REPLACEMENT_REASON_REQUIRED", (await svc.ReplaceCohortMentorAsync(cohort, new(MentorB, "", Guid.NewGuid(), 1), default)).ErrorCode);
                var preview = JsonSerializer.SerializeToElement((await svc.PreviewMentorReplacementAsync(cohort, MentorB, null, default)).Result);
                Assert.True(preview.GetProperty("Eligible").GetBoolean(), preview.ToString());
                Assert.Equal(3, preview.GetProperty("FromSessionNumber").GetInt32());
                Assert.Equal(2, preview.GetProperty("Impact").GetProperty("RetainedSessionCount").GetInt32());
                revision = preview.GetProperty("Revision").GetInt32();
                Assert.Equal("REPLACEMENT_COHORT_STALE", (await svc.ReplaceCohortMentorAsync(cohort, new(MentorB, "Cố vấn A nghỉ dài hạn", Guid.NewGuid(), revision + 5), default)).ErrorCode);
            }
            var key = Guid.NewGuid();
            int bookingsBefore;
            await using (var db = w.Db())
            {
                bookingsBefore = await db.MentorBookings.CountAsync();
                var res = await Service(db, AdminId, Role.Admin).ReplaceCohortMentorAsync(cohort, new(MentorB, "Cố vấn A nghỉ dài hạn", key, revision), default);
                Assert.True(res.IsSuccess, res.ErrorMessage);
            }
            await using (var db = w.Db())
            {
                var retry = await Service(db, AdminId, Role.Admin).ReplaceCohortMentorAsync(cohort, new(MentorB, "Cố vấn A nghỉ dài hạn", key, revision), default);
                Assert.True(JsonSerializer.SerializeToElement(retry.Result).GetProperty("Idempotent").GetBoolean());
            }
            await using (var db = w.Db())
            {
                var c = await db.StudyCohorts.Include(x => x.Sessions).ThenInclude(s => s.Availability).Include(x => x.Enrollments).SingleAsync(x => x.Id == cohort);
                Assert.Equal(MentorB, c.MentorId); Assert.Equal(4, c.Capacity); Assert.Equal(2, c.Enrollments.Count);
                // Completed sessions keep the original slot, Mentor A and their attendance; nothing is soft-deleted.
                foreach (var s in c.Sessions.Where(s => s.Number <= 2))
                {
                    Assert.Contains(s.AvailabilityId, pastSlotIds); Assert.Equal(MentorA, s.Availability.MentorId); Assert.False(s.Availability.IsDeleted);
                }
                var future = c.Sessions.Single(s => s.Number == 3);
                Assert.Equal(MentorB, future.Availability.MentorId); Assert.DoesNotContain(future.AvailabilityId, pastSlotIds);
                var learnerSessions = await db.Set<StudyCourseSession>().Where(s => courses.Contains(s.CourseId)).ToListAsync();
                Assert.All(learnerSessions.Where(s => s.Number <= 2), s => { Assert.Equal("Present", s.Status); Assert.Equal(MentorA, s.ConfirmedById); });
                Assert.All(learnerSessions.Where(s => s.Number == 3), s =>
                {
                    Assert.Equal(future.AvailabilityId, s.AvailabilityId); Assert.Equal($"/meeting-room/{future.Availability.RoomId}", s.MeetingUrl);
                });
                Assert.Equal(1, await db.StudyCohortMentorAssignments.CountAsync(a => a.CohortId == cohort));
                Assert.Equal(bookingsBefore, await db.MentorBookings.CountAsync());
                var admin = Mock.Of<IClaimService>(x => x.GetUserClaim() == new ClaimDTO { Id = AdminId, Role = Role.Admin });
                var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)); var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
                var kpiA = (await new MentorKpiService(db, admin).GetAdminMentorKpiByMentorAsync(MentorA, from, to)).Result!;
                var kpiB = (await new MentorKpiService(db, admin).GetAdminMentorKpiByMentorAsync(MentorB, from, to)).Result!;
                Assert.Equal(2, kpiA.CompletedValidSessions); Assert.Equal(2, kpiA.TotalAssignedSessions);
                Assert.Equal(180, kpiA.ScheduledCompletedMinutes);
                Assert.Equal(0, kpiB.CompletedValidSessions); Assert.Equal(1, kpiB.TotalAssignedSessions);
                Assert.Equal(3, kpiA.TotalAssignedSessions + kpiB.TotalAssignedSessions); // no duplicated teaching sessions
            }
            await using (var db = w.Db())
            {
                // Mentor A can no longer confirm the future session; Mentor B can't rewrite A's completed ones.
                var futureSession = await db.Set<StudyCourseSession>().SingleAsync(s => s.CourseId == courses[0] && s.Number == 3);
                Assert.Equal(403, (await Service(db, MentorA, Role.Mentor).AttendanceAsync(courses[0], futureSession.Id, new("CancelledByMentor", "Thử"), default)).StatusCode);
                var pastSession = await db.Set<StudyCourseSession>().SingleAsync(s => s.CourseId == courses[0] && s.Number == 1);
                Assert.False((await Service(db, MentorB, Role.Mentor).AttendanceAsync(courses[0], pastSession.Id, new("Absent", "Thử"), default)).IsSuccess);
            }
        }
        finally { await DropAsync(w); }
    }

    private sealed class FlakyNotifications(bool fail) : INotificationService
    {
        public int Calls;
        public Task<ApiResponse<NotificationResponse>> SendNotificationToUserAsync(int targetUserId, string title, string message, string type, string? linkUrl, CancellationToken cancellationToken, string? idempotencyKey = null)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(fail ? ApiResponse<NotificationResponse>.Fail("push down") : ApiResponse<NotificationResponse>.Success(new NotificationResponse()));
        }
        public Task<ApiResponse<RikiPath.Application.Common.PagedResult<NotificationResponse>>> GetMyNotificationsAsync(bool onlyUnread, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse<int>> GetUnreadCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse> MarkAsReadAsync(int notificationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApiResponse> MarkAllAsReadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CreateNotificationAsync(string title, string message, string type, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [CoursePostgresFact]
    public async Task Notification_delivery_failure_after_commit_keeps_transfer_and_retries_outbox()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3));
            var target = await CreateCohortAsync(w, MentorB, w.PlanN3, Future(10));
            var course = await EnrollAsync(w, Learners[0], source);
            var requestId = await RequestAsync(w, Learners[0], course, target);
            await using (var db = w.Db())
                Assert.True((await Service(db, AdminId, Role.Admin).ApproveTransferAsync(requestId, new(), default)).IsSuccess);

            async Task<int> Dispatch(INotificationService notifications)
            {
                var services = new ServiceCollection();
                services.AddDbContext<AppDbContext>(o => o.UseNpgsql(w.Cs));
                services.AddSingleton(notifications);
                await using var provider = services.BuildServiceProvider();
                return await new MentorOutboxDispatcherService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MentorOutboxDispatcherService>.Instance)
                    .ProcessPendingOutboxEventsAsync();
            }
            var failing = new FlakyNotifications(fail: true);
            await Dispatch(failing);
            Assert.True(failing.Calls > 0);
            await using (var db = w.Db())
            {
                var evt = await db.MentorOutboxEvents.SingleAsync(e => e.EventKey == $"cohort-transfer:{requestId}:approved");
                Assert.Null(evt.DeliveredAtUtc); Assert.Equal(1, evt.Attempts);
                Assert.Equal("Approved", (await db.CohortTransferRequests.SingleAsync(r => r.Id == requestId)).Status);
                Assert.Equal("Transferred", (await db.StudyCourses.SingleAsync(c => c.Id == course)).Status);
                // Make the leased events visible again to simulate the retry window elapsing.
                await db.MentorOutboxEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.AvailableAtUtc, DateTime.UtcNow.AddMinutes(-1)));
            }
            var healthy = new FlakyNotifications(fail: false);
            await Dispatch(healthy);
            await using (var db = w.Db())
            {
                var evt = await db.MentorOutboxEvents.SingleAsync(e => e.EventKey == $"cohort-transfer:{requestId}:approved");
                Assert.NotNull(evt.DeliveredAtUtc); Assert.Equal(2, evt.Attempts);
                Assert.Equal(3, JsonDocument.Parse(evt.PayloadJson).RootElement.GetProperty("Notifications").GetArrayLength());
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Policy_disabling_transfer_blocks_new_requests_and_is_validated_server_side()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, MentorA, w.PlanN3, Future(3));
            var course = await EnrollAsync(w, Learners[0], source);
            await using (var db = w.Db())
            {
                var policy = await db.MentorGroupPolicies.SingleAsync(p => p.Id == w.PolicyId);
                policy.LearnerTransferEnabled = false; await db.SaveChangesAsync();
                var res = await Service(db, Learners[0], Role.Learner).RequestTransferAsync(course, null, "Lý do đủ dài để gửi", Guid.NewGuid(), null, default);
                Assert.Equal(422, res.StatusCode); Assert.Equal("TRANSFER_DISABLED_BY_POLICY", res.ErrorCode);
                var invalid = await new MentorGroupPolicyService(db).CreateVersionAsync(new RikiPath.Application.Requests.MentorScheduling.CreateMentorGroupPolicyRequest
                {
                    ExpectedCurrentVersion = 1, EffectiveFromUtc = DateTime.UtcNow.Date.AddDays(3).AddHours(-7), MaxLearners = 4, SessionMinutes = 90,
                    DailySessionCap = 4, PublicationHorizonDays = 30, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, KpiReportingPeriodDays = 30,
                    RemediationCompletionThresholdPercent = 90, RecurringWeekdayPatterns = [[1, 3, 5]], MentorReplacementLeadHours = 9999
                }, AdminId);
                Assert.Equal(400, invalid.StatusCode);
            }
        }
        finally { await DropAsync(w); }
    }
}
