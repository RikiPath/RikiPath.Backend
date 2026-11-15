using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Migrations;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class AtomicLearningGoalTests
{
    [Fact]
    public async Task Complete_goal_create_edit_and_certificate_clear_persist_across_reload()
    {
        await using var db = CreateContext();
        Seed(db);
        var service = CreateService(db, 7101);
        var exam = DateTime.UtcNow.Date.AddMonths(8);

        var created = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71005,
            TargetJlptLevelId = 71003,
            DailyStudyMinutes = 45,
            TargetExamDate = exam,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71004,
            ExpectedVersion = 0,
        }, CancellationToken.None);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        Assert.Equal(1, created.Result!.LearningGoalVersion);
        Assert.Equal(71005, created.Result.CurrentStudyLevelId);
        Assert.Equal("N5", created.Result.CurrentStudyLevelName);
        Assert.Equal(71003, created.Result.TargetJlptLevelId);
        Assert.Equal("N3", created.Result.TargetJlptLevelName);
        Assert.Equal(45, created.Result.DailyStudyMinutes);
        Assert.Equal(exam, created.Result.TargetExamDate);
        Assert.True(created.Result.HasJlptCertificate);
        Assert.Equal(71004, created.Result.CertifiedJlptLevelId);
        Assert.Equal("N4", created.Result.CertifiedJlptLevelName);

        db.ChangeTracker.Clear();
        var reloaded = await service.GetProfileAsync(CancellationToken.None);
        Assert.Equal(71005, reloaded.Result!.CurrentStudyLevelId);
        Assert.Equal("N5", reloaded.Result.CurrentStudyLevelName);
        Assert.Equal(71003, reloaded.Result.TargetJlptLevelId);
        Assert.Equal("N3", reloaded.Result.TargetJlptLevelName);
        Assert.Equal(45, reloaded.Result.DailyStudyMinutes);
        Assert.Equal(exam, reloaded.Result.TargetExamDate);
        Assert.True(reloaded.Result.HasJlptCertificate);
        Assert.Equal(71004, reloaded.Result.CertifiedJlptLevelId);
        Assert.Equal("N4", reloaded.Result.CertifiedJlptLevelName);

        var edited = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 75,
            TargetExamDate = null,
            HasJlptCertificate = false,
            CertifiedJlptLevelId = null,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.True(edited.IsSuccess, $"{edited.ErrorMessage}: {string.Join("; ", edited.Errors ?? [])}");
        db.ChangeTracker.Clear();
        var final = await service.GetProfileAsync(CancellationToken.None);
        Assert.Equal(2, final.Result!.LearningGoalVersion);
        Assert.Equal(71004, final.Result.CurrentStudyLevelId);
        Assert.Equal("N4", final.Result.CurrentStudyLevelName);
        Assert.Equal(71002, final.Result.TargetJlptLevelId);
        Assert.Equal("N2", final.Result.TargetJlptLevelName);
        Assert.Equal(75, final.Result.DailyStudyMinutes);
        Assert.Null(final.Result.TargetExamDate);
        Assert.False(final.Result.HasJlptCertificate);
        Assert.Null(final.Result.CertifiedJlptLevelId);
        Assert.Null(final.Result.CertifiedJlptLevelName);
    }

    [Fact]
    public async Task Invalid_field_and_stale_version_leave_all_persisted_fields_unchanged()
    {
        await using var db = CreateContext();
        Seed(db);
        var service = CreateService(db, 7101);
        var initialExam = DateTime.UtcNow.Date.AddMonths(6);
        var valid = new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71005,
            TargetJlptLevelId = 71003,
            DailyStudyMinutes = 30,
            TargetExamDate = initialExam,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71005,
            ExpectedVersion = 0,
        };
        Assert.True((await service.SetLearningGoalAsync(valid, CancellationToken.None)).IsSuccess);

        // 1. Invalid minutes (0 and 1441)
        var zeroMinutes = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 0,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(zeroMinutes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, zeroMinutes.StatusCode);

        var excessiveMinutes = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 1441,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(excessiveMinutes.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, excessiveMinutes.StatusCode);

        // 2. Invalid study level (N2 = 71002, N1 = 71001, unknown = 99999)
        foreach (var unsupportedCurrentId in new[] { 71002, 71001, 99999 })
        {
            var invalidStudy = await service.SetLearningGoalAsync(new SetLearningGoalRequest
            {
                CurrentStudyLevelId = unsupportedCurrentId,
                TargetJlptLevelId = 71002,
                DailyStudyMinutes = 45,
                ExpectedVersion = 1,
            }, CancellationToken.None);
            Assert.False(invalidStudy.IsSuccess);
            Assert.Equal((int)HttpStatusCode.BadRequest, invalidStudy.StatusCode);
        }

        // 3. Invalid target level (N1 = 71001, unknown = 99999)
        foreach (var unsupportedTargetId in new[] { 71001, 99999 })
        {
            var invalidTarget = await service.SetLearningGoalAsync(new SetLearningGoalRequest
            {
                CurrentStudyLevelId = 71004,
                TargetJlptLevelId = unsupportedTargetId,
                DailyStudyMinutes = 45,
                ExpectedVersion = 1,
            }, CancellationToken.None);
            Assert.False(invalidTarget.IsSuccess);
            Assert.Equal((int)HttpStatusCode.BadRequest, invalidTarget.StatusCode);
        }

        // 4. Invalid certificate combinations (true+null, false+id, true+N1, true+unknown)
        var certTrueWithoutId = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = null,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(certTrueWithoutId.IsSuccess);

        var certFalseWithId = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            HasJlptCertificate = false,
            CertifiedJlptLevelId = 71004,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(certFalseWithId.IsSuccess);

        var certTrueWithUnsupportedN1 = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71001,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(certTrueWithUnsupportedN1.IsSuccess);

        // 5. Invalid exam date (today or past in Vietnam business time)
        var pastExam = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            TargetExamDate = DateTime.UtcNow.AddHours(7).Date,
            ExpectedVersion = 1,
        }, CancellationToken.None);
        Assert.False(pastExam.IsSuccess);
        Assert.Equal("Ngày dự định thi phải sau hôm nay.", pastExam.ErrorMessage);

        // 6. Stale / negative version
        var stale = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            ExpectedVersion = 0,
        }, CancellationToken.None);
        Assert.Equal((int)HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("GOAL_VERSION_CONFLICT", stale.ErrorCode);

        var negativeVersion = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 45,
            ExpectedVersion = -1,
        }, CancellationToken.None);
        Assert.False(negativeVersion.IsSuccess);

        // Verify none of the failed attempts modified any persisted field
        db.ChangeTracker.Clear();
        var saved = await service.GetProfileAsync(CancellationToken.None);
        Assert.Equal(1, saved.Result!.LearningGoalVersion);
        Assert.Equal(71005, saved.Result.CurrentStudyLevelId);
        Assert.Equal(71003, saved.Result.TargetJlptLevelId);
        Assert.Equal(30, saved.Result.DailyStudyMinutes);
        Assert.Equal(initialExam, saved.Result.TargetExamDate);
        Assert.True(saved.Result.HasJlptCertificate);
        Assert.Equal(71005, saved.Result.CertifiedJlptLevelId);
    }

    [Fact]
    public async Task Updating_one_user_does_not_modify_another_users_goal()
    {
        await using var db = CreateContext();
        Seed(db);
        var service = CreateService(db, 7101);
        var result = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71005,
            TargetJlptLevelId = 71003,
            DailyStudyMinutes = 45,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71005,
            ExpectedVersion = 0,
        }, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        db.ChangeTracker.Clear();
        var other = await db.Users.SingleAsync(user => user.Id == 7102);
        Assert.Null(other.StartingCertificateLevelId);
        Assert.Null(other.TargetCertificateLevelId);
        Assert.Null(other.SelfDeclaredCertificateLevelId);
        Assert.Equal(0, other.LearningGoalVersion);
    }

    [Fact]
    public async Task Legacy_endpoints_remain_compatible_increment_version_and_preserve_certificate()
    {
        await using var db = CreateContext();
        Seed(db);
        var service = CreateService(db, 7101);
        var examDate = DateTime.UtcNow.AddHours(7).Date.AddMonths(7);

        var initial = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71005,
            TargetJlptLevelId = 71004,
            DailyStudyMinutes = 30,
            TargetExamDate = examDate,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71005,
            ExpectedVersion = 0,
        }, CancellationToken.None);
        Assert.True(initial.IsSuccess);
        Assert.Equal(1, initial.Result!.LearningGoalVersion);

        // Legacy POST /api/UserProfile/me/jlpt-goal preserves certificate and increments version
        var legacyGoal = await service.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = 71003,
            DailyStudyMinutes = 60,
        }, CancellationToken.None);
        Assert.True(legacyGoal.IsSuccess);
        Assert.Equal(2, legacyGoal.Result!.LearningGoalVersion);

        // Legacy PUT /api/UserProfile/me/study-level preserves certificate and increments version
        var legacyStudy = await service.SetStudyLevelAsync(new SetStudyLevelRequest
        {
            CurrentStudyLevelId = 71004,
            Confirmed = true,
        }, CancellationToken.None);
        Assert.True(legacyStudy.IsSuccess);
        Assert.Equal(3, legacyStudy.Result!.LearningGoalVersion);

        db.ChangeTracker.Clear();
        var reloaded = await service.GetProfileAsync(CancellationToken.None);
        Assert.Equal(3, reloaded.Result!.LearningGoalVersion);
        Assert.Equal(71004, reloaded.Result.CurrentStudyLevelId);
        Assert.Equal(71003, reloaded.Result.TargetJlptLevelId);
        Assert.Equal(60, reloaded.Result.DailyStudyMinutes);
        Assert.Equal(examDate, reloaded.Result.TargetExamDate);
        Assert.True(reloaded.Result.HasJlptCertificate);
        Assert.Equal(71005, reloaded.Result.CertifiedJlptLevelId);
    }

    [Fact]
    public async Task Active_ai_roadmap_and_placement_assessment_are_protected_during_goal_updates()
    {
        await using var db = CreateContext();
        Seed(db);
        var user = await db.Users.SingleAsync(u => u.Id == 7101);
        user.StartingCertificateLevelId = 71005;
        user.TargetCertificateLevelId = 71003;
        user.AssessedCertificateLevelId = 71004;
        user.PlacementDecision = PlacementDecision.Completed;
        user.LearningMode = LearningMode.AiPersonalized;
        user.DailyStudyMinutes = 45;
        user.LearningGoalVersion = 2;
        db.RecommendedLearningPaths.Add(new RecommendedLearningPath
        {
            Id = 801,
            UserId = 7101,
            TargetCertificateLevelId = 71003,
            Status = LearningPathStatus.Active,
            Version = 1,
            GeneratedAt = DateTime.UtcNow,
            Summary = "Active N3 Path",
            SuggestionJson = "{}",
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, 7101);

        // Attempting to change TargetJlptLevelId while an ACTIVE AI path exists is rejected with 409 PATH_PREVIEW_REQUIRED
        var blockedTargetChange = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 90,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71004,
            ExpectedVersion = 2,
        }, CancellationToken.None);
        Assert.False(blockedTargetChange.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, blockedTargetChange.StatusCode);
        Assert.Equal("PATH_PREVIEW_REQUIRED", blockedTargetChange.ErrorCode);

        // Verify none of the other fields (current level, minutes, certificate, version) were partially saved
        db.ChangeTracker.Clear();
        var afterBlocked = await service.GetProfileAsync(CancellationToken.None);
        Assert.Equal(2, afterBlocked.Result!.LearningGoalVersion);
        Assert.Equal(71005, afterBlocked.Result.CurrentStudyLevelId);
        Assert.Equal(71003, afterBlocked.Result.TargetJlptLevelId);
        Assert.Equal(45, afterBlocked.Result.DailyStudyMinutes);
        Assert.False(afterBlocked.Result.HasJlptCertificate);
        Assert.Equal(71004, afterBlocked.Result.AssessedJlptLevelId);
        Assert.Equal("COMPLETED", afterBlocked.Result.PlacementDecision);

        // Updating non-conflicting fields (study level, minutes, certificate, exam date) keeps Placement & Active AI path intact
        var allowedUpdate = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71004,
            TargetJlptLevelId = 71003,
            DailyStudyMinutes = 90,
            TargetExamDate = DateTime.UtcNow.AddHours(7).Date.AddMonths(5),
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71004,
            ExpectedVersion = 2,
        }, CancellationToken.None);
        Assert.True(allowedUpdate.IsSuccess, allowedUpdate.ErrorMessage);
        Assert.Equal(3, allowedUpdate.Result!.LearningGoalVersion);
        Assert.Equal(71004, allowedUpdate.Result.AssessedJlptLevelId);
        Assert.Equal("COMPLETED", allowedUpdate.Result.PlacementDecision);
        Assert.Equal(LearningPathStatus.Active, (await db.RecommendedLearningPaths.SingleAsync(p => p.Id == 801)).Status);
    }

    [Fact]
    public async Task Concurrency_exception_and_transaction_failure_do_not_commit_partial_state()
    {
        await using var db = CreateContext();
        Seed(db);
        var user = await db.Users.SingleAsync(u => u.Id == 7101);
        user.StartingCertificateLevelId = 71005;
        user.TargetCertificateLevelId = 71004;
        user.DailyStudyMinutes = 30;
        user.LearningGoalVersion = 1;
        await db.SaveChangesAsync();

        var uow = new Mock<IUnitOfWork>();
        var realUow = new UnitOfWork(db);
        uow.SetupGet(x => x.CertificateLevels).Returns(realUow.CertificateLevels);
        uow.SetupGet(x => x.UserAccounts).Returns(realUow.UserAccounts);
        uow.SetupGet(x => x.RecommendedLearningPaths).Returns(realUow.RecommendedLearningPaths);
        uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Simulated concurrent row update"));

        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 7101, Role = Role.Learner });
        var service = new UserProfileService(uow.Object, Mock.Of<IFileStorageService>(), claims.Object);

        var concurrentResult = await service.SetLearningGoalAsync(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71003,
            TargetJlptLevelId = 71002,
            DailyStudyMinutes = 90,
            HasJlptCertificate = true,
            CertifiedJlptLevelId = 71003,
            ExpectedVersion = 1,
        }, CancellationToken.None);

        Assert.False(concurrentResult.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Conflict, concurrentResult.StatusCode);
        Assert.Equal("GOAL_VERSION_CONFLICT", concurrentResult.ErrorCode);

        db.ChangeTracker.Clear();
        var unchanged = await db.Users.SingleAsync(u => u.Id == 7101);
        Assert.Equal(71005, unchanged.StartingCertificateLevelId);
        Assert.Equal(71004, unchanged.TargetCertificateLevelId);
        Assert.Equal(30, unchanged.DailyStudyMinutes);
        Assert.Null(unchanged.SelfDeclaredCertificateLevelId);
        Assert.Equal(1, unchanged.LearningGoalVersion);
    }

    [Fact]
    public async Task Controller_enforces_learner_authorization_and_migration_is_scoped_cleanly()
    {
        var authorizeAttr = typeof(UserProfileController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorizeAttr);
        Assert.Equal("Learner", authorizeAttr!.Roles);

        var method = typeof(UserProfileController).GetMethod(nameof(UserProfileController.SetLearningGoal));
        Assert.NotNull(method);
        var httpPut = method!.GetCustomAttribute<HttpPutAttribute>();
        Assert.NotNull(httpPut);
        Assert.Equal("me/learning-goal", httpPut!.Template);

        await using var db = CreateContext();
        Seed(db);
        var service = CreateService(db, 7101);
        var controller = new UserProfileController(service);
        var actionResult = await controller.SetLearningGoal(new SetLearningGoalRequest
        {
            CurrentStudyLevelId = 71005,
            TargetJlptLevelId = 71004,
            DailyStudyMinutes = 45,
            ExpectedVersion = 0,
        }, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.OK, objectResult.StatusCode);

        // Verify migration only adds LearningGoalVersion and SelfDeclaredCertificateLevelId (+ index & FK)
        var migration = new AddLearningGoalCertificateAndVersion();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(AddLearningGoalCertificateAndVersion)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var addColumns = builder.Operations.OfType<AddColumnOperation>().Select(op => op.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["LearningGoalVersion", "SelfDeclaredCertificateLevelId"], addColumns);
        Assert.Empty(builder.Operations.OfType<DropColumnOperation>());
        Assert.Single(builder.Operations.OfType<CreateIndexOperation>());
        var fk = Assert.Single(builder.Operations.OfType<AddForeignKeyOperation>());
        Assert.Equal(ReferentialAction.SetNull, fk.OnDelete);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static void Seed(AppDbContext db)
    {
        var type = new CertificateType { Id = 71, Name = "JLPT", IsActive = true, SortOrder = 1 };
        db.CertificateTypes.Add(type);
        db.CertificateLevels.AddRange(
            new CertificateLevel { Id = 71005, CertificateType = type, Code = "N5", SortOrder = 1 },
            new CertificateLevel { Id = 71004, CertificateType = type, Code = "N4", SortOrder = 2 },
            new CertificateLevel { Id = 71003, CertificateType = type, Code = "N3", SortOrder = 3 },
            new CertificateLevel { Id = 71002, CertificateType = type, Code = "N2", SortOrder = 4 },
            new CertificateLevel { Id = 71001, CertificateType = type, Code = "N1", SortOrder = 5 });
        db.Users.AddRange(
            new UserAccount { Id = 7101, Email = "goal-one@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner },
            new UserAccount { Id = 7102, Email = "goal-two@example.test", PasswordHash = [], PasswordSalt = [], Role = Role.Learner });
        db.SaveChanges();
    }

    private static UserProfileService CreateService(AppDbContext db, int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        return new UserProfileService(new UnitOfWork(db), Mock.Of<IFileStorageService>(), claims.Object);
    }
}
