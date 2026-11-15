using Microsoft.EntityFrameworkCore;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public class PracticeLessonUnlockTests
{
    [Theory]
    [InlineData(69.99, false, 7, false)]
    [InlineData(70, true, 7, true)]
    [InlineData(100, true, 8, false)]
    [InlineData(100, false, 7, false)]
    public async Task Summary_returns_exercise_id_and_only_completed_passing_attempts_for_current_learner(
        double score, bool completed, int attemptUserId, bool expectedPassed)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new AppDbContext(options);
        var exercise = new PracticeExercise
        {
            Id = 11, LessonId = 3, Title = "Practice",
            LanguageSkill = new LanguageSkill { Id = 1, Name = "Grammar" },
        };
        context.Set<PracticeExercise>().Add(exercise);
        context.Set<PracticeExerciseAttempt>().Add(new PracticeExerciseAttempt
        {
            Id = 12, PracticeExerciseId = 11, UserId = attemptUserId,
            ScorePercent = score, IsCompleted = completed, StartedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
        var lessons = new Mock<ILessonRepository>();
        lessons.Setup(repo => repo.GetLearnerVisibleByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lesson { Id = 3, Title = "Lesson" });
        var real = new UnitOfWork(context);
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(item => item.Lessons).Returns(lessons.Object);
        unit.SetupGet(item => item.PracticeExercises).Returns(real.PracticeExercises);
        unit.SetupGet(item => item.PracticeExerciseAttempts).Returns(real.PracticeExerciseAttempts);
        var claims = new Mock<IClaimService>();
        claims.Setup(item => item.GetUserClaim()).Returns(new ClaimDTO { Id = 7 });
        var service = new PracticeQuizService(unit.Object, claims.Object, Mock.Of<ILearningPathService>());

        var result = await service.GetLessonExercisesAsync(3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(result.Result!);
        Assert.Equal(11, summary.PracticeExerciseId);
        Assert.Equal(70, summary.MinimumPassingScorePercent);
        Assert.Equal(expectedPassed, summary.Passed);
    }
}
