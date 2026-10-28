using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class LearningPathPersistenceTests
{
    [Fact]
    public async Task Latest_returns_persisted_normalized_contract_without_calling_ai()
    {
        var generatedAt = new DateTime(2026, 10, 26, 3, 4, 5, DateTimeKind.Utc);
        var persisted = new LearningPathResponse
        {
            UserId = 7,
            GeneratedAt = generatedAt,
            Summary = "Stable path",
            Feasibility = "Feasible",
            DailySchedules =
            [
                new DailyScheduleResponse
                {
                    Day = 1,
                    Title = "Day one",
                    Focus = "Grammar",
                    LessonIds = [501],
                    Lessons = ["Real lesson"],
                },
            ],
        };
        var pathRepository = new Mock<IRecommendedLearningPathRepository>();
        pathRepository
            .Setup(repository => repository.GetLatestByUserIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendedLearningPath
            {
                Id = 9,
                UserId = 7,
                GeneratedAt = generatedAt,
                Summary = persisted.Summary,
                SuggestionJson = JsonSerializer.Serialize(
                    persisted,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(item => item.RecommendedLearningPaths).Returns(pathRepository.Object);
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = 7, Role = Role.Learner });
        var ai = new Mock<IAiLearningPathClient>();
        var service = new LearningPathService(
            unitOfWork.Object,
            ai.Object,
            claims.Object,
            Mock.Of<ILogger<LearningPathService>>());

        var result = await service.GetLatestLearningPathAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Result!.UserId);
        Assert.Equal(generatedAt, result.Result.GeneratedAt);
        var lesson = Assert.Single(Assert.Single(result.Result.DailySchedules).LessonIds);
        Assert.Equal(501, lesson);
        ai.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Generate_rejects_learner_with_only_completed_ordinary_mock_test_and_accepts_completed_placement_attempt()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var context = new AppDbContext(options);

        var certType = new CertificateType { Id = 71, Name = "JLPT", IsActive = true, SortOrder = 1 };
        var levelN4 = new CertificateLevel
        {
            Id = 71004,
            CertificateTypeId = certType.Id,
            CertificateType = certType,
            Code = "N4",
            SortOrder = 2,
        };
        var user = new UserAccount
        {
            Id = 9101,
            Email = "lp-isolation@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
            TargetCertificateLevelId = levelN4.Id,
            TargetCertificateLevel = levelN4,
            DailyStudyMinutes = 45,
            TargetExamDate = DateTime.UtcNow.Date.AddDays(5),
        };
        var skill = new LanguageSkill
        {
            Id = 1,
            Name = "Grammar",
        };
        var lesson = new Lesson
        {
            Id = 4401,
            Title = "N4 Bài 1",
            CertificateLevelId = levelN4.Id,
            CertificateLevel = levelN4,
            LanguageSkillId = skill.Id,
            LanguageSkill = skill,
            SortOrder = 1,
        };

        // Ordinary completed MockTest attempt must NOT satisfy Placement prerequisite
        var ordinaryAttempt = new MockTestAttempt
        {
            Id = 1,
            UserId = user.Id,
            AttemptKind = AttemptKind.MockTest,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            IsCompleted = true,
            TotalScore = 85.0,
        };

        context.CertificateTypes.Add(certType);
        context.CertificateLevels.Add(levelN4);
        context.LanguageSkills.Add(skill);
        context.Users.Add(user);
        context.Lessons.Add(lesson);
        context.MockTestAttempts.Add(ordinaryAttempt);
        await context.SaveChangesAsync();

        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = user.Id, Role = Role.Learner });

        var ai = new Mock<IAiLearningPathClient>();
        ai.Setup(client => client.GenerateLearningPathJsonAsync(
                user.Id,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string prompt, CancellationToken _) =>
            {
                // Extract expected days from prompt or build 5-day schedule
                var schedules = Enumerable.Range(1, 5).Select(day => new
                {
                    day,
                    title = $"Day {day}",
                    focus = "Grammar",
                    lessonIds = new[] { 4401 },
                });
                return JsonSerializer.Serialize(new
                {
                    summary = "Lộ trình N4",
                    strengths = new[] { "Từ vựng" },
                    weaknesses = new[] { "Ngữ pháp" },
                    feasibility = "Khả thi",
                    focusTopics = new[] { "N4" },
                    dailySchedules = schedules,
                });
            });

        var service = new LearningPathService(
            new UnitOfWork(context),
            ai.Object,
            claims.Object,
            Mock.Of<ILogger<LearningPathService>>());

        var rejected = await service.GenerateLearningPathAsync(
            new GenerateLearningPathRequest(),
            CancellationToken.None);

        Assert.False(rejected.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, rejected.StatusCode);
        ai.VerifyNoOtherCalls();

        // A completed placement attempt without an immutable completed result must
        // not be used to reconstruct a learning-path score from mutable tables.
        var placementAttempt = new MockTestAttempt
        {
            Id = 2,
            UserId = user.Id,
            AttemptKind = AttemptKind.Placement,
            StartedAt = DateTime.UtcNow.AddMinutes(-15),
            SubmittedAt = DateTime.UtcNow.AddMinutes(-2),
            IsCompleted = true,
            TotalScore = 70.0,
        };
        context.MockTestAttempts.Add(placementAttempt);
        await context.SaveChangesAsync();

        // Adjust exam date so daysUntilExam == 5 in Vietnam time (UTC+7)
        user.TargetExamDate = DateTime.UtcNow.AddHours(7).Date.AddDays(5);
        await context.SaveChangesAsync();

        var rejectedWithoutSnapshot = await service.GenerateLearningPathAsync(
            new GenerateLearningPathRequest(),
            CancellationToken.None);

        Assert.False(rejectedWithoutSnapshot.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, rejectedWithoutSnapshot.StatusCode);
        ai.VerifyNoOtherCalls();

        placementAttempt.QuestionSnapshotJson = CreateCompletedPlacementSnapshot(
            placementAttempt.Id,
            placementAttempt.StartedAt,
            placementAttempt.SubmittedAt!.Value,
            levelN4.Id);
        await context.SaveChangesAsync();

        var generated = await service.GenerateLearningPathAsync(
            new GenerateLearningPathRequest(),
            CancellationToken.None);

        Assert.True(generated.IsSuccess);
        Assert.Equal(5, generated.Result!.DailySchedules.Count);
        Assert.Equal(levelN4.Id, user.TargetCertificateLevelId);
    }

    private static string CreateCompletedPlacementSnapshot(
        int attemptId,
        DateTime startedAt,
        DateTime submittedAt,
        int jlptLevelId)
    {
        var completedResult = new PlacementTestResultResponse
        {
            AttemptId = attemptId,
            AttemptType = "placement",
            Status = "completed",
            StartedAt = startedAt,
            SubmittedAt = submittedAt,
            TotalScore = 70,
            CorrectCount = 7,
            TotalQuestions = 10,
            AssessedJlptLevelId = jlptLevelId,
            AssessedJlptLevelName = "N4",
            RecommendedTargetJlptLevelId = jlptLevelId,
            RecommendedTargetJlptLevelName = "N4",
            WrongQuestionIds = [1, 2, 3],
            LevelBreakdowns =
            [
                new PlacementLevelBreakdownItem
                {
                    JlptLevelId = jlptLevelId,
                    JlptLevelName = "N4",
                    CorrectCount = 7,
                    TotalCount = 10,
                    ScorePercent = 70,
                },
            ],
            Sections =
            [
                new SectionResultItem
                {
                    MockTestSectionId = 40,
                    SectionTitle = "N4 Grammar",
                    LanguageSkillName = "Grammar",
                    CorrectCount = 7,
                    TotalCount = 10,
                    Score = 70,
                    ScorePercent = 70,
                },
            ],
        };

        return JsonSerializer.Serialize(
            new { schemaVersion = 2, completedResult },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
