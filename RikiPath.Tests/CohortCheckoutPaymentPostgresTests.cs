using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Npgsql;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;
using RikiPath.Infrastructure.Services;

namespace RikiPath.Tests;

// PostgreSQL integration tests for the paid Cohort checkout (PayOS) path.
// Each test runs in an isolated schema of RIKIPATH_COURSE_TEST_DB; PayOS is never called:
// the gateway is a local fake whose webhook verification delegates to the REAL PayOsClient HMAC check.
public sealed class CohortCheckoutPaymentPostgresTests
{
    static CohortCheckoutPaymentPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private const int AdminId = 8101, MentorId = 8102;
    private static readonly int[] Learners = [8111, 8112, 8113, 8114];
    private const string ChecksumKey = "test-checksum-key";
    private const decimal PlanPrice = 500_000m;

    private static AppSettings Settings() => new()
    {
        FrontendBaseUrl = "https://test.invalid",
        PayOs = new PayOsSettings { ClientId = "t", ApiKey = "t", ChecksumKey = ChecksumKey }
    };

    // ---------------------------------------------------------------- test doubles

    private sealed class TestPayOsGateway : IPaymentGatewayClient
    {
        private readonly PayOsClient verifier;
        private int createCalls;
        private int infoCalls;
        public TestPayOsGateway(AppSettings settings) => verifier = new PayOsClient(new HttpClient(), settings);
        public int CreateCalls => Volatile.Read(ref createCalls);
        public int InfoCalls => Volatile.Read(ref infoCalls);
        public int LastAmount { get; private set; }
        public Func<long, PaymentLinkInformation>? Information { get; set; }
        public Exception? InformationFailure { get; set; }

        public Task<PaymentLinkResult> CreatePaymentLinkAsync(long orderCode, int amount, string description,
            string returnUrl, string cancelUrl, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref createCalls);
            LastAmount = amount;
            return Task.FromResult(new PaymentLinkResult($"https://pay.test.invalid/{orderCode}", $"plink-{orderCode}", ""));
        }

        public PaymentCallbackResult VerifyWebhook(PayOsWebhookRequest webhook) => verifier.VerifyWebhook(webhook);

        public Task<PaymentLinkInformation> GetPaymentInformationAsync(long orderCode, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref infoCalls);
            if (InformationFailure is not null) throw InformationFailure;
            if (Information is null) throw new InvalidOperationException("No payment information configured.");
            return Task.FromResult(Information(orderCode));
        }
    }

    // Throws once when an entitlement row is about to be inserted (simulates a crash mid-grant).
    private sealed class FailOnceOnEntitlementInterceptor : SaveChangesInterceptor
    {
        public int Thrown;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Thrown == 0 && eventData.Context!.ChangeTracker.Entries<CohortEnrollmentEntitlement>().Any(e => e.State == EntityState.Added))
            {
                Thrown++;
                throw new InvalidOperationException("Simulated failure while granting the cohort entitlement.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    // ---------------------------------------------------------------- webhook signing (mirrors PayOsClient)

    private static string Sign(PayOsWebhookData d, string key)
    {
        var fields = new (string Key, string Value)[]
        {
            ("accountNumber", d.AccountNumber ?? ""),
            ("amount", d.Amount.ToString()),
            ("code", d.Code ?? ""),
            ("counterAccountBankId", d.CounterAccountBankId ?? ""),
            ("counterAccountBankName", d.CounterAccountBankName ?? ""),
            ("counterAccountName", d.CounterAccountName ?? ""),
            ("counterAccountNumber", d.CounterAccountNumber ?? ""),
            ("currency", d.Currency ?? ""),
            ("desc", d.Desc ?? ""),
            ("description", d.Description ?? ""),
            ("orderCode", d.OrderCode.ToString()),
            ("paymentLinkId", d.PaymentLinkId ?? ""),
            ("reference", d.Reference ?? ""),
            ("transactionDateTime", d.TransactionDateTime ?? ""),
            ("virtualAccountName", d.VirtualAccountName ?? ""),
            ("virtualAccountNumber", d.VirtualAccountNumber ?? ""),
        };
        var data = string.Join("&", fields.Select(f => $"{f.Key}={f.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private static PayOsWebhookRequest SignedWebhook(long orderCode, int amount, bool success, string reference, string key = ChecksumKey)
    {
        var data = new PayOsWebhookData
        {
            OrderCode = orderCode, Amount = amount, Description = $"COURSE {orderCode - 700_000_000L}",
            AccountNumber = "0001112223", Reference = reference, TransactionDateTime = "2026-11-17 10:00:00",
            Currency = "VND", PaymentLinkId = $"plink-{orderCode}",
            Code = success ? "00" : "01", Desc = success ? "success" : "failed"
        };
        return new PayOsWebhookRequest
        {
            Code = data.Code, Desc = data.Desc, Success = success, Data = data, Signature = Sign(data, key)
        };
    }

    // ---------------------------------------------------------------- world

    private sealed class World
    {
        public required string Schema { get; init; }
        public required string Cs { get; init; }
        public int LevelId { get; set; }
        public int PlanId { get; set; }
        public int PolicyId { get; set; }
        public long RuleId { get; set; }
        public AppDbContext Db(IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cs);
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new AppDbContext(builder.Options);
        }
    }

    private static async Task<World> CreateWorldAsync()
    {
        var baseCs = Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")!;
        var (schema, cs) = await CanonicalGroupLearningHttpE2ETests.CreateIsolatedSchemaAsync(baseCs);
        var w = new World { Schema = schema, Cs = cs };
        await using var db = w.Db();
        UserAccount User(int id, Role role, string name) => new()
        {
            Id = id, Role = role, FirstName = name, LastName = id.ToString(), Email = $"checkout{id}@test.invalid",
            PasswordHash = [], PasswordSalt = []
        };
        db.Users.AddRange([User(AdminId, Role.Admin, "Admin"), User(MentorId, Role.Mentor, "Mentor"),
            .. Learners.Select(id => User(id, Role.Learner, "Learner"))]);
        var level = new CertificateLevel { Code = "N3", SortOrder = 3, CertificateType = new CertificateType { Name = "JLPT", IsActive = true } };
        var plan = new StudyCoursePlan
        {
            Name = "N3 Group Checkout", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90,
            TotalSessions = 3, DurationMonths = 2, SessionsPerWeek = 3, Price = PlanPrice, IsPublished = true
        };
        var policy = new MentorGroupPolicy
        {
            Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60,
            SessionMinutes = 90, RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24,
            ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7, PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90m,
            RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30, RecurringWeekdayPatternsJson = "[[1,3,5]]",
            LearnerTransferEnabled = true, MentorReplacementEnabled = true, MentorReplacementLeadHours = 24,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
        var rule = new MentorScheduleRule
        {
            Schedule = new MentorSchedule { MentorId = MentorId, PolicyVersion = 1, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date), CreatedAtUtc = DateTime.UtcNow },
            Weekday = 1, DaypartCode = "Evening", LocalStart = new TimeOnly(19, 0), LocalEnd = new TimeOnly(22, 0),
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date)
        };
        db.AddRange(level, plan, policy, rule);
        await db.SaveChangesAsync();
        w.LevelId = level.Id; w.PlanId = plan.Id; w.PolicyId = policy.Id; w.RuleId = rule.Id;
        return w;
    }

    private static Task DropAsync(World w) => CanonicalGroupLearningHttpE2ETests.DropSchemaAsync(w.Schema, w.Cs);

    private static DateTime Utc(DateTime v) => new(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, DateTimeKind.Utc);

    private static DateTime[] Future(double firstDays) =>
        [Utc(DateTime.UtcNow.AddDays(firstDays)), Utc(DateTime.UtcNow.AddDays(firstDays + 1)), Utc(DateTime.UtcNow.AddDays(firstDays + 2))];

    // Legacy-style slots have no GenerationKey/GroupPolicyId; policySlots=true binds every slot to policy v1 + a schedule rule.
    private static async Task<int> CreateCohortAsync(World w, DateTime[] starts, int capacity = 4, string status = "Published",
        bool policySlots = false, string name = "Cohort")
    {
        await using var db = w.Db();
        var cohort = new StudyCohort
        {
            PlanId = w.PlanId, Name = name, MentorId = MentorId, Capacity = capacity, CapacitySnapshot = capacity, Status = status,
            StartsAt = starts.Min(), EndsAt = starts.Max().AddMinutes(90)
        };
        if (policySlots) { cohort.GroupPolicyId = w.PolicyId; cohort.PolicyVersionSnapshot = 1; }
        for (var i = 0; i < starts.Length; i++)
        {
            var slot = new MentorAvailability
            {
                MentorId = MentorId, StartTime = starts[i], EndTime = starts[i].AddMinutes(90), MaxLearners = capacity,
                IsApproved = true, RoomId = Guid.NewGuid(), AdminName = "QA"
            };
            if (policySlots)
            {
                slot.GroupPolicyId = w.PolicyId; slot.PolicyVersion = 1; slot.SessionMinutesSnapshot = 90; slot.RestMinutesSnapshot = 15;
                slot.MentorScheduleRuleId = w.RuleId; slot.GenerationKey = $"checkout-{Guid.NewGuid():N}";
                slot.LocalBusinessDate = DateOnly.FromDateTime(starts[i]);
            }
            cohort.Sessions.Add(new StudyCohortSession { Number = i + 1, Availability = slot });
        }
        db.StudyCohorts.Add(cohort);
        await db.SaveChangesAsync();
        return cohort.Id;
    }

    // Direct Active/Paid enrollment row (e.g. a source enrollment for a transfer).
    private static async Task<int> InsertPaidEnrollmentAsync(World w, int learnerId, int cohortId, Action<StudyCourse>? tweak = null)
    {
        await using var db = w.Db();
        var cohort = await db.StudyCohorts.Include(c => c.Plan).Include(c => c.Sessions).ThenInclude(s => s.Availability).SingleAsync(c => c.Id == cohortId);
        var course = new StudyCourse
        {
            CohortId = cohort.Id, MentorId = cohort.MentorId, StartsAt = cohort.StartsAt, EndsAt = cohort.EndsAt, SessionType = "Group",
            LearnerId = learnerId, PurchaseKey = Guid.NewGuid(), PlanId = cohort.PlanId, Name = cohort.Plan.Name,
            CertificateLevelId = cohort.Plan.CertificateLevelId, PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3,
            TotalSessions = 3, SessionMinutes = 90, Amount = cohort.Plan.Price, PaymentStatus = "Paid", Status = "Active",
            PaymentReference = $"PAID-{learnerId}-{cohortId}"
        };
        foreach (var s in cohort.Sessions)
            course.Sessions.Add(new StudyCourseSession
            {
                Number = s.Number, ScheduledAt = s.Availability.StartTime, AvailabilityId = s.AvailabilityId, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}"
            });
        tweak?.Invoke(course);
        db.StudyCourses.Add(course);
        await db.SaveChangesAsync();
        return course.Id;
    }

    // ---------------------------------------------------------------- service helpers

    private static IClaimService Claim(int userId, Role role) =>
        Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = userId, Role = role });

    private static StudyCourseService CourseService(AppDbContext db, int userId, Role role, IPaymentGatewayClient gateway)
    {
        var uow = new UnitOfWork(db);
        var settings = Settings();
        return new StudyCourseService(uow, Claim(userId, role), gateway, Mock.Of<IFileStorageService>(), settings,
            groupEntitlementContract: new GroupEnrollmentEntitlementContract(uow), groupPolicies: new MentorGroupPolicyService(db, settings));
    }

    private static async Task<ApiResponse<object>> CheckoutAsync(World w, int learnerId, int cohortId, TestPayOsGateway gateway,
        Guid? key = null, string? entitlementReference = null)
    {
        await using var db = w.Db();
        return await CourseService(db, learnerId, Role.Learner, gateway)
            .EnrollCohortAsync(cohortId, new EnrollCohortRequest(key ?? Guid.NewGuid(), entitlementReference), default);
    }

    // Webhooks go through PaymentService exactly like the production controller (system actor, no learner claim).
    private static async Task<ApiResponse<bool>> DeliverAsync(AppDbContext db, PayOsWebhookRequest webhook)
    {
        var settings = Settings();
        var gateway = new TestPayOsGateway(settings);
        var courses = CourseService(db, 0, Role.Admin, gateway);
        return await new PaymentService(new UnitOfWork(db), gateway, settings, Mock.Of<IMentorCreditLedgerService>(), courses)
            .HandlePayOsWebhookAsync(webhook, default);
    }

    private static async Task<ApiResponse<bool>> DeliverAsync(World w, PayOsWebhookRequest webhook)
    {
        await using var db = w.Db();
        return await DeliverAsync(db, webhook);
    }

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
    private static int IdOf(ApiResponse<object> res) => Json(res.Result).GetProperty("Id").GetInt32();
    private static long OrderCode(int courseId) => 700_000_000L + courseId;
    private static PayOsWebhookRequest Paid(int courseId, string? reference = null, int amount = (int)PlanPrice)
        => SignedWebhook(OrderCode(courseId), amount, true, reference ?? $"FT-{courseId}-{Guid.NewGuid():N}");

    private static async Task<int> SeatsAvailableAsync(World w, int cohortId)
    {
        await using var db = w.Db();
        var res = await CourseService(db, AdminId, Role.Admin, new TestPayOsGateway(Settings())).CohortsAsync(default);
        Assert.True(res.IsSuccess, res.ErrorMessage);
        return Json(res.Result).EnumerateArray().Single(x => x.GetProperty("Id").GetInt32() == cohortId).GetProperty("SeatsAvailable").GetInt32();
    }

    private static async Task<StudyCourse> CourseAsync(World w, int id)
    {
        await using var db = w.Db();
        return await db.StudyCourses.AsNoTracking().Include(c => c.Sessions).SingleAsync(c => c.Id == id);
    }

    private static async Task<int> EntitlementCountAsync(World w, int? courseId = null)
    {
        await using var db = w.Db();
        return courseId is null
            ? await db.CohortEnrollmentEntitlements.CountAsync()
            : await db.CohortEnrollmentEntitlements.CountAsync(e => e.ConsumedByCourseId == courseId || e.EntitlementReference == $"PURCHASE-{courseId}");
    }

    private static async Task AssertUnpaidAndUntouchedAsync(World w, int courseId)
    {
        var c = await CourseAsync(w, courseId);
        Assert.Equal("Pending", c.PaymentStatus);
        Assert.Equal("PendingPayment", c.Status);
        Assert.Null(c.ProviderPaymentReference);
        Assert.Null(c.PaymentConfirmedAtUtc);
        Assert.Empty(c.Sessions);
        Assert.Equal(0, await EntitlementCountAsync(w, courseId));
    }

    // ================================================================ 1
    [CoursePostgresFact]
    public async Task Valid_full_course_checkout_creates_pending_order_with_frozen_snapshot_https_link_and_reserves_seat()
    {
        var w = await CreateWorldAsync();
        try
        {
            var legacy = await CreateCohortAsync(w, Future(5), name: "Legacy");
            var policyCohort = await CreateCohortAsync(w, Future(12), policySlots: true, name: "Policy");
            Assert.Equal(4, await SeatsAvailableAsync(w, legacy));
            var gateway = new TestPayOsGateway(Settings());

            var res = await CheckoutAsync(w, Learners[0], legacy, gateway);
            Assert.True(res.IsSuccess, res.ErrorMessage);
            var json = Json(res.Result);
            var id = json.GetProperty("Id").GetInt32();
            Assert.Equal("Pending", json.GetProperty("PaymentStatus").GetString());
            Assert.Equal(PlanPrice, json.GetProperty("Amount").GetDecimal());
            var url = json.GetProperty("CheckoutUrl").GetString()!;
            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https", url);
            Assert.Equal($"https://pay.test.invalid/{OrderCode(id)}", url);
            Assert.Equal(1, gateway.CreateCalls);
            Assert.Equal((int)PlanPrice, gateway.LastAmount);

            var course = await CourseAsync(w, id);
            Assert.Equal("PendingPayment", course.Status);
            Assert.Equal(PlanPrice, course.Amount);
            Assert.Equal("VND", course.Currency);
            Assert.True(course.PaymentExpiresAt > DateTime.UtcNow);
            Assert.Empty(course.Sessions);
            using (var snapshot = JsonDocument.Parse(course.OrderSnapshot!))
            {
                var root = snapshot.RootElement;
                Assert.Equal(PlanPrice, root.GetProperty("price").GetDecimal());
                Assert.Equal("VND", root.GetProperty("currency").GetString());
                Assert.Equal(legacy, root.GetProperty("cohortId").GetInt32());
                Assert.True(root.TryGetProperty("policyVersion", out _));
                Assert.Equal("PayOS", root.GetProperty("checkoutKind").GetString());
            }
            Assert.Equal(3, await SeatsAvailableAsync(w, legacy));
            Assert.Equal(0, await EntitlementCountAsync(w));

            // Policy-slot cohort: snapshot freezes the slot policy version.
            var policyRes = await CheckoutAsync(w, Learners[1], policyCohort, gateway);
            Assert.True(policyRes.IsSuccess, policyRes.ErrorMessage);
            var policyCourse = await CourseAsync(w, IdOf(policyRes));
            Assert.Equal("Pending", policyCourse.PaymentStatus);
            Assert.Equal(PlanPrice, policyCourse.Amount);
            Assert.Equal(w.PolicyId, policyCourse.GroupPolicyId);
            Assert.Equal(1, policyCourse.PolicyVersionSnapshot);
            using (var snapshot = JsonDocument.Parse(policyCourse.OrderSnapshot!))
            {
                Assert.Equal(1, snapshot.RootElement.GetProperty("policyVersion").GetInt32());
                Assert.Equal(policyCohort, snapshot.RootElement.GetProperty("cohortId").GetInt32());
            }
            Assert.Equal(3, await SeatsAvailableAsync(w, policyCohort));
            Assert.Equal(2, gateway.CreateCalls);

            // Learner list view: paid checkout is the canonical path, my enrollment shows the payment state.
            await using var db = w.Db();
            var cohorts = await CourseService(db, Learners[0], Role.Learner, gateway).CohortsAsync(default);
            var row = Json(cohorts.Result).EnumerateArray().Single(x => x.GetProperty("Id").GetInt32() == legacy);
            Assert.Equal("PayOS", row.GetProperty("CheckoutMode").GetString());
            Assert.Equal("AwaitingPayment", row.GetProperty("MyEnrollmentPaymentState").GetString());
            Assert.False(row.GetProperty("EnrollmentOpen").GetBoolean());
            var otherRow = Json((await CourseService(db, Learners[2], Role.Learner, gateway).CohortsAsync(default)).Result)
                .EnumerateArray().Single(x => x.GetProperty("Id").GetInt32() == policyCohort);
            Assert.True(otherRow.GetProperty("EnrollmentOpen").GetBoolean());
            Assert.Equal(JsonValueKind.Null, otherRow.GetProperty("EntitlementStatusMessage").ValueKind);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 2
    [CoursePostgresFact]
    public async Task Price_tampering_never_changes_the_frozen_order_amount_and_mismatched_webhook_amount_is_rejected()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var gateway = new TestPayOsGateway(Settings());
            var key = Guid.NewGuid();
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway, key));
            await using (var db = w.Db())
                await db.StudyCoursePlans.Where(p => p.Id == w.PlanId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 1000m));

            var retry = await CheckoutAsync(w, Learners[0], cohort, gateway, key);
            Assert.True(retry.IsSuccess, retry.ErrorMessage);
            Assert.Equal(id, IdOf(retry));
            Assert.Equal(PlanPrice, Json(retry.Result).GetProperty("Amount").GetDecimal());
            Assert.Equal(PlanPrice, (await CourseAsync(w, id)).Amount);

            var tampered = await DeliverAsync(w, Paid(id, amount: 1000));
            Assert.False(tampered.IsSuccess);
            Assert.Equal("PAYMENT_AMOUNT_MISMATCH", tampered.ErrorCode);
            Assert.Equal(409, tampered.StatusCode);
            await AssertUnpaidAndUntouchedAsync(w, id);

            // The frozen order amount is still payable.
            Assert.True((await DeliverAsync(w, Paid(id))).IsSuccess);
            Assert.Equal("Active", (await CourseAsync(w, id)).Status);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 3
    [CoursePostgresFact]
    public async Task Unpublished_or_missing_cohort_cannot_be_checked_out()
    {
        var w = await CreateWorldAsync();
        try
        {
            var draft = await CreateCohortAsync(w, Future(5), status: "Draft");
            var gateway = new TestPayOsGateway(Settings());
            var closed = await CheckoutAsync(w, Learners[0], draft, gateway);
            Assert.False(closed.IsSuccess);
            Assert.Equal(409, closed.StatusCode);
            Assert.Equal("COHORT_ENROLLMENT_CLOSED", closed.ErrorCode);
            var missing = await CheckoutAsync(w, Learners[0], 987_654, gateway);
            Assert.Equal(404, missing.StatusCode);
            Assert.Equal(0, gateway.CreateCalls);
            await using var db = w.Db();
            Assert.Equal(0, await db.StudyCourses.CountAsync());
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 4
    [CoursePostgresFact]
    public async Task Already_started_cohort_cannot_be_checked_out()
    {
        var w = await CreateWorldAsync();
        try
        {
            var started = await CreateCohortAsync(w,
                [Utc(DateTime.UtcNow.AddHours(-2)), Utc(DateTime.UtcNow.AddDays(2)), Utc(DateTime.UtcNow.AddDays(4))]);
            var gateway = new TestPayOsGateway(Settings());
            var res = await CheckoutAsync(w, Learners[0], started, gateway);
            Assert.False(res.IsSuccess);
            Assert.Equal("COHORT_ENROLLMENT_CLOSED", res.ErrorCode);
            Assert.Equal(0, gateway.CreateCalls);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 5
    [CoursePostgresFact]
    public async Task Checkout_after_policy_enrollment_cutoff_is_closed()
    {
        var w = await CreateWorldAsync();
        try
        {
            // First session in ~30 minutes; policy cutoff is 60 minutes before the first session.
            var cohort = await CreateCohortAsync(w,
                [Utc(DateTime.UtcNow.AddMinutes(31)), Utc(DateTime.UtcNow.AddDays(3)), Utc(DateTime.UtcNow.AddDays(4))], policySlots: true);
            var gateway = new TestPayOsGateway(Settings());
            var res = await CheckoutAsync(w, Learners[0], cohort, gateway);
            Assert.False(res.IsSuccess);
            Assert.Equal("COHORT_ENROLLMENT_CLOSED", res.ErrorCode);
            Assert.Equal(0, gateway.CreateCalls);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 6
    [CoursePostgresFact]
    public async Task Full_cohort_rejects_checkout_with_COHORT_FULL()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5), capacity: 1);
            var gateway = new TestPayOsGateway(Settings());
            Assert.True((await CheckoutAsync(w, Learners[0], cohort, gateway)).IsSuccess);
            var full = await CheckoutAsync(w, Learners[1], cohort, gateway);
            Assert.False(full.IsSuccess);
            Assert.Equal(409, full.StatusCode);
            Assert.Equal("COHORT_FULL", full.ErrorCode);
            Assert.Equal(1, gateway.CreateCalls);
            Assert.Equal(0, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 7
    [CoursePostgresFact]
    public async Task Failed_payment_webhook_marks_order_failed_releases_seat_and_grants_nothing()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5), capacity: 1);
            var gateway = new TestPayOsGateway(Settings());
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));
            Assert.Equal(0, await SeatsAvailableAsync(w, cohort));

            var failed = await DeliverAsync(w, SignedWebhook(OrderCode(id), (int)PlanPrice, false, $"FT-fail-{id}"));
            Assert.True(failed.IsSuccess, failed.ErrorMessage);

            var c = await CourseAsync(w, id);
            Assert.Equal("Failed", c.PaymentStatus);
            Assert.Equal("PaymentFailed", c.Status);
            Assert.Empty(c.Sessions);
            Assert.Equal(0, await EntitlementCountAsync(w));
            Assert.Equal(1, await SeatsAvailableAsync(w, cohort));
            // The released seat is available to another learner.
            Assert.True((await CheckoutAsync(w, Learners[1], cohort, gateway)).IsSuccess);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 8
    [CoursePostgresFact]
    public async Task Late_success_after_reservation_expired_goes_to_payment_review_and_reconcile_expired_marks_payment_expired()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var gateway = new TestPayOsGateway(Settings());
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));
            await using (var db = w.Db())
                await db.StudyCourses.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.PaymentExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

            var late = await DeliverAsync(w, Paid(id, $"FT-late-{id}"));
            Assert.True(late.IsSuccess, late.ErrorMessage);
            var c = await CourseAsync(w, id);
            Assert.Equal("Paid", c.PaymentStatus);
            Assert.Equal("PaymentReview", c.Status);
            Assert.Equal("RESERVATION_EXPIRED", c.PaymentReviewReason);
            Assert.Equal($"FT-late-{id}", c.ProviderPaymentReference);
            Assert.Empty(c.Sessions);
            Assert.Equal(0, await EntitlementCountAsync(w));
            await using (var db = w.Db())
            {
                var detail = await CourseService(db, Learners[0], Role.Learner, gateway).DetailAsync(id, default);
                Assert.Equal("PaidUnderReview", Json(detail.Result).GetProperty("PaymentState").GetString());
            }

            // Reconcile: PayOS reports the link expired -> PaymentExpired, seat released.
            var second = IdOf(await CheckoutAsync(w, Learners[1], cohort, gateway));
            gateway.Information = code => new PaymentLinkInformation(code, PlanPrice, 0, "EXPIRED", $"plink-{code}");
            await using (var db = w.Db())
            {
                var reconciled = await CourseService(db, Learners[1], Role.Learner, gateway).ReconcileCoursePaymentAsync(second, default);
                Assert.True(reconciled.IsSuccess, reconciled.ErrorMessage);
                Assert.Equal("Expired", Json(reconciled.Result).GetProperty("PaymentState").GetString());
            }
            var expired = await CourseAsync(w, second);
            Assert.Equal("Expired", expired.PaymentStatus);
            Assert.Equal("PaymentExpired", expired.Status);
            Assert.Empty(expired.Sessions);
            Assert.Equal(4, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 9
    [CoursePostgresFact]
    public async Task Verified_success_webhook_activates_enrollment_with_sessions_and_exactly_one_consumed_purchase_entitlement()
    {
        var w = await CreateWorldAsync();
        try
        {
            var legacy = await CreateCohortAsync(w, Future(5), name: "Legacy");
            var policyCohort = await CreateCohortAsync(w, Future(12), policySlots: true, name: "Policy");
            var gateway = new TestPayOsGateway(Settings());
            foreach (var (learner, cohortId) in new[] { (Learners[0], legacy), (Learners[1], policyCohort) })
            {
                var id = IdOf(await CheckoutAsync(w, learner, cohortId, gateway));
                var reference = $"FT-ok-{id}";
                var res = await DeliverAsync(w, Paid(id, reference));
                Assert.True(res.IsSuccess, res.ErrorMessage);

                await using var db = w.Db();
                var c = await db.StudyCourses.AsNoTracking().Include(x => x.Sessions).SingleAsync(x => x.Id == id);
                Assert.Equal("Paid", c.PaymentStatus);
                Assert.Equal("Active", c.Status);
                Assert.Equal(reference, c.ProviderPaymentReference);
                Assert.NotNull(c.PaymentConfirmedAtUtc);
                var cohortSlots = await db.Set<StudyCohortSession>().Where(s => s.CohortId == cohortId).Select(s => s.AvailabilityId).ToListAsync();
                Assert.Equal(cohortSlots.Count, c.Sessions.Count);
                Assert.Equal(cohortSlots.OrderBy(x => x), c.Sessions.Select(s => s.AvailabilityId!.Value).OrderBy(x => x));
                var entitlement = Assert.Single(await db.CohortEnrollmentEntitlements.AsNoTracking().Where(e => e.LearnerId == learner).ToListAsync());
                Assert.Equal("VerifiedCoursePurchase", entitlement.SourceType);
                Assert.Equal("Consumed", entitlement.Status);
                Assert.Equal(id, entitlement.ConsumedByCourseId);
                Assert.Equal($"PURCHASE-{id}", entitlement.EntitlementReference);
                Assert.Equal(cohortId, entitlement.StudyCohortId);
                var detail = await CourseService(db, learner, Role.Learner, gateway).DetailAsync(id, default);
                Assert.Equal("Confirmed", Json(detail.Result).GetProperty("PaymentState").GetString());
                Assert.Equal(3, await SeatsAvailableAsync(w, cohortId));
            }
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 10
    [CoursePostgresFact]
    public async Task Forged_webhooks_are_rejected_and_leave_the_order_unchanged()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, new TestPayOsGateway(Settings())));

            var wrongKey = await DeliverAsync(w, SignedWebhook(OrderCode(id), (int)PlanPrice, true, $"FT-forged-{id}", key: "attacker-key"));
            Assert.False(wrongKey.IsSuccess);

            var tampered = SignedWebhook(OrderCode(id), 1000, true, $"FT-tamper-{id}");
            tampered.Data.Amount = (int)PlanPrice; // amount edited after signing
            Assert.False((await DeliverAsync(w, tampered)).IsSuccess);

            var flipped = SignedWebhook(OrderCode(id), (int)PlanPrice, false, $"FT-flip-{id}");
            flipped.Success = true; flipped.Data.Code = "00"; // failure flipped to success after signing
            Assert.False((await DeliverAsync(w, flipped)).IsSuccess);

            var unsigned = SignedWebhook(OrderCode(id), (int)PlanPrice, true, $"FT-unsigned-{id}");
            unsigned.Signature = "";
            Assert.False((await DeliverAsync(w, unsigned)).IsSuccess);

            await AssertUnpaidAndUntouchedAsync(w, id);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 11
    [CoursePostgresFact]
    public async Task Duplicate_and_concurrent_webhook_deliveries_enroll_once_and_grant_one_entitlement()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var gateway = new TestPayOsGateway(Settings());

            // Sequential replay of the exact same payload.
            var first = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));
            var webhook = Paid(first, $"FT-dup-{first}");
            Assert.True((await DeliverAsync(w, webhook)).IsSuccess);
            var replay = await DeliverAsync(w, webhook);
            Assert.True(replay.IsSuccess, replay.ErrorMessage);

            // Two concurrent deliveries, each with its own DbContext, released together.
            var second = IdOf(await CheckoutAsync(w, Learners[1], cohort, gateway));
            var concurrentWebhook = Paid(second, $"FT-conc-{second}");
            using var gate = new SemaphoreSlim(0, 2);
            async Task<ApiResponse<bool>> Deliver()
            {
                await gate.WaitAsync();
                await using var db = w.Db();
                return await DeliverAsync(db, concurrentWebhook);
            }
            var t1 = Task.Run(() => Deliver()); var t2 = Task.Run(() => Deliver());
            gate.Release(2);
            var results = await Task.WhenAll(t1, t2);
            Assert.All(results, r => Assert.True(r.IsSuccess, r.ErrorMessage));

            await using var check = w.Db();
            foreach (var id in new[] { first, second })
            {
                var c = await check.StudyCourses.AsNoTracking().Include(x => x.Sessions).SingleAsync(x => x.Id == id);
                Assert.Equal("Active", c.Status);
                Assert.Equal(3, c.Sessions.Count);
                Assert.Equal(1, await check.CohortEnrollmentEntitlements.CountAsync(e => e.ConsumedByCourseId == id));
                Assert.Equal(1, await check.Set<CourseAuditEntry>().CountAsync(a => a.CourseId == id && a.Action == "CohortPurchaseConfirmed"));
            }
            Assert.Equal(2, await check.StudyCourses.CountAsync(c => c.CohortId == cohort));
            Assert.Equal(2, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 12
    [CoursePostgresFact]
    public async Task Two_learners_racing_for_the_last_seat_produce_exactly_one_pending_order()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5), capacity: 1);
            var gateway = new TestPayOsGateway(Settings());
            using var gate = new SemaphoreSlim(0, 2);
            async Task<ApiResponse<object>> Checkout(int learner)
            {
                await gate.WaitAsync();
                return await CheckoutAsync(w, learner, cohort, gateway);
            }
            var t1 = Task.Run(() => Checkout(Learners[0])); var t2 = Task.Run(() => Checkout(Learners[1]));
            gate.Release(2);
            var results = await Task.WhenAll(t1, t2);

            var winner = Assert.Single(results, r => r.IsSuccess);
            var loser = Assert.Single(results, r => !r.IsSuccess);
            Assert.Equal(409, loser.StatusCode);
            Assert.Equal("COHORT_FULL", loser.ErrorCode);
            Assert.Equal("Pending", Json(winner.Result).GetProperty("PaymentStatus").GetString());
            await using var db = w.Db();
            var orders = await db.StudyCourses.Where(c => c.CohortId == cohort).ToListAsync();
            var order = Assert.Single(orders);
            Assert.Equal("Pending", order.PaymentStatus);
            Assert.Equal(1, gateway.CreateCalls);
            Assert.Equal(0, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 13
    [CoursePostgresFact]
    public async Task Successful_payment_when_enrollment_became_impossible_is_recorded_for_review_without_entitlement()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5), capacity: 1, policySlots: true);
            var gateway = new TestPayOsGateway(Settings());
            var payer = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));

            // The payer's hold lapses, and an Admin-granted entitlement enrollment takes the only seat.
            await using (var db = w.Db())
            {
                await db.StudyCourses.Where(c => c.Id == payer).ExecuteUpdateAsync(s => s.SetProperty(c => c.PaymentExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
                db.CohortEnrollmentEntitlements.Add(new CohortEnrollmentEntitlement
                {
                    StudyCohortId = cohort, PlanId = w.PlanId, LearnerId = Learners[1], SourceType = "AdminManualGrant",
                    EntitlementReference = $"ADMIN-{cohort}-{Learners[1]}", Status = "Active", GrantedById = AdminId
                });
                await db.SaveChangesAsync();
            }
            var admitted = await CheckoutAsync(w, Learners[1], cohort, gateway, entitlementReference: $"ADMIN-{cohort}-{Learners[1]}");
            Assert.True(admitted.IsSuccess, admitted.ErrorMessage);
            Assert.Equal("Active", Json(admitted.Result).GetProperty("Status").GetString());
            Assert.Equal(0m, Json(admitted.Result).GetProperty("Amount").GetDecimal());

            // The gateway still honoured the payer's link (hold window re-extended), and the money arrives.
            await using (var db = w.Db())
                await db.StudyCourses.Where(c => c.Id == payer).ExecuteUpdateAsync(s => s.SetProperty(c => c.PaymentExpiresAt, DateTime.UtcNow.AddMinutes(10)));
            var paid = await DeliverAsync(w, Paid(payer, $"FT-full-{payer}"));
            Assert.True(paid.IsSuccess, paid.ErrorMessage);

            var c = await CourseAsync(w, payer);
            Assert.Equal("Paid", c.PaymentStatus);
            Assert.Equal("PaymentReview", c.Status);
            Assert.Equal("COHORT_FULL", c.PaymentReviewReason);
            Assert.Equal($"FT-full-{payer}", c.ProviderPaymentReference);
            Assert.NotNull(c.PaymentConfirmedAtUtc);
            Assert.Empty(c.Sessions);
            await using (var db = w.Db())
            {
                Assert.Equal(0, await db.CohortEnrollmentEntitlements.CountAsync(e => e.EntitlementReference == $"PURCHASE-{payer}"));
                Assert.Equal(1, await db.StudyCourses.CountAsync(x => x.CohortId == cohort && x.Status == "Active"));
            }
            Assert.Equal(0, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 14
    [CoursePostgresFact]
    public async Task Duplicate_purchase_entitlement_is_prevented_by_unique_index_and_replay()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, new TestPayOsGateway(Settings())));
            var webhook = Paid(id, $"FT-uniq-{id}");
            Assert.True((await DeliverAsync(w, webhook)).IsSuccess);

            await using (var db = w.Db())
            {
                db.CohortEnrollmentEntitlements.Add(new CohortEnrollmentEntitlement
                {
                    StudyCohortId = cohort, PlanId = w.PlanId, LearnerId = Learners[0], SourceType = "VerifiedCoursePurchase",
                    EntitlementReference = $"PURCHASE-{id}", Status = "Consumed", ConsumedByCourseId = id, ConsumedAtUtc = DateTime.UtcNow
                });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
            }
            Assert.True((await DeliverAsync(w, webhook)).IsSuccess);
            await using (var db = w.Db())
                Assert.Equal(1, await db.CohortEnrollmentEntitlements.CountAsync(e => e.EntitlementReference == $"PURCHASE-{id}"));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 15 / 16
    private static async Task<(int CourseId, PayOsWebhookRequest Webhook)> FailEntitlementGrantOnceAsync(World w)
    {
        var cohort = await CreateCohortAsync(w, Future(5));
        var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, new TestPayOsGateway(Settings())));
        var webhook = Paid(id, $"FT-crash-{id}");
        var interceptor = new FailOnceOnEntitlementInterceptor();
        await using (var db = w.Db(interceptor))
        {
            var res = await DeliverAsync(db, webhook);
            Assert.False(res.IsSuccess);
        }
        Assert.Equal(1, interceptor.Thrown);
        return (id, webhook);
    }

    [CoursePostgresFact]
    public async Task Failure_while_granting_entitlement_rolls_back_payment_enrollment_and_entitlement_together()
    {
        var w = await CreateWorldAsync();
        try
        {
            var (id, _) = await FailEntitlementGrantOnceAsync(w);
            await AssertUnpaidAndUntouchedAsync(w, id);
            Assert.Equal(0, await EntitlementCountAsync(w));
        }
        finally { await DropAsync(w); }
    }

    [CoursePostgresFact]
    public async Task Webhook_redelivered_after_process_restart_succeeds_exactly_once()
    {
        var w = await CreateWorldAsync();
        try
        {
            var (id, webhook) = await FailEntitlementGrantOnceAsync(w);
            // Fresh DbContext + fresh services (simulated restart).
            await using (var db = w.Db())
                Assert.True((await DeliverAsync(db, webhook)).IsSuccess);
            await using (var db = w.Db())
                Assert.True((await DeliverAsync(db, webhook)).IsSuccess);

            var c = await CourseAsync(w, id);
            Assert.Equal("Paid", c.PaymentStatus);
            Assert.Equal("Active", c.Status);
            Assert.Equal(3, c.Sessions.Count);
            Assert.Equal(1, await EntitlementCountAsync(w, id));
            Assert.Equal(1, await EntitlementCountAsync(w));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 17
    [CoursePostgresFact]
    public async Task Double_click_checkout_reuses_one_order_and_one_payment_link()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var gateway = new TestPayOsGateway(Settings());
            var key = Guid.NewGuid();
            var a = await CheckoutAsync(w, Learners[0], cohort, gateway, key);
            var b = await CheckoutAsync(w, Learners[0], cohort, gateway, key);
            Assert.True(a.IsSuccess, a.ErrorMessage); Assert.True(b.IsSuccess, b.ErrorMessage);
            Assert.Equal(IdOf(a), IdOf(b));
            Assert.Equal(Json(a.Result).GetProperty("CheckoutUrl").GetString(), Json(b.Result).GetProperty("CheckoutUrl").GetString());

            var c = await CheckoutAsync(w, Learners[0], cohort, gateway, Guid.NewGuid());
            Assert.True(c.IsSuccess, c.ErrorMessage);
            Assert.Equal(IdOf(a), IdOf(c));
            Assert.True(Json(c.Result).GetProperty("Resumed").GetBoolean());
            Assert.Equal(1, gateway.CreateCalls);
            await using var db = w.Db();
            Assert.Equal(1, await db.StudyCourses.CountAsync(x => x.LearnerId == Learners[0]));
            Assert.Equal(3, await SeatsAvailableAsync(w, cohort));
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 18 / 19 shared 1-1 fixtures
    private static async Task<(int SubscriptionId, long GrantId)> SeedOneOnOneCreditAsync(World w, int learnerId)
    {
        await using var db = w.Db();
        var subPlan = new SubscriptionPlan
        {
            Name = "1-1 Plan", Price = 1000, DurationDays = 30, MeetingSessionCount = 2, CertificateLevelId = w.LevelId, IsActive = true
        };
        db.Add(subPlan);
        await db.SaveChangesAsync();
        var sub = new UserSubscription
        {
            UserId = learnerId, SubscriptionPlanId = subPlan.Id, CertificateLevelId = w.LevelId, MeetingSessionsIncluded = 2,
            MeetingSessionsUsed = 0, PaymentStatus = PaymentStatus.Paid, StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30), PurchasedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Add(sub);
        await db.SaveChangesAsync();
        var grant = new MentorCreditGrant
        {
            LearnerId = learnerId, UserSubscriptionId = sub.Id, CertificateLevelId = w.LevelId, InitialQuantity = 2, AvailableQuantity = 2,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1), ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };
        db.MentorCreditGrants.Add(grant);
        await db.SaveChangesAsync();
        return (sub.Id, grant.Id);
    }

    [CoursePostgresFact]
    public async Task Historical_one_on_one_consultation_credit_cannot_pay_for_or_be_consumed_by_a_cohort()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5), policySlots: true);
            var (subId, grantId) = await SeedOneOnOneCreditAsync(w, Learners[0]);
            var gateway = new TestPayOsGateway(Settings());

            var credit = await CheckoutAsync(w, Learners[0], cohort, gateway, entitlementReference: "CREDIT-1");
            Assert.False(credit.IsSuccess);
            Assert.Equal(409, credit.StatusCode);
            Assert.Equal("COHORT_ENTITLEMENT_INVALID", credit.ErrorCode);
            Assert.Equal(0, gateway.CreateCalls);

            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));
            Assert.Equal(PlanPrice, (await CourseAsync(w, id)).Amount);
            Assert.True((await DeliverAsync(w, Paid(id))).IsSuccess);
            Assert.Equal("Active", (await CourseAsync(w, id)).Status);

            await using var db = w.Db();
            var grant = await db.MentorCreditGrants.AsNoTracking().SingleAsync(g => g.Id == grantId);
            Assert.Equal(2, grant.AvailableQuantity);
            Assert.Equal(2, grant.InitialQuantity);
            Assert.Equal(0, (await db.UserSubscriptions.AsNoTracking().SingleAsync(s => s.Id == subId)).MeetingSessionsUsed);
            var entitlement = Assert.Single(await db.CohortEnrollmentEntitlements.AsNoTracking().ToListAsync());
            Assert.Equal("VerifiedCoursePurchase", entitlement.SourceType);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 19
    [CoursePostgresFact]
    public async Task Cohort_checkout_and_payment_never_create_one_on_one_bookings_or_subscriptions()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            await SeedOneOnOneCreditAsync(w, Learners[0]);
            int bookings, subscriptions, grants;
            await using (var db = w.Db())
            {
                bookings = await db.MentorBookings.CountAsync();
                subscriptions = await db.UserSubscriptions.CountAsync();
                grants = await db.MentorCreditGrants.CountAsync();
            }
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, new TestPayOsGateway(Settings())));
            Assert.True((await DeliverAsync(w, Paid(id))).IsSuccess);
            await using (var db = w.Db())
            {
                Assert.Equal(bookings, await db.MentorBookings.CountAsync());
                Assert.Equal(subscriptions, await db.UserSubscriptions.CountAsync());
                Assert.Equal(grants, await db.MentorCreditGrants.CountAsync());
                Assert.Equal("Active", (await db.StudyCourses.SingleAsync(c => c.Id == id)).Status);
            }
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ 20
    [CoursePostgresFact]
    public async Task Reconciliation_without_webhook_activates_via_status_query_is_idempotent_and_tolerates_gateway_outage()
    {
        var w = await CreateWorldAsync();
        try
        {
            var cohort = await CreateCohortAsync(w, Future(5));
            var gateway = new TestPayOsGateway(Settings());
            var id = IdOf(await CheckoutAsync(w, Learners[0], cohort, gateway));
            gateway.Information = code => new PaymentLinkInformation(code, PlanPrice, PlanPrice, "PAID", $"plink-reconcile-{code}");

            await using (var db = w.Db())
            {
                // Another learner can't reconcile this order.
                Assert.Equal(403, (await CourseService(db, Learners[1], Role.Learner, gateway).ReconcileCoursePaymentAsync(id, default)).StatusCode);
                var res = await CourseService(db, Learners[0], Role.Learner, gateway).ReconcileCoursePaymentAsync(id, default);
                Assert.True(res.IsSuccess, res.ErrorMessage);
                var json = Json(res.Result);
                Assert.Equal("Active", json.GetProperty("Status").GetString());
                Assert.Equal("Confirmed", json.GetProperty("PaymentState").GetString());
                Assert.False(json.GetProperty("GatewayUnavailable").GetBoolean());
            }
            await using (var db = w.Db())
            {
                var again = await CourseService(db, Learners[0], Role.Learner, gateway).ReconcileCoursePaymentAsync(id, default);
                Assert.True(again.IsSuccess, again.ErrorMessage);
                Assert.Equal("Confirmed", Json(again.Result).GetProperty("PaymentState").GetString());
            }
            Assert.Equal(1, gateway.InfoCalls); // a confirmed order is not re-queried
            var c = await CourseAsync(w, id);
            Assert.Equal(3, c.Sessions.Count);
            Assert.Equal($"plink-reconcile-{OrderCode(id)}", c.ProviderPaymentReference);
            await using (var db = w.Db())
            {
                var entitlement = Assert.Single(await db.CohortEnrollmentEntitlements.AsNoTracking().ToListAsync());
                Assert.Equal("VerifiedCoursePurchase", entitlement.SourceType);
                Assert.Equal(id, entitlement.ConsumedByCourseId);
                Assert.Contains(await db.Set<CourseAuditEntry>().Where(a => a.CourseId == id).Select(a => a.Detail).ToListAsync(),
                    d => d.Contains("PayOSStatusQuery"));
            }

            // Gateway outage: nothing changes and the caller is told the gateway is unavailable.
            var down = new TestPayOsGateway(Settings()) { InformationFailure = new HttpRequestException("PayOS unreachable") };
            var pending = IdOf(await CheckoutAsync(w, Learners[1], cohort, down));
            await using (var db = w.Db())
            {
                var res = await CourseService(db, Learners[1], Role.Learner, down).ReconcileCoursePaymentAsync(pending, default);
                Assert.True(res.IsSuccess, res.ErrorMessage);
                Assert.True(Json(res.Result).GetProperty("GatewayUnavailable").GetBoolean());
            }
            await AssertUnpaidAndUntouchedAsync(w, pending);
        }
        finally { await DropAsync(w); }
    }

    // ================================================================ transfer destination
    [CoursePostgresFact]
    public async Task Signed_webhook_for_a_transfer_destination_enrollment_is_rejected_as_not_a_purchase()
    {
        var w = await CreateWorldAsync();
        try
        {
            var source = await CreateCohortAsync(w, Future(5), name: "Source");
            var target = await CreateCohortAsync(w, Future(12), name: "Target");
            var sourceCourse = await InsertPaidEnrollmentAsync(w, Learners[0], source, c => c.Status = "Transferred");
            var dest = await InsertPaidEnrollmentAsync(w, Learners[0], target, c =>
            {
                c.TransferredFromCourseId = sourceCourse; c.Amount = 0; c.PaymentReference = $"TRANSFER:{sourceCourse}";
            });

            var res = await DeliverAsync(w, Paid(dest, $"FT-transfer-{dest}"));
            Assert.False(res.IsSuccess);
            Assert.Equal("PAYMENT_ORDER_NOT_PURCHASE", res.ErrorCode);
            Assert.Equal(409, res.StatusCode);

            var c = await CourseAsync(w, dest);
            Assert.Equal(0m, c.Amount);
            Assert.Equal($"TRANSFER:{sourceCourse}", c.PaymentReference);
            Assert.Null(c.ProviderPaymentReference);
            Assert.Equal(3, c.Sessions.Count);
            Assert.Equal(0, await EntitlementCountAsync(w));
        }
        finally { await DropAsync(w); }
    }
}
