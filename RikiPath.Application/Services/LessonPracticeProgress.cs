using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

internal static class LessonPracticeProgress
{
    public const double PassingScore = 70;

    public static IQueryable<PracticeExercise> ReadyExercises(IUnitOfWork unit)
        => unit.PracticeExercises.Query().Where(exercise => !exercise.IsDeleted &&
            exercise.Questions!.Any(question => !question.IsDeleted) &&
            exercise.Questions!.Where(question => !question.IsDeleted).All(question =>
                question.Options!.Count(option => !option.IsDeleted) >= 2 &&
                question.Options!.Count(option => !option.IsDeleted && option.IsCorrect) == 1));

    public static async Task<HashSet<int>> WithReadyExercisesAsync(IUnitOfWork unit,
        IReadOnlyCollection<int> lessonIds, CancellationToken ct)
    {
        if (lessonIds.Count == 0) return [];
        return (await ReadyExercises(unit)
            .Where(exercise => exercise.LessonId.HasValue
                && exercise.Scope == PracticeExerciseScopes.LessonQuickCheck
                && lessonIds.Contains(exercise.LessonId.Value))
            .Select(exercise => exercise.LessonId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public static async Task<HashSet<int>> PassedAsync(IUnitOfWork unit, int userId,
        IReadOnlyCollection<int> lessonIds, CancellationToken ct, bool requireReadyExercise = false)
    {
        if (lessonIds.Count == 0) return [];
        var exercises = (requireReadyExercise ? ReadyExercises(unit) : unit.PracticeExercises.Query())
            .Where(exercise => exercise.LessonId.HasValue && exercise.Scope == PracticeExerciseScopes.LessonQuickCheck);
        return (await unit.PracticeExerciseAttempts.Query()
            .Join(exercises, attempt => attempt.PracticeExerciseId,
                exercise => exercise.Id, (attempt, exercise) => new { attempt, exercise })
            .Where(row => !row.attempt.IsDeleted && !row.exercise.IsDeleted &&
                row.attempt.UserId == userId && row.attempt.HomeworkAssignmentId == null && row.attempt.IsCompleted &&
                // Same rule as the roadmap: the Quick Check's own passing score; PassingScore only when unset.
                row.attempt.ScorePercent >= (row.exercise.PassingScorePercent > 0 ? row.exercise.PassingScorePercent : PassingScore) &&
                lessonIds.Contains(row.exercise.LessonId!.Value))
            .Select(row => row.exercise.LessonId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public static async Task<HashSet<int>> WithExercisesAsync(IUnitOfWork unit,
        IReadOnlyCollection<int> lessonIds, CancellationToken ct)
    {
        if (lessonIds.Count == 0) return [];
        return (await unit.PracticeExercises.Query()
            .Where(row => !row.IsDeleted && row.LessonId.HasValue && lessonIds.Contains(row.LessonId.Value))
            .Select(row => row.LessonId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
    }

    public static async Task<bool> HasStudiedAsync(IUnitOfWork unit, int userId, int lessonId, CancellationToken ct)
    {
        var progress = await unit.LessonProgresses.GetByUserAndLessonAsync(userId, lessonId, ct);
        // A previous passing result remains valid for learners who studied before this step was introduced.
        return progress is { IsCompleted: true, IsDeleted: false } ||
            (await PassedAsync(unit, userId, [lessonId], ct)).Contains(lessonId);
    }

    public static async Task<bool> AllClearedAsync(IUnitOfWork unit, int userId,
        IReadOnlyCollection<int> lessonIds, CancellationToken ct)
    {
        var passed = await PassedAsync(unit, userId, lessonIds, ct);
        var remaining = lessonIds.Where(id => !passed.Contains(id)).ToArray();
        if (remaining.Length == 0) return true;
        if ((await WithExercisesAsync(unit, remaining, ct)).Count > 0) return false;

        var studiedCount = await unit.LessonProgresses.Query()
            .Where(row => !row.IsDeleted && row.UserId == userId && row.IsCompleted && remaining.Contains(row.LessonId))
            .Select(row => row.LessonId).Distinct().CountAsync(ct);
        return studiedCount == remaining.Length;
    }
}
