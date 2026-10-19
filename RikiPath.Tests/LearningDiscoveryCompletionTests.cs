using System.Net;
using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class LearningDiscoveryCompletionTests
{
    [Fact]
    public async Task Discovery_returns_real_ids_owned_progress_and_stable_order()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var otherLevel = await fixture.AddLevelAsync(12, "N4");
        var skill = await fixture.AddSkillAsync(21, "Vocabulary");
        await fixture.AddLessonAsync(103, level, skill, sortOrder: 2, title: "Third by order");
        await fixture.AddLessonAsync(101, level, skill, sortOrder: 1, title: "First");
        await fixture.AddLessonAsync(102, level, skill, sortOrder: 2, title: "Second by id", description: null);
        await fixture.AddLessonAsync(104, otherLevel, skill, sortOrder: 0, title: "Other level");
        await fixture.AddProgressAsync(userId: 7, lessonId: 101, percent: 100, completed: true);
        await fixture.AddProgressAsync(userId: 99, lessonId: 102, percent: 100, completed: true);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.GetLearningDiscoveryAsync(11, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(11, result.Result!.CertificateLevelId);
        Assert.Equal("N5", result.Result.LevelCode);
        Assert.Equal([101, 102, 103], result.Result.Lessons.Select(item => item.LessonId));
        Assert.Null(result.Result.Lessons[1].Description);
        Assert.Equal(1, result.Result.CompletedLessons);
        Assert.True(result.Result.Lessons[0].IsCompleted);
        Assert.False(result.Result.Lessons[1].IsCompleted);
    }

    [Fact]
    public async Task Discovery_returns_empty_success_for_valid_level_without_lessons()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        await fixture.AddLevelAsync(11, "N5");
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.GetLearningDiscoveryAsync(11, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Result!.Lessons);
        Assert.Equal(0, result.Result.TotalLessons);
        Assert.Equal(0, result.Result.OverallProgressPercent);
    }

    [Fact]
    public async Task Discovery_excludes_deleted_lessons_and_lessons_with_hidden_skills()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var visibleSkill = await fixture.AddSkillAsync(21, "Vocabulary");
        var hiddenSkill = await fixture.AddSkillAsync(22, "Grammar");
        hiddenSkill.IsDeleted = true;
        var deletedLesson = await fixture.AddLessonAsync(
            102,
            level,
            visibleSkill,
            2,
            "Deleted lesson");
        deletedLesson.IsDeleted = true;
        await fixture.AddLessonAsync(103, level, hiddenSkill, 3, "Hidden skill lesson");
        await fixture.AddLessonAsync(101, level, visibleSkill, 1, "Visible lesson");
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.GetLearningDiscoveryAsync(11, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([101], result.Result!.Lessons.Select(item => item.LessonId));
    }

    [Fact]
    public async Task Complete_is_idempotent_and_does_not_create_duplicate_progress()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var skill = await fixture.AddSkillAsync(21, "Vocabulary");
        await fixture.AddLessonAsync(101, level, skill, 1, "Lesson");
        fixture.Context.ChangeTracker.Clear();

        var first = await fixture.Service.CompleteLessonAsync(101, CancellationToken.None);
        var completedAt = first.Result!.CompletedAt;
        var second = await fixture.Service.CompleteLessonAsync(101, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(second.Result!.IsCompleted);
        Assert.Equal(100, second.Result.ProgressPercent);
        Assert.Equal(completedAt, second.Result.CompletedAt);
        Assert.Equal(1, await fixture.Context.LessonProgresses.CountAsync());
    }

    [Fact]
    public async Task Complete_uses_jwt_owner_and_never_mutates_another_users_progress()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var skill = await fixture.AddSkillAsync(21, "Vocabulary");
        await fixture.AddLessonAsync(101, level, skill, 1, "Lesson");
        await fixture.AddProgressAsync(userId: 99, lessonId: 101, percent: 35, completed: false);
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.CompleteLessonAsync(101, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var rows = await fixture.Context.LessonProgresses.OrderBy(item => item.UserId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.True(rows.Single(item => item.UserId == 7).IsCompleted);
        var other = rows.Single(item => item.UserId == 99);
        Assert.False(other.IsCompleted);
        Assert.Equal(35, other.ProgressPercent);
    }

    [Fact]
    public async Task Hidden_lesson_returns_not_found_without_writing_progress()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var skill = await fixture.AddSkillAsync(21, "Vocabulary");
        var lesson = await fixture.AddLessonAsync(101, level, skill, 1, "Hidden");
        lesson.IsDeleted = true;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.CompleteLessonAsync(101, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
        Assert.Empty(await fixture.Context.LessonProgresses.ToListAsync());
    }

    [Fact]
    public async Task Playback_sets_last_watched_and_completes_only_at_threshold()
    {
        await using var fixture = await CreateFixtureAsync(userId: 7);
        var level = await fixture.AddLevelAsync(11, "N5");
        var skill = await fixture.AddSkillAsync(21, "Listening");
        await fixture.AddLessonAsync(101, level, skill, 1, "Audio lesson");
        fixture.Context.ChangeTracker.Clear();

        var partial = await fixture.Service.UpdatePlaybackPositionAsync(
            101,
            new UpdatePlaybackPositionRequest { PositionSeconds = 90, DurationSeconds = 100 },
            CancellationToken.None);
        var complete = await fixture.Service.UpdatePlaybackPositionAsync(
            101,
            new UpdatePlaybackPositionRequest { PositionSeconds = 95, DurationSeconds = 100 },
            CancellationToken.None);

        Assert.False(partial.Result!.IsCompleted);
        Assert.True(complete.Result!.IsCompleted);
        var row = await fixture.Context.LessonProgresses.SingleAsync();
        Assert.NotNull(row.LastWatchedAt);
        Assert.Equal(95, row.ProgressPercent);
    }

    private static async Task<LearningFixture> CreateFixtureAsync(int userId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"learning-{Guid.NewGuid():N}")
            .Options;
        var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim())
            .Returns(new ClaimDTO { Id = userId, Role = Role.Learner });
        await context.Database.EnsureCreatedAsync();
        return new LearningFixture(context, new LessonProgressService(unitOfWork, claims.Object));
    }

    private sealed class LearningFixture(AppDbContext context, LessonProgressService service) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;
        public LessonProgressService Service { get; } = service;

        public async Task<CertificateLevel> AddLevelAsync(int id, string code)
        {
            var type = new CertificateType
            {
                Id = id + 1000,
                Name = "JLPT",
                IsActive = true,
                SortOrder = id,
            };
            var level = new CertificateLevel
            {
                Id = id,
                Code = code,
                CertificateTypeId = type.Id,
                CertificateType = type,
                SortOrder = id,
            };
            Context.CertificateLevels.Add(level);
            await Context.SaveChangesAsync();
            return level;
        }

        public async Task<LanguageSkill> AddSkillAsync(int id, string name)
        {
            var skill = new LanguageSkill { Id = id, Name = name };
            Context.LanguageSkills.Add(skill);
            await Context.SaveChangesAsync();
            return skill;
        }

        public async Task<Lesson> AddLessonAsync(
            int id,
            CertificateLevel level,
            LanguageSkill skill,
            int sortOrder,
            string title,
            string? description = "Description")
        {
            var lesson = new Lesson
            {
                Id = id,
                Title = title,
                Description = description,
                SortOrder = sortOrder,
                CertificateLevelId = level.Id,
                CertificateLevel = level,
                LanguageSkillId = skill.Id,
                LanguageSkill = skill,
            };
            Context.Lessons.Add(lesson);
            await Context.SaveChangesAsync();
            return lesson;
        }

        public async Task AddProgressAsync(int userId, int lessonId, int percent, bool completed)
        {
            Context.LessonProgresses.Add(new LessonProgress
            {
                UserId = userId,
                LessonId = lessonId,
                ProgressPercent = percent,
                IsCompleted = completed,
                LastPositionSeconds = percent,
                LastWatchedAt = DateTime.UtcNow,
                ModifiedDate = completed ? DateTime.UtcNow : null,
            });
            await Context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
