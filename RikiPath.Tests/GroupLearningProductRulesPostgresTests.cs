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

//// Quy tắc sản phẩm chính thức của Học nhóm (Product Owner) trên PostgreSQL thật, schema cô lập trong RIKIPATH_COURSE_TEST_DB:
//// 1 Mentor chính, 1..4 học viên (không chờ đủ 4), khóa 10 tuần, buổi 90 phút, cường độ 2/3/4 buổi/tuần,
//// giữ chỗ 10 phút, không chuyển lớp/không tự hủy sau khai giảng.
//public sealed class GroupLearningProductRulesPostgresTests
//{
//    static GroupLearningProductRulesPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

//    private const int AdminId = 9101, MentorA = 9102, MentorB = 9103;
//    private static readonly int[] Learners = [9111, 9112, 9113, 9114, 9115, 9116];

//    private sealed class World
//    {
//        public required string Schema { get; init; }
//        public required string Cs { get; init; }
//        public int LevelN3 { get; set; }
//        public int LevelN4 { get; set; }
//        public int PolicyId { get; set; }
//        public long RuleId { get; set; }
//        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs).Options);
//    }

//    private static Mock<IPaymentGatewayClient> Gateway()
//    {
//        var gateway = new Mock<IPaymentGatewayClient>();
//        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
//                It.IsAny<string>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/rules", "plink", ""));
//        gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
//                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
//            .ReturnsAsync(new PaymentLinkResult("https://pay.test.invalid/rules", "plink", ""));
//        return gateway;
//    }

//    private static StudyCourseService Service(AppDbContext db, int userId, Role role, IPaymentGatewayClient? payments = null)
//        => new(new UnitOfWork(db), Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role }),
//            payments ?? Gateway().Object, Mock.Of<IFileStorageService>(), new AppSettings { FrontendBaseUrl = "https://test.invalid" },
//            groupPolicies: new MentorGroupPolicyService(db));

//    private static JsonElement J(ApiResponse<object> r) => JsonSerializer.SerializeToElement(r.Result);
//    private static int IdOf(ApiResponse<object> r) => J(r).GetProperty("Id").GetInt32();

//    private static async Task<World> CreateWorldAsync()
//    {
//        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(
//            Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!);
//        var w = new World { Schema = schema, Cs = cs };
//        await using var db = w.Db();
//        UserAccount User(int id, Role role, string name, bool mentor = false) => new()
//        {
//            Id = id, Role = role, FirstName = name, LastName = id.ToString(), Email = $"u{id}@test.invalid", PasswordHash = [], PasswordSalt = [],
//            MentorProfile = mentor ? new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3" } : null
//        };
//        db.Users.AddRange(User(AdminId, Role.Admin, "Admin"), User(MentorA, Role.Mentor, "Sora", true), User(MentorB, Role.Mentor, "Ren", true));
//        db.Users.AddRange(Learners.Select(id => User(id, Role.Learner, "Learner")));
//        var type = new CertificateType { Name = "JLPT", IsActive = true };
//        var n3 = new CertificateLevel { Code = "N3", CertificateType = type, SortOrder = 3 };
//        var n4 = new CertificateLevel { Code = "N4", CertificateType = type, SortOrder = 2 };
//        var policy = new MentorGroupPolicy
//        {
//            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60,
//            SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, WeeklySessionCap = 0, PublicationHorizonDays = 30,
//            CancellationLeadHours = 24, ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2,
//            RemediationCompletionThresholdPercent = 90m, RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30,
//            RecurringWeekdayPatternsJson = "[[1,3,5]]", LearnerTransferEnabled = true, MentorReplacementEnabled = true,
//            MentorReplacementLeadHours = 24, CourseDurationWeeks = 10, AllowedSessionsPerWeekCsv = "2,3,4",
//            DefaultSessionsPerWeek = 3, CheckoutHoldMinutes = 10, MinLearnersToStart = 1, CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
//        };
//        var schedule = new MentorSchedule
//        {
//            MentorId = MentorA, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date), CreatedAtUtc = DateTime.UtcNow
//        };
//        var rule = new MentorScheduleRule
//        {
//            Schedule = schedule, Weekday = 1, DaypartCode = "Evening", LocalStart = new TimeOnly(18, 0), LocalEnd = new TimeOnly(22, 0),
//            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
//        };
//        db.AddRange(n3, n4, policy, schedule, rule);
//        await db.SaveChangesAsync();
//        w.LevelN3 = n3.Id; w.LevelN4 = n4.Id; w.PolicyId = policy.Id; w.RuleId = rule.Id;
//        return w;
//    }

//    private static Task DropAsync(World w) => CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

//    // Thứ 2 tuần tới (00:00 UTC = 07:00 giờ Việt Nam cùng ngày), ít nhất 8 ngày sau hôm nay.
//    private static DateTime BaseMondayUtc()
//    {
//        var d = DateTime.UtcNow.Date.AddDays(8);
//        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
//        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
//    }

//    private static int[] DayOffsets(int perWeek) => perWeek switch
//    {
//        2 => [0, 2],          // Thứ 2, Thứ 4
//        3 => [0, 2, 4],       // Thứ 2, Thứ 4, Thứ 6
//        4 => [0, 1, 3, 5],    // Thứ 2, Thứ 3, Thứ 5, Thứ 7
//        _ => throw new ArgumentOutOfRangeException(nameof(perWeek))
//    };

//    // Lịch nhiều tuần liên tiếp; giờ UTC < 17 để cùng ngày theo giờ Việt Nam.
//    private static List<DateTime> WeeklyStarts(DateTime mondayUtc, int weeks, int perWeek, int hourUtc, Func<int, bool>? skipWeek = null)
//    {
//        var result = new List<DateTime>();
//        for (var week = 0; week < weeks; week++)
//        {
//            if (skipWeek?.Invoke(week) == true) continue;
//            foreach (var day in DayOffsets(perWeek))
//                result.Add(mondayUtc.AddDays(7 * week + day).AddHours(hourUtc));
//        }
//        return result;
//    }

//    // Khung giờ sinh theo chính sách nhóm đã duyệt (đủ điều kiện để Admin chọn khi tạo lớp).
//    private static async Task<List<int>> SlotsAsync(World w, int mentorId, IEnumerable<DateTime> starts, int minutes = 90)
//    {
//        await using var db = w.Db();
//        var rows = starts.Select(start => new MentorAvailability
//        {
//            MentorId = mentorId, IsApproved = true, StartTime = start, EndTime = start.AddMinutes(minutes), MaxLearners = 4,
//            GroupPolicyId = w.PolicyId, PolicyVersion = 1, SessionMinutesSnapshot = minutes, RestMinutesSnapshot = 15,
//            MentorScheduleRuleId = w.RuleId, GenerationKey = $"rules-{Guid.NewGuid():N}",
//            LocalBusinessDate = DateOnly.FromDateTime(start.AddHours(7)), RoomId = Guid.NewGuid(), AdminName = "QA"
//        }).ToList();
//        db.MentorAvailabilities.AddRange(rows);
//        await db.SaveChangesAsync();
//        return rows.Select(x => x.Id).ToList();
//    }

//    private static SaveCoursePlan GroupPlan(int levelId, int perWeek, int? totalSessions = null, int? minutes = 90, int capacity = 4,
//        string name = "JLPT nhóm")
//        => new(name, levelId, 3, perWeek, minutes, totalSessions ?? 10 * perWeek, 60, 1_500_000m, true, 1, "Group", capacity);

//    private static async Task<int> CreatePlanAsync(World w, SaveCoursePlan plan)
//    {
//        await using var db = w.Db();
//        var res = await Service(db, AdminId, Role.Admin).SavePlanAsync(null, plan, default);
//        Assert.True(res.IsSuccess, res.ErrorMessage);
//        return IdOf(res);
//    }

//    private static async Task<ApiResponse<object>> PublishAsync(World w, int planId, int mentorId, List<int> slotIds, string name, int capacity = 4)
//    {
//        await using var db = w.Db();
//        return await Service(db, AdminId, Role.Admin).SaveCohortAsync(null, new SaveStudyCohort(planId, name, mentorId, capacity, slotIds, true), default);
//    }

//    private static async Task<int> StandardCohortAsync(World w, int levelId, int perWeek, int mentorId, int hourUtc, string name)
//    {
//        var plan = await CreatePlanAsync(w, GroupPlan(levelId, perWeek, name: name + " plan"));
//        var slots = await SlotsAsync(w, mentorId, WeeklyStarts(BaseMondayUtc(), 10, perWeek, hourUtc));
//        var res = await PublishAsync(w, plan, mentorId, slots, name);
//        Assert.True(res.IsSuccess, res.ErrorMessage);
//        return IdOf(res);
//    }

//    private static async Task<ApiResponse<object>> EnrollAsync(World w, int learnerId, int cohortId, Guid? key = null)
//    {
//        await using var db = w.Db();
//        return await Service(db, learnerId, Role.Learner).EnrollCohortAsync(cohortId, new EnrollCohortRequest(key ?? Guid.NewGuid()), default);
//    }

//    [CoursePostgresFact]
//    public async Task Group_plan_accepts_only_10_week_intensities_of_90_minutes_with_capacity_1_to_policy_max()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var light = await CreatePlanAsync(w, GroupPlan(w.LevelN3, 2, name: "Nhẹ"));
//            var standard = await CreatePlanAsync(w, GroupPlan(w.LevelN3, 3, name: "Tiêu chuẩn"));
//            var advanced = await CreatePlanAsync(w, GroupPlan(w.LevelN3, 4, name: "Nâng cao"));
//            var single = await CreatePlanAsync(w, GroupPlan(w.LevelN3, 3, capacity: 1, name: "Một học viên"));

//            await using (var db = w.Db())
//            {
//                var admin = Service(db, AdminId, Role.Admin);
//                async Task Expect(SaveCoursePlan plan, string code)
//                {
//                    var res = await admin.SavePlanAsync(null, plan, default);
//                    Assert.False(res.IsSuccess);
//                    Assert.Equal(422, res.StatusCode);
//                    Assert.Equal(code, res.ErrorCode);
//                }
//                await Expect(GroupPlan(w.LevelN3, 3, totalSessions: 25), "COURSE_PLAN_INVALID_DURATION");
//                await Expect(GroupPlan(w.LevelN3, 5), "COURSE_PLAN_INVALID_INTENSITY");
//                await Expect(GroupPlan(w.LevelN3, 3, minutes: 60), "COURSE_PLAN_INVALID_DURATION");
//                await Expect(GroupPlan(w.LevelN3, 3, capacity: 5), "COURSE_PLAN_INVALID_CAPACITY");
//                // Gói 1-1 đã ngừng hỗ trợ.
//                var individual = await admin.SavePlanAsync(null, new SaveCoursePlan("1-1", w.LevelN3, 3, 2, 60, 7, 60, 900_000m, true), default);
//                Assert.False(individual.IsSuccess);
//                Assert.Equal(400, individual.StatusCode);
//                Assert.Equal("INDIVIDUAL_PLAN_DISCONTINUED", individual.ErrorCode);
//            }

//            await using (var db = w.Db())
//            {
//                var plans = J(await Service(db, Learners[0], Role.Learner).PlansAsync(default)).EnumerateArray().ToList();
//                JsonElement Row(int id) => plans.Single(p => p.GetProperty("Id").GetInt32() == id);
//                Assert.Equal("Light", Row(light).GetProperty("Intensity").GetString());
//                Assert.Equal("Nhẹ", Row(light).GetProperty("IntensityLabel").GetString());
//                Assert.False(Row(light).GetProperty("IsRecommendedIntensity").GetBoolean());
//                Assert.Equal(30m, Row(light).GetProperty("TotalTeachingHours").GetDecimal());
//                Assert.Equal(20, Row(light).GetProperty("TotalSessions").GetInt32());
//                Assert.Equal("Standard", Row(standard).GetProperty("Intensity").GetString());
//                Assert.Equal("Tiêu chuẩn", Row(standard).GetProperty("IntensityLabel").GetString());
//                Assert.True(Row(standard).GetProperty("IsRecommendedIntensity").GetBoolean());
//                Assert.Equal(45m, Row(standard).GetProperty("TotalTeachingHours").GetDecimal());
//                Assert.Equal("Advanced", Row(advanced).GetProperty("Intensity").GetString());
//                Assert.Equal("Nâng cao", Row(advanced).GetProperty("IntensityLabel").GetString());
//                Assert.Equal(60m, Row(advanced).GetProperty("TotalTeachingHours").GetDecimal());
//                Assert.Equal(1, Row(single).GetProperty("Capacity").GetInt32());
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Whole_course_publish_succeeds_for_10_consecutive_weeks_at_2_3_and_4_sessions_per_week()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            // Mỗi cường độ một khung giờ khác nhau trong ngày (08:00, 12:00, 19:00 giờ Việt Nam) để không trùng lịch Mentor.
//            foreach (var (perWeek, hourUtc) in new[] { (2, 1), (3, 5), (4, 12) })
//            {
//                var plan = await CreatePlanAsync(w, GroupPlan(w.LevelN3, perWeek, name: $"Plan {perWeek}"));
//                var slots = await SlotsAsync(w, MentorA, WeeklyStarts(BaseMondayUtc(), 10, perWeek, hourUtc));
//                Assert.Equal(10 * perWeek, slots.Count);
//                var res = await PublishAsync(w, plan, MentorA, slots, $"Lớp {perWeek} buổi/tuần");
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//                await using var db = w.Db();
//                var cohort = await db.StudyCohorts.Include(c => c.Sessions).SingleAsync(c => c.Id == IdOf(res));
//                Assert.Equal("Published", cohort.Status);
//                Assert.Equal(10 * perWeek, cohort.Sessions.Count);
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Missing_week_or_extra_session_in_a_week_is_rejected_as_incomplete_schedule_naming_the_week()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var plan = await CreatePlanAsync(w, GroupPlan(w.LevelN3, 3));
//            var monday = BaseMondayUtc();

//            // Bỏ trống tuần 5 (30 buổi kéo sang tuần 11).
//            var gap = await SlotsAsync(w, MentorA, WeeklyStarts(monday, 11, 3, 12, skipWeek: week => week == 4));
//            Assert.Equal(30, gap.Count);
//            var missing = await PublishAsync(w, plan, MentorA, gap, "Thiếu tuần");
//            Assert.Equal(422, missing.StatusCode);
//            Assert.Equal("COHORT_SCHEDULE_INCOMPLETE", missing.ErrorCode);
//            Assert.Contains("Tuần 5", missing.ErrorMessage);

//            // Tuần 3 có 4 buổi, tuần 8 chỉ 2 buổi (tổng vẫn 30).
//            var starts = WeeklyStarts(monday, 10, 3, 5);
//            starts.Remove(monday.AddDays(7 * 7 + 4).AddHours(5));
//            starts.Add(monday.AddDays(7 * 2 + 5).AddHours(5));
//            var extra = await SlotsAsync(w, MentorB, starts);
//            Assert.Equal(30, extra.Count);
//            var extraRes = await PublishAsync(w, plan, MentorB, extra, "Thừa buổi");
//            Assert.Equal(422, extraRes.StatusCode);
//            Assert.Equal("COHORT_SCHEDULE_INCOMPLETE", extraRes.ErrorCode);
//            Assert.Contains("Tuần 3", extraRes.ErrorMessage);

//            // Sĩ số lớp vượt chính sách -> từ chối; 1 học viên -> hợp lệ.
//            var ok = await SlotsAsync(w, MentorB, WeeklyStarts(monday, 10, 3, 12));
//            var tooMany = await PublishAsync(w, plan, MentorB, ok, "Quá sĩ số", capacity: 5);
//            Assert.Equal("COURSE_PLAN_INVALID_CAPACITY", tooMany.ErrorCode);
//            var one = await PublishAsync(w, plan, MentorB, ok, "Một học viên", capacity: 1);
//            Assert.True(one.IsSuccess, one.ErrorMessage);

//            await using var db = w.Db();
//            Assert.Equal(1, await db.StudyCohorts.CountAsync());
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task One_paid_learner_keeps_the_class_active_and_joinable_without_a_minimum_of_two()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohortId = await StandardCohortAsync(w, w.LevelN3, 2, MentorA, 12, "Lớp một người");
//            var checkout = await EnrollAsync(w, Learners[0], cohortId);
//            Assert.True(checkout.IsSuccess, checkout.ErrorMessage);
//            var courseId = IdOf(checkout);
//            await using (var db = w.Db())
//            {
//                var amount = (await db.StudyCourses.SingleAsync(c => c.Id == courseId)).Amount;
//                var confirmed = await Service(db, Learners[0], Role.Learner).ConfirmPaymentAsync(courseId,
//                    new PaymentCallbackResult(true, true, 700_000_000L + courseId, amount, $"COURSE {courseId}"), "PAYOS-REF-1", default);
//                Assert.True(confirmed.IsSuccess, confirmed.ErrorMessage);
//            }
//            Guid roomId;
//            await using (var db = w.Db())
//            {
//                var course = await db.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == courseId);
//                Assert.Equal("Active", course.Status);
//                Assert.Equal("Paid", course.PaymentStatus);
//                Assert.Equal(20, course.Sessions.Count);
//                Assert.All(course.Sessions, s => Assert.Equal("Scheduled", s.Status));
//                var cohort = await db.StudyCohorts.Include(c => c.Sessions).ThenInclude(s => s.Availability).SingleAsync(c => c.Id == cohortId);
//                Assert.Equal("Published", cohort.Status);
//                // Đưa buổi 1 vào khung giờ vào phòng (bắt đầu sau 5 phút) để kiểm tra quyền vào phòng với một học viên.
//                var first = cohort.Sessions.Single(s => s.Number == 1).Availability;
//                first.StartTime = DateTime.UtcNow.AddMinutes(5); first.EndTime = first.StartTime.AddMinutes(90);
//                course.Sessions.Single(s => s.Number == 1).ScheduledAt = first.StartTime;
//                roomId = first.RoomId;
//                await db.SaveChangesAsync();
//            }
//            await using (var db = w.Db())
//            {
//                var meeting = await new MentorMeetingAccessService(new UnitOfWork(db)).GetMeetingAsync(roomId, default);
//                Assert.NotNull(meeting);
//                Assert.Equal(Learners[0], Assert.Single(meeting!.Learners).UserId);
//            }
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Fifth_learner_is_rejected_as_full_and_the_hold_is_10_minutes_recorded_in_the_order_snapshot()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var cohortId = await StandardCohortAsync(w, w.LevelN3, 2, MentorA, 12, "Lớp bốn chỗ");
//            var before = DateTime.UtcNow;
//            var first = await EnrollAsync(w, Learners[0], cohortId);
//            var after = DateTime.UtcNow;
//            Assert.True(first.IsSuccess, first.ErrorMessage);
//            await using (var db = w.Db())
//            {
//                var order = await db.StudyCourses.SingleAsync(c => c.Id == IdOf(first));
//                Assert.Equal("Pending", order.PaymentStatus);
//                Assert.InRange(order.PaymentExpiresAt!.Value, before.AddMinutes(10).AddSeconds(-5), after.AddMinutes(10).AddSeconds(5));
//                using var snapshot = JsonDocument.Parse(order.OrderSnapshot!);
//                Assert.Equal(10, snapshot.RootElement.GetProperty("holdMinutes").GetInt32());
//            }
//            for (var i = 1; i < 4; i++)
//            {
//                var res = await EnrollAsync(w, Learners[i], cohortId);
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//            }
//            var fifth = await EnrollAsync(w, Learners[4], cohortId);
//            Assert.Equal(409, fifth.StatusCode);
//            Assert.Equal("COHORT_FULL", fifth.ErrorCode);
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Hold_is_capped_by_the_enrollment_cutoff_and_an_expired_hold_allows_a_new_order_with_a_new_key()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            // Lớp (dữ liệu cũ) có buổi đầu sau 65 phút; khóa đăng ký 60 phút trước buổi đầu => giữ chỗ tối đa ~5 phút.
//            int cohortId;
//            DateTime cutoff;
//            await using (var db = w.Db())
//            {
//                var plan = new StudyCoursePlan
//                {
//                    Name = "Legacy one-session", CertificateLevelId = w.LevelN3, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
//                    TotalSessions = 1, DurationMonths = 1, SessionsPerWeek = 1, Price = 500_000, IsPublished = true
//                };
//                db.StudyCoursePlans.Add(plan);
//                await db.SaveChangesAsync();
//                var start = DateTime.UtcNow.AddMinutes(65);
//                var slotId = (await SlotsAsync(w, MentorA, [start]))[0];
//                var cohort = new StudyCohort
//                {
//                    PlanId = plan.Id, Name = "Sắp khóa", MentorId = MentorA, Capacity = 4, CapacitySnapshot = 4, Status = "Published",
//                    GroupPolicyId = w.PolicyId, PolicyVersionSnapshot = 1, StartsAt = start, EndsAt = start.AddMinutes(90),
//                    Sessions = [new StudyCohortSession { Number = 1, AvailabilityId = slotId }]
//                };
//                db.StudyCohorts.Add(cohort);
//                await db.SaveChangesAsync();
//                cohortId = cohort.Id;
//                cutoff = (await db.MentorAvailabilities.SingleAsync(a => a.Id == slotId)).StartTime.AddMinutes(-60);
//            }
//            var held = await EnrollAsync(w, Learners[0], cohortId, Guid.NewGuid());
//            Assert.True(held.IsSuccess, held.ErrorMessage);
//            var heldId = IdOf(held);
//            await using (var db = w.Db())
//            {
//                var order = await db.StudyCourses.SingleAsync(c => c.Id == heldId);
//                Assert.True(order.PaymentExpiresAt <= cutoff.AddSeconds(1));
//                Assert.InRange(order.PaymentExpiresAt!.Value, cutoff.AddSeconds(-5), cutoff.AddSeconds(5));
//                Assert.True(order.PaymentExpiresAt < DateTime.UtcNow.AddMinutes(10));
//                // Hết hạn giữ chỗ.
//                await db.StudyCourses.Where(c => c.Id == heldId)
//                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.PaymentExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
//            }
//            var renewed = await EnrollAsync(w, Learners[0], cohortId, Guid.NewGuid());
//            Assert.True(renewed.IsSuccess, renewed.ErrorMessage);
//            Assert.NotEqual(heldId, IdOf(renewed));
//            Assert.False(J(renewed).GetProperty("Resumed").GetBoolean());
//            await using (var db = w.Db())
//                Assert.Equal(2, await db.StudyCourses.CountAsync(c => c.CohortId == cohortId && c.LearnerId == Learners[0]));
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Discovery_filters_by_mentor_level_intensity_weekday_and_time_and_returns_the_schedule_pattern()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            // A: N3, Nhẹ (Thứ 2/Thứ 4, 08:00 giờ VN) của Mentor A. B: N4, Tiêu chuẩn (Thứ 2/4/6, 19:00 giờ VN) của Mentor B.
//            var a = await StandardCohortAsync(w, w.LevelN3, 2, MentorA, 1, "Lớp sáng N3");
//            var b = await StandardCohortAsync(w, w.LevelN4, 3, MentorB, 12, "Lớp tối N4");
//            await using var db = w.Db();
//            var svc = Service(db, Learners[0], Role.Learner);
//            async Task<List<int>> Ids(CohortDiscoveryFilter? f)
//            {
//                var res = f is null ? await svc.CohortsAsync(default) : await svc.CohortsAsync(f, default);
//                Assert.True(res.IsSuccess, res.ErrorMessage);
//                return J(res).EnumerateArray().Select(x => x.GetProperty("Id").GetInt32()).OrderBy(x => x).ToList();
//            }
//            Assert.Equal(new[] { a, b }.OrderBy(x => x), await Ids(null));
//            Assert.Equal([b], await Ids(new CohortDiscoveryFilter(MentorId: MentorB)));
//            Assert.Equal([a], await Ids(new CohortDiscoveryFilter(Level: "N3")));
//            Assert.Equal([b], await Ids(new CohortDiscoveryFilter(Intensity: "Standard")));
//            Assert.Equal([a], await Ids(new CohortDiscoveryFilter(Intensity: "Light")));
//            Assert.Empty(await Ids(new CohortDiscoveryFilter(Intensity: "Advanced")));
//            Assert.Equal([b], await Ids(new CohortDiscoveryFilter(Weekday: 5)));
//            Assert.Equal(new[] { a, b }.OrderBy(x => x), await Ids(new CohortDiscoveryFilter(Weekday: 1)));
//            Assert.Empty(await Ids(new CohortDiscoveryFilter(Weekday: 0)));
//            Assert.Equal([b], await Ids(new CohortDiscoveryFilter(FromTime: new TimeOnly(18, 0), ToTime: new TimeOnly(21, 0))));
//            Assert.Equal([a], await Ids(new CohortDiscoveryFilter(FromTime: new TimeOnly(7, 0), ToTime: new TimeOnly(9, 0))));
//            Assert.Empty(await Ids(new CohortDiscoveryFilter(Weekday: 5, FromTime: new TimeOnly(7, 0), ToTime: new TimeOnly(9, 0))));
//            Assert.Equal(400, (await svc.CohortsAsync(new CohortDiscoveryFilter(Intensity: "N3"), default)).StatusCode);

//            var rows = J(await svc.CohortsAsync(default)).EnumerateArray().ToList();
//            var rowB = rows.Single(x => x.GetProperty("Id").GetInt32() == b);
//            Assert.Equal("Standard", rowB.GetProperty("Intensity").GetString());
//            Assert.Equal("Tiêu chuẩn", rowB.GetProperty("IntensityLabel").GetString());
//            Assert.True(rowB.GetProperty("IsRecommendedIntensity").GetBoolean());
//            Assert.Equal(3, rowB.GetProperty("SessionsPerWeek").GetInt32());
//            Assert.Equal(45m, rowB.GetProperty("TotalTeachingHours").GetDecimal());
//            var patternB = rowB.GetProperty("SchedulePattern").EnumerateArray()
//                .Select(p => (p.GetProperty("Weekday").GetInt32(), p.GetProperty("WeekdayLabel").GetString(),
//                    p.GetProperty("LocalStart").GetString(), p.GetProperty("LocalEnd").GetString())).ToList();
//            Assert.Equal([(1, "Thứ 2", "19:00", "20:30"), (3, "Thứ 4", "19:00", "20:30"), (5, "Thứ 6", "19:00", "20:30")], patternB);
//            var rowA = rows.Single(x => x.GetProperty("Id").GetInt32() == a);
//            Assert.Equal("Light", rowA.GetProperty("Intensity").GetString());
//            Assert.Equal("Nhẹ", rowA.GetProperty("IntensityLabel").GetString());
//            Assert.False(rowA.GetProperty("IsRecommendedIntensity").GetBoolean());
//            Assert.Equal(30m, rowA.GetProperty("TotalTeachingHours").GetDecimal());
//            var patternA = rowA.GetProperty("SchedulePattern").EnumerateArray()
//                .Select(p => (p.GetProperty("Weekday").GetInt32(), p.GetProperty("LocalStart").GetString())).ToList();
//            Assert.Equal([(1, "08:00"), (3, "08:00")], patternA);
//        }
//        finally { await DropAsync(w); }
//    }

//    // Lớp (dữ liệu cũ) đã khai giảng: buổi 1 đã diễn ra, buổi 2-3 trong tương lai; học viên đã thanh toán.
//    private static async Task<(int CohortId, int CourseId)> StartedCohortWithLearnerAsync(World w, int learnerId)
//    {
//        await using var db = w.Db();
//        var plan = new StudyCoursePlan
//        {
//            Name = "Legacy N3", CertificateLevelId = w.LevelN3, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
//            TotalSessions = 3, DurationMonths = 2, SessionsPerWeek = 3, Price = 500_000, IsPublished = true
//        };
//        db.StudyCoursePlans.Add(plan);
//        await db.SaveChangesAsync();
//        var starts = new[] { DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(7) }
//            .Select(d => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0, DateTimeKind.Utc)).ToArray();
//        var cohort = new StudyCohort
//        {
//            PlanId = plan.Id, Name = "Đã khai giảng", MentorId = MentorA, Capacity = 4, CapacitySnapshot = 4, Status = "Published",
//            StartsAt = starts[0], EndsAt = starts[^1].AddMinutes(90)
//        };
//        for (var i = 0; i < starts.Length; i++)
//            cohort.Sessions.Add(new StudyCohortSession
//            {
//                Number = i + 1,
//                Availability = new MentorAvailability
//                {
//                    MentorId = MentorA, StartTime = starts[i], EndTime = starts[i].AddMinutes(90), MaxLearners = 4,
//                    IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
//                }
//            });
//        db.StudyCohorts.Add(cohort);
//        await db.SaveChangesAsync();
//        var course = new StudyCourse
//        {
//            CohortId = cohort.Id, MentorId = MentorA, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt, SessionType = "Group",
//            LearnerId = learnerId, PurchaseKey = Guid.NewGuid(), PlanId = plan.Id, Name = plan.Name, CertificateLevelId = plan.CertificateLevelId,
//            PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3, TotalSessions = 3, SessionMinutes = 90, Amount = plan.Price,
//            PaymentStatus = "Paid", Status = "Active", PaymentReference = $"PAID-{learnerId}"
//        };
//        foreach (var s in cohort.Sessions)
//            course.Sessions.Add(new StudyCourseSession
//            {
//                Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId,
//                MeetingUrl = $"/meeting-room/{s.Availability.RoomId}", Status = s.Number == 1 ? "Present" : "Scheduled"
//            });
//        db.StudyCourses.Add(course);
//        await db.SaveChangesAsync();
//        return (cohort.Id, course.Id);
//    }

//    [CoursePostgresFact]
//    public async Task Post_start_transfer_request_is_rejected()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var (_, courseId) = await StartedCohortWithLearnerAsync(w, Learners[0]);
//            var target = await StandardCohortAsync(w, w.LevelN3, 2, MentorB, 12, "Lớp đích");
//            await using var db = w.Db();
//            var res = await Service(db, Learners[0], Role.Learner).RequestTransferAsync(courseId, target,
//                "Lịch làm việc thay đổi nên cần lớp khác", Guid.NewGuid(), null, default);
//            Assert.Equal(422, res.StatusCode);
//            Assert.Equal("TRANSFER_SOURCE_STARTED", res.ErrorCode);
//            Assert.Equal(0, await db.CohortTransferRequests.CountAsync());
//            Assert.Equal("Active", (await db.StudyCourses.AsNoTracking().SingleAsync(c => c.Id == courseId)).Status);
//        }
//        finally { await DropAsync(w); }
//    }

//    [CoursePostgresFact]
//    public async Task Learner_cannot_cancel_an_ongoing_course_but_admin_cancellation_still_works()
//    {
//        var w = await CreateWorldAsync();
//        try
//        {
//            var (_, courseId) = await StartedCohortWithLearnerAsync(w, Learners[0]);
//            await using (var db = w.Db())
//            {
//                var denied = await Service(db, Learners[0], Role.Learner).CancelCohortEnrollmentAsync(courseId,
//                    new CancelCohortEnrollmentRequest("Muốn nghỉ giữa khóa"), default);
//                Assert.Equal(409, denied.StatusCode);
//                Assert.Equal("COHORT_CANCELLATION_NOT_ALLOWED", denied.ErrorCode);
//            }
//            await using (var db = w.Db())
//            {
//                var course = await db.StudyCourses.Include(c => c.Sessions).AsNoTracking().SingleAsync(c => c.Id == courseId);
//                Assert.Equal("Active", course.Status);
//                Assert.Equal(2, course.Sessions.Count(s => s.Status == "Scheduled"));
//            }
//            await using (var db = w.Db())
//            {
//                var admin = await Service(db, AdminId, Role.Admin).CancelCohortEnrollmentAsync(courseId,
//                    new CancelCohortEnrollmentRequest("Admin xử lý theo yêu cầu pháp lý/hoàn tiền"), default);
//                Assert.True(admin.IsSuccess, admin.ErrorMessage);
//            }
//            await using (var db = w.Db())
//            {
//                var course = await db.StudyCourses.Include(c => c.Sessions).AsNoTracking().SingleAsync(c => c.Id == courseId);
//                Assert.Equal("Cancelled", course.Status);
//                Assert.Equal("Present", course.Sessions.Single(s => s.Number == 1).Status);
//                Assert.All(course.Sessions.Where(s => s.Number > 1), s => Assert.Equal("CancelledByLearner", s.Status));
//            }
//        }
//        finally { await DropAsync(w); }
//    }
//}
