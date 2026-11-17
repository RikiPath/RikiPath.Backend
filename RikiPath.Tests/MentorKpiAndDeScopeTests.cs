using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Repositories;
using RikiPath.Infrastructure.Services;
using RikiPath.WebApi.Authorization;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class MentorKpiAndDeScopeTests
{
    private sealed class MutableClaimService(int userId, Role role) : IClaimService
    {
        public int UserId { get; set; } = userId;
        public Role Role { get; set; } = role;

        public ClaimDTO GetUserClaim() => new() { Id = UserId, Role = Role };
    }

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"mentor_kpi_descope_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options);
        db.CertificateTypes.Add(new CertificateType { Id = 1, Name = "JLPT", IsActive = true });
        db.CertificateLevels.AddRange(
            new CertificateLevel { Id = 1, CertificateTypeId = 1, Code = "N5", SortOrder = 1 },
            new CertificateLevel { Id = 2, CertificateTypeId = 1, Code = "N4", SortOrder = 2 },
            new CertificateLevel { Id = 3, CertificateTypeId = 1, Code = "N3", SortOrder = 3 },
            new CertificateLevel { Id = 4, CertificateTypeId = 1, Code = "N2", SortOrder = 4 },
            new CertificateLevel { Id = 5, CertificateTypeId = 1, Code = "N1", SortOrder = 5 });
        db.SaveChanges();
        return db;
    }

    private static AuthorizationFilterContext BuildAuthContext(bool? enabledFlag)
    {
        var configValues = new Dictionary<string, string?>();
        if (enabledFlag.HasValue)
        {
            configValues["MentorFinance:PayrollAndPayoutEnabled"] = enabledFlag.Value ? "true" : "false";
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Fact]
    public void MentorFinanceDisabled_BlocksFinancialEndpoints_AndKeepsQualificationsActive()
    {
        var mentorDiscontinued = new[]
        {
            nameof(MentorCompensationController.GetMyOverview),
            nameof(MentorCompensationController.GetMyHourlyRates),
            nameof(MentorCompensationController.GetMyEarnings),
            nameof(MentorCompensationController.GetMyPayrollPeriods),
            nameof(MentorCompensationController.GetMyPayrollPeriod),
            nameof(MentorCompensationController.GetMyBankAccounts),
            nameof(MentorCompensationController.UpsertMyBankAccount),
            nameof(MentorCompensationController.GetMyPayouts),
            nameof(MentorCompensationController.GetMyPayoutObligation),
        };

        foreach (var methodName in mentorDiscontinued)
        {
            var method = typeof(MentorCompensationController).GetMethod(methodName);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<MentorFinanceDisabledAttribute>());
        }

        var mentorQualMethod = typeof(MentorCompensationController).GetMethod(nameof(MentorCompensationController.GetMyQualifications));
        Assert.NotNull(mentorQualMethod);
        Assert.Null(mentorQualMethod!.GetCustomAttribute<MentorFinanceDisabledAttribute>());

        var adminDiscontinued = new[]
        {
            nameof(AdminMentorCompensationController.GetHourlyRates),
            nameof(AdminMentorCompensationController.ConfigureHourlyRate),
            nameof(AdminMentorCompensationController.GetJlptCoefficients),
            nameof(AdminMentorCompensationController.ConfigureJlptCoefficient),
            nameof(AdminMentorCompensationController.EvaluateBookingAttendance),
            nameof(AdminMentorCompensationController.ReviewBookingAttendance),
            nameof(AdminMentorCompensationController.GetBookingAttendanceApproval),
            nameof(AdminMentorCompensationController.GetEarnings),
            nameof(AdminMentorCompensationController.GenerateMonthlyPayroll),
            nameof(AdminMentorCompensationController.GetPayrollPeriods),
            nameof(AdminMentorCompensationController.GetPayrollPeriod),
            nameof(AdminMentorCompensationController.AddPayrollAdjustment),
            nameof(AdminMentorCompensationController.ApproveMonthlyPayroll),
            nameof(AdminMentorCompensationController.GetMentorBankAccounts),
            nameof(AdminMentorCompensationController.VerifyMentorBankAccount),
            nameof(AdminMentorCompensationController.GetOrganizationPayoutAccounts),
            nameof(AdminMentorCompensationController.ConfigureOrganizationPayoutAccount),
            nameof(AdminMentorCompensationController.GetPayoutObligations),
            nameof(AdminMentorCompensationController.GetPayoutObligation),
            nameof(AdminMentorCompensationController.SelectPayoutMethod),
            nameof(AdminMentorCompensationController.InitiateManualPayout),
            nameof(AdminMentorCompensationController.ConfirmManualPayout),
            nameof(AdminMentorCompensationController.ExecutePayOsPayout),
            nameof(AdminMentorCompensationController.ExecutePayOsBatchPayout),
            nameof(AdminMentorCompensationController.ReconcilePayoutObligation),
            nameof(AdminMentorCompensationController.GetPayOsPayoutBalance),
            nameof(AdminMentorCompensationController.EstimatePayOsBatchCredit),
        };

        foreach (var methodName in adminDiscontinued)
        {
            var method = typeof(AdminMentorCompensationController).GetMethod(methodName);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttribute<MentorFinanceDisabledAttribute>());
        }

        foreach (var keptQualMethod in new[]
        {
            nameof(AdminMentorCompensationController.GetQualifications),
            nameof(AdminMentorCompensationController.GrantQualification),
            nameof(AdminMentorCompensationController.RevokeQualification),
        })
        {
            var method = typeof(AdminMentorCompensationController).GetMethod(keptQualMethod);
            Assert.NotNull(method);
            Assert.Null(method!.GetCustomAttribute<MentorFinanceDisabledAttribute>());
        }

        // Runtime filter check: default (missing config) and explicit false -> 403 MENTOR_FINANCE_OUT_OF_SCOPE
        var filter = new MentorFinanceDisabledAttribute();
        foreach (bool? flag in new bool?[] { null, false })
        {
            var authCtx = BuildAuthContext(flag);
            filter.OnAuthorization(authCtx);
            var objResult = Assert.IsType<ObjectResult>(authCtx.Result);
            Assert.Equal((int)HttpStatusCode.Forbidden, objResult.StatusCode);
            var payload = Assert.IsType<ApiResponse<object>>(objResult.Value);
            Assert.False(payload.IsSuccess);
            Assert.Equal("MENTOR_FINANCE_OUT_OF_SCOPE", payload.ErrorCode);
        }

        var enabledCtx = BuildAuthContext(true);
        filter.OnAuthorization(enabledCtx);
        Assert.Null(enabledCtx.Result);
    }

    [Fact]
    public async Task BookSlotAsync_SucceedsWithoutHourlyRateOrJlptCoefficient_WhenQualificationExists()
    {
        await using var db = CreateInMemoryDb();
        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", Email = "admin@riki.jp", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 10, Role = Role.Mentor, FirstName = "Haruto", LastName = "Sato", Email = "haruto@riki.jp", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 20, Role = Role.Learner, FirstName = "An", LastName = "Nguyen", Email = "an@riki.jp", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);
        db.MentorProfiles.Add(new MentorProfile
        {
            MentorId = mentor.Id,
            VerificationStatus = MentorVerificationStatus.Approved,
            Bio = "JLPT N3/N2 Mentor"
        });

        db.MentorTeachingQualifications.Add(new MentorTeachingQualification
        {
            MentorUserId = mentor.Id,
            CertificateLevelId = 3, // N3
            IsActive = true,
            VerifiedByAdminId = admin.Id,
            VerifiedAtUtc = DateTime.UtcNow.AddDays(-5)
        });

        var slotStart = DateTime.UtcNow.Date.AddDays(3).AddHours(3);
        var slot = new MentorAvailability
        {
            Id = 500,
            MentorId = mentor.Id,
            StartTime = slotStart,
            EndTime = slotStart.AddMinutes(90),
            MaxLearners = 1,
            BookedCount = 0,
            IsApproved = true,
            RoomId = Guid.NewGuid()
        };
        db.MentorAvailabilities.Add(slot);

        var sub = new UserSubscription
        {
            Id = 600,
            UserId = learner.Id,
            SubscriptionPlanId = 1,
            CertificateLevelId = 3, // N3
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 2,
            PurchasedAt = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        db.UserSubscriptions.Add(sub);

        var grant = new MentorCreditGrant
        {
            Id = 700,
            LearnerId = learner.Id,
            UserSubscriptionId = sub.Id,
            CertificateLevelId = 3,
            InitialQuantity = 2,
            AvailableQuantity = 2,
            EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MentorCreditGrants.Add(grant);
        await db.SaveChangesAsync();

        // Zero MentorHourlyRates and zero JlptLevelPayCoefficients seeded
        Assert.Empty(db.MentorHourlyRates);
        Assert.Empty(db.JlptLevelPayCoefficients);

        var claims = new MutableClaimService(learner.Id, Role.Learner);
        var uow = new UnitOfWork(db);
        var creditLedger = new MentorCreditLedgerService(uow, claims, NullLogger<MentorCreditLedgerService>.Instance);
        var settings = new AppSettings
        {
            FrontendBaseUrl = "https://rikipath.test"
        };
        var bookingService = new MentorBookingService(db, uow, claims, creditLedger, settings, NullLogger<MentorBookingService>.Instance);

        var result = await bookingService.BookSlotAsync(new BookIncludedMeetingRequest
        {
            MentorAvailabilityId = slot.Id,
            JlptLevel = "N3",
            Question = "Em muốn luyện đọc hiểu N3."
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);

        var savedBooking = await db.MentorBookings.SingleAsync(b => b.Id == result.Result!.Id);
        Assert.Equal(3, savedBooking.CompensationLevelId);
        Assert.Null(savedBooking.FrozenHourlyRate);
        Assert.Null(savedBooking.FrozenLevelCoefficient);
        Assert.Null(savedBooking.ScheduledCompensationAmount);
        Assert.Equal(1, (await db.MentorCreditGrants.FindAsync(grant.Id))!.AvailableQuantity);
    }

    [Fact]
    public async Task MentorKpiService_AggregatesSessionsAttendanceHomeworkReviewsDisputes_AndHandlesNullTurnaround()
    {
        await using var db = CreateInMemoryDb();
        var admin = new UserAccount { Id = 1, Role = Role.Admin, FirstName = "Admin", Email = "admin@riki.jp", PasswordHash = [], PasswordSalt = [] };
        var mentor = new UserAccount { Id = 11, Role = Role.Mentor, FirstName = "Yuki", LastName = "Tanaka", Email = "yuki@riki.jp", PasswordHash = [], PasswordSalt = [] };
        var learner = new UserAccount { Id = 21, Role = Role.Learner, FirstName = "Binh", LastName = "Le", Email = "binh@riki.jp", PasswordHash = [], PasswordSalt = [] };
        db.Users.AddRange(admin, mentor, learner);

        db.MentorTeachingQualifications.AddRange(
            new MentorTeachingQualification { MentorUserId = mentor.Id, CertificateLevelId = 3, IsActive = true, VerifiedByAdminId = admin.Id, VerifiedAtUtc = DateTime.UtcNow },
            new MentorTeachingQualification { MentorUserId = mentor.Id, CertificateLevelId = 4, IsActive = true, VerifiedByAdminId = admin.Id, VerifiedAtUtc = DateTime.UtcNow });

        var plan = new StudyCoursePlan
        {
            Id = 1,
            Name = "N3 1-1",
            CertificateLevelId = 3,
            DurationMonths = 1,
            SessionsPerWeek = 2,
            SessionMinutes = 60,
            TotalSessions = 4,
            Price = 1_500_000m,
            IsPublished = true
        };
        db.StudyCoursePlans.Add(plan);

        var sub = new UserSubscription
        {
            Id = 10,
            UserId = learner.Id,
            SubscriptionPlanId = 1,
            CertificateLevelId = 3,
            PaymentStatus = PaymentStatus.Paid,
            MeetingSessionsIncluded = 4,
            PurchasedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.UserSubscriptions.Add(sub);

        var now = DateTime.UtcNow;
        var slotCompleted = new MentorAvailability { Id = 101, MentorId = mentor.Id, StartTime = now.AddDays(-4), EndTime = now.AddDays(-4).AddMinutes(90), IsApproved = true, RoomId = Guid.NewGuid() };
        var slotCancelled = new MentorAvailability { Id = 102, MentorId = mentor.Id, StartTime = now.AddDays(-3), EndTime = now.AddDays(-3).AddMinutes(90), IsApproved = true, RoomId = Guid.NewGuid() };
        var slotMissed = new MentorAvailability { Id = 103, MentorId = mentor.Id, StartTime = now.AddDays(-2), EndTime = now.AddDays(-2).AddMinutes(90), IsApproved = true, RoomId = Guid.NewGuid() };
        var slotUpcoming = new MentorAvailability { Id = 104, MentorId = mentor.Id, StartTime = now.AddDays(2), EndTime = now.AddDays(2).AddMinutes(90), IsApproved = true, RoomId = Guid.NewGuid() };
        db.MentorAvailabilities.AddRange(slotCompleted, slotCancelled, slotMissed, slotUpcoming);

        var bCompleted = new MentorBooking { Id = 201, UserSubscriptionId = sub.Id, MentorAvailabilityId = slotCompleted.Id, CompensationLevelId = 3, Status = MentorStatus.Completed, ScheduledAt = slotCompleted.StartTime, AttendedAt = slotCompleted.StartTime.AddMinutes(5), CompletedAt = slotCompleted.EndTime };
        var bCancelled = new MentorBooking { Id = 202, UserSubscriptionId = sub.Id, MentorAvailabilityId = slotCancelled.Id, CompensationLevelId = 3, Status = MentorStatus.Cancelled, CancelledByRole = "Mentor", CancelledAtUtc = now.AddDays(-3) };
        var bMissed = new MentorBooking { Id = 203, UserSubscriptionId = sub.Id, MentorAvailabilityId = slotMissed.Id, CompensationLevelId = 3, Status = MentorStatus.Accepted, ScheduledAt = slotMissed.StartTime };
        var bUpcoming = new MentorBooking { Id = 204, UserSubscriptionId = sub.Id, MentorAvailabilityId = slotUpcoming.Id, CompensationLevelId = 3, Status = MentorStatus.Accepted, ScheduledAt = slotUpcoming.StartTime };
        db.MentorBookings.AddRange(bCompleted, bCancelled, bMissed, bUpcoming);

        db.MentorAttendanceRecords.AddRange(
            new MentorAttendanceRecord { Id = 301, MentorBookingId = bCompleted.Id, RoomId = slotCompleted.RoomId, UserId = mentor.Id, Role = "Mentor", Status = MentorAttendanceStatus.Completed, TotalDurationSeconds = 75 * 60, FirstJoinedAtUtc = slotCompleted.StartTime },
            new MentorAttendanceRecord { Id = 302, MentorBookingId = bCompleted.Id, RoomId = slotCompleted.RoomId, UserId = learner.Id, Role = "Learner", Status = MentorAttendanceStatus.Completed, TotalDurationSeconds = 70 * 60, FirstJoinedAtUtc = slotCompleted.StartTime });

        var course = new StudyCourse
        {
            Id = 401,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            PlanId = plan.Id,
            Name = "Lộ trình N3",
            CertificateLevelId = 3,
            PlanRevision = 1,
            DurationMonths = 1,
            SessionsPerWeek = 2,
            SessionMinutes = 60,
            TotalSessions = 4,
            Amount = 1_500_000m,
            PaymentStatus = "Paid",
            Status = "Active",
            StartsAt = now.AddDays(-10),
            EndsAt = now.AddDays(20),
            PurchaseKey = Guid.NewGuid()
        };
        course.Sessions.Add(new StudyCourseSession { Id = 501, Number = 1, Attempt = 1, ScheduledAt = now.AddDays(-5), Status = "Present", ConfirmedById = mentor.Id, ConfirmedAt = now.AddDays(-5).AddHours(1) });
        course.Sessions.Add(new StudyCourseSession { Id = 502, Number = 2, Attempt = 1, ScheduledAt = now.AddDays(-1), Status = "CancelledByMentor", Note = "Bận đột xuất", ConfirmedById = mentor.Id, ConfirmedAt = now.AddDays(-1) });
        course.Homework.Add(new StudyCourseHomework
        {
            Id = 601,
            Title = "BTVN Ngữ pháp N3",
            Instructions = "Hoàn thành 10 câu",
            DueAt = now.AddDays(5),
            SubmittedAt = now.AddDays(-2),
            ReviewedAt = now.AddDays(-2).AddMinutes(120),
            Passed = true,
            Feedback = "Tốt"
        });
        db.StudyCourses.Add(course);

        db.HomeworkAssignments.Add(new HomeworkAssignment
        {
            Id = 701,
            LearnerId = learner.Id,
            MentorId = mentor.Id,
            MentorBookingId = bCompleted.Id,
            Title = "Bài tập Kanji 1-1",
            Status = "Submitted",
            CreatedDate = now.AddDays(-4),
            Submission = new HomeworkSubmission
            {
                Id = 702,
                LearnerId = learner.Id,
                SubmittedAt = now.AddDays(-3)
            }
        });

        db.MentorReviews.AddRange(
            new MentorReview { Id = 801, MentorBookingId = bCompleted.Id, MentorId = mentor.Id, LearnerId = learner.Id, Rating = 5, ModerationStatus = MentorReviewStatus.Approved, CreatedAtUtc = now.AddDays(-3) },
            new MentorReview { Id = 802, MentorBookingId = bCancelled.Id, MentorId = mentor.Id, LearnerId = learner.Id, Rating = 4, ModerationStatus = MentorReviewStatus.Approved, CreatedAtUtc = now.AddDays(-2) });

        db.MentorDisputes.Add(new MentorDispute
        {
            Id = 901,
            MentorBookingId = bMissed.Id,
            MentorId = mentor.Id,
            LearnerId = learner.Id,
            Status = MentorDisputeStatus.UnderReview,
            Reason = "Chờ kiểm tra",
            CreatedAtUtc = now.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var claims = new MutableClaimService(mentor.Id, Role.Mentor);
        var kpiService = new MentorKpiService(db, claims);

        var from = DateOnly.FromDateTime(now.AddDays(-15));
        var to = DateOnly.FromDateTime(now.AddDays(5));
        var res = await kpiService.GetMyKpiSummaryAsync(from, to);

        Assert.True(res.IsSuccess, res.ErrorMessage);
        var kpi = res.Result!;
        Assert.Equal(["N3", "N2"], kpi.TeachingJlptLevels);
        Assert.Equal(6, kpi.TotalAssignedSessions); // 4 bookings + 2 course sessions
        Assert.Equal(4, kpi.AssignedBookingsCount);
        Assert.Equal(2, kpi.AssignedCourseSessionsCount);
        Assert.Equal(2, kpi.CompletedValidSessions); // 1 booking + 1 course present
        Assert.Equal(75 + 60, kpi.ConfirmedTeachingMinutes); // 75m WebRTC booking + 60m course fallback
        Assert.Equal(90 + 60, kpi.ScheduledCompletedMinutes);
        Assert.Equal(2, kpi.CancelledSessionsCount);
        Assert.Equal(2, kpi.CancelledByMentorCount);
        Assert.Equal(1, kpi.MissedOrNoShowSessionsCount);
        Assert.Equal(1, kpi.UpcomingOrIncompleteSessionsCount);
        Assert.Equal(2, kpi.HomeworkAssignedCount);
        Assert.Equal(2, kpi.HomeworkSubmittedCount);
        Assert.Equal(1, kpi.HomeworkGradedCount);
        Assert.Equal(120.0, kpi.AverageFeedbackTurnaroundMinutes);
        Assert.Equal(1, kpi.FeedbackTurnaroundSampleCount);
        Assert.Equal(4.5, kpi.AverageRating);
        Assert.Equal(2, kpi.TotalReviewsCount);
        Assert.Equal(1, kpi.TotalDisputesCount);
        Assert.Equal(1, kpi.OpenDisputesCount);
        Assert.NotEmpty(kpi.WeeklyBreakdown);
        Assert.NotEmpty(kpi.MonthlyBreakdown);
        Assert.Equal(6, kpi.RecentSessions.Count);

        // Role enforcement + Admin attendance detail
        var forbiddenAdminCall = await kpiService.GetAdminBookingAttendanceAsync(bCompleted.Id);
        Assert.Equal((int)HttpStatusCode.Forbidden, forbiddenAdminCall.StatusCode);

        claims.UserId = admin.Id;
        claims.Role = Role.Admin;

        var adminKpiList = await kpiService.GetAdminMentorKpisAsync(from, to, mentor.Id);
        Assert.True(adminKpiList.IsSuccess);
        Assert.Single(adminKpiList.Result!);

        var adminAtt = await kpiService.GetAdminBookingAttendanceAsync(bCompleted.Id);
        Assert.True(adminAtt.IsSuccess, adminAtt.ErrorMessage);
        Assert.True(adminAtt.Result!.BothJoinedAtLeastOneMinute);
        Assert.Equal(75 * 60, adminAtt.Result.MentorObservedSeconds);
        Assert.Equal(70 * 60, adminAtt.Result.LearnerObservedSeconds);

        // Null turnaround verification when there are zero graded submissions
        var mentorNoGrades = new UserAccount { Id = 12, Role = Role.Mentor, FirstName = "Ken", Email = "ken@riki.jp", PasswordHash = [], PasswordSalt = [] };
        db.Users.Add(mentorNoGrades);
        await db.SaveChangesAsync();

        var emptyTurnaround = await kpiService.GetAdminMentorKpiByMentorAsync(mentorNoGrades.Id, from, to);
        Assert.True(emptyTurnaround.IsSuccess);
        Assert.Null(emptyTurnaround.Result!.AverageFeedbackTurnaroundMinutes);
        Assert.Equal(0, emptyTurnaround.Result.FeedbackTurnaroundSampleCount);
        Assert.Null(emptyTurnaround.Result.AverageRating);
    }

    [Fact]
    public async Task MentorKpiService_DeduplicatesCohortGroupSessions_FourLearnersEqualOneTeachingSession()
    {
        await using var db = CreateInMemoryDb();
        var mentor = new UserAccount { Id = 21, Role = Role.Mentor, FirstName = "Akiko", LastName = "Sato", Email = "akiko@riki.jp", PasswordHash = [], PasswordSalt = [] };
        var learners = Enumerable.Range(201, 4).Select(id => new UserAccount
        {
            Id = id, Role = Role.Learner, FirstName = "HV", LastName = id.ToString(), Email = $"hv{id}@riki.jp", PasswordHash = [], PasswordSalt = []
        }).ToArray();
        db.Users.AddRange([mentor, .. learners]);

        var plan = new StudyCoursePlan
        {
            Id = 50, Name = "N4 Nhóm", CertificateLevelId = 2, SessionType = "Group", Capacity = 4,
            DurationMonths = 1, SessionsPerWeek = 3, SessionMinutes = 90, TotalSessions = 2, Price = 500_000m, IsPublished = true
        };
        var now = DateTime.UtcNow;
        var room1 = Guid.NewGuid();
        var room2 = Guid.NewGuid();
        var slot1 = new MentorAvailability { Id = 601, MentorId = mentor.Id, StartTime = now.AddDays(-2), EndTime = now.AddDays(-2).AddMinutes(90), MaxLearners = 4, IsApproved = true, RoomId = room1 };
        var slot2 = new MentorAvailability { Id = 602, MentorId = mentor.Id, StartTime = now.AddDays(3), EndTime = now.AddDays(3).AddMinutes(90), MaxLearners = 4, IsApproved = true, RoomId = room2 };
        var cohort = new StudyCohort
        {
            Id = 701, Name = "N4 T2-T4-T6", PlanId = plan.Id, MentorId = mentor.Id, Capacity = 4, Status = "Published",
            StartsAt = slot1.StartTime, EndsAt = slot2.EndTime,
            Sessions = [new() { Id = 801, Number = 1, AvailabilityId = slot1.Id }, new() { Id = 802, Number = 2, AvailabilityId = slot2.Id }]
        };
        db.AddRange(plan, slot1, slot2, cohort);

        for (var i = 0; i < learners.Length; i++)
        {
            var enrollment = new StudyCourse
            {
                Id = 900 + i, CohortId = cohort.Id, PlanId = plan.Id, CertificateLevelId = 2, LearnerId = learners[i].Id,
                Name = cohort.Name, SessionType = "Group", DurationMonths = 1, SessionsPerWeek = 3, TotalSessions = 2, SessionMinutes = 90,
                PaymentStatus = "Paid", Status = "Active", StartsAt = slot1.StartTime, EndsAt = slot2.EndTime, PurchaseKey = Guid.NewGuid()
            };
            enrollment.Sessions.Add(new StudyCourseSession { Id = 1000 + i * 2, Number = 1, AvailabilityId = slot1.Id, ScheduledAt = slot1.StartTime, Status = "Present", ConfirmedById = mentor.Id, ConfirmedAt = slot1.EndTime });
            enrollment.Sessions.Add(new StudyCourseSession { Id = 1001 + i * 2, Number = 2, AvailabilityId = slot2.Id, ScheduledAt = slot2.StartTime, Status = "Scheduled" });
            db.StudyCourses.Add(enrollment);
        }

        // Single Mentor attendance record on Room1 (90 minutes observed).
        db.MentorAttendanceRecords.Add(new MentorAttendanceRecord
        {
            Id = 1101, RoomId = room1, UserId = mentor.Id, Role = "Mentor",
            Status = MentorAttendanceStatus.Completed, TotalDurationSeconds = 90 * 60, FirstJoinedAtUtc = slot1.StartTime
        });
        await db.SaveChangesAsync();

        var claims = new MutableClaimService(mentor.Id, Role.Mentor);
        var kpiService = new MentorKpiService(db, claims);
        var from = DateOnly.FromDateTime(now.AddDays(-7));
        var to = DateOnly.FromDateTime(now.AddDays(7));
        var res = await kpiService.GetMyKpiSummaryAsync(from, to);

        Assert.True(res.IsSuccess, res.ErrorMessage);
        var kpi = res.Result!;
        // 4 learners x 2 sessions MUST deduplicate to exactly 2 Mentor teaching sessions (1 past completed 90m, 1 upcoming 90m).
        Assert.Equal(2, kpi.TotalAssignedSessions);
        Assert.Equal(2, kpi.AssignedCourseSessionsCount);
        Assert.Equal(1, kpi.CompletedValidSessions);
        Assert.Equal(1, kpi.UpcomingOrIncompleteSessionsCount);
        Assert.Equal(90, kpi.ScheduledCompletedMinutes);
        Assert.Equal(90, kpi.ConfirmedTeachingMinutes);
        Assert.Equal(2, kpi.RecentSessions.Count);
    }
}
