using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Application.Responses.PracticeExercises;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using System.Net;

namespace RikiPath.Application.Services;

public class PracticeQuizService(
    IUnitOfWork unitOfWork,
    IClaimService claimService,
    ILearningPathService learningPathService,
    IStandardRoadmapService? standardRoadmapService = null) : IPracticeQuizService
{
    public const double MinimumPassingScorePercent = LessonPracticeProgress.PassingScore;

    public Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetLessonExercisesAsync(
        int lessonId, CancellationToken cancellationToken)
        => LoadLessonExercisesAsync(lessonId, cancellationToken);

    public Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetAssessmentRoadmapExercisesAsync(
        int lessonId, CancellationToken cancellationToken)
        => LoadLessonExercisesAsync(lessonId, cancellationToken, fromAssessmentRoadmap: true);

    public async Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> GetCheckpointsAsync(
        int? topicId, int? certificateLevelId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var query = LessonPracticeProgress.ReadyExercises(unitOfWork)
                .Where(e => e.Scope == PracticeExerciseScopes.GroupQuiz || e.Scope == PracticeExerciseScopes.StageAssessment);

            if (topicId.HasValue)
                query = query.Where(e => e.TopicId == topicId.Value);
            if (certificateLevelId.HasValue)
                query = query.Where(e => e.CertificateLevelId == certificateLevelId.Value);

            var list = await query
                .OrderBy(e => e.SortOrder).ThenBy(e => e.Id)
                .Select(e => new PracticeExerciseSummaryResponse
                {
                    Id = e.Id,
                    MinimumPassingScorePercent = e.PassingScorePercent > 0 ? e.PassingScorePercent : MinimumPassingScorePercent,
                    Passed = unitOfWork.PracticeExerciseAttempts.Query().Any(a =>
                        !a.IsDeleted && a.UserId == userId && a.HomeworkAssignmentId == null &&
                        a.IsCompleted && a.PracticeExerciseId == e.Id &&
                        a.ScorePercent >= (e.PassingScorePercent > 0 ? e.PassingScorePercent : MinimumPassingScorePercent)),
                    LessonId = e.LessonId ?? 0,
                    TopicId = e.TopicId,
                    CertificateLevelId = e.CertificateLevelId,
                    Scope = e.Scope,
                    Title = e.Title,
                    ImageUrl = e.ImageUrl,
                    Description = e.Description,
                    LanguageSkillId = e.LanguageSkillId,
                    LanguageSkillName = e.LanguageSkill.Name,
                    QuestionCount = e.Questions!.Count(q => !q.IsDeleted),
                })
                .ToListAsync(cancellationToken);

            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Success(list);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Fail(
                "Không thể tải danh sách bài kiểm tra chặng.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<PracticeExerciseAttemptResponse>> StartCheckpointAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(
                practiceExerciseId, cancellationToken);
            if (exercise is null || exercise.IsDeleted ||
                (exercise.Scope != PracticeExerciseScopes.GroupQuiz && exercise.Scope != PracticeExerciseScopes.StageAssessment))
                return ApiResponse<PracticeExerciseAttemptResponse>.NotFound("Không tìm thấy bài kiểm tra chặng.");

            // Check unlock status against standard adaptive roadmap so learner must study preceding lessons first
            var roadmapSvc = standardRoadmapService ?? new StandardRoadmapService(unitOfWork, claimService);
            var roadmapRes = await roadmapSvc.GetAssessmentRoadmapAsync(cancellationToken);
            if (roadmapRes.IsSuccess && roadmapRes.Result is { } roadmap)
            {
                var checkpoint = roadmap.Stages
                    .SelectMany(s => new[] { s.GroupQuiz, s.StageAssessment })
                    .FirstOrDefault(cp => cp is not null && cp.ExerciseId == practiceExerciseId);
                if (checkpoint is { IsUnlocked: false })
                {
                    return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                        checkpoint.LockReason ?? "Hãy học xong các bài trong chặng trước khi làm bài kiểm tra.",
                        HttpStatusCode.Conflict, "CHECKPOINT_LOCKED");
                }
            }

            var questions = GetVisibleQuestions(exercise);
            if (!CanTakeExercise(questions, requireSingleCorrectOption: true))
                return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                    "Bài kiểm tra chặng chưa có đủ câu hỏi và lựa chọn hợp lệ.", HttpStatusCode.Conflict);

            var attempt = new PracticeExerciseAttempt
            {
                UserId = userId,
                PracticeExerciseId = exercise.Id,
                HomeworkAssignmentId = null,
                StartedAt = DateTime.UtcNow,
            };
            await unitOfWork.PracticeExerciseAttempts.AddAsync(attempt, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var passingScore = exercise.PassingScorePercent > 0 ? exercise.PassingScorePercent : MinimumPassingScorePercent;
            return ApiResponse<PracticeExerciseAttemptResponse>.Success(new PracticeExerciseAttemptResponse
            {
                AttemptId = attempt.Id,
                PracticeExerciseId = exercise.Id,
                ExerciseTitle = exercise.Title,
                Scope = exercise.Scope,
                ImageUrl = exercise.ImageUrl,
                LessonId = exercise.LessonId ?? 0,
                TopicId = exercise.TopicId,
                CertificateLevelId = exercise.CertificateLevelId,
                PassingScorePercent = passingScore,
                Questions = questions.Select(question => new PracticeExerciseQuestionResponse
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    ImageUrl = question.ImageUrl,
                    SortOrder = question.SortOrder,
                    Options = (question.Options ?? []).Where(option => !option.IsDeleted)
                        .OrderBy(option => option.SortOrder)
                        .Select(option => new PracticeExerciseOptionResponse
                        {
                            OptionId = option.Id,
                            OptionText = option.OptionText,
                            ImageUrl = option.ImageUrl,
                            SortOrder = option.SortOrder,
                        }).ToList(),
                }).ToList(),
            });
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                "Không thể bắt đầu bài kiểm tra chặng.", HttpStatusCode.InternalServerError);
        }
    }

    public Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitCheckpointAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
        => WithLearnerLockAsync(
            token => SubmitAsync(attemptId, null, request, token, fromAssessmentRoadmap: false, isCheckpoint: true),
            cancellationToken);

    private async Task<ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>> LoadLessonExercisesAsync(
        int lessonId, CancellationToken cancellationToken, bool fromAssessmentRoadmap = false)
    {
        if (lessonId <= 0)
            return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Fail("LessonId không hợp lệ.");

        try
        {
            if (fromAssessmentRoadmap)
            {
                var access = await CheckAssessmentRoadmapLessonAsync(lessonId, cancellationToken);
                if (!access.IsSuccess)
                    return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.Fail(
                        access.ErrorMessage!, (HttpStatusCode)access.StatusCode, access.ErrorCode);
            }

            var lesson = await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(lessonId, cancellationToken);
            if (lesson is null)
                return ApiResponse<IReadOnlyList<PracticeExerciseSummaryResponse>>.NotFound("Không tìm thấy lesson.");

            var userId = claimService.GetUserClaim().Id;
            var source = fromAssessmentRoadmap
                ? LessonPracticeProgress.ReadyExercises(unitOfWork)
                : unitOfWork.PracticeExercises.Query();
            var exercises = await source
                .Where(exercise => !exercise.IsDeleted && exercise.LessonId == lessonId && exercise.Scope == PracticeExerciseScopes.LessonQuickCheck)
                .OrderBy(exercise => exercise.SortOrder)
                .ThenBy(exercise => exercise.Id)
                .Select(exercise => new PracticeExerciseSummaryResponse
                {
                    Id = exercise.Id,
                    MinimumPassingScorePercent = exercise.PassingScorePercent > 0 ? exercise.PassingScorePercent : MinimumPassingScorePercent,
                    Passed = unitOfWork.PracticeExerciseAttempts.Query().Any(attempt =>
                        !attempt.IsDeleted && attempt.UserId == userId && attempt.HomeworkAssignmentId == null &&
                        attempt.IsCompleted &&
                        attempt.PracticeExerciseId == exercise.Id &&
                        attempt.ScorePercent >= (exercise.PassingScorePercent > 0 ? exercise.PassingScorePercent : MinimumPassingScorePercent)),
                    LessonId = exercise.LessonId ?? 0,
                    TopicId = exercise.TopicId,
                    CertificateLevelId = exercise.CertificateLevelId,
                    Scope = exercise.Scope,
                    Title = exercise.Title,
                    ImageUrl = exercise.ImageUrl,
                    Description = exercise.Description,
                    LanguageSkillId = exercise.LanguageSkillId,
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

    public Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken, bool fromCatalog = false, string? skill = null)
        => StartAsync(practiceExerciseId, null, cancellationToken, fromCatalog, skill);

    public Task<ApiResponse<PracticeExerciseAttemptResponse>> StartHomeworkAttemptAsync(
        int assignmentId, int exerciseId, CancellationToken cancellationToken)
        => WithLearnerLockAsync(token => StartAsync(exerciseId, assignmentId, token), cancellationToken);

    public Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAssessmentRoadmapAttemptAsync(
        int practiceExerciseId, CancellationToken cancellationToken)
        => StartAsync(practiceExerciseId, null, cancellationToken, fromAssessmentRoadmap: true);

    private async Task<ApiResponse<PracticeExerciseAttemptResponse>> StartAsync(
        int practiceExerciseId, int? assignmentId, CancellationToken cancellationToken, bool fromCatalog = false,
        string? skill = null, bool fromAssessmentRoadmap = false)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            if (assignmentId.HasValue && !await unitOfWork.HomeworkAssignments.Query().AnyAsync(assignment =>
                    assignment.Id == assignmentId && assignment.LearnerId == userId && !assignment.IsDeleted &&
                    assignment.Status == "Assigned" && assignment.Exercises.Any(link =>
                        link.PracticeExerciseId == practiceExerciseId), cancellationToken))
                return ApiResponse<PracticeExerciseAttemptResponse>.NotFound("Không tìm thấy bài luyện tập được giao.");

            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(
                practiceExerciseId, cancellationToken);
            if (exercise is null || exercise.IsDeleted)
                return ApiResponse<PracticeExerciseAttemptResponse>.NotFound("Không tìm thấy bài Practice Exercise.");

            var lesson = exercise.LessonId is int exLessonId
                ? await unitOfWork.Lessons.GetLearnerVisibleByIdAsync(exLessonId, cancellationToken)
                : null;
            if (exercise.LessonId.HasValue && lesson is null)
                return ApiResponse<PracticeExerciseAttemptResponse>.NotFound("Không tìm thấy bài Practice Exercise.");

            if (!assignmentId.HasValue && lesson is not null &&
                !await LessonPracticeProgress.HasStudiedAsync(unitOfWork, userId, lesson.Id, cancellationToken))
                return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                    "Hãy đánh dấu đã học xong bài trước khi làm bài luyện tập.",
                    HttpStatusCode.Conflict, "LESSON_STUDY_REQUIRED");

            if (fromAssessmentRoadmap && lesson is not null)
            {
                var access = await CheckAssessmentRoadmapLessonAsync(lesson.Id, cancellationToken);
                if (!access.IsSuccess)
                    return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                        access.ErrorMessage!, (HttpStatusCode)access.StatusCode, access.ErrorCode);
            }
            else if (!assignmentId.HasValue && fromCatalog && lesson is not null)
            {
                if (skill is not null && (skill is not ("Reading" or "Listening") || lesson.LanguageSkill.Name != skill))
                    return ApiResponse<PracticeExerciseAttemptResponse>.Fail("Kỹ năng ôn luyện không hợp lệ.");
                var catalog = await unitOfWork.Lessons.GetLearnerVisibleByLevelAsync(
                    lesson.CertificateLevelId, userId, cancellationToken);
                var preceding = catalog.Where(item => skill is null || item.LanguageSkill.Name == skill)
                    .TakeWhile(item => item.Id != lesson.Id).Select(item => item.Id).ToArray();
                if (!await LessonPracticeProgress.AllClearedAsync(unitOfWork, userId, preceding, cancellationToken))
                    return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                        "Hãy hoàn thành bài học trước để mở bài luyện tập này.",
                        HttpStatusCode.Conflict, "CATALOG_LESSON_LOCKED");
            }
            else if (!assignmentId.HasValue && lesson is not null)
            {
                var path = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
                var schedules = path.Result?.DailySchedules.OrderBy(day => day.Day).ToList();
                var lessonDay = schedules?.FirstOrDefault(day => day.IsStarted && !day.IsCompleted && day.LessonIds.Contains(lesson.Id))
                    ?? schedules?.FirstOrDefault(day => day.LessonIds.Contains(lesson.Id));
                if (lessonDay is not null)
                {
                    var preceding = lessonDay.LessonIds.TakeWhile(id => id != lesson.Id).Distinct().ToArray();
                    if (schedules!.Any(day => day.Day < lessonDay.Day && !day.IsCompleted) || !lessonDay.IsStarted ||
                        !await LessonPracticeProgress.AllClearedAsync(unitOfWork, userId, preceding, cancellationToken))
                        return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                            "Hãy hoàn thành bài học trước và bắt đầu ngày học này trước khi làm bài.",
                            HttpStatusCode.Conflict, "LEARNING_PATH_LESSON_LOCKED");
                }
            }

            var questions = GetVisibleQuestions(exercise);
            if (!CanTakeExercise(questions, fromAssessmentRoadmap))
                return ApiResponse<PracticeExerciseAttemptResponse>.Fail(
                    "Bài Practice Exercise chưa có đủ câu hỏi và lựa chọn để làm.", HttpStatusCode.Conflict);

            var attempt = assignmentId.HasValue
                ? await unitOfWork.PracticeExerciseAttempts.Query().SingleOrDefaultAsync(item =>
                    !item.IsDeleted && item.HomeworkAssignmentId == assignmentId &&
                    item.PracticeExerciseId == exercise.Id && item.UserId == userId, cancellationToken)
                : null;
            if (attempt?.IsCompleted == true)
                return ApiResponse<PracticeExerciseAttemptResponse>.Fail("Bài luyện tập này đã được nộp.",
                    HttpStatusCode.Conflict, "PRACTICE_ATTEMPT_ALREADY_SUBMITTED");

            if (attempt is null)
            {
                attempt = new PracticeExerciseAttempt
                {
                    UserId = userId,
                    PracticeExerciseId = exercise.Id,
                    HomeworkAssignmentId = assignmentId,
                    StartedAt = DateTime.UtcNow,
                };
                await unitOfWork.PracticeExerciseAttempts.AddAsync(attempt, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var passingScore = exercise.PassingScorePercent > 0 ? exercise.PassingScorePercent : MinimumPassingScorePercent;
            return ApiResponse<PracticeExerciseAttemptResponse>.Success(new PracticeExerciseAttemptResponse
            {
                AttemptId = attempt.Id,
                PracticeExerciseId = exercise.Id,
                ExerciseTitle = exercise.Title,
                Scope = exercise.Scope,
                ImageUrl = exercise.ImageUrl,
                LessonId = exercise.LessonId ?? 0,
                TopicId = exercise.TopicId,
                CertificateLevelId = exercise.CertificateLevelId,
                PassingScorePercent = passingScore,
                Questions = questions.Select(question => new PracticeExerciseQuestionResponse
                {
                    QuestionId = question.Id,
                    QuestionText = question.QuestionText,
                    ImageUrl = question.ImageUrl,
                    SortOrder = question.SortOrder,
                    Options = (question.Options ?? []).Where(option => !option.IsDeleted)
                        .OrderBy(option => option.SortOrder)
                        .Select(option => new PracticeExerciseOptionResponse
                        {
                            OptionId = option.Id,
                            OptionText = option.OptionText,
                            ImageUrl = option.ImageUrl,
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

    public Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
        => WithLearnerLockAsync(token => SubmitAsync(attemptId, null, request, token), cancellationToken);

    public Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAssessmentRoadmapAttemptAsync(
        int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
        => WithLearnerLockAsync(
            token => SubmitAsync(attemptId, null, request, token, fromAssessmentRoadmap: true),
            cancellationToken);

    public Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitHomeworkAttemptAsync(
        int assignmentId, int attemptId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
        => WithLearnerLockAsync(token => SubmitAsync(attemptId, assignmentId, request, token), cancellationToken);

    private async Task<ApiResponse<PracticeExerciseAttemptResultResponse>> SubmitAsync(
        int attemptId, int? assignmentId, SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken,
        bool fromAssessmentRoadmap = false, bool isCheckpoint = false)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var attempt = await unitOfWork.PracticeExerciseAttempts.GetByIdAsync(attemptId, cancellationToken);
            if (attempt is null || attempt.IsDeleted || attempt.UserId != userId || attempt.HomeworkAssignmentId != assignmentId)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.NotFound("Không tìm thấy lượt làm bài.");
            if (assignmentId.HasValue && !await unitOfWork.HomeworkAssignments.Query().AnyAsync(assignment =>
                    assignment.Id == assignmentId && assignment.LearnerId == userId && !assignment.IsDeleted &&
                    assignment.Status == "Assigned", cancellationToken))
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Bài tập đã đóng.",
                    HttpStatusCode.Conflict, "HOMEWORK_CLOSED");
            if (attempt.IsCompleted)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                    "Lượt làm này đã được nộp. Hãy tạo lượt mới nếu muốn làm lại.",
                    HttpStatusCode.Conflict, "PRACTICE_ATTEMPT_ALREADY_SUBMITTED");

            var exercise = await unitOfWork.PracticeExercises.GetWithQuestionsAndOptionsAsync(
                attempt.PracticeExerciseId, cancellationToken);
            if (exercise is null || exercise.IsDeleted)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.NotFound("Không tìm thấy bài Practice Exercise.");

            if (fromAssessmentRoadmap && exercise.LessonId is int roadmapLessonId)
            {
                var access = await CheckAssessmentRoadmapLessonAsync(roadmapLessonId, cancellationToken);
                if (!access.IsSuccess)
                    return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                        access.ErrorMessage!, (HttpStatusCode)access.StatusCode, access.ErrorCode);
            }
            else if (isCheckpoint)
            {
                var roadmapRes = await (standardRoadmapService ?? new StandardRoadmapService(unitOfWork, claimService))
                    .GetAssessmentRoadmapAsync(cancellationToken);
                if (roadmapRes.IsSuccess && roadmapRes.Result is { } gateRoadmap)
                {
                    var cpGate = gateRoadmap.Stages
                        .SelectMany(s => new[] { s.GroupQuiz, s.StageAssessment })
                        .FirstOrDefault(cp => cp is not null && cp.ExerciseId == exercise.Id);
                    if (cpGate is { IsUnlocked: false })
                        return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                            cpGate.LockReason ?? "Hãy học xong các bài trong chặng trước khi làm bài kiểm tra.",
                            HttpStatusCode.Conflict, "CHECKPOINT_LOCKED");
                }
            }

            var questions = GetVisibleQuestions(exercise);
            if (questions.Count == 0)
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Bài tập không có câu hỏi.");
            if ((fromAssessmentRoadmap || isCheckpoint) && !CanTakeExercise(questions, requireSingleCorrectOption: true))
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                    "Bài luyện tập chưa có bộ câu hỏi hợp lệ.", HttpStatusCode.Conflict);

            var suppliedAnswers = request.Answers ?? [];
            if (suppliedAnswers.GroupBy(answer => answer.QuestionId).Any(group => group.Count() > 1))
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Không được gửi đáp án trùng câu hỏi.");
            if ((assignmentId.HasValue || fromAssessmentRoadmap || isCheckpoint) && (suppliedAnswers.Count != questions.Count ||
                suppliedAnswers.Any(answer => !answer.SelectedOptionId.HasValue)))
                return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail("Hãy trả lời tất cả câu hỏi trước khi nộp.");

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
            var questionResults = new List<PracticeQuestionReviewResponse>(questions.Count);

            foreach (var question in questions)
            {
                answerMap.TryGetValue(question.Id, out var supplied);
                var visibleOptions = (question.Options ?? []).Where(o => !o.IsDeleted).ToList();
                var selectedOptionId = supplied?.SelectedOptionId;
                var selectedOpt = selectedOptionId.HasValue
                    ? visibleOptions.FirstOrDefault(o => o.Id == selectedOptionId.Value)
                    : null;
                var correctOpt = visibleOptions.FirstOrDefault(o => o.IsCorrect);
                var isCorrect = selectedOpt is not null && selectedOpt.IsCorrect;
                if (isCorrect) correctCount++;

                persistedAnswers.Add(new LearnerPracticeAnswer
                {
                    PracticeExerciseAttemptId = attempt.Id,
                    PracticeQuestionId = question.Id,
                    SelectedOptionId = selectedOptionId,
                    IsCorrect = isCorrect,
                });

                questionResults.Add(new PracticeQuestionReviewResponse
                {
                    QuestionId = question.Id,
                    SortOrder = question.SortOrder,
                    QuestionText = question.QuestionText,
                    SelectedOptionId = selectedOptionId,
                    SelectedOptionText = selectedOpt?.OptionText,
                    CorrectOptionId = correctOpt?.Id,
                    CorrectOptionText = correctOpt?.OptionText,
                    IsCorrect = isCorrect,
                    Explanation = question.Explanation,
                });
            }

            var scorePercent = Math.Round(correctCount * 100d / questions.Count, 2);
            attempt.ScorePercent = scorePercent;
            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.IsCompleted = true;
            unitOfWork.PracticeExerciseAttempts.Update(attempt);
            await unitOfWork.LearnerPracticeAnswers.AddRangeAsync(persistedAnswers, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var passingScore = exercise.PassingScorePercent > 0 ? exercise.PassingScorePercent : MinimumPassingScorePercent;
            var passed = scorePercent >= passingScore;
            var unlockedNextDay = false;
            if (passed && !assignmentId.HasValue && !fromAssessmentRoadmap && !isCheckpoint && exercise.LessonId is int completedLessonId)
            {
                var latestPath = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
                var activeDay = latestPath.Result?.DailySchedules.FirstOrDefault(day =>
                    day.IsStarted && !day.IsCompleted && day.LessonIds.Contains(completedLessonId));
                if (activeDay is not null)
                {
                    var completion = await learningPathService.CompleteActiveDayAsync(
                        activeDay.Day, cancellationToken);
                    unlockedNextDay = completion.IsSuccess;
                }
            }

            var progression = fromAssessmentRoadmap || isCheckpoint
                ? await BuildProgressionAsync(userId, attempt.Id, exercise, passingScore, passed, cancellationToken)
                : null;

            var weakAreas = passed ? [] : new List<string> { exercise.Title };
            var nextRecommendation = passed
                ? (exercise.Scope == PracticeExerciseScopes.StageAssessment
                    ? "Xuất sắc! Bạn đã hoàn thành đánh giá cuối chặng và cập nhật hồ sơ năng lực."
                    : exercise.Scope == PracticeExerciseScopes.GroupQuiz
                        ? "Bạn đã nắm vững kiến thức chặng này và sẵn sàng sang chặng tiếp theo."
                        : "Bạn đã vượt qua Quick Check và cập nhật tiến độ học tập.")
                : "Hãy xem kỹ phần giải thích từng câu bên dưới, ôn lại kiến thức trọng tâm và thử làm lại.";

            return ApiResponse<PracticeExerciseAttemptResultResponse>.Success(new PracticeExerciseAttemptResultResponse
            {
                AttemptId = attempt.Id,
                PracticeExerciseId = exercise.Id,
                ExerciseTitle = exercise.Title,
                Scope = exercise.Scope,
                LessonId = exercise.LessonId ?? 0,
                ScorePercent = scorePercent,
                CorrectCount = correctCount,
                TotalQuestions = questions.Count,
                MinimumPassingScorePercent = passingScore,
                Passed = passed,
                UnlocksNextLearningPathDay = unlockedNextDay,
                Progression = progression,
                QuestionResults = questionResults,
                WeakAreas = weakAreas,
                NextRecommendation = nextRecommendation,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return ApiResponse<PracticeExerciseAttemptResultResponse>.Fail(
                "Không thể nộp bài Practice Exercise.", HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Reads the roadmap back (same service the Stage Detail page uses) after the attempt is saved and reports the
    /// transition this submission caused. A milestone is only reported on the first passing attempt, so retakes,
    /// duplicate requests (rejected earlier as already submitted) and refreshes never produce a second celebration.
    /// Never fails the submission: progression problems return null and the page simply refetches the roadmap.
    /// </summary>
    private async Task<RoadmapProgressionResponse?> BuildProgressionAsync(
        int userId, int attemptId, PracticeExercise exercise, double passingScore, bool passed, CancellationToken cancellationToken)
    {
        try
        {
            var wasAlreadyPassed = await unitOfWork.PracticeExerciseAttempts.Query().AsNoTracking()
                .AnyAsync(a => a.UserId == userId && a.PracticeExerciseId == exercise.Id && a.Id != attemptId
                    && !a.IsDeleted && a.HomeworkAssignmentId == null && a.IsCompleted
                    && a.ScorePercent >= passingScore, cancellationToken);

            var roadmapResult = await (standardRoadmapService ?? new StandardRoadmapService(unitOfWork, claimService))
                .GetAssessmentRoadmapAsync(cancellationToken);
            if (!roadmapResult.IsSuccess || roadmapResult.Result is not { } roadmap) return null;

            var stages = roadmap.Stages.OrderBy(s => s.SortOrder).ToList();
            var stage = exercise.Scope switch
            {
                PracticeExerciseScopes.LessonQuickCheck => stages.FirstOrDefault(s => s.Lessons.Any(l => l.LessonId == exercise.LessonId)),
                PracticeExerciseScopes.GroupQuiz => stages.FirstOrDefault(s => s.GroupQuiz?.ExerciseId == exercise.Id),
                PracticeExerciseScopes.StageAssessment => stages.FirstOrDefault(s => s.StageAssessment?.ExerciseId == exercise.Id),
                _ => null,
            };
            if (stage is null) return null;

            // A stage is done only when every required check on it is passed (group quiz and, on the last stage of a
            // level, the level assessment) — the same rule the server uses to open the next stage.
            static bool StageDone(StandardRoadmapStageResponse s) => s.Status is "Completed" or "Mastered"
                && s.GroupQuiz is not { IsPassed: false } && s.StageAssessment is not { IsPassed: false };
            var stageIndex = stages.IndexOf(stage);
            var nextStage = stageIndex >= 0 && stageIndex + 1 < stages.Count ? stages[stageIndex + 1] : null;
            var lesson = exercise.LessonId is int lid ? stage.Lessons.FirstOrDefault(l => l.LessonId == lid) : null;
            var nextLesson = stage.Lessons
                .Where(l => !l.IsCompleted && !l.IsLocked)
                .OrderBy(l => l.SortOrder)
                .FirstOrDefault();
            var checkpoint = stage.GroupQuiz is { IsPassed: false } gq ? gq
                : stage.StageAssessment is { IsPassed: false } sa ? sa
                : null;

            var progression = new RoadmapProgressionResponse
            {
                WasAlreadyPassed = wasAlreadyPassed,
                LessonId = lesson?.LessonId,
                LessonTitle = lesson?.Title,
                StageId = stage.StageId,
                StageTitle = stage.Title,
                StageNumber = stage.SortOrder,
                StageCompletedLessons = stage.CompletedLessons,
                StageTotalLessons = stage.TotalLessons,
                StageStatus = stage.Status,
                NextLessonId = nextLesson?.LessonId,
                NextLessonTitle = nextLesson?.Title,
                CheckpointExerciseId = checkpoint?.ExerciseId,
                CheckpointTitle = checkpoint?.Title,
                CheckpointScope = checkpoint?.Scope,
                CheckpointUnlocked = checkpoint?.IsUnlocked ?? false,
                RoadmapCompletedLessons = roadmap.CompletedLessons,
                RoadmapTotalLessons = roadmap.TotalLessons,
                RoadmapCompletedStages = stages.Count(StageDone),
                RoadmapTotalStages = stages.Count,
                TargetLevelCode = roadmap.TargetJlptLevelName,
            };

            if (!passed)
            {
                // A lower retake never re-locks or "un-completes" a step: the best passing attempt still counts.
                progression.Milestone = wasAlreadyPassed ? "NONE" : "NOT_PASSED";
                return progression;
            }
            if (wasAlreadyPassed)
            {
                progression.Milestone = "NONE";
                return progression;
            }

            if (exercise.Scope == PracticeExerciseScopes.LessonQuickCheck)
            {
                var stageLessonsDone = stage.Lessons.Count > 0 && stage.Lessons.All(l => l.IsCompleted);
                progression.Milestone = stageLessonsDone && checkpoint is { IsUnlocked: true }
                    ? "STAGE_CHECKPOINT_UNLOCKED"
                    : stageLessonsDone && StageDone(stage)
                        ? (stages.All(StageDone) ? "ROADMAP_COMPLETED" : "STAGE_COMPLETED")
                        : "LESSON_COMPLETED";
                if (lesson is not null && roadmap.CompletedLessons == 1)
                    progression.MomentOccurrenceKey = $"moment:first-lesson:{userId}";
            }
            else if (StageDone(stage))
            {
                progression.Milestone = stages.All(StageDone) ? "ROADMAP_COMPLETED" : "STAGE_COMPLETED";
                progression.MomentOccurrenceKey = exercise.Scope == PracticeExerciseScopes.StageAssessment
                    ? $"moment:stage-assessment:{userId}:{exercise.Id}"
                    : $"moment:group-quiz:{userId}:{exercise.Id}";
            }
            else if (checkpoint is { IsUnlocked: true } && checkpoint.ExerciseId != exercise.Id)
            {
                // Group quiz passed and the level assessment on this stage is now the next required check.
                progression.Milestone = "STAGE_CHECKPOINT_UNLOCKED";
                progression.MomentOccurrenceKey = $"moment:group-quiz:{userId}:{exercise.Id}";
            }
            else
            {
                // Checkpoint passed but the stage still has open requirements (e.g. an unfinished lesson).
                progression.Milestone = "NONE";
            }

            if (progression.Milestone is "STAGE_COMPLETED" or "ROADMAP_COMPLETED" && nextStage is not null)
            {
                var nextOpen = nextStage.Lessons.Any(l => !l.IsLocked) || nextStage.GroupQuiz is { IsUnlocked: true };
                if (nextOpen)
                {
                    progression.NextStageId = nextStage.StageId;
                    progression.NextStageTitle = nextStage.Title;
                    progression.NextStageNumber = nextStage.SortOrder;
                    var firstOpen = nextStage.Lessons.Where(l => !l.IsCompleted && !l.IsLocked).OrderBy(l => l.SortOrder).FirstOrDefault();
                    progression.NextLessonId = firstOpen?.LessonId;
                    progression.NextLessonTitle = firstOpen?.Title;
                }
            }
            return progression;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    private Task<ApiResponse> CheckAssessmentRoadmapLessonAsync(int lessonId, CancellationToken cancellationToken)
        => (standardRoadmapService ?? new StandardRoadmapService(unitOfWork, claimService))
            .CheckAssessmentLessonAsync(lessonId, cancellationToken);

    private async Task<ApiResponse<T>> WithLearnerLockAsync<T>(
        Func<CancellationToken, Task<ApiResponse<T>>> operation, CancellationToken cancellationToken)
    {
        var response = ApiResponse<T>.NotFound("Không tìm thấy người học.");
        var userId = claimService.GetUserClaim().Id;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token) is { IsDeleted: false })
                response = await operation(token);
        }, cancellationToken);
        return response;
    }

    private static List<PracticeQuestion> GetVisibleQuestions(PracticeExercise exercise)
        => (exercise.Questions ?? []).Where(question => !question.IsDeleted)
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .ToList();

    private static bool CanTakeExercise(List<PracticeQuestion> questions, bool requireSingleCorrectOption)
        => questions.Count > 0 && questions.All(question =>
            (question.Options ?? []).Count(option => !option.IsDeleted) >= 2 &&
            (!requireSingleCorrectOption ||
             (question.Options ?? []).Count(option => !option.IsDeleted && option.IsCorrect) == 1));
}
