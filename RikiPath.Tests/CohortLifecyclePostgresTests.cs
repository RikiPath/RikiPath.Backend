//using System.Text.Json;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using RikiPath.Application;
//using RikiPath.Application.IClients;
//using RikiPath.Application.IServices;
//using RikiPath.Application.Requests.StudyCourses;
//using RikiPath.Application.Responses;
//using RikiPath.Application.Services;
//using RikiPath.Domain;
//using RikiPath.Domain.Entities;
//using RikiPath.Domain.Enums;
//using RikiPath.Infrastructure;
//using RikiPath.Infrastructure.Services;

//namespace RikiPath.Tests;

//// Dời toàn bộ lịch / hủy lớp trống: Mentor chính hoặc Admin yêu cầu, Admin duyệt từng lần (PostgreSQL, schema cô lập).
//public sealed class CohortLifecyclePostgresTests
//{
//    static CohortLifecyclePostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

//    private const int AdminId = 8001, MentorA = 8002, MentorOther = 8003, LearnerId = 8011;

//    private sealed class World
//    {
//        public required string Schema { get; init; }
//        public required string Cs { get; init; }
//        public int PlanId { get; set; }
//        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs).Options);
//    }

//    private static StudyCourseService Service(AppDbContext db, int userId, Role role)
//        => new(new UnitOfWork(db), Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role }),
//            Mock.Of<IPaymentGatewayClient>(MockBehavior.Strict), Mock.Of<IFileStorageService>(),
//            new AppSettings { FrontendBaseUrl = "https://test.invalid" }, groupPolicies: new MentorGroupPolicyService(db));

//    private static DateTime Utc(DateTime v) => new(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, DateTimeKind.Utc);
//    private static DateTime[] Future(double firstDays) =>
//        [Utc(DateTime.UtcNow.AddDays(firstDays)), Utc(DateTime.UtcNow.AddDays(firstDays + 2)), Utc(DateTime.UtcNow.AddDays(firstDays + 4))];
//    private static JsonElement J(ApiResponse<object> r) => JsonSerializer.SerializeToElement(r.Result);

//    private static async Task<World> CreateWorldAsync()
//    {
//        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!);
//        var w = new World { Schema = schema, Cs = cs };
//        await using var db = w.Db();
//        UserAccount User(int id, Role role) => new()
//        {
//            Id = id, Role = role, FirstName = role.ToString(), LastName = id.ToString(), Email = $"u{id}@test.invalid", PasswordHash = [], PasswordSalt = [],
//            MentorProfile = role == Role.Mentor ? new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3" } : null
//        };
//        db.Users.AddRange(User(AdminId, Role.Admin), User(MentorA, Role.Mentor), User(MentorOther, Role.Mentor), User(LearnerId, Role.Learner));
//        var level = new CertificateLevel { Code = "N3", CertificateType = new CertificateType { Name = "JLPT", IsActive = true }, SortOrder = 3 };
//        var plan = new StudyCoursePlan
//        {
//            Name = "N3 Group", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90, TotalSessions = 3,
//            DurationMonths = 2, SessionsPerWeek = 3, Price = 500_000, IsPublished = true
//        };
//        db.AddRange(level, plan, new MentorGroupPolicy
//        {
//            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60,
//            SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24,
//            ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90m,
//            RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30, RecurringWeekdayPatternsJson = "[[1,3,5]]",
//            LearnerTransferEnabled = true, MentorReplacementEnabled = true, MentorReplacementLeadHours = 24, CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
//        });
//        await db.SaveChangesAsync();
//        w.PlanId = plan.Id;
//        return w;
//    }

//    private static async Task<int> CreateCohortAsync(World w, int mentorId, DateTime[] starts, string status = "Published", string name = "Cohort")
//    {
//        await using var db = w.Db();
//        var cohort = new StudyCohort
//        {
//            PlanId = w.PlanId, Name = name, MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4, Status = status,
//            StartsAt = starts.Min(), EndsAt = starts.Max().AddMinutes(90)
//        };
//        for (var i = 0; i < starts.Length; i++)
//            cohort.Sessions.Add(new StudyCohortSession
//            {
//                Number = i + 1,
//                Availability = new MentorAvailability
//                {
//                    MentorId = mentorId, StartTime = starts[i], EndTime = starts[i].AddMinutes(90), MaxLearners = 4,
//                    IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
//                }
//            });
//        db.StudyCohorts.Add(cohort);
//        await db.SaveChangesAsync();
//        return cohort.Id;
//    }

//    private static async Task<int> EnrollAsync(World w, int cohortId, string paymentStatus, string status, DateTime? expiresAt = null, string? checkoutUrl = null)
//    {
//        await using var db = w.Db();
//        var cohort = await db.StudyCohorts.Include(c => c.Plan).Include(c => c.Sessions).ThenInclude(s => s.Availability).SingleAsync(c => c.Id == cohortId);
//        var course = new StudyCourse
//        {
//            CohortId = cohort.Id, MentorId = cohort.MentorId, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt, SessionType = "Group",
//            LearnerId = LearnerId, PurchaseKey = Guid.NewGuid(), PlanId = cohort.PlanId, Name = cohort.Plan.Name,
//            CertificateLevelId = cohort.Plan.CertificateLevelId, PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3,
//            TotalSessions = 3, SessionMinutes = 90, Amount = cohort.Plan.Price, PaymentStatus = paymentStatus, Status = status,
//            PaymentExpiresAt = expiresAt, CheckoutUrl = checkoutUrl, PaymentReference = $"REF-{Guid.NewGuid():N}"
//        };
//        foreach (var s in cohort.Sessions)
//            course.Sessions.Add(new StudyCourseSession { Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId });
//        db.StudyCourses.Add(course);
//        await db.SaveChangesAsync();
//        return course.Id;
//    }

//    private static async Task<ApiResponse<object>> RequestAsync(World w, int actor, Role role, int cohortId, string kind = "ShiftWeeks", int? weeks = 1, Guid? key = null)
//    {
//        await using var db = w.Db();
//        return await Service(db, actor, role).CreateCohortScheduleRequestAsync(cohortId,
//            new CreateCohortScheduleRequest(kind, weeks, "Chưa có học viên đăng ký, đề nghị dời lớp", key ?? Guid.NewGuid()), default);
//    }

//    private static async Task<ApiResponse<object>> ApproveAsync(World w, int requestId, int actor = AdminId, Role role = Role.Admin)
//    {
//        await using var db = w.Db();
//        return await Service(db, actor, role).ApproveCohortScheduleRequestAsync(requestId, new ApproveCohortScheduleRequest("Đồng ý"), default);
//    }

//    private static async Task<(List<int> SlotIds, List<DateTime> Starts, int Revision)> SnapshotAsync(World w, int cohortId)
//    {
//        await using var db = w.Db();
//        var c = await db.StudyCohorts.Include(x => x.Sessions).ThenInclude(s => s.Availability).SingleAsync(x => x.Id == cohortId);
//        var ordered = c.Sessions.OrderBy(s => s.Number).ToList();
//        return (ordered.Select(s => s.AvailabilityId).ToList(), ordered.Select(s => s.Availability.StartTime).ToList(), c.Revision);
//    }

//    private static async Task DropAsync(World w) => await CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

//    [CoursePostgresFact]
//    public async Task Zero_learner_shift_moves_every_session_exactly_one_week_retires_old_slots_and_bumps_revision()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohort = await CreateCohortAsync(w, MentorA, Future(3));
//            var before = await SnapshotAsync(w, cohort);
//            await using (var db = w.Db())
//            {
//                var preview = J(await Service(db, MentorA, Role.Mentor).CohortShiftPreviewAsync(cohort, 1, default));
//                Assert.True(preview.GetProperty("Eligible").GetBoolean(), preview.ToString());
//                Assert.Equal(3, preview.GetProperty("ProposedSessions").GetArrayLength());
//            }
//            var created = await RequestAsync(w, MentorA, Role.Mentor, cohort);
//            Assert.True(created.IsSuccess, created.ErrorMessage);
//            var rid = J(created).GetProperty("Id").GetInt32();
//            var approved = await ApproveAsync(w, rid);
//            Assert.True(approved.IsSuccess, approved.ErrorMessage);
//            var after = await SnapshotAsync(w, cohort);
//            Assert.Equal(3, after.Starts.Count);
//            for (var i = 0; i < 3; i++) Assert.Equal(before.Starts[i].AddDays(7), after.Starts[i]);
//            Assert.Empty(after.SlotIds.Intersect(before.SlotIds));
//            Assert.Equal(before.Revision + 1, after.Revision);
//            await using (var db = w.Db())
//            {
//                Assert.All(await db.MentorAvailabilities.Where(a => before.SlotIds.Contains(a.Id)).ToListAsync(), a => Assert.True(a.IsDeleted));
//                var c = await db.StudyCohorts.SingleAsync(x => x.Id == cohort);
//                Assert.Equal(before.Starts[0].AddDays(7), c.StartsAt);
//                var req = await db.CohortScheduleRequests.Include(x => x.History).SingleAsync(x => x.Id == rid);
//                Assert.Equal("Approved", req.Status); Assert.Equal(AdminId, req.ReviewedById);
//                Assert.Equal(new[] { "Requested", "Approved" }, req.History.OrderBy(h => h.Id).Select(h => h.Action).ToArray());
//                Assert.Equal(1, await db.MentorOutboxEvents.CountAsync(e => e.EventKey == $"cohort-schedule:{rid}:approved"));
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Shift_two_weeks_and_three_successive_requests_are_each_approved_separately_with_history()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohort = await CreateCohortAsync(w, MentorA, Future(3));
//            var before = await SnapshotAsync(w, cohort);
//            var first = J(await RequestAsync(w, MentorA, Role.Mentor, cohort, weeks: 2)).GetProperty("Id").GetInt32();
//            Assert.True((await ApproveAsync(w, first)).IsSuccess);
//            for (var i = 0; i < 2; i++)
//            {
//                var res = await RequestAsync(w, i == 0 ? MentorA : AdminId, i == 0 ? Role.Mentor : Role.Admin, cohort);
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//                Assert.True((await ApproveAsync(w, J(res).GetProperty("Id").GetInt32())).IsSuccess);
//            }
//            var after = await SnapshotAsync(w, cohort);
//            for (var i = 0; i < 3; i++) Assert.Equal(before.Starts[i].AddDays(28), after.Starts[i]);
//            Assert.Equal(before.Revision + 3, after.Revision);
//            await using var db = w.Db();
//            var rows = J(await Service(db, MentorA, Role.Mentor).CohortScheduleRequestsAsync(cohort, default)).EnumerateArray().ToList();
//            Assert.Equal(3, rows.Count);
//            Assert.All(rows, r => Assert.Equal("Approved", r.GetProperty("Status").GetString()));
//            Assert.All(rows, r => Assert.Equal(2, r.GetProperty("History").GetArrayLength()));
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Mentor_busy_in_shifted_week_is_refused_with_canonical_code_and_nothing_changes()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var starts = Future(3);
//            var cohort = await CreateCohortAsync(w, MentorA, starts);
//            await CreateCohortAsync(w, MentorA, starts.Select(s => s.AddDays(7)).ToArray(), name: "Busy");
//            var before = await SnapshotAsync(w, cohort);
//            var rid = J(await RequestAsync(w, MentorA, Role.Mentor, cohort)).GetProperty("Id").GetInt32();
//            var res = await ApproveAsync(w, rid);
//            Assert.Equal(409, res.StatusCode); Assert.Equal("MENTOR_SCHEDULE_CONFLICT", res.ErrorCode);
//            var after = await SnapshotAsync(w, cohort);
//            Assert.Equal(before.SlotIds, after.SlotIds); Assert.Equal(before.Revision, after.Revision);
//            await using var db = w.Db();
//            Assert.Equal("Pending", (await db.CohortScheduleRequests.SingleAsync(x => x.Id == rid)).Status);
//            Assert.False(await db.MentorAvailabilities.AnyAsync(a => before.SlotIds.Contains(a.Id) && a.IsDeleted));
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Confirmed_learner_blocks_request_and_approval()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var full = await CreateCohortAsync(w, MentorA, Future(3));
//            await EnrollAsync(w, full, "Paid", "Active");
//            var denied = await RequestAsync(w, MentorA, Role.Mentor, full);
//            Assert.Equal(409, denied.StatusCode); Assert.Equal("COHORT_HAS_CONFIRMED_LEARNERS", denied.ErrorCode);
//            // Learner enrolls after the request was filed -> approval re-evaluates and refuses.
//            var later = await CreateCohortAsync(w, MentorA, Future(20), name: "Later");
//            var rid = J(await RequestAsync(w, MentorA, Role.Mentor, later)).GetProperty("Id").GetInt32();
//            await EnrollAsync(w, later, "Paid", "Active");
//            var res = await ApproveAsync(w, rid);
//            Assert.Equal(409, res.StatusCode); Assert.Equal("COHORT_HAS_CONFIRMED_LEARNERS", res.ErrorCode);
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Payment_holds_and_reviews_block_approval_but_request_is_created_with_blockers_and_admin_notice()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohort = await CreateCohortAsync(w, MentorA, Future(3));
//            var hold = await EnrollAsync(w, cohort, "Pending", "PendingPayment", DateTime.UtcNow.AddMinutes(15), "https://pay.invalid/x");
//            var created = await RequestAsync(w, MentorA, Role.Mentor, cohort);
//            Assert.True(created.IsSuccess, created.ErrorMessage);
//            var rid = J(created).GetProperty("Id").GetInt32();
//            Assert.NotEmpty(J(created).GetProperty("Preview").GetProperty("Blockers").EnumerateArray());
//            await using (var db = w.Db())
//            {
//                var evt = await db.MentorOutboxEvents.SingleAsync(e => e.EventKey == $"cohort-schedule:{rid}:payment-blocker");
//                Assert.Contains("Lớp có giao dịch cần đối soát trước khi dời lịch", evt.PayloadJson);
//            }
//            var res = await ApproveAsync(w, rid);
//            Assert.Equal(409, res.StatusCode); Assert.Equal("COHORT_PAYMENT_PENDING", res.ErrorCode);
//            // Expired 2h ago but not reconciled -> still blocked; >24h -> no longer blocks.
//            await using (var db = w.Db())
//            {
//                var c = await db.StudyCourses.SingleAsync(x => x.Id == hold);
//                c.PaymentExpiresAt = DateTime.UtcNow.AddHours(-2); await db.SaveChangesAsync();
//            }
//            Assert.Equal("COHORT_PAYMENT_PENDING", (await ApproveAsync(w, rid)).ErrorCode);
//            await using (var db = w.Db())
//            {
//                var c = await db.StudyCourses.SingleAsync(x => x.Id == hold);
//                c.PaymentExpiresAt = DateTime.UtcNow.AddHours(-30); await db.SaveChangesAsync();
//            }
//            Assert.True((await ApproveAsync(w, rid)).IsSuccess);
//            await using (var db = w.Db())
//                Assert.Equal("Pending", (await db.StudyCourses.SingleAsync(x => x.Id == hold)).PaymentStatus); // payment evidence kept

//            var reviewed = await CreateCohortAsync(w, MentorA, Future(40), name: "Review");
//            await EnrollAsync(w, reviewed, "Paid", "PaymentReview");
//            var rid2 = J(await RequestAsync(w, MentorA, Role.Mentor, reviewed)).GetProperty("Id").GetInt32();
//            var res2 = await ApproveAsync(w, rid2);
//            Assert.Equal(409, res2.StatusCode); Assert.Equal("COHORT_PAYMENT_REVIEW_OPEN", res2.ErrorCode);
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Authorization_duplicates_idempotency_rejection_and_withdrawal()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohort = await CreateCohortAsync(w, MentorA, Future(3));
//            Assert.Equal(403, (await RequestAsync(w, MentorOther, Role.Mentor, cohort)).StatusCode);
//            var key = Guid.NewGuid();
//            var rid = J(await RequestAsync(w, MentorA, Role.Mentor, cohort, key: key)).GetProperty("Id").GetInt32();
//            var retry = J(await RequestAsync(w, MentorA, Role.Mentor, cohort, key: key));
//            Assert.Equal(rid, retry.GetProperty("Id").GetInt32()); Assert.True(retry.GetProperty("Idempotent").GetBoolean());
//            var dup = await RequestAsync(w, AdminId, Role.Admin, cohort);
//            Assert.Equal(409, dup.StatusCode); Assert.Equal("COHORT_SCHEDULE_REQUEST_PENDING", dup.ErrorCode);
//            Assert.Equal(403, (await ApproveAsync(w, rid, MentorA, Role.Mentor)).StatusCode);
//            await using (var db = w.Db())
//            {
//                var missing = await Service(db, AdminId, Role.Admin).RejectCohortScheduleRequestAsync(rid, new RejectCohortScheduleRequest("  "), default);
//                Assert.Equal(422, missing.StatusCode); Assert.Equal("COHORT_SCHEDULE_REQUEST_REASON_REQUIRED", missing.ErrorCode);
//                Assert.True((await Service(db, AdminId, Role.Admin).RejectCohortScheduleRequestAsync(rid, new RejectCohortScheduleRequest("Chờ thêm học viên"), default)).IsSuccess);
//            }
//            Assert.Equal(409, (await ApproveAsync(w, rid)).StatusCode);
//            var rid2 = J(await RequestAsync(w, MentorA, Role.Mentor, cohort)).GetProperty("Id").GetInt32();
//            Assert.True((await ApproveAsync(w, rid2)).IsSuccess);
//            Assert.True(J(await ApproveAsync(w, rid2)).GetProperty("Idempotent").GetBoolean());
//            var rid3 = J(await RequestAsync(w, MentorA, Role.Mentor, cohort)).GetProperty("Id").GetInt32();
//            await using (var db = w.Db())
//            {
//                Assert.Equal(403, (await Service(db, AdminId, Role.Admin).WithdrawCohortScheduleRequestAsync(rid3, default)).StatusCode);
//                Assert.True((await Service(db, MentorA, Role.Mentor).WithdrawCohortScheduleRequestAsync(rid3, default)).IsSuccess);
//                Assert.Equal(403, (await Service(db, MentorOther, Role.Mentor).CohortScheduleRequestsAsync(cohort, default)).StatusCode);
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Empty_cohort_cancellation_frees_mentor_schedule_and_draft_shift_does_not_publish()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var starts = Future(3);
//            var cohort = await CreateCohortAsync(w, MentorA, starts);
//            var rid = J(await RequestAsync(w, MentorA, Role.Mentor, cohort, kind: "CancelEmptyCohort", weeks: null)).GetProperty("Id").GetInt32();
//            Assert.True((await ApproveAsync(w, rid)).IsSuccess);
//            await using (var db = w.Db())
//            {
//                var c = await db.StudyCohorts.Include(x => x.Sessions).ThenInclude(s => s.Availability).SingleAsync(x => x.Id == cohort);
//                Assert.Equal("Cancelled", c.Status);
//                Assert.All(c.Sessions, s => Assert.False(s.Availability.IsDeleted));
//                var report = await new MentorTeachingConflictService(new UnitOfWork(db)).CheckAsync(MentorA,
//                    [new ProposedTeachingInterval(starts[0], starts[0].AddMinutes(90), 1)], null, default);
//                Assert.True(report.IsValid);
//            }
//            // Approval never publishes: a shifted Draft cohort stays Draft and invisible to learners (SaveCohortAsync publishes).
//            var draft = await CreateCohortAsync(w, MentorA, Future(30), status: "Draft", name: "Draft");
//            var rid2 = J(await RequestAsync(w, AdminId, Role.Admin, draft)).GetProperty("Id").GetInt32();
//            Assert.True((await ApproveAsync(w, rid2)).IsSuccess);
//            await using (var db = w.Db())
//            {
//                Assert.Equal("Draft", (await db.StudyCohorts.SingleAsync(x => x.Id == draft)).Status);
//                var visible = J(await Service(db, LearnerId, Role.Learner).CohortsAsync(default)).EnumerateArray();
//                Assert.DoesNotContain(visible, x => x.GetProperty("Id").GetInt32() == draft);
//            }
//        }
//        finally { await DropAsync(w); }
//    }
//}
