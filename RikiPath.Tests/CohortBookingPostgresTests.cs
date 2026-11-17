using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Npgsql;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class CohortBookingPostgresTests
{
    static CohortBookingPostgresTests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    private sealed class BookingDb(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            var keep = new HashSet<Type> { typeof(UserAccount), typeof(CertificateType), typeof(CertificateLevel),
                typeof(StudyCoursePlan), typeof(StudyCourse), typeof(StudyCourseSession), typeof(StudyCourseHomework),
                typeof(CourseRetakeRequest), typeof(CourseAuditEntry), typeof(StudyCohort), typeof(StudyCohortSession),
                typeof(MentorAvailability), typeof(MentorGroupPolicy), typeof(MentorBooking), typeof(UserSubscription), typeof(SubscriptionPlan),
                typeof(CourseScheduleRequest), typeof(JlptExamSchedule), typeof(MentorCalendarConnection), typeof(MentorOutboxEvent) };
            foreach (var type in b.Model.GetEntityTypes().Select(x => x.ClrType).Distinct().Where(t => !keep.Contains(t)).ToList()) b.Ignore(type);

        }
    }

    [CoursePostgresFact]
    public async Task Last_seat_is_atomic_expiration_releases_it_and_late_payment_is_reviewed()
    {
        var schema = "booking_qa_" + Guid.NewGuid().ToString("N");
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RIKIPATH_COURSE_TEST_DB")) { SearchPath = schema }.ConnectionString;
        await using var conn = new NpgsqlConnection(cs); await conn.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", conn)) await command.ExecuteNonQueryAsync();
        BookingDb Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        try
        {
            await using var setup = Context();
            var operations = setup.GetService<IMigrationsModelDiffer>().GetDifferences(null,
                setup.GetService<IDesignTimeModel>().Model.GetRelationalModel()).Where(x => x is not InsertDataOperation).ToList();
            foreach (var sql in setup.GetService<IMigrationsSqlGenerator>().Generate(operations, setup.Model))
                await setup.Database.ExecuteSqlRawAsync(sql.CommandText.Replace("{", "{{").Replace("}", "}}"));
            var mentor = new UserAccount { Id = 1100, Role = Role.Mentor, Email = "mentor@test.invalid", PasswordHash = [], PasswordSalt = [] };
            var users = new[] { 1101, 1102, 1103, 1104, 1105 }.Select(id => new UserAccount { Id = id, Role = Role.Learner, Email = $"{id}@test.invalid", PasswordHash = [], PasswordSalt = [] }).ToArray();
            setup.Users.Add(mentor); setup.Users.AddRange(users);
            var level = new CertificateLevel { Code = "N5", CertificateType = new CertificateType { Name = "JLPT", IsActive = true } };
            var plan = new StudyCoursePlan { Name = "Group", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 60, TotalSessions = 1, DurationMonths = 1, SessionsPerWeek = 1, Price = 100, IsPublished = true };
            var startsAt = DateTime.UtcNow.AddDays(5);
            var slot = new MentorAvailability { MentorId = mentor.Id, IsApproved = true, StartTime = startsAt, EndTime = startsAt.AddHours(1), MaxLearners = 4 };
            var cohort = new StudyCohort { Name = "Class", MentorId = mentor.Id, Plan = plan, Capacity = 4, Status = "Published", StartsAt = slot.StartTime, EndsAt = slot.EndTime,
                Sessions = [new() { Number = 1, Availability = slot }] };
            foreach (var learner in users.Skip(2))
                cohort.Enrollments.Add(new() { LearnerId = learner.Id, Plan = plan, Name = "Existing", CertificateLevel = level,
                    PurchaseKey = Guid.NewGuid(), PaymentStatus = "Paid", Status = "Active" });
            setup.Add(cohort); await setup.SaveChangesAsync();
            var gateway = new Mock<IPaymentGatewayClient>();
            gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/test", "test", ""));
            gateway.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentLinkResult("https://pay.invalid/test", "test", ""));
            StudyCourseService Service(BookingDb db, int learnerId) => new(new UnitOfWork(db),
                Mock.Of<IClaimService>(c => c.GetUserClaim() == new ClaimDTO { Id = learnerId, Role = Role.Learner }),
                gateway.Object, Mock.Of<IFileStorageService>(), new AppSettings { FrontendBaseUrl = "https://test.invalid" });
            async Task<RikiPath.Application.Responses.ApiResponse<object>> Purchase(int learnerId, Guid? key = null)
            {
                await using var db = Context();
                return await Service(db, learnerId).PurchaseAsync(new PurchaseCourse(plan.Id, key ?? Guid.NewGuid(), cohort.Id), default);
            }
            var firstKey = Guid.NewGuid(); var secondKey = Guid.NewGuid();
            var outcomes = await Task.WhenAll(Purchase(users[0].Id, firstKey), Purchase(users[1].Id, secondKey));
            Assert.True(outcomes.Count(x => x.IsSuccess) == 1, string.Join(" | ", outcomes.Select(x => x.ErrorMessage)));
            await using var verify = Context();
            var hold = await verify.StudyCourses.SingleAsync(c => c.CohortId == cohort.Id && c.PaymentStatus == "Pending");
            var winnerKey = hold.LearnerId == users[0].Id ? firstKey : secondKey;
            Assert.True((await Purchase(hold.LearnerId, winnerKey)).IsSuccess);
            Assert.Equal(1, await verify.StudyCourses.CountAsync(c => c.CohortId == cohort.Id
                && c.LearnerId == hold.LearnerId && c.PaymentStatus == "Pending"));
            hold.PaymentExpiresAt = DateTime.UtcNow.AddSeconds(-1); await verify.SaveChangesAsync();
            var loser = hold.LearnerId == users[0].Id ? users[1].Id : users[0].Id;
            Assert.True((await Purchase(loser)).IsSuccess);
            await using var late = Context();
            Assert.True((await Service(late, hold.LearnerId).ConfirmPaymentAsync(hold.Id,
                new PaymentCallbackResult(true, true, 700000000 + hold.Id, 100, ""), "late", default)).IsSuccess);
            var paid = await late.StudyCourses.Include(c => c.Sessions).SingleAsync(c => c.Id == hold.Id);
            Assert.Equal("PaymentReview", paid.Status); Assert.Equal("Paid", paid.PaymentStatus); Assert.Empty(paid.Sessions);
            var individualPlan = new StudyCoursePlan { Name = "Individual", CertificateLevelId = level.Id, SessionType = "Individual", Capacity = 1,
                SessionMinutes = 60, TotalSessions = 1, DurationMonths = 1, SessionsPerWeek = 1, Price = 100, IsPublished = true };
            var individual = new StudyCourse { Plan = individualPlan, LearnerId = users[0].Id, CertificateLevelId = level.Id,
                PurchaseKey = Guid.NewGuid(), Name = "Paid 1–1", PaymentStatus = "Paid", Status = "Ready", TotalSessions = 1, SessionMinutes = 60, DurationMonths = 1, SessionsPerWeek = 1 };
            var individualStart = DateTime.UtcNow.AddMinutes(5);
            var individualSlot = new MentorAvailability { MentorId = mentor.Id, StartTime = individualStart, EndTime = individualStart.AddMinutes(60), MaxLearners = 1, IsApproved = true };
            late.Add(individual); late.Add(individualSlot); await late.SaveChangesAsync();
            var individualService = Service(late, users[0].Id);
            var available = await individualService.AvailableSlotsAsync(individual.Id, default);
            Assert.True(available.IsSuccess, available.ErrorMessage);
            Assert.True((await individualService.BookSlotAsync(individual.Id, new(individualSlot.Id), default)).IsSuccess);
            var meeting = await new MentorMeetingAccessService(new UnitOfWork(late)).GetMeetingAsync(individualSlot.RoomId, default);
            Assert.NotNull(meeting); Assert.Equal(1, meeting.Capacity);
            Assert.Equal(users[0].Id, Assert.Single(meeting.Learners).UserId);
            var windowStart = DateTime.UtcNow.AddDays(8);
            windowStart = new DateTime(windowStart.Ticks / 10 * 10, DateTimeKind.Utc); // PostgreSQL microsecond precision.
            var window = new MentorAvailability { MentorId = mentor.Id, StartTime = windowStart, EndTime = windowStart.AddMinutes(150), IsApproved = true, MaxLearners = 1 };
            late.Add(window); await late.SaveChangesAsync();
            var review = new MentorAvailabilityReviewService(new UnitOfWork(late), Mock.Of<IClaimService>(x => x.GetUserClaim() == new ClaimDTO { Id = 9000, Role = Role.Admin }));
            var split = await review.SplitAsync(window.Id, 60, default);
            Assert.True(split.IsSuccess, split.ErrorMessage); Assert.Equal(3, split.Result!.Count);
            Assert.Equal(windowStart, split.Result[0].StartTime); Assert.Equal(window.EndTime, split.Result[^1].EndTime);
            Assert.True(window.IsDeleted); Assert.All(split.Result, child => Assert.True(child.IsApproved));
            Assert.False((await review.SplitAsync(individualSlot.Id, 15, default)).IsSuccess);


        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", conn); await command.ExecuteNonQueryAsync();
        }
    }
}
