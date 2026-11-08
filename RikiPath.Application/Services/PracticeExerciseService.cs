using System.Net;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeExercises;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    public sealed class PracticeExerciseService(
        IUnitOfWork unitOfWork,
        ILogger<PracticeExerciseService> logger) : IPracticeExerciseService
    {
        private const int MaxTextAnswerLength = 2000;

        public async Task<ApiResponse<List<PracticeExerciseSummaryResponse>>> GetByLessonAsync(
            int lessonId,
            int userId,
            CancellationToken cancellationToken)
        {
            if (lessonId <= 0)
                return ApiResponse<List<PracticeExerciseSummaryResponse>>.Fail(
                    "LessonId phải là số nguyên dương.");

            try
            {
                var lesson = await unitOfWork.Lessons.GetByIdAsync(lessonId, cancellationToken);
                if (lesson is null || lesson.IsDeleted)
                    return ApiResponse<List<PracticeExerciseSummaryResponse>>.NotFound(
                        "Không tìm thấy bài học.");

                var exercises = await unitOfWork.PracticeExercises.FindAsync(
                    exercise => exercise.LessonId == lessonId && !exercise.IsDeleted,
                    cancellationToken);

                if (exercises.Count == 0)
                    return ApiResponse<List<PracticeExerciseSummaryResponse>>.Success(new());

                var exerciseIds = exercises.Select(exercise => exercise.Id).ToList();
                var skillIds = exercises.Select(exercise => exercise.LanguageSkillId).Distinct().ToList();

                // Các query chạy tuần tự vì DbContext không an toàn khi dùng song song.
                var skills = await unitOfWork.LanguageSkills.FindAsync(
                    skill => skillIds.Contains(skill.Id) && !skill.IsDeleted,
                    cancellationToken);
                var questions = await unitOfWork.PracticeQuestions.FindAsync(
                    question => exerciseIds.Contains(question.PracticeExerciseId) && !question.IsDeleted,
                    cancellationToken);
                var attempts = await unitOfWork.PracticeExerciseAttempts.FindAsync(
                    attempt => attempt.UserId == userId
                               && !attempt.IsDeleted
                               && exerciseIds.Contains(attempt.PracticeExerciseId),
                    cancellationToken);

                var skillNames = skills.ToDictionary(skill => skill.Id, skill => skill.Name);
                var questionCounts = questions
                    .GroupBy(question => question.PracticeExerciseId)
                    .ToDictionary(group => group.Key, group => group.Count());
                var attemptsByExercise = attempts
                    .GroupBy(attempt => attempt.PracticeExerciseId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                var response = exercises
                    .OrderBy(exercise => exercise.SortOrder)
                    .Select(exercise =>
                    {
                        attemptsByExercise.TryGetValue(exercise.Id, out var exerciseAttempts);
                        var latest = exerciseAttempts?
                            .OrderByDescending(attempt => attempt.StartedAt)
                            .FirstOrDefault();
                        var best = exerciseAttempts?
                            .Where(attempt => attempt.IsCompleted && attempt.ScorePercent.HasValue)
                            .Max(attempt => attempt.ScorePercent);

                        return new PracticeExerciseSummaryResponse
                        {
                            Id = exercise.Id,
                            LessonId = exercise.LessonId,
                            Title = exercise.Title,
                            Description = exercise.Description,
                            LanguageSkillId = exercise.LanguageSkillId,
                            LanguageSkillName = skillNames.GetValueOrDefault(exercise.LanguageSkillId, string.Empty),
                            SortOrder = exercise.SortOrder,
                            QuestionCount = questionCounts.GetValueOrDefault(exercise.Id),
                            BestScorePercent = best,
                            LatestAttempt = latest is null
                                ? null
                                : new PracticeLatestAttemptResponse
                                {
                                    AttemptId = latest.Id,
                                    IsCompleted = latest.IsCompleted,
                                    ScorePercent = latest.ScorePercent,
                                    StartedAt = latest.StartedAt,
                                    SubmittedAt = latest.SubmittedAt,
                                },
                        };
                    })
                    .ToList();

                return ApiResponse<List<PracticeExerciseSummaryResponse>>.Success(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể tải danh sách bài luyện tập của Lesson {LessonId}.", lessonId);
                return ApiResponse<List<PracticeExerciseSummaryResponse>>.Fail(
                    "Không thể tải danh sách bài luyện tập.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<StartPracticeAttemptResponse>> StartAttemptAsync(
            int practiceExerciseId,
            int userId,
            CancellationToken cancellationToken)
        {
            if (practiceExerciseId <= 0)
                return ApiResponse<StartPracticeAttemptResponse>.Fail(
                    "PracticeExerciseId phải là số nguyên dương.");

            try
            {
                var exercise = await unitOfWork.PracticeExercises.GetByIdAsync(
                    practiceExerciseId,
                    cancellationToken);
                if (exercise is null || exercise.IsDeleted)
                    return ApiResponse<StartPracticeAttemptResponse>.NotFound(
                        "Không tìm thấy bài luyện tập.");

                var questions = await unitOfWork.PracticeQuestions.FindAsync(
                    question => question.PracticeExerciseId == practiceExerciseId && !question.IsDeleted,
                    cancellationToken);
                if (questions.Count == 0)
                    return ApiResponse<StartPracticeAttemptResponse>.Fail(
                        "Bài luyện tập chưa có câu hỏi.");

                var questionIds = questions.Select(question => question.Id).ToList();
                var options = await unitOfWork.PracticeQuestionOptions.FindAsync(
                    option => questionIds.Contains(option.PracticeQuestionId) && !option.IsDeleted,
                    cancellationToken);

                // Nếu learner đang có lượt làm bài chưa hoàn thành thì tiếp tục lượt đó, không tạo lượt mới.
                var openAttempts = await unitOfWork.PracticeExerciseAttempts.FindAsync(
                    attempt => attempt.UserId == userId
                               && attempt.PracticeExerciseId == practiceExerciseId
                               && !attempt.IsCompleted
                               && !attempt.IsDeleted,
                    cancellationToken);

                var attempt = openAttempts
                    .OrderByDescending(item => item.StartedAt)
                    .FirstOrDefault();
                var resumed = attempt is not null;

                if (attempt is null)
                {
                    var now = DateTime.UtcNow;
                    attempt = new PracticeExerciseAttempt
                    {
                        UserId = userId,
                        PracticeExerciseId = practiceExerciseId,
                        StartedAt = now,
                        IsCompleted = false,
                        CreatedDate = now,
                        IsDeleted = false,
                    };

                    await unitOfWork.PracticeExerciseAttempts.AddAsync(attempt, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                var savedAnswers = await unitOfWork.LearnerPracticeAnswers.FindAsync(
                    answer => answer.PracticeExerciseAttemptId == attempt.Id && !answer.IsDeleted,
                    cancellationToken);

                var optionsByQuestion = options
                    .GroupBy(option => option.PracticeQuestionId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(option => option.SortOrder).ToList());

                var response = new StartPracticeAttemptResponse
                {
                    AttemptId = attempt.Id,
                    PracticeExerciseId = exercise.Id,
                    Title = exercise.Title,
                    StartedAt = attempt.StartedAt,
                    Resumed = resumed,
                    Questions = questions
                        .OrderBy(question => question.SortOrder)
                        .Select(question => new PracticeQuestionResponse
                        {
                            Id = question.Id,
                            QuestionText = question.QuestionText,
                            SortOrder = question.SortOrder,
                            Options = optionsByQuestion.TryGetValue(question.Id, out var questionOptions)
                                ? questionOptions.Select(option => new PracticeOptionResponse
                                {
                                    Id = option.Id,
                                    OptionText = option.OptionText,
                                    SortOrder = option.SortOrder,
                                }).ToList()
                                : new List<PracticeOptionResponse>(),
                        })
                        .ToList(),
                    SavedAnswers = savedAnswers
                        .Select(answer => new PracticeSavedAnswerResponse
                        {
                            QuestionId = answer.PracticeQuestionId,
                            SelectedOptionId = answer.SelectedOptionId,
                            TextAnswer = answer.TextAnswer,
                        })
                        .ToList(),
                };

                return resumed
                    ? ApiResponse<StartPracticeAttemptResponse>.Success(response)
                    : ApiResponse<StartPracticeAttemptResponse>.Created(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể bắt đầu lượt làm bài luyện tập {PracticeExerciseId}.", practiceExerciseId);
                return ApiResponse<StartPracticeAttemptResponse>.Fail(
                    "Không thể bắt đầu lượt làm bài luyện tập.",
                    HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<SubmitPracticeAttemptResponse>> SubmitAttemptAsync(
            int attemptId,
            int userId,
            SubmitPracticeAttemptRequest request,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(attemptId, request);
            if (validationError is not null)
                return ApiResponse<SubmitPracticeAttemptResponse>.Fail(validationError);

            try
            {
                var attempt = await unitOfWork.PracticeExerciseAttempts.GetByIdAsync(
                    attemptId,
                    cancellationToken);
                if (attempt is null || attempt.IsDeleted)
                    return ApiResponse<SubmitPracticeAttemptResponse>.NotFound(
                        "Không tìm thấy lượt làm bài.");

                if (attempt.UserId != userId)
                    return ApiResponse<SubmitPracticeAttemptResponse>.Fail(
                        "Bạn không có quyền nộp lượt làm bài này.",
                        HttpStatusCode.Forbidden);

                if (attempt.IsCompleted)
                    return ApiResponse<SubmitPracticeAttemptResponse>.Fail(
                        "Lượt làm bài đã được nộp trước đó.",
                        HttpStatusCode.Conflict);

                var questions = (await unitOfWork.PracticeQuestions.FindAsync(
                        question => question.PracticeExerciseId == attempt.PracticeExerciseId && !question.IsDeleted,
                        cancellationToken))
                    .OrderBy(question => question.SortOrder)
                    .ToList();

                var questionIds = questions.Select(question => question.Id).ToList();
                var optionsByQuestion = (await unitOfWork.PracticeQuestionOptions.FindAsync(
                        option => questionIds.Contains(option.PracticeQuestionId) && !option.IsDeleted,
                        cancellationToken))
                    .GroupBy(option => option.PracticeQuestionId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                // Mọi câu trả lời phải thuộc bài này và option phải thuộc đúng câu hỏi.
                foreach (var answer in request.Answers)
                {
                    if (!questionIds.Contains(answer.QuestionId))
                        return ApiResponse<SubmitPracticeAttemptResponse>.Fail(
                            $"Câu hỏi {answer.QuestionId} không thuộc bài luyện tập này.");

                    if (answer.SelectedOptionId is int selectedOptionId
                        && !(optionsByQuestion.TryGetValue(answer.QuestionId, out var validOptions)
                             && validOptions.Any(option => option.Id == selectedOptionId)))
                        return ApiResponse<SubmitPracticeAttemptResponse>.Fail(
                            $"Đáp án {selectedOptionId} không thuộc câu hỏi {answer.QuestionId}.");
                }

                // Câu có option đúng → chấm tự động.
                // Câu không có option đúng (tự luận) → IsCorrect = null và không tính vào điểm.
                var submitted = request.Answers.ToDictionary(answer => answer.QuestionId);
                var now = DateTime.UtcNow;
                var newAnswers = new List<LearnerPracticeAnswer>();
                var results = new List<PracticeAnswerResultResponse>();
                var gradedCount = 0;
                var correctCount = 0;

                foreach (var question in questions)
                {
                    submitted.TryGetValue(question.Id, out var answer);
                    optionsByQuestion.TryGetValue(question.Id, out var questionOptions);
                    var correctOptionId = questionOptions?.FirstOrDefault(option => option.IsCorrect)?.Id;

                    bool? isCorrect = null;
                    if (correctOptionId.HasValue)
                    {
                        gradedCount++;
                        isCorrect = answer?.SelectedOptionId == correctOptionId;
                        if (isCorrect == true)
                            correctCount++;
                    }

                    var textAnswer = NormalizeOptional(answer?.TextAnswer);
                    newAnswers.Add(new LearnerPracticeAnswer
                    {
                        PracticeExerciseAttemptId = attempt.Id,
                        PracticeQuestionId = question.Id,
                        SelectedOptionId = answer?.SelectedOptionId,
                        TextAnswer = textAnswer,
                        IsCorrect = isCorrect,
                        CreatedDate = now,
                        IsDeleted = false,
                    });

                    results.Add(new PracticeAnswerResultResponse
                    {
                        QuestionId = question.Id,
                        SelectedOptionId = answer?.SelectedOptionId,
                        TextAnswer = textAnswer,
                        CorrectOptionId = correctOptionId,
                        IsCorrect = isCorrect,
                        Explanation = question.Explanation,
                    });
                }

                var scorePercent = gradedCount == 0
                    ? 0
                    : Math.Round(correctCount * 100.0 / gradedCount, 2);

                // Bỏ đáp án nháp cũ (nếu có) rồi lưu bộ đáp án cuối cùng.
                var oldAnswers = await unitOfWork.LearnerPracticeAnswers.FindAsync(
                    answer => answer.PracticeExerciseAttemptId == attempt.Id && !answer.IsDeleted,
                    cancellationToken);
                if (oldAnswers.Count > 0)
                    unitOfWork.LearnerPracticeAnswers.RemoveRange(oldAnswers);

                await unitOfWork.LearnerPracticeAnswers.AddRangeAsync(newAnswers, cancellationToken);

                attempt.ScorePercent = scorePercent;
                attempt.IsCompleted = true;
                attempt.SubmittedAt = now;
                attempt.ModifiedDate = now;
                unitOfWork.PracticeExerciseAttempts.Update(attempt);

                // Một lần SaveChanges duy nhất để đáp án và lượt làm bài được lưu cùng lúc.
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<SubmitPracticeAttemptResponse>.Success(new SubmitPracticeAttemptResponse
                {
                    AttemptId = attempt.Id,
                    ScorePercent = scorePercent,
                    CorrectCount = correctCount,
                    GradedQuestions = gradedCount,
                    TotalQuestions = questions.Count,
                    SubmittedAt = now,
                    Results = results,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Không thể nộp lượt làm bài luyện tập {AttemptId}.", attemptId);
                return ApiResponse<SubmitPracticeAttemptResponse>.Fail(
                    "Không thể nộp bài luyện tập.",
                    HttpStatusCode.InternalServerError);
            }
        }

        private static string? Validate(int attemptId, SubmitPracticeAttemptRequest? request)
        {
            if (attemptId <= 0)
                return "AttemptId phải là số nguyên dương.";
            if (request?.Answers is null)
                return "Answers là bắt buộc.";
            if (request.Answers.Any(answer => answer.QuestionId <= 0))
                return "QuestionId phải là số nguyên dương.";
            if (request.Answers.Any(answer => answer.SelectedOptionId is <= 0))
                return "SelectedOptionId phải là số nguyên dương khi được cung cấp.";
            if (request.Answers.Any(answer => answer.TextAnswer?.Trim().Length > MaxTextAnswerLength))
                return $"TextAnswer không được vượt quá {MaxTextAnswerLength} ký tự.";
            if (request.Answers.GroupBy(answer => answer.QuestionId).Any(group => group.Count() > 1))
                return "Mỗi câu hỏi chỉ được trả lời một lần.";

            return null;
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
