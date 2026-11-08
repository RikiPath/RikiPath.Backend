using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeExercises;
using RikiPath.Domain.Entities;
using System.Net;

namespace RikiPath.Application.Services;

public class PracticeQuizService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    ILearningPathService learningPathService) : IPracticeQuizService
{
    public const double MinimumPassingScorePercent = 70;

    public async Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetLessonExercisesAsync(
        int lessonId, CancellationToken cancellationToken)
    {
        if (lessonId <= 0)
            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Fail("LessonId không hợp lệ.");

        try
        {
            var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(lessonId, cancellationToken);
            if (lesson is null)
                return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.NotFound("Không tìm thấy lesson.");

            var exercises = await unitOfWork.PracticeExercises.Query()
                .Where(exercise => !exercise.IsDeleted && exercise.LessonId == lessonId)
                .OrderBy(exercise => exercise.SortOrder)
                .ThenBy(exercise => exercise.Id)
                .Select(exercise => new PracticeExerciseSummaryResponse
                {
                    LessonId = exercise.LessonId,
                    Title = exercise.Title,
                    Description = exercise.Description,
                    LanguageSkillName = exercise.LanguageSkill.Name,
                    QuestionCount = exercise.Questions!.Count(question => !question.IsDeleted),
                })
                .ToListAsync(cancellationToken);

            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Success(exercises);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Fail(
                "Không thể tải bài Practice Exercise của lesson.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(
                practiceExerciseId, cancellationToken);
            if (exercise is null || exercise.IsDeleted ||
                await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(exercise.LessonId, cancellationToken) is null)
                return ApiResponse<PracticeExerciseAttemptResponse>.NotFound("Không tìm thấy bài Practice Exercise.");

            var questions = GetVisibleQuestions(exercise);
            if (questions.Count == 0 || questions.Any(question =>
                    (question.Options ?? []).Count(option => !option.IsDeleted) < 2))
                return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                    "Bài Practice Exercise chưa có đủ câu hỏi và lựa chọn để làm.", HttpStatusCode.Conflict);

            var attempt = new PracticeExerciseAttempt
            {
                UserId = userId,
                PracticeExerciseId = exercise.Id,
                StartedAt = DateTime.UtcNow,
            };
            await unitOfWork.PracticeExerciseAttempts.AddAsync(attempt, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<PracticeExerciseAttemptResponse>.Success(new PracticeExerciseAttemptResponse
            {
                AttemptId = attempt.Id,
                PracticeExerciseId = exercise.Id,
                ExerciseTitle = exercise.Title,
                LessonId = exercise.LessonId,
                Questions = questions.Select(question => new PracticeExerciseQuestionResponse
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    SortOrder = question.SortOrder,
                    Options = (question.Options ?? []).Where(option => !option.IsDeleted)
                        .OrderBy(option => option.SortOrder)
                        .Select(option => new PracticeExerciseOptionResponse
                        {
                            OptionId = option.Id,
                            OptionText = option.OptionText,
                            SortOrder = option.SortOrder,
                        }).ToList(),
                }).ToList(),
            });
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                "Không thể bắt đầu bài Practice Exercise.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var attempt = await unitOfWork.PracticeExerciseAttempts.GetByIdAsync(attemptId, cancellationToken);
            if (attempt is null || attempt.IsDeleted || attempt.UserId != userId)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.NotFound("Không tìm thấy lượt làm bài.");
            if (attempt.IsCompleted)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                    "Lượt làm này đã được nộp. Hãy tạo lượt mới nếu muốn làm lại.",
                    HttpStatusCode.Conflict, "PRACTICE_ATTEMPT_ALREADY_SUBMITTED");

            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(
                attempt.PracticeExerciseId, cancellationToken);
            if (exercise is null || exercise.IsDeleted)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.NotFound("Không tìm thấy bài Practice Exercise.");

            var questions = GetVisibleQuestions(exercise);
            if (questions.Count == 0)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Bài tập không có câu hỏi.");

            var suppliedAnswers = request.Answers ?? [];
            if (suppliedAnswers.GroupBy(answer => answer.QuestionId).Any(group => group.Count() > 1))
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Không được gửi đáp án trùng câu hỏi.");

            var questionMap = questions.ToDictionary(question => question.Id);
            foreach (var answer in suppliedAnswers)
            {
                if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                    return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                        "Có câu trả lời không thuộc bài tập này.");
                if (answer.SelectedOptionId.HasValue &&
                    !(question.Options ?? []).Any(option => !option.IsDeleted && option.Id == answer.SelectedOptionId.Value))
                    return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                        "Có lựa chọn trả lời không thuộc câu hỏi.");
            }

            var answerMap = suppliedAnswers.ToDictionary(answer => answer.QuestionId);
            var correctCount = 0;
            var persistedAnswers = new List<LearnerPracticeAnswer>(questions.Count);
            foreach (var question in questions)
            {
                answerMap.TryGetValue(question.Id, out var supplied);
                var selectedOption = supplied?.SelectedOptionId;
                var isCorrect = selectedOption.HasValue &&
                    (question.Options ?? []).Any(option => !option.IsDeleted &&
                        option.Id == selectedOption.Value && option.IsCorrect);
                if (isCorrect) correctCount++;
                persistedAnswers.Add(new LearnerPracticeAnswer
                {
                    PracticeExerciseAttemptId = attempt.Id,
                    PracticeQuestionId = question.Id,
                    SelectedOptionId = selectedOption,
                    IsCorrect = isCorrect,
                });
            }

            var scorePercent = Math.Round(correctCount * 100d / questions.Count, 2);
            attempt.ScorePercent = scorePercent;
            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.IsCompleted = true;
            unitOfWork.PracticeExerciseAttempts.Update(attempt);
            await unitOfWork.LearnerPracticeAnswers.AddRangeAsync(persistedAnswers, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var passed = scorePercent >= MinimumPassingScorePercent;
            var unlockedNextDay = false;
            if (passed)
            {
                var latestPath = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
                var activeDay = latestPath.Result?.DailySchedules.FirstOrDefault(day =>
                    day.IsStarted && !day.IsCompleted && day.LessonIds.Contains(exercise.LessonId));
                if (activeDay is not null)
                {
                    var completion = await learningPathService.CompleteActiveDayAsync(
                        activeDay.Day, cancellationToken);
                    unlockedNextDay = completion.IsSuccess;
                }
            }

            return ApiResponse<PracticeExerciseAttemptResultResponse>.Success(new PracticeExerciseAttemptResultResponse
            {
                AttemptId = attempt.Id,
                PracticeExerciseId = exercise.Id,
                LessonId = exercise.LessonId,
                ScorePercent = scorePercent,
                CorrectCount = correctCount,
                TotalQuestions = questions.Count,
                MinimumPassingScorePercent = MinimumPassingScorePercent,
                Passed = passed,
                UnlocksNextLearningPathDay = unlockedNextDay,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                "Không thể nộp bài Practice Exercise.", HttpStatusCode.InternalServerError);
        }
    }

    private static List<PracticeQuestion> GetVisibleQuestions(PracticeExercise exercise)
        => (exercise.Questions ?? []).Where(question => !question.IsDeleted)
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToList();
}
