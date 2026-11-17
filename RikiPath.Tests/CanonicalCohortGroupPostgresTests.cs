using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

public sealed class CanonicalCohortGroupPostgresTests
{
    static CanonicalCohortGroupPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private sealed class AllowDirectGroupEntitlementContract : IGroupEnrollmentEntitlementContract
    {
        public GroupEnrollmentEntitlementDecision EvaluateGroupEnrollment(
            StudyCohort cohort, StudyCoursePlan plan, int learnerId, string? entitlementReference)
            => new(true, RequiresCheckout: false, EntitlementReference: entitlementReference ?? $"DIRECT-{cohort.Id}-{learnerId}");
    }

    private static async Task<(string Schema, string ConnectionString)> CreateIsolatedSchemaAsync()
    {
        var schema = "canonical_qa_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB"))
        {
            SearchPath = schema
        }.ConnectionString;
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", conn))
            await create.ExecuteNonQueryAsync();
        await using var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        var operations = setup.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, setup.GetService<IDesignTimeModel>().Model.GetRelationalModel())
            .Where(x => x is not InsertDataOperation)
            .ToList();
        foreach (var sql in setup.GetService<IMigrationsSqlGenerator>().Generate(operations, setup.Model))
            await setup.Database.ExecuteSqlRawAsync(sql.CommandText.Replace("{", "{{").Replace("}", "}}"));
        return (schema, cs);
    }

    private static async Task DropSchemaAsync(string schema, string cs)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", conn);
        await drop.ExecuteNonQueryAsync();
    }

    private static IClaimService Claim(int userId, Role role) =>
        Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role });

    private static StudyCourseService CreateCourseService(
        AppDbContext db,
        int userId,
        Role role,
        IGroupEnrollmentEntitlementContract? contract = null,
        IMentorGroupPolicyService? policyService = null,
        IPaymentGatewayClient? payments = null)
        => new(
            new UnitOfWork(db),
            Claim(userId, role),
            payments ?? Mock.Of<IPaymentGatewayClient>(),
            Mock.Of<IFileStorageService>(),
            new AppSettings { FrontendBaseUrl = "https://test.invalid" },
            groupEntitlementContract: contract,
            groupPolicies: policyService ?? new MentorGroupPolicyService(db));

    private static MentorGroupPolicy DefaultPolicy(int version = 1, int maxLearners = 4, int cutoffMinutes = 60, DateTime? effectiveFromUtc = null)
        => new()
        {
            Version = version,
            EffectiveFromUtc = effectiveFromUtc ?? DateTime.UtcNow.AddDays(-10),
            MaxLearners = maxLearners,
            EnrollmentCutoffMinutesBeforeFirstSession = cutoffMinutes,
            SessionMinutes = 90,
            RestMinutes = 15,
            DailySessionCap = 4,
            PublicationHorizonDays = 30,
            CancellationLeadHours = 24,
            ReschedulingLeadHours = 24,
            HomeworkDeadlineDays = 7,
            PlacementAttemptLimit = 2,
            RemediationCompletionThresholdPercent = 90m,
            RemediationThresholdComparison = ">",
            RequireOfficialExamEvidence = true,
            RequireAdminRemediationReview = true,
            KpiReportingPeriodDays = 30,
            RecurringWeekdayPatternsJson = "[[1,3,5],[2,4,6]]",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

    [CoursePostgresFact]
    public async Task Cohort_enrollment_is_fail_closed_by_default_and_handles_concurrency_idempotency_cancellation_reschedule_cutoff_overlap_and_policy_versioning()
    {
        var (schema, cs) = await CreateIsolatedSchemaAsync();
        AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        try
        {
            await using var setup = Context();
            var mentor = new UserAccount { Id = 2100, Role = Role.Mentor, FirstName = "Sensei", LastName = "Tanaka", Email = "mentor2100@test.invalid", PasswordHash = [], PasswordSalt = [] };
            var admin = new UserAccount { Id = 2101, Role = Role.Admin, FirstName = "Admin", LastName = "QA", Email = "admin2101@test.invalid", PasswordHash = [], PasswordSalt = [] };
            var learners = Enumerable.Range(2111, 6).Select(id => new UserAccount
            {
                Id = id, Role = Role.Learner, FirstName = "Learner", LastName = id.ToString(),
                Email = $"learner{id}@test.invalid", PasswordHash = [], PasswordSalt = []
            }).ToArray();
            setup.Users.AddRange([mentor, admin, .. learners]);

            var level = new CertificateLevel { Code = "N3", CertificateType = new CertificateType { Name = "JLPT", IsActive = true } };
            var plan = new StudyCoursePlan
            {
                Name = "N3 Group Canonical",
                CertificateLevel = level,
                SessionType = "Group",
                Capacity = 4,
                SessionMinutes = 90,
                TotalSessions = 2,
                DurationMonths = 2,
                SessionsPerWeek = 3,
                Price = 500_000,
                IsPublished = true
            };
            var policyV1 = DefaultPolicy(version: 1, maxLearners: 4, cutoffMinutes: 60);
            var schedule = new MentorSchedule
            {
                MentorId = mentor.Id, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                CreatedAtUtc = DateTime.UtcNow
            };
            var rule = new MentorScheduleRule
            {
                Schedule = schedule, Weekday = 1, DaypartCode = "Evening",
                LocalStart = new TimeOnly(19, 0), LocalEnd = new TimeOnly(22, 0),
                EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            };
            setup.AddRange(level, plan, policyV1, schedule, rule);
            await setup.SaveChangesAsync();

            var nowSec = DateTime.UtcNow;
            var s1Start = new DateTime(nowSec.Year, nowSec.Month, nowSec.Day, nowSec.Hour, nowSec.Minute, nowSec.Second, DateTimeKind.Utc).AddDays(5);
            var s2Start = s1Start.AddDays(2);
            var s3MoveStart = s1Start.AddDays(4);
            MentorAvailability PolicySlot(DateTime start, string tag) => new()
            {
                MentorId = mentor.Id, IsApproved = true, StartTime = start, EndTime = start.AddMinutes(90),
                MaxLearners = 4, GroupPolicyId = policyV1.Id, PolicyVersion = policyV1.Version,
                SessionMinutesSnapshot = 90, RestMinutesSnapshot = 15, ScheduleRule = rule,
                GenerationKey = $"{tag}-{Guid.NewGuid():N}", LocalBusinessDate = DateOnly.FromDateTime(start),
                RoomId = Guid.NewGuid()
            };
            var slot1 = PolicySlot(s1Start, "s1");
            var slot2 = PolicySlot(s2Start, "s2");
            var moveSlotRestConflict = PolicySlot(s2Start.AddMinutes(95), "mconf");
            var moveSlotValid = PolicySlot(s3MoveStart, "mval");
            var overlapCohortSlot1 = PolicySlot(s1Start.AddMinutes(30), "ov1");
            var overlapCohortSlot2 = PolicySlot(s2Start.AddDays(1), "ov2");
            setup.MentorAvailabilities.AddRange(slot1, slot2, moveSlotRestConflict, moveSlotValid, overlapCohortSlot1, overlapCohortSlot2);
            await setup.SaveChangesAsync();

            int cohortId;
            await using (var adminDb = Context())
            {
                var saveRes = await CreateCourseService(adminDb, admin.Id, Role.Admin).SaveCohortAsync(
                    null, new SaveStudyCohort(plan.Id, "N3 Evening Class", mentor.Id, 4, [slot1.Id, slot2.Id], true), default);
                Assert.True(saveRes.IsSuccess, saveRes.ErrorMessage);
                cohortId = (int)saveRes.Result!.GetType().GetProperty("Id")!.GetValue(saveRes.Result)!;

                // Overlapping published cohort for the same mentor must fail.
                var overlapRes = await CreateCourseService(adminDb, admin.Id, Role.Admin).SaveCohortAsync(
                    null, new SaveStudyCohort(plan.Id, "Overlapping Cohort", mentor.Id, 4, [overlapCohortSlot1.Id, overlapCohortSlot2.Id], true), default);
                Assert.False(overlapRes.IsSuccess);
            }

            // 1. Default contract must fail-closed (no unpaid free pass / no implicit 1-1 credit deduction):
            //    without an entitlement the learner gets a Pending PayOS checkout at the published price.
            await using (var failClosedDb = Context())
            {
                var payOs = new Mock<IPaymentGatewayClient>();
                payOs.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/canonical", "plink", ""));
                payOs.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/canonical", "plink", ""));
                var learnerService = CreateCourseService(failClosedDb, learners[0].Id, Role.Learner, payments: payOs.Object);
                var checkout = await learnerService.EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.True(checkout.IsSuccess, checkout.ErrorMessage);
                var pending = await failClosedDb.StudyCourses.AsNoTracking().Include(x => x.Sessions)
                    .SingleAsync(x => x.CohortId == cohortId && x.LearnerId == learners[0].Id);
                Assert.Equal("Pending", pending.PaymentStatus);
                Assert.True(pending.Amount > 0);
                Assert.Empty(pending.Sessions);
                Assert.Equal(0, await failClosedDb.CohortEnrollmentEntitlements.CountAsync(x => x.StudyCohortId == cohortId));

                // Release the reserved seat so later steps see the original capacity.
                var released = await learnerService.CancelPendingEnrollmentAsync(pending.Id, default);
                Assert.True(released.IsSuccess, released.ErrorMessage);
            }

            // 2. 1-1 credit booking onto a Cohort session slot must be rejected (409).
            await using (var creditBookingDb = Context())
            {
                var subPlan = new SubscriptionPlan
                {
                    Name = "1-1 Plan", Price = 1000, DurationDays = 30, MeetingSessionCount = 1,
                    CertificateLevelId = level.Id, IsActive = true
                };
                creditBookingDb.Add(subPlan);
                await creditBookingDb.SaveChangesAsync();
                var sub = new UserSubscription
                {
                    UserId = learners[0].Id, SubscriptionPlanId = subPlan.Id, CertificateLevelId = level.Id,
                    MeetingSessionsIncluded = 1, MeetingSessionsUsed = 0, PaymentStatus = PaymentStatus.Paid,
                    StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(30), PurchasedAt = DateTime.UtcNow.AddDays(-1)
                };
                creditBookingDb.Add(sub);
                await creditBookingDb.SaveChangesAsync();

                var ledgerMock = new Mock<IMentorCreditLedgerService>();
                var bookingSvc = new MentorBookingService(
                    creditBookingDb,
                    new UnitOfWork(creditBookingDb),
                    Claim(learners[0].Id, Role.Learner),
                    ledgerMock.Object,
                    new AppSettings { FrontendBaseUrl = "https://test.invalid" },
                    Mock.Of<ILogger<MentorBookingService>>());
                var bookCohortSlotRes = await bookingSvc.BookSlotAsync(new BookIncludedMeetingRequest { MentorAvailabilityId = slot1.Id });
                Assert.False(bookCohortSlotRes.IsSuccess);
                Assert.Equal(409, bookCohortSlotRes.StatusCode);
            }

            var contract = new AllowDirectGroupEntitlementContract();
            // 3. Enroll first 2 learners sequentially.
            var keyL1 = Guid.NewGuid();
            await using (var l1Db = Context())
            {
                var r1 = await CreateCourseService(l1Db, learners[0].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(keyL1), default);
                Assert.True(r1.IsSuccess, r1.ErrorMessage);
                var r2 = await CreateCourseService(l1Db, learners[1].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.True(r2.IsSuccess, r2.ErrorMessage);

                // Retry L1 with exact same IdempotencyKey -> idempotent, zero duplicate enrollment.
                var r1Retry = await CreateCourseService(l1Db, learners[0].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(keyL1), default);
                Assert.True(r1Retry.IsSuccess, r1Retry.ErrorMessage);
                Assert.Equal(1, await l1Db.StudyCourses.CountAsync(x => x.CohortId == cohortId && x.LearnerId == learners[0].Id
                    && x.Status != "Cancelled"));

                // Same IdempotencyKey with different target plan/cohort -> 409 conflict.
                var wrongPayload = await CreateCourseService(l1Db, learners[0].Id, Role.Learner, contract)
                    .PurchaseAsync(new PurchaseCourse(plan.Id, keyL1, null), default);
                Assert.False(wrongPayload.IsSuccess);
                Assert.Equal(409, wrongPayload.StatusCode);
            }

            // Enroll learner 3 sequentially, then race learner 4 vs learner 5 for the 4th/last seat.
            await using (var l3Db = Context())
            {
                var r3 = await CreateCourseService(l3Db, learners[2].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.True(r3.IsSuccess, r3.ErrorMessage);
            }

            async Task<RikiPath.Application.Responses.ApiResponse<object>> RaceEnroll(int learnerId)
            {
                await using var db = Context();
                return await CreateCourseService(db, learnerId, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
            }

            var race = await Task.WhenAll(RaceEnroll(learners[3].Id), RaceEnroll(learners[4].Id));
            Assert.Equal(1, race.Count(x => x.IsSuccess));
            Assert.Equal(1, race.Count(x => !x.IsSuccess && x.StatusCode == 409));

            // 6th learner must also fail 409 because 4/4 seats are full.
            await using (var fullDb = Context())
            {
                var sixth = await CreateCourseService(fullDb, learners[5].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.False(sixth.IsSuccess);
                Assert.Equal(409, sixth.StatusCode);
                Assert.Equal(4, await fullDb.StudyCourses.CountAsync(x => x.CohortId == cohortId && x.Status == "Active"));
            }

            // 4. Policy version 2 (MaxLearners = 2) created later must NOT invalidate Cohort created under Policy version 1 (CapacitySnapshot = 4).
            await using (var policyDb = Context())
            {
                var v2Res = await new MentorGroupPolicyService(policyDb).CreateVersionAsync(new CreateMentorGroupPolicyRequest
                {
                    ExpectedCurrentVersion = 1,
                    EffectiveFromUtc = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).Date.AddDays(1), DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime,
                    MaxLearners = 2,
                    EnrollmentCutoffMinutesBeforeFirstSession = 60,
                    SessionMinutes = 90,
                    RestMinutes = 15,
                    DailySessionCap = 4,
                    PublicationHorizonDays = 30,
                    CancellationLeadHours = 24,
                    ReschedulingLeadHours = 24,
                    HomeworkDeadlineDays = 7,
                    PlacementAttemptLimit = 2,
                    RemediationCompletionThresholdPercent = 90m,
                    RemediationThresholdComparison = ">",
                    RequireOfficialExamEvidence = true,
                    RequireAdminRemediationReview = true,
                    KpiReportingPeriodDays = 30,
                    RecurringWeekdayPatterns = [[1, 3, 5]]
                }, admin.Id, default);
                Assert.True(v2Res.IsSuccess, v2Res.ErrorMessage);
                var existingCohort = await policyDb.StudyCohorts.AsNoTracking().SingleAsync(x => x.Id == cohortId);
                Assert.Equal(1, existingCohort.PolicyVersionSnapshot);
                Assert.Equal(4, existingCohort.CapacitySnapshot);
            }

            // 5. Cancel 1 learner's enrollment -> releases ONLY 1 seat, lets 6th learner enroll, does not touch other 3 active enrollments.
            await using (var cancelDb = Context())
            {
                var learner1Course = await cancelDb.StudyCourses.SingleAsync(x => x.CohortId == cohortId && x.LearnerId == learners[0].Id && x.Status == "Active");
                var cancelRes = await CreateCourseService(cancelDb, learners[0].Id, Role.Learner, contract)
                    .CancelCohortEnrollmentAsync(learner1Course.Id, new CancelCohortEnrollmentRequest("Schedule change"), default);
                Assert.True(cancelRes.IsSuccess, cancelRes.ErrorMessage);
                Assert.Equal(3, await cancelDb.StudyCourses.CountAsync(x => x.CohortId == cohortId && x.Status == "Active"));

                var sixthEnrollsAfterRelease = await CreateCourseService(cancelDb, learners[5].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.True(sixthEnrollsAfterRelease.IsSuccess, sixthEnrollsAfterRelease.ErrorMessage);
                Assert.Equal(4, await cancelDb.StudyCourses.CountAsync(x => x.CohortId == cohortId && x.Status == "Active"));
            }

            // 6. Reschedule Session 1 via Admin ReviewChangeAsync(Kind = Move, EntireClassConfirmed = true):
            // rest-conflict slot fails (409), valid slot moves StudyCohortSession + all 4 learners atomically with BookedCount intact (0).
            await using (var moveDb = Context())
            {
                var repCourse = await moveDb.StudyCourses.Include(x => x.Sessions)
                    .Where(x => x.CohortId == cohortId && x.Status == "Active")
                    .OrderBy(x => x.Id).FirstAsync();
                var targetSession = repCourse.Sessions.Single(s => s.AvailabilityId == slot1.Id);
                var reqRes = await CreateCourseService(moveDb, repCourse.LearnerId, Role.Learner, contract)
                    .RequestChangeAsync(repCourse.Id, new RequestCourseChange(targetSession.Id, "Move", "Need evening shift"), default);
                Assert.True(reqRes.IsSuccess, reqRes.ErrorMessage);
                var changeReq = await moveDb.CourseScheduleRequests.SingleAsync(x => x.CourseId == repCourse.Id && x.Status == "Pending");

                var restRejected = await CreateCourseService(moveDb, admin.Id, Role.Admin, contract)
                    .ReviewChangeAsync(repCourse.Id, changeReq.Id,
                        new ReviewCourseChange(true, "Try conflicting slot", moveSlotRestConflict.Id, EntireClassConfirmed: true), default);
                Assert.False(restRejected.IsSuccess);
                Assert.Equal(409, restRejected.StatusCode);

                var moved = await CreateCourseService(moveDb, admin.Id, Role.Admin, contract)
                    .ReviewChangeAsync(repCourse.Id, changeReq.Id,
                        new ReviewCourseChange(true, "Approved class move", moveSlotValid.Id, EntireClassConfirmed: true), default);
                Assert.True(moved.IsSuccess, moved.ErrorMessage);

                var refreshedEnrollments = await moveDb.StudyCourses.Include(x => x.Sessions)
                    .Where(x => x.CohortId == cohortId && x.Status == "Active").ToListAsync();
                Assert.Equal(4, refreshedEnrollments.Count);
                Assert.All(refreshedEnrollments, e =>
                {
                    var s = Assert.Single(e.Sessions, x => x.Number == 1);
                    Assert.Equal(moveSlotValid.Id, s.AvailabilityId);
                    Assert.Equal(moveSlotValid.StartTime, s.ScheduledAt);
                    Assert.Contains(moveSlotValid.RoomId.ToString(), s.MeetingUrl);
                });
                Assert.Equal(0, (await moveDb.MentorAvailabilities.AsNoTracking().SingleAsync(x => x.Id == slot1.Id)).BookedCount);
            }

            // 7. Release another seat and verify EnrollmentCutoffUtc blocks enrollment once inside cutoff window even with an open seat.
            await using (var cutoffDb = Context())
            {
                var oneEnrollment = await cutoffDb.StudyCourses.FirstAsync(x => x.CohortId == cohortId && x.Status == "Active");
                var cancelForCutoff = await CreateCourseService(cutoffDb, admin.Id, Role.Admin, contract)
                    .CancelCohortEnrollmentAsync(oneEnrollment.Id, new CancelCohortEnrollmentRequest("Free seat"), default);
                Assert.True(cancelForCutoff.IsSuccess);

                // Bring first remaining session inside the 60-minute cutoff window while still in the future (+20 min).
                var firstSlot = await cutoffDb.Set<StudyCohortSession>().Where(x => x.CohortId == cohortId)
                    .Select(x => x.Availability).OrderBy(x => x.StartTime).FirstAsync();
                firstSlot.StartTime = DateTime.UtcNow.AddMinutes(20);
                firstSlot.EndTime = firstSlot.StartTime.AddMinutes(90);
                await cutoffDb.SaveChangesAsync();

                var lateEnroll = await CreateCourseService(cutoffDb, learners[0].Id, Role.Learner, contract)
                    .EnrollCohortAsync(cohortId, new EnrollCohortRequest(Guid.NewGuid()), default);
                Assert.False(lateEnroll.IsSuccess);
                Assert.Equal(409, lateEnroll.StatusCode);
            }
        }
        finally
        {
            await DropSchemaAsync(schema, cs);
        }
    }

    [CoursePostgresFact]
    public async Task Cohort_shared_room_ids_authorize_four_learners_and_mentor_across_two_sessions_deny_stranger_and_keep_single_mentor_attendance()
    {
        var (schema, cs) = await CreateIsolatedSchemaAsync();
        AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        try
        {
            await using var setup = Context();
            var mentor = new UserAccount { Id = 2200, Role = Role.Mentor, FirstName = "Mentor", LastName = "Room", Email = "m2200@test.invalid", PasswordHash = [], PasswordSalt = [] };
            var learners = Enumerable.Range(2211, 4).Select(id => new UserAccount
            {
                Id = id, Role = Role.Learner, FirstName = "Learner", LastName = id.ToString(), Email = $"l{id}@test.invalid", PasswordHash = [], PasswordSalt = []
            }).ToArray();
            var stranger = new UserAccount { Id = 2299, Role = Role.Learner, FirstName = "Stranger", LastName = "X", Email = "s2299@test.invalid", PasswordHash = [], PasswordSalt = [] };
            setup.Users.AddRange([mentor, stranger, .. learners]);

            var level = new CertificateLevel { Code = "N4", CertificateType = new CertificateType { Name = "JLPT", IsActive = true } };
            var plan = new StudyCoursePlan
            {
                Name = "N4 Room Cohort", CertificateLevel = level, SessionType = "Group",
                Capacity = 4, SessionMinutes = 90, TotalSessions = 2, DurationMonths = 1, SessionsPerWeek = 3, Price = 300_000, IsPublished = true
            };
            var policy = DefaultPolicy(version: 1, maxLearners: 4, cutoffMinutes: 60);
            var schedule = new MentorSchedule
            {
                MentorId = mentor.Id, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                CreatedAtUtc = DateTime.UtcNow
            };
            var rule = new MentorScheduleRule
            {
                Schedule = schedule, Weekday = 1, DaypartCode = "Evening",
                LocalStart = new TimeOnly(19, 0), LocalEnd = new TimeOnly(22, 0),
                EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            };
            var s1Room = Guid.NewGuid();
            var s2Room = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var slot1 = new MentorAvailability
            {
                MentorId = mentor.Id, IsApproved = true, StartTime = now.AddMinutes(-5), EndTime = now.AddMinutes(85),
                MaxLearners = 4, GroupPolicy = policy, PolicyVersion = 1, SessionMinutesSnapshot = 90, RestMinutesSnapshot = 15,
                ScheduleRule = rule, GenerationKey = $"room-s1-{Guid.NewGuid():N}", LocalBusinessDate = DateOnly.FromDateTime(now), RoomId = s1Room
            };
            var slot2 = new MentorAvailability
            {
                MentorId = mentor.Id, IsApproved = true, StartTime = now.AddDays(2), EndTime = now.AddDays(2).AddMinutes(90),
                MaxLearners = 4, GroupPolicy = policy, PolicyVersion = 1, SessionMinutesSnapshot = 90, RestMinutesSnapshot = 15,
                ScheduleRule = rule, GenerationKey = $"room-s2-{Guid.NewGuid():N}", LocalBusinessDate = DateOnly.FromDateTime(now.AddDays(2)), RoomId = s2Room
            };
            var cohort = new StudyCohort
            {
                Name = "N4 Live Cohort", Plan = plan, MentorId = mentor.Id, Capacity = 4, CapacitySnapshot = 4,
                GroupPolicy = policy, PolicyVersionSnapshot = 1, Status = "Published", StartsAt = slot1.StartTime, EndsAt = slot2.EndTime,
                Sessions = [new() { Number = 1, Availability = slot1 }, new() { Number = 2, Availability = slot2 }]
            };
            foreach (var learner in learners)
            {
                var enrollment = new StudyCourse
                {
                    Cohort = cohort, LearnerId = learner.Id, Plan = plan, CertificateLevel = level, Name = "N4 Room Cohort",
                    SessionType = "Group", DurationMonths = 1, SessionsPerWeek = 3, TotalSessions = 2, SessionMinutes = 90,
                    PurchaseKey = Guid.NewGuid(), PaymentStatus = "Paid", Status = "Active", Amount = 0
                };
                enrollment.Sessions.Add(new StudyCourseSession { Number = 1, Availability = slot1, ScheduledAt = slot1.StartTime, MeetingUrl = $"/meeting-room/{s1Room}", Status = "Scheduled" });
                enrollment.Sessions.Add(new StudyCourseSession { Number = 2, Availability = slot2, ScheduledAt = slot2.StartTime, MeetingUrl = $"/meeting-room/{s2Room}", Status = "Scheduled" });
                cohort.Enrollments.Add(enrollment);
            }
            setup.Add(cohort);
            await setup.SaveChangesAsync();

            MentorAttendanceService Attendance(AppDbContext db, int userId, Role role) =>
                new(db, Claim(userId, role), Mock.Of<ILogger<MentorAttendanceService>>());

            // Session 1 live: Mentor joins, disconnects, reconnects -> single MentorAttendanceRecord with ReconnectionCount = 1.
            await using (var s1Db = Context())
            {
                var mentorSvc = Attendance(s1Db, mentor.Id, Role.Mentor);
                Assert.True(await mentorSvc.CanAccessRoomAsync(s1Room, mentor.Id));
                var conn1 = "m-conn-" + Guid.NewGuid();
                var conn2 = "m-conn-" + Guid.NewGuid();
                Assert.True((await mentorSvc.RecordJoinAsync(s1Room, mentor.Id, conn1)).IsSuccess);
                Assert.True((await mentorSvc.RecordLeaveAsync(s1Room, mentor.Id, conn1)).IsSuccess);
                Assert.True((await mentorSvc.RecordJoinAsync(s1Room, mentor.Id, conn2)).IsSuccess);

                foreach (var learner in learners)
                {
                    var learnerSvc = Attendance(s1Db, learner.Id, Role.Learner);
                    Assert.True(await learnerSvc.CanAccessRoomAsync(s1Room, learner.Id));
                    Assert.True((await learnerSvc.RecordJoinAsync(s1Room, learner.Id, $"l-conn-{learner.Id}-{Guid.NewGuid()}")).IsSuccess);
                }

                var strangerSvc = Attendance(s1Db, stranger.Id, Role.Learner);
                Assert.False(await strangerSvc.CanAccessRoomAsync(s1Room, stranger.Id));
                var strangerJoin = await strangerSvc.RecordJoinAsync(s1Room, stranger.Id, "stranger-conn");
                Assert.False(strangerJoin.IsSuccess);
                Assert.Equal(403, strangerJoin.StatusCode);

                var accessInfo = await new MentorMeetingAccessService(new UnitOfWork(s1Db)).GetMeetingAsync(s1Room, default);
                Assert.NotNull(accessInfo);
                Assert.Equal(4, accessInfo!.Capacity);
                Assert.Equal(mentor.Id, accessInfo.Mentor.UserId);
                Assert.Equal(4, accessInfo.Learners.Count);

                var mentorRecords = await s1Db.MentorAttendanceRecords.Where(r => r.RoomId == s1Room && r.UserId == mentor.Id).ToListAsync();
                var mentorRecord = Assert.Single(mentorRecords);
                Assert.Equal(1, mentorRecord.ReconnectionCount);
                Assert.Equal(5, await s1Db.MentorAttendanceRecords.CountAsync(r => r.RoomId == s1Room));
            }

            // Session 2 live window: advance time window into Session 2's distinct RoomId and verify same 4 learners + mentor enter while stranger is denied.
            await using (var s2Db = Context())
            {
                var dbSlot2 = await s2Db.MentorAvailabilities.SingleAsync(x => x.Id == slot2.Id);
                dbSlot2.StartTime = DateTime.UtcNow.AddMinutes(-2);
                dbSlot2.EndTime = dbSlot2.StartTime.AddMinutes(90);
                await s2Db.SaveChangesAsync();

                Assert.True((await Attendance(s2Db, mentor.Id, Role.Mentor).RecordJoinAsync(s2Room, mentor.Id, "s2-mentor")).IsSuccess);
                foreach (var learner in learners)
                    Assert.True((await Attendance(s2Db, learner.Id, Role.Learner).RecordJoinAsync(s2Room, learner.Id, $"s2-l-{learner.Id}")).IsSuccess);

                var strangerS2 = await Attendance(s2Db, stranger.Id, Role.Learner).RecordJoinAsync(s2Room, stranger.Id, "s2-stranger");
                Assert.False(strangerS2.IsSuccess);
                Assert.Equal(403, strangerS2.StatusCode);
                Assert.Equal(5, await s2Db.MentorAttendanceRecords.CountAsync(r => r.RoomId == s2Room));
            }
        }
        finally
        {
            await DropSchemaAsync(schema, cs);
        }
    }
}
