using System.Net;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Services;

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
            Name = "N3 Group",
            CertificateLevelId = 3,
            SessionType = "Group",
            Capacity = 4,
            DurationMonths = 1,
            SessionsPerWeek = 2,
            SessionMinutes = 60,
            TotalSessions = 4,
            Price = 1_500_000m,
            IsPublished = true
        };
        db.StudyCoursePlans.Add(plan);

        var now = DateTime.UtcNow;
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
            StudyCourseId = course.Id,
            Title = "Bài tập Kanji",
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
            new MentorReview { Id = 801, MentorBookingId = 201, MentorId = mentor.Id, LearnerId = learner.Id, Rating = 5, ModerationStatus = MentorReviewStatus.Approved, CreatedAtUtc = now.AddDays(-3) },
            new MentorReview { Id = 802, MentorBookingId = 202, MentorId = mentor.Id, LearnerId = learner.Id, Rating = 4, ModerationStatus = MentorReviewStatus.Approved, CreatedAtUtc = now.AddDays(-2) });

        db.MentorDisputes.Add(new MentorDispute
        {
            Id = 901,
            MentorBookingId = 203,
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
        Assert.Equal(2, kpi.TotalAssignedSessions);
        Assert.Equal(0, kpi.AssignedBookingsCount);
        Assert.Equal(2, kpi.AssignedCourseSessionsCount);
        Assert.Equal(1, kpi.CompletedValidSessions);
        Assert.Equal(60, kpi.ConfirmedTeachingMinutes);
        Assert.Equal(60, kpi.ScheduledCompletedMinutes);
        Assert.Equal(1, kpi.CancelledSessionsCount);
        Assert.Equal(1, kpi.CancelledByMentorCount);
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
        Assert.Equal(2, kpi.RecentSessions.Count);

        claims.UserId = admin.Id;
        claims.Role = Role.Admin;

        var adminKpiList = await kpiService.GetAdminMentorKpisAsync(from, to, mentor.Id);
        Assert.True(adminKpiList.IsSuccess);
        Assert.Single(adminKpiList.Result!);

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
