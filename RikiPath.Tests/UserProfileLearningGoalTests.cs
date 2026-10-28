using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class UserProfileLearningGoalTests
{
    [Fact]
    public async Task Set_goal_persists_target_exam_date_and_profile_returns_it()
    {
        await using var context = CreateContext();
        var certificateType = new CertificateType
        {
            Id = 71,
            Name = "JLPT",
            IsActive = true,
            SortOrder = 1,
        };
        var level = new CertificateLevel
        {
            Id = 71005,
            CertificateTypeId = certificateType.Id,
            CertificateType = certificateType,
            Code = "N5",
            SortOrder = 1,
        };
        context.Users.Add(new UserAccount
        {
            Id = 7001,
            Email = "flow1-goal@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
        });
        context.CertificateTypes.Add(certificateType);
        context.CertificateLevels.Add(level);
        await context.SaveChangesAsync();

        var service = CreateService(context, userId: 7001);
        var targetExamDate = DateTime.UtcNow.AddHours(7).Date.AddMonths(6);

        var update = await service.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = level.Id,
            DailyStudyMinutes = 45,
            TargetExamDate = targetExamDate,
        }, CancellationToken.None);
        context.ChangeTracker.Clear();
        var profile = await service.GetProfileAsync(CancellationToken.None);

        Assert.True(update.IsSuccess);
        Assert.Equal(targetExamDate, update.Result!.TargetExamDate);
        Assert.True(profile.IsSuccess);
        Assert.Equal(level.Id, profile.Result!.TargetJlptLevelId);
        Assert.Equal("N5", profile.Result.TargetJlptLevelName);
        Assert.Equal(45, profile.Result.DailyStudyMinutes);
        Assert.Equal(targetExamDate, profile.Result.TargetExamDate);
    }

    [Fact]
    public async Task Set_goal_rejects_non_future_target_exam_date()
    {
        await using var context = CreateContext();
        var service = CreateService(context, userId: 7002);

        var result = await service.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = 71005,
            DailyStudyMinutes = 30,
            TargetExamDate = DateTime.UtcNow.Date,
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Ngày dự định thi phải sau hôm nay.", result.ErrorMessage);
    }

    [Fact]
    public async Task Set_goal_with_omitted_target_exam_date_preserves_previous_date()
    {
        await using var context = CreateContext();
        var certificateType = new CertificateType
        {
            Id = 71,
            Name = "JLPT",
            IsActive = true,
            SortOrder = 1,
        };
        var level = new CertificateLevel
        {
            Id = 71005,
            CertificateTypeId = certificateType.Id,
            CertificateType = certificateType,
            Code = "N5",
            SortOrder = 1,
        };
        context.Users.Add(new UserAccount
        {
            Id = 7003,
            Email = "flow1-null-goal@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
            TargetCertificateLevelId = level.Id,
            TargetExamDate = new DateTime(2027, 7, 5),
        });
        context.CertificateTypes.Add(certificateType);
        context.CertificateLevels.Add(level);
        await context.SaveChangesAsync();

        var service = CreateService(context, userId: 7003);

        var update = await service.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = level.Id,
            DailyStudyMinutes = 60,
            TargetExamDate = null,
        }, CancellationToken.None);

        Assert.True(update.IsSuccess);
        Assert.Equal(new DateTime(2027, 7, 5), update.Result!.TargetExamDate);

        var persisted = await context.Users.SingleAsync(u => u.Id == 7003);
        Assert.Equal(new DateTime(2027, 7, 5), persisted.TargetExamDate);
    }

    [Fact]
    public async Task Target_exam_date_validation_aligns_across_Vietnam_midnight_and_UTC_midnight_rollover()
    {
        await using var context = CreateContext();
        var certType = new CertificateType { Id = 71, Name = "JLPT", IsActive = true, SortOrder = 1 };
        var levelN5 = new CertificateLevel { Id = 71005, CertificateTypeId = 71, CertificateType = certType, Code = "N5", SortOrder = 1 };
        var levelN4 = new CertificateLevel { Id = 71004, CertificateTypeId = 71, CertificateType = certType, Code = "N4", SortOrder = 2 };
        var levelN3 = new CertificateLevel { Id = 71003, CertificateTypeId = 71, CertificateType = certType, Code = "N3", SortOrder = 3 };
        var skill = new LanguageSkill { Id = 1, Name = "Grammar" };

        context.CertificateTypes.Add(certType);
        context.CertificateLevels.AddRange(levelN5, levelN4, levelN3);
        context.LanguageSkills.Add(skill);
        context.Lessons.Add(new Lesson
        {
            Id = 5501,
            Title = "N5 Bài 1",
            CertificateLevelId = levelN5.Id,
            CertificateLevel = levelN5,
            LanguageSkillId = skill.Id,
            LanguageSkill = skill,
            SortOrder = 1,
        });
        context.MockTests.AddRange(
            CreateMiniTest(105, levelN5, skill, 50, 501, 502),
            CreateMiniTest(104, levelN4, skill, 40, 401, 402),
            CreateMiniTest(103, levelN3, skill, 30, 301, 302));
        context.Users.Add(new UserAccount
        {
            Id = 7004,
            Email = "tz-boundary@example.test",
            PasswordHash = [],
            PasswordSalt = [],
            Role = Role.Learner,
        });
        await context.SaveChangesAsync();

        // 1) 2026-10-26T16:59:59Z == 2026-10-26 23:59:59 ICT (Vietnam date is still Oct 26).
        // Exam date 2026-10-27 is 1 day ahead in Vietnam time -> valid in Goal, Status, and LearningPath.
        var beforeVnMidnightUtc = new DateTime(2026, 10, 26, 16, 59, 59, DateTimeKind.Utc);
        var currentClock = beforeVnMidnightUtc;

        var claims = new Mock<IClaimService>();
        claims.Setup(c => c.GetUserClaim()).Returns(new ClaimDTO { Id = 7004, Role = Role.Learner });

        var profileService = new UserProfileService(new UnitOfWork(context), Mock.Of<IFileStorageService>(), claims.Object)
        {
            UtcNowProvider = () => currentClock,
        };
        var placementService = new PlacementTestService(new UnitOfWork(context), claims.Object)
        {
            UtcNowProvider = () => currentClock,
        };
        var aiClient = new Mock<IAiLearningPathClient>();
        aiClient
            .Setup(c => c.GenerateLearningPathJsonAsync(7004, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new
            {
                summary = "Lộ trình 1 ngày",
                strengths = new[] { "Từ vựng" },
                weaknesses = new[] { "Ngữ pháp" },
                feasibility = "Khả thi",
                focusTopics = new[] { "N5" },
                dailySchedules = new[]
                {
                    new { day = 1, title = "Day 1", focus = "Grammar", lessonIds = new[] { 5501 } },
                },
            }));
        var learningPathService = new LearningPathService(
            new UnitOfWork(context),
            aiClient.Object,
            claims.Object,
            Mock.Of<ILogger<LearningPathService>>())
        {
            UtcNowProvider = () => currentClock,
        };

        var start = await placementService.StartPlacementTestAsync(CancellationToken.None);
        var submit = await placementService.SubmitPlacementTestAsync(
            start.Result!.AttemptId,
            new SubmitPlacementTestRequest { Answers = [] },
            CancellationToken.None);
        Assert.True(submit.IsSuccess);

        var oct27Date = new DateTime(2026, 10, 27);
        var goalBeforeRollover = await profileService.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = levelN5.Id,
            DailyStudyMinutes = 45,
            TargetExamDate = oct27Date,
        }, CancellationToken.None);
        Assert.True(goalBeforeRollover.IsSuccess);

        var statusBeforeRollover = await placementService.GetStatusAsync(CancellationToken.None);
        Assert.True(statusBeforeRollover.Result!.IsReadyForLearningPath);

        var pathBeforeRollover = await learningPathService.GenerateLearningPathAsync(
            new GenerateLearningPathRequest(),
            CancellationToken.None);
        Assert.True(pathBeforeRollover.IsSuccess);

        // 2) 1 second later: 2026-10-26T17:00:00Z == 2026-10-27 00:00:00 ICT (Vietnam date rolled over to Oct 27, while UTC is still Oct 26!).
        // Exam date 2026-10-27 must now be rejected by SetJlptGoal, mark IsReadyForLearningPath=false in Status, and be rejected by LearningPath!
        currentClock = new DateTime(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc);

        var goalAtVnRollover = await profileService.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = levelN5.Id,
            DailyStudyMinutes = 45,
            TargetExamDate = oct27Date,
        }, CancellationToken.None);
        Assert.False(goalAtVnRollover.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, goalAtVnRollover.StatusCode);

        var statusAtVnRollover = await placementService.GetStatusAsync(CancellationToken.None);
        Assert.False(statusAtVnRollover.Result!.IsReadyForLearningPath);

        var pathAtVnRollover = await learningPathService.GenerateLearningPathAsync(
            new GenerateLearningPathRequest(),
            CancellationToken.None);
        Assert.False(pathAtVnRollover.IsSuccess);
        // The previously generated AI plan is protected from silent replacement.
        // The profile update above already proved that the expired exam date is rejected.
        Assert.Equal((int)HttpStatusCode.Conflict, pathAtVnRollover.StatusCode);
        Assert.Equal("PATH_PREVIEW_REQUIRED", pathAtVnRollover.ErrorCode);

        // 3) At UTC midnight 2026-10-27T00:00:00Z (2026-10-27 07:00:00 ICT), Oct 27 is still rejected and Oct 28 is accepted by all three.
        currentClock = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
        var oct28Date = new DateTime(2026, 10, 28);

        var goalOct28 = await profileService.SetJlptGoalAsync(new SetJlptGoalRequest
        {
            TargetJlptLevelId = levelN5.Id,
            DailyStudyMinutes = 45,
            TargetExamDate = oct28Date,
        }, CancellationToken.None);
        Assert.True(goalOct28.IsSuccess);

        var statusOct28 = await placementService.GetStatusAsync(CancellationToken.None);
        Assert.True(statusOct28.Result!.IsReadyForLearningPath);

        // An active path exists, so changing the target date must use the existing
        // preview/confirmation replacement flow instead of silently regenerating it.
        var pathOct28 = await learningPathService.PreviewLearningPathAsync(
            new PreviewLearningPathRequest
            {
                StartingJlptLevelId = levelN5.Id,
                TargetJlptLevelId = levelN5.Id,
            },
            CancellationToken.None);
        Assert.True(pathOct28.IsSuccess,
            $"Expected a valid target date to allow preview, got HTTP {pathOct28.StatusCode}, code '{pathOct28.ErrorCode}', message '{pathOct28.ErrorMessage}'.");
    }

    private static MockTest CreateMiniTest(
        int testId,
        CertificateLevel level,
        LanguageSkill skill,
        int sectionId,
        int q1Id,
        int q2Id)
    {
        var test = new MockTest
        {
            Id = testId,
            Title = $"Test {level.Code}",
            Description = $"Test {level.Code}",
            TimeLimitMinutes = 20,
            CertificateLevelId = level.Id,
            CertificateLevel = level,
        };
        test.Sections =
        [
            new MockTestSection
            {
                Id = sectionId,
                MockTestId = testId,
                MockTest = test,
                LanguageSkillId = skill.Id,
                LanguageSkill = skill,
                Title = $"{level.Code} Section",
                SortOrder = 1,
                Questions =
                [
                    new MockQuestion
                    {
                        Id = q1Id,
                        MockTestSectionId = sectionId,
                        QuestionText = $"Q{q1Id}",
                        SortOrder = 1,
                        Options =
                        [
                            new MockQuestionOption { Id = q1Id * 10 + 1, MockQuestionId = q1Id, OptionText = "A", IsCorrect = true, SortOrder = 1 },
                            new MockQuestionOption { Id = q1Id * 10 + 2, MockQuestionId = q1Id, OptionText = "B", IsCorrect = false, SortOrder = 2 },
                        ],
                    },
                    new MockQuestion
                    {
                        Id = q2Id,
                        MockTestSectionId = sectionId,
                        QuestionText = $"Q{q2Id}",
                        SortOrder = 2,
                        Options =
                        [
                            new MockQuestionOption { Id = q2Id * 10 + 1, MockQuestionId = q2Id, OptionText = "A", IsCorrect = true, SortOrder = 1 },
                            new MockQuestionOption { Id = q2Id * 10 + 2, MockQuestionId = q2Id, OptionText = "B", IsCorrect = false, SortOrder = 2 },
                        ],
                    },
                ],
            },
        ];
        return test;
    }

    private static UserProfileService CreateService(AppDbContext context, int userId)
    {
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        return new UserProfileService(
            new UnitOfWork(context),
            Mock.Of<IFileStorageService>(),
            claims.Object);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
