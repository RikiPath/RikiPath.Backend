using System.Data.Common;
using System.Net;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>Deterministic, non-AI curriculum from owned profile and canonical lesson progress.</summary>
public sealed class StandardRoadmapService(IUnitOfWork unitOfWork, IClaimService claimService,
    ILogger<StandardRoadmapService>? logger = null)
    : IStandardRoadmapService
{
    public async Task<ApiResponse<StandardRoadmapResponse>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            if (user is null)
                return ApiResponse<StandardRoadmapResponse>.NotFound("Không tìm thấy người dùng.");

            var levelId = user.StartingCertificateLevelId ?? user.TargetCertificateLevelId;
            if (!levelId.HasValue)
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Hãy chọn cấp độ đang học trước khi xem lộ trình tiêu chuẩn.",
                    HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_REQUIRED");

            var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(
                levelId.Value, cancellationToken);
            if (level is null || !JlptCurriculum.IsSupportedStudyLevel(level.Code))
                return ApiResponse<StandardRoadmapResponse>.Fail(
                    "Cấp độ học không khả dụng; lộ trình hiện hỗ trợ N5–N3.",
                    HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_UNSUPPORTED");

            var lessons = await unitOfWork.Lessons.GetByCertificateLevelAsync(level.Id, cancellationToken);
            var progress = await unitOfWork.LessonProgresses.GetByUserAsync(userId, cancellationToken);
            var progressByLesson = progress
                .GroupBy(item => item.LessonId)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(item => item.IsCompleted)
                        .ThenByDescending(item => item.ProgressPercent).First());
            var rows = lessons.OrderBy(item => item.SortOrder).ThenBy(item => item.Id)
                .Select(lesson =>
                {
                    progressByLesson.TryGetValue(lesson.Id, out var item);
                    var completed = item?.IsCompleted == true || item?.ProgressPercent >= 100;
                    return new StandardRoadmapLessonResponse
                    {
                        LessonId = lesson.Id,
                        Title = lesson.Title,
                        SortOrder = lesson.SortOrder,
                        ProgressPercent = completed ? 100 : Math.Clamp(item?.ProgressPercent ?? 0, 0, 99),
                        IsCompleted = completed,
                    };
                }).ToList();

            return ApiResponse<StandardRoadmapResponse>.Success(new StandardRoadmapResponse
            {
                CurrentStudyLevelId = level.Id,
                CurrentStudyLevelName = level.Code,
                TargetJlptLevelId = user.TargetCertificateLevelId,
                TargetJlptLevelName = user.TargetCertificateLevel?.Code,
                LearningMode = user.LearningMode == LearningMode.AiPersonalized ? "AI_PERSONALIZED" : "STANDARD",
                TotalLessons = rows.Count,
                CompletedLessons = rows.Count(row => row.IsCompleted),
                NextLessonId = rows.FirstOrDefault(row => !row.IsCompleted)?.LessonId,
                Lessons = rows,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Không thể tải lộ trình tiêu chuẩn.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse<StandardRoadmapResponse>> GetAssessmentRoadmapAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = claimService.GetUserClaim().Id;
            var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
            if (user is null)
                return ApiResponse<StandardRoadmapResponse>.NotFound("Không tìm thấy người dùng.");

            var target = user.TargetCertificateLevelId is int targetId
                ? await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(targetId, cancellationToken)
                : null;
            if (target is null || JlptCurriculum.Rank(target.Code) == 0)
                return ApiResponse<StandardRoadmapResponse>.Fail("Hãy chọn mục tiêu JLPT từ N5 đến N2.",
                    HttpStatusCode.BadRequest, "STUDY_LEVEL_REQUIRED");

            var targetCode = target.Code.Trim().ToUpperInvariant();
            var comparisonCode = PlacementAssessmentModePolicy.ComparisonLevel(targetCode);
            var attempts = await unitOfWork.MockTestAttempts.GetPlacementByUserAsync(userId, cancellationToken);
            var (attempt, result) = PracticeTestService.FindLatestCompletedAssessment(attempts, target.Id, requireMatchingGoal: true);
            if (attempt is null || result is null)
                return ApiResponse<StandardRoadmapResponse>.Fail("Hãy hoàn thành bài đánh giá năng lực để xem lộ trình tự học.",
                    HttpStatusCode.Conflict, "STANDARD_ROADMAP_PLACEMENT_REQUIRED");

            var assessments = JlptSkillAssessmentPolicy.Assess(result, targetCode,
                PracticeTestService.TryGetSkillEvidenceFromSnapshot(attempt.QuestionSnapshotJson));

            var starting = user.StartingCertificateLevel;
            if (starting is null || starting.IsDeleted || starting.CertificateTypeId != target.CertificateTypeId ||
                !JlptCurriculum.IsValidSpan(starting, target))
            {
                starting = target;
                if (comparisonCode is not null)
                {
                    var levels = await unitOfWork.CertificateLevels.GetLearnerVisibleJlptLevelsAsync(cancellationToken);
                    starting = levels.FirstOrDefault(level => level.CertificateTypeId == target.CertificateTypeId &&
                        level.Code.Equals(comparisonCode, StringComparison.OrdinalIgnoreCase)) ?? target;
                }
            }

            var studyLevels = starting.Id == target.Id ? new[] { target } : new[] { target, starting };
            var skills = assessments.ToDictionary(assessment => assessment.SkillCode,
                assessment => new StandardRoadmapSkillResponse { Assessment = assessment });
            var selected = new List<(AssessmentRoadmapLesson Lesson, string SkillCode, string LevelCode)>();
            foreach (var level in studyLevels)
            {
                var levelCode = level.Code.Trim().ToUpperInvariant();
                var lessons = await unitOfWork.Lessons.GetAssessmentRoadmapLessonsAsync(level.Id, userId, cancellationToken);
                var levelSkills = new HashSet<string>();
                foreach (var lesson in lessons)
                {
                    var skillCode = LessonSkillCode(lesson);
                    if (!skills.ContainsKey(skillCode))
                    {
                        skills.Add(skillCode, new StandardRoadmapSkillResponse
                        {
                            Assessment = new LearningPathSectionAssessmentResponse
                            {
                                SkillCode = skillCode,
                                Skill = lesson.LanguageSkillName,
                                SectionTitle = lesson.LanguageSkillName,
                            }
                        });
                    }
                    levelSkills.Add(skillCode);
                    selected.Add((lesson, skillCode, levelCode));
                }

                foreach (var assessment in assessments)
                {
                    if (!levelSkills.Contains(assessment.SkillCode))
                        skills[assessment.SkillCode].UnavailableLevelCodes.Add(levelCode);
                }
            }

            var ids = selected.Select(item => item.Lesson.Id).ToArray();
            var withExercises = await LessonPracticeProgress.WithReadyExercisesAsync(unitOfWork, ids, cancellationToken);
            var passed = await LessonPracticeProgress.PassedAsync(
                unitOfWork, userId, ids, cancellationToken, requireReadyExercise: true);
            var rows = new List<StandardRoadmapLessonResponse>();
            var blockedLevels = new HashSet<string>();
            foreach (var (lesson, skillCode, levelCode) in selected)
            {
                var progress = lesson.Progress;
                var practicePassed = passed.Contains(lesson.Id);
                rows.Add(new StandardRoadmapLessonResponse
                {
                    LessonId = lesson.Id,
                    Title = lesson.Title,
                    SortOrder = rows.Count + 1,
                    SkillCode = skillCode,
                    LanguageSkillName = lesson.LanguageSkillName,
                    LevelCode = levelCode,
                    KanjiCount = lesson.KanjiCount,
                    VocabularyCount = lesson.VocabularyCount,
                    GrammarCount = lesson.GrammarCount,
                    HasPracticeExercise = withExercises.Contains(lesson.Id),
                    PracticePassed = practicePassed,
                    IsStudied = practicePassed || progress?.IsCompleted == true,
                    IsCompleted = practicePassed,
                    IsLocked = !practicePassed && blockedLevels.Contains(levelCode),
                    ProgressPercent = practicePassed ? 100 : Math.Clamp(progress?.ProgressPercent ?? 0, 0, 99),
                });
                if (!practicePassed) blockedLevels.Add(levelCode);
            }

            foreach (var skill in skills.Values)
                skill.Assessment.SuggestedStudySharePercent = rows.Count == 0 ? null :
                    Math.Round(rows.Count(row => row.SkillCode == skill.Assessment.SkillCode) * 100d / rows.Count, 1);

            var nextLesson = rows.FirstOrDefault(row => row.LevelCode == targetCode && !row.IsCompleted && !row.IsLocked);

            return ApiResponse<StandardRoadmapResponse>.Success(new StandardRoadmapResponse
            {
                CurrentStudyLevelId = starting.Id,
                CurrentStudyLevelName = starting.Code.Trim().ToUpperInvariant(),
                TargetJlptLevelId = target.Id,
                TargetJlptLevelName = targetCode,
                PlacementAttemptId = attempt.Id,
                ComparisonLevelCode = comparisonCode,
                TotalLessons = rows.Count,
                CompletedLessons = rows.Count(row => row.IsCompleted),
                NextLessonId = nextLesson?.LessonId,
                Skills = skills.Values.ToList(),
                Lessons = rows,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException exception) when (exception.SqlState is "42703" or "42P01")
        {
            logger?.LogError(exception, "The database schema is incomplete for the self-study roadmap.");
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Cơ sở dữ liệu cần được cập nhật trước khi tải lộ trình tự học.",
                HttpStatusCode.InternalServerError, "STUDY_SCHEMA_UPDATE_REQUIRED");
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to load the self-study roadmap from the assessment.");
            return ApiResponse<StandardRoadmapResponse>.Fail(
                "Không thể tải lộ trình tự học theo bài đánh giá.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ApiResponse> CheckAssessmentLessonAsync(int lessonId, CancellationToken cancellationToken)
    {
        var result = await GetAssessmentRoadmapAsync(cancellationToken);
        if (!result.IsSuccess)
            return ApiResponse.Fail(result.ErrorMessage ?? "Không thể tải lộ trình tự học.",
                (HttpStatusCode)result.StatusCode, result.ErrorCode);

        var lesson = result.Result!.Lessons.Find(row => row.LessonId == lessonId);
        if (lesson is null)
            return ApiResponse.NotFound("Bài này không thuộc lộ trình tự học hiện tại.");
        return lesson.IsLocked
            ? ApiResponse.Fail("Hãy đạt bài luyện tập của bài trước ở cấp này.", HttpStatusCode.Conflict, "SELF_STUDY_LESSON_LOCKED")
            : ApiResponse.Success();
    }

    private static string LessonSkillCode(AssessmentRoadmapLesson lesson)
    {
        var name = lesson.LanguageSkillName;
        if (name.Equals("Kanji", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Hán tự", StringComparison.OrdinalIgnoreCase))
            return "vocabulary";
        return JlptSkillAssessmentPolicy.Classify(name)?.ToString().ToLowerInvariant() ?? $"skill-{lesson.LanguageSkillId}";
    }
}
