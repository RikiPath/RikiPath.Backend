using System.Net;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

/// <summary>Deterministic, non-AI curriculum from owned profile and canonical lesson progress.</summary>
public sealed class StandardRoadmapService(IUnitOfWork unitOfWork, IClaimService claimService)
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
            if (level is null || JlptCurriculum.Rank(level.Code) == 0)
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
}
