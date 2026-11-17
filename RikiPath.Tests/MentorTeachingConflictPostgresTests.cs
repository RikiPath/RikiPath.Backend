using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorScheduling;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

// Kiểm tra trùng lịch dạy chuẩn của Mentor trên PostgreSQL thật (schema cô lập trong RIKIPATH_COURSE_TEST_DB).
public sealed class MentorTeachingConflictPostgresTests
{
    static MentorTeachingConflictPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private const int AdminId = 8101, MentorA = 8102, MentorB = 8103, MentorC = 8104, LearnerId = 8111;

    private sealed class World
    {
        public required string Schema { get; init; }
        public required string Cs { get; init; }
        public int LevelId { get; set; }
        public int Plan2 { get; set; }
        public int Plan10 { get; set; }
        public int PolicyId { get; set; }
        public long RuleId { get; set; }
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs).Options);
    }

    private static IClaimService Claim(int id, Role role) =>
        Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = id, Role = role });

    private static StudyCourseService Service(AppDbContext db, int userId, Role role)
        => new(new UnitOfWork(db), Claim(userId, role), Mock.Of<IPaymentGatewayClient>(MockBehavior.Strict),
            Mock.Of<IFileStorageService>(), new AppSettings { FrontendBaseUrl = "https://test.invalid" },
            groupPolicies: new MentorGroupPolicyService(db));

    private static MentorTeachingConflictService Conflicts(AppDbContext db)
        => new(new UnitOfWork(db), new MentorGroupPolicyService(db));

    // Thứ 2, 12:00 UTC = 19:00 giờ Việt Nam, ít nhất 8 ngày sau hôm nay.
    private static DateTime BaseMondayUtc()
    {
        var d = DateTime.UtcNow.Date.AddDays(8);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return DateTime.SpecifyKind(d.AddHours(12), DateTimeKind.Utc);
    }

    private static ProposedTeachingInterval Iv(DateTime start, int minutes = 90, int? number = null)
        => new(start, start.AddMinutes(minutes), number);

    private static async Task<World> CreateWorldAsync(int weeklyCap = 0)
    {
        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(
            Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!);
        var w = new World { Schema = schema, Cs = cs };
        await using var db = w.Db();
        UserAccount User(int id, Role role, string name, bool mentor = false) => new()
        {
            Id = id, Role = role, FirstName = name, LastName = id.ToString(), Email = $"u{id}@test.invalid", PasswordHash = [], PasswordSalt = [],
            MentorProfile = mentor ? new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3" } : null
        };
        db.Users.AddRange(User(AdminId, Role.Admin, "Admin"), User(MentorA, Role.Mentor, "MentorA", true),
            User(MentorB, Role.Mentor, "MentorB", true), User(MentorC, Role.Mentor, "MentorC", true), User(LearnerId, Role.Learner, "Learner"));
        var level = new CertificateLevel { Code = "N5", CertificateType = new CertificateType { Name = "JLPT", IsActive = true }, SortOrder = 5 };
        var plan2 = new StudyCoursePlan
        {
            Name = "N5 Group 2", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
            TotalSessions = 2, DurationMonths = 2, SessionsPerWeek = 3, Price = 500_000, IsPublished = true
        };
        var plan10 = new StudyCoursePlan
        {
            Name = "N5 Group 10", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
            TotalSessions = 10, DurationMonths = 4, SessionsPerWeek = 1, Price = 900_000, IsPublished = true
        };
        var policy = new MentorGroupPolicy
        {
            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60,
            SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, WeeklySessionCap = weeklyCap, PublicationHorizonDays = 30,
            CancellationLeadHours = 24, ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2,
            RemediationCompletionThresholdPercent = 90m, RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30,
            RecurringWeekdayPatternsJson = "[[1,3,5]]", LearnerTransferEnabled = true, MentorReplacementEnabled = true,
            MentorReplacementLeadHours = 24, CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        var schedule = new MentorSchedule
        {
            MentorId = MentorA, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date), CreatedAtUtc = DateTime.UtcNow
        };
        var rule = new MentorScheduleRule
        {
            Schedule = schedule, Weekday = 1, DaypartCode = "Evening", LocalStart = new TimeOnly(18, 0), LocalEnd = new TimeOnly(22, 0),
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
        };
        db.AddRange(level, plan2, plan10, policy, schedule, rule);
        await db.SaveChangesAsync();
        foreach (var mentorId in new[] { MentorA, MentorB, MentorC })
            db.MentorTeachingQualifications.Add(new MentorTeachingQualification
            {
                MentorUserId = mentorId, CertificateLevelId = level.Id, IsActive = true, VerifiedByAdminId = AdminId, VerifiedAtUtc = DateTime.UtcNow.AddDays(-60)
            });
        await db.SaveChangesAsync();
        w.LevelId = level.Id; w.Plan2 = plan2.Id; w.Plan10 = plan10.Id; w.PolicyId = policy.Id; w.RuleId = rule.Id;
        return w;
    }

    private static async Task DropAsync(World w) => await CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

    // Khung giờ sinh theo chính sách nhóm (đủ điều kiện để Admin chọn khi tạo lớp).
    private static async Task<List<int>> SlotsAsync(World w, int mentorId, params DateTime[] starts)
    {
        await using var db = w.Db();
        var rows = starts.Select(start => new MentorAvailability
        {
            MentorId = mentorId, IsApproved = true, StartTime = start, EndTime = start.AddMinutes(90), MaxLearners = 4,
            GroupPolicyId = w.PolicyId, PolicyVersion = 1, SessionMinutesSnapshot = 90, RestMinutesSnapshot = 15,
            MentorScheduleRuleId = w.RuleId, GenerationKey = $"tc-{Guid.NewGuid():N}",
            LocalBusinessDate = DateOnly.FromDateTime(start.AddHours(7)), RoomId = Guid.NewGuid(), AdminName = "QA"
        }).ToList();
        db.MentorAvailabilities.AddRange(rows);
        await db.SaveChangesAsync();
        return rows.Select(x => x.Id).ToList();
    }

    // Lớp được ghi thẳng vào DB (khung giờ cũ, không chính sách) để dựng các trạng thái biên.
    private static async Task<(int CohortId, List<int> SlotIds)> SeedCohortAsync(World w, int mentorId, string name, string status,
        params DateTime[] starts)
    {
        await using var db = w.Db();
        var cohort = new StudyCohort
        {
            PlanId = w.Plan2, Name = name, MentorId = mentorId, Capacity = 4, CapacitySnapshot = 4, Status = status,
            StartsAt = starts.Min(), EndsAt = starts.Max().AddMinutes(90)
        };
        for (var i = 0; i < starts.Length; i++)
            cohort.Sessions.Add(new StudyCohortSession
            {
                Number = i + 1,
                Availability = new MentorAvailability
                {
                    MentorId = mentorId, StartTime = starts[i], EndTime = starts[i].AddMinutes(90), MaxLearners = 4,
                    IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
                }
            });
        db.StudyCohorts.Add(cohort);
        await db.SaveChangesAsync();
        return (cohort.Id, cohort.Sessions.OrderBy(s => s.Number).Select(s => s.AvailabilityId).ToList());
    }

    private static async Task<ApiResponse<object>> SaveAsync(World w, string name, int mentorId, int planId, List<int> slotIds,
        bool publish = true, int? id = null, int revision = 1)
    {
        await using var db = w.Db();
        return await Service(db, AdminId, Role.Admin).SaveCohortAsync(id, new SaveStudyCohort(planId, name, mentorId, 4, slotIds, publish, revision), default);
    }

    private static int IdOf(ApiResponse<object> r) => JsonSerializer.SerializeToElement(r.Result).GetProperty("Id").GetInt32();

    private static async Task<TeachingConflictReport> CheckAsync(World w, int mentorId, TeachingConflictOptions? options,
        params ProposedTeachingInterval[] intervals)
    {
        await using var db = w.Db();
        return await Conflicts(db).CheckAsync(mentorId, intervals, options, default);
    }

    [CoursePostgresFact]
    public async Task Publishing_non_overlapping_cohorts_succeeds_and_overlap_variants_are_rejected_with_readable_message()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var a = await SaveAsync(w, "JLPT N5-A", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t, t.AddDays(2)));
            Assert.True(a.IsSuccess, a.ErrorMessage);

            // Không trùng: ngày khác -> công bố được.
            var ok = await SaveAsync(w, "JLPT N5-B", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t.AddDays(1), t.AddDays(3)));
            Assert.True(ok.IsSuccess, ok.ErrorMessage);

            // Trùng một phần qua luồng lưu lớp -> 409 kèm tên lớp và thứ trong tuần.
            var partial = await SaveAsync(w, "JLPT N5-C", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t.AddMinutes(45), t.AddDays(5)));
            Assert.Equal(409, partial.StatusCode);
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", partial.ErrorCode);
            Assert.Contains("JLPT N5-A", partial.ErrorMessage);
            Assert.Contains("thứ 2", partial.ErrorMessage);
            Assert.Contains("19:00", partial.ErrorMessage);
            Assert.DoesNotContain("Learner", partial.ErrorMessage);

            // Trùng hoàn toàn (cùng khung giờ) qua luồng lưu lớp.
            var full = await SaveAsync(w, "JLPT N5-D", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t, t.AddDays(6)));
            Assert.Equal(409, full.StatusCode); Assert.Equal("MENTOR_SCHEDULE_CONFLICT", full.ErrorCode);

            // Các biến thể quy tắc với khoảng nửa mở [start, end).
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", (await CheckAsync(w, MentorA, null, Iv(t.AddMinutes(-30), 150))).First!.Code); // bao trùm
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", (await CheckAsync(w, MentorA, null, Iv(t.AddMinutes(45)))).First!.Code);      // một phần
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", (await CheckAsync(w, MentorA, null, Iv(t, 60))).First!.Code);                 // cùng giờ bắt đầu
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", (await CheckAsync(w, MentorA, null, Iv(t.AddMinutes(30), 60))).First!.Code);  // cùng giờ kết thúc

            // Liền kề: nghỉ 15 phút -> vi phạm; nghỉ 0 -> được phép; cách đúng 15 phút -> được phép.
            var adjacent = await CheckAsync(w, MentorA, null, Iv(t.AddMinutes(90)));
            Assert.False(adjacent.IsValid); Assert.Equal("MENTOR_REST_INTERVAL_VIOLATION", adjacent.First!.Code);
            Assert.Contains("15 phút", adjacent.First.Message);
            Assert.True((await CheckAsync(w, MentorA, new TeachingConflictOptions { RestMinutes = 0 }, Iv(t.AddMinutes(90)))).IsValid);
            Assert.True((await CheckAsync(w, MentorA, null, Iv(t.AddMinutes(105)))).IsValid);

            // Hai khung giờ dự kiến trùng giờ bắt đầu.
            var dup = await CheckAsync(w, MentorA, null, Iv(t.AddDays(10)), Iv(t.AddDays(10)));
            Assert.Equal("COHORT_SESSION_DUPLICATE", Assert.Single(dup.Conflicts.Where(c => c.Code == "COHORT_SESSION_DUPLICATE")).Code);

            await using var db = w.Db();
            Assert.Equal(2, await db.StudyCohorts.CountAsync(c => c.MentorId == MentorA && c.Status == "Published"));
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Conflict_only_in_week_8_of_a_10_week_schedule_is_detected_and_reported_for_session_8()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var a = await SaveAsync(w, "JLPT N5 Weekly", MentorA, w.Plan10,
                await SlotsAsync(w, MentorA, Enumerable.Range(0, 10).Select(k => t.AddDays(7 * k)).ToArray()));
            Assert.True(a.IsSuccess, a.ErrorMessage);
            var bStarts = Enumerable.Range(0, 10).Select(k => k == 7 ? t.AddDays(7 * k).AddMinutes(45) : t.AddDays(7 * k + 1)).ToArray();
            var bSlots = await SlotsAsync(w, MentorA, bStarts);

            var report = await CheckAsync(w, MentorA, null, bStarts.Select((s, i) => Iv(s, 90, i + 1)).ToArray());
            Assert.False(report.IsValid);
            Assert.All(report.Conflicts, c => Assert.Equal(8, c.SessionNumber));
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", report.First!.Code);

            var b = await SaveAsync(w, "JLPT N5 Weekly B", MentorA, w.Plan10, bSlots);
            Assert.Equal(409, b.StatusCode); Assert.Equal("MENTOR_SCHEDULE_CONFLICT", b.ErrorCode);
            Assert.Contains(DateOnly.FromDateTime(t.AddDays(49).AddHours(7)).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), b.ErrorMessage);
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Concurrent_publishes_of_overlapping_cohorts_for_one_mentor_let_exactly_one_succeed()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var x = await SlotsAsync(w, MentorA, t, t.AddDays(2));
            var y = await SlotsAsync(w, MentorA, t.AddMinutes(30), t.AddDays(3));
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<ApiResponse<object>> Publish(string name, List<int> ids)
            {
                await gate.Task;
                return await SaveAsync(w, name, MentorA, w.Plan2, ids);
            }
            var first = Task.Run(() => Publish("Concurrent X", x));
            var second = Task.Run(() => Publish("Concurrent Y", y));
            gate.SetResult();
            var results = await Task.WhenAll(first, second);

            Assert.Single(results, r => r.IsSuccess);
            var loser = Assert.Single(results, r => !r.IsSuccess);
            Assert.Equal(409, loser.StatusCode);
            Assert.Equal("MENTOR_SCHEDULE_CONFLICT", loser.ErrorCode);
            await using var db = w.Db();
            Assert.Equal(1, await db.StudyCohorts.CountAsync(c => c.MentorId == MentorA));
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Retrying_an_update_with_a_stale_revision_returns_revision_conflict()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var slots = await SlotsAsync(w, MentorA, t, t.AddDays(2));
            var draft = await SaveAsync(w, "Draft class", MentorA, w.Plan2, slots, publish: false);
            Assert.True(draft.IsSuccess, draft.ErrorMessage);
            var id = IdOf(draft);
            var update = await SaveAsync(w, "Draft class v2", MentorA, w.Plan2, slots, publish: false, id: id, revision: 1);
            Assert.True(update.IsSuccess, update.ErrorMessage);
            var retry = await SaveAsync(w, "Draft class v2", MentorA, w.Plan2, slots, publish: false, id: id, revision: 1);
            Assert.Equal(409, retry.StatusCode);
            Assert.Equal("COHORT_SCHEDULE_REVISION_CONFLICT", retry.ErrorCode);
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Replacement_mentor_with_a_published_class_conflicts_but_an_open_slot_does_not()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var cohort = await SaveAsync(w, "Replace me", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t, t.AddDays(2)));
            Assert.True(cohort.IsSuccess, cohort.ErrorMessage);
            var busy = await SaveAsync(w, "Busy B", MentorB, w.Plan2, await SlotsAsync(w, MentorB, t.AddDays(2).AddMinutes(30), t.AddDays(8)));
            Assert.True(busy.IsSuccess, busy.ErrorMessage);
            await SlotsAsync(w, MentorC, t.AddMinutes(30)); // khung giờ rảnh chưa có người đặt: không phải cam kết dạy

            await using var db = w.Db();
            var svc = Service(db, AdminId, Role.Admin);
            var b = JsonSerializer.SerializeToElement((await svc.PreviewMentorReplacementAsync(IdOf(cohort), MentorB, null, default)).Result);
            Assert.False(b.GetProperty("Eligible").GetBoolean());
            var check = b.GetProperty("Checks").EnumerateArray().Single(c => c.GetProperty("Code").GetString() == "REPLACEMENT_MENTOR_SCHEDULE_CONFLICT");
            Assert.False(check.GetProperty("Passed").GetBoolean());
            Assert.Contains("Busy B", check.GetProperty("Message").GetString());
            Assert.Equal(2, b.GetProperty("Conflicts").EnumerateArray().First().GetProperty("SessionNumber").GetInt32());

            var c = JsonSerializer.SerializeToElement((await svc.PreviewMentorReplacementAsync(IdOf(cohort), MentorC, null, default)).Result);
            Assert.True(c.GetProperty("Eligible").GetBoolean(), c.ToString());
            Assert.Empty(c.GetProperty("Conflicts").EnumerateArray());
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Admin_move_of_a_cohort_session_onto_a_conflicting_slot_is_rejected_and_a_free_slot_is_accepted()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var a = await SaveAsync(w, "Move class", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t, t.AddDays(2)));
            Assert.True(a.IsSuccess, a.ErrorMessage);
            var other = await SaveAsync(w, "Other class", MentorA, w.Plan2, await SlotsAsync(w, MentorA, t.AddDays(4), t.AddDays(6)));
            Assert.True(other.IsSuccess, other.ErrorMessage);
            var conflicting = (await SlotsAsync(w, MentorA, t.AddDays(4).AddMinutes(30)))[0];
            var free = (await SlotsAsync(w, MentorA, t.AddDays(5)))[0];

            int courseId, sessionId;
            await using (var db = w.Db())
            {
                var cohort = await db.StudyCohorts.Include(c => c.Plan).Include(c => c.Sessions).ThenInclude(s => s.Availability).SingleAsync(c => c.Id == IdOf(a));
                var course = new StudyCourse
                {
                    CohortId = cohort.Id, MentorId = MentorA, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt, SessionType = "Group",
                    LearnerId = LearnerId, PurchaseKey = Guid.NewGuid(), PlanId = cohort.PlanId, Name = cohort.Plan.Name,
                    CertificateLevelId = cohort.Plan.CertificateLevelId, PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3,
                    TotalSessions = 2, SessionMinutes = 90, Amount = cohort.Plan.Price, PaymentStatus = "Paid", Status = "Active", PaymentReference = "PAID-MOVE"
                };
                foreach (var s in cohort.Sessions)
                    course.Sessions.Add(new StudyCourseSession { Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}" });
                db.StudyCourses.Add(course);
                await db.SaveChangesAsync();
                courseId = course.Id; sessionId = course.Sessions.Single(s => s.Number == 1).Id;
            }
            await using (var db = w.Db())
            {
                var req = await Service(db, LearnerId, Role.Learner).RequestChangeAsync(courseId, new RequestCourseChange(sessionId, "Move", "Bận công tác đột xuất"), default);
                Assert.True(req.IsSuccess, req.ErrorMessage);
            }
            int requestId;
            await using (var db = w.Db()) requestId = await db.CourseScheduleRequests.Where(r => r.CourseId == courseId).Select(r => r.Id).SingleAsync();

            await using (var db = w.Db())
            {
                var rejected = await Service(db, AdminId, Role.Admin).ReviewChangeAsync(courseId, requestId,
                    new ReviewCourseChange(true, "Dời buổi học bù", conflicting, EntireClassConfirmed: true), default);
                Assert.Equal(409, rejected.StatusCode);
                Assert.Equal("MENTOR_SCHEDULE_CONFLICT", rejected.ErrorCode);
                Assert.Contains("Other class", rejected.ErrorMessage);
            }
            await using (var db = w.Db())
            {
                var moved = await Service(db, AdminId, Role.Admin).ReviewChangeAsync(courseId, requestId,
                    new ReviewCourseChange(true, "Dời buổi học bù", free, EntireClassConfirmed: true), default);
                Assert.True(moved.IsSuccess, moved.ErrorMessage);
            }
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task No_false_conflicts_from_another_mentor_deleted_slot_draft_cohort_or_open_availability()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            await SeedCohortAsync(w, MentorB, "Mentor B class", "Published", t);                       // Mentor khác
            var (_, deletedSlots) = await SeedCohortAsync(w, MentorA, "Deleted slot class", "Published", t.AddDays(1));
            await using (var db = w.Db())
            {
                var slot = await db.MentorAvailabilities.SingleAsync(x => x.Id == deletedSlots[0]);
                slot.IsDeleted = true;
                await db.SaveChangesAsync();
            }
            await SeedCohortAsync(w, MentorA, "Draft class", "Draft", t.AddDays(2));                    // Lớp nháp
            await SlotsAsync(w, MentorA, t.AddDays(3));                                                 // Khung giờ rảnh

            var report = await CheckAsync(w, MentorA, null, Iv(t, 90, 1), Iv(t.AddDays(1), 90, 2), Iv(t.AddDays(2), 90, 3), Iv(t.AddDays(3), 90, 4));
            Assert.True(report.IsValid, string.Join(" | ", report.Conflicts.Select(c => c.Message)));

            // Đối chứng: lớp đã công bố của Mentor B vẫn là xung đột của Mentor B.
            Assert.False((await CheckAsync(w, MentorB, null, Iv(t))).IsValid);
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Daily_cap_counts_commitments_and_proposed_sessions_per_local_business_date()
    {
        var w = await CreateWorldAsync();
        try
        {
            var day = BaseMondayUtc().Date; // 00:00 UTC = 07:00 giờ Việt Nam cùng ngày
            await SeedCohortAsync(w, MentorA, "Morning class", "Published", day.AddHours(1), day.AddHours(3), day.AddHours(5));
            Assert.True((await CheckAsync(w, MentorA, null, Iv(day.AddHours(7)))).IsValid);
            var over = await CheckAsync(w, MentorA, null, Iv(day.AddHours(7)), Iv(day.AddHours(9)));
            Assert.False(over.IsValid);
            Assert.Equal("MENTOR_DAILY_LIMIT_EXCEEDED", over.First!.Code);
            Assert.Contains("thứ 2", over.First.Message);
            // 17:30 UTC đã sang ngày hôm sau theo giờ Việt Nam -> không tính vào ngày cũ.
            Assert.True((await CheckAsync(w, MentorA, null, Iv(day.AddHours(7)), Iv(day.AddHours(17).AddMinutes(30)))).IsValid);
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Weekly_cap_counts_sessions_per_local_iso_week_when_enabled()
    {
        var w = await CreateWorldAsync(weeklyCap: 3);
        try
        {
            var monday = BaseMondayUtc();
            await SeedCohortAsync(w, MentorA, "Weekly class", "Published", monday, monday.AddDays(2));
            Assert.True((await CheckAsync(w, MentorA, null, Iv(monday.AddDays(4)))).IsValid);
            var over = await CheckAsync(w, MentorA, null, Iv(monday.AddDays(4)), Iv(monday.AddDays(5)));
            Assert.False(over.IsValid);
            Assert.Equal("MENTOR_WEEKLY_LIMIT_EXCEEDED", over.First!.Code);
            // Thứ 2 tuần sau thuộc tuần khác.
            Assert.True((await CheckAsync(w, MentorA, null, Iv(monday.AddDays(4)), Iv(monday.AddDays(7)))).IsValid);
            // 0 = không giới hạn theo tuần.
            Assert.True((await CheckAsync(w, MentorA, new TeachingConflictOptions { WeeklyCap = 0 }, Iv(monday.AddDays(4)), Iv(monday.AddDays(5)))).IsValid);
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Mentor_conflict_preview_only_uses_the_signed_in_mentors_own_schedule()
    {
        var w = await CreateWorldAsync();
        try
        {
            var t = BaseMondayUtc();
            var (cohortA, _) = await SeedCohortAsync(w, MentorA, "JLPT N5-A", "Published", t);
            var request = new TeachingConflictPreviewRequest { Intervals = [new() { StartUtc = t.AddMinutes(30), EndUtc = t.AddMinutes(120) }] };
            await using var db = w.Db();
            MentorScheduleManagementService Svc(int id, Role role) => new(new UnitOfWork(db), Claim(id, role),
                Mock.Of<IMentorProfileService>(), Mock.Of<IMentorSlotGenerator>(), NullLogger<MentorScheduleManagementService>.Instance,
                new MentorGroupPolicyService(db));

            var own = await Svc(MentorA, Role.Mentor).PreviewTeachingConflictsAsync(request, default);
            Assert.True(own.IsSuccess);
            Assert.False(own.Result!.IsValid);
            Assert.Equal("JLPT N5-A", own.Result.First!.CohortName);

            var otherMentor = await Svc(MentorB, Role.Mentor).PreviewTeachingConflictsAsync(request, default);
            Assert.True(otherMentor.IsSuccess);
            Assert.True(otherMentor.Result!.IsValid);
            Assert.Empty(otherMentor.Result.Conflicts);

            var foreignCohort = await Svc(MentorB, Role.Mentor).PreviewTeachingConflictsAsync(
                new TeachingConflictPreviewRequest { Intervals = request.Intervals, ExcludeCohortId = cohortA }, default);
            Assert.Equal(404, foreignCohort.StatusCode);
            Assert.Equal(403, (await Svc(LearnerId, Role.Learner).PreviewTeachingConflictsAsync(request, default)).StatusCode);
        }
        finally { await DropAsync(w); }
    }
}
