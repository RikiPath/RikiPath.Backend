using Domain.Entities;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Lessons;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần thêm cột Lesson.AttachmentUrl (string?) và
    // LessonProgress.ResumePositionSeconds (int, default 0) nếu chưa có.
    public class LessonProgressService(IUnitOfWork unitOfWork) : ILessonProgressService
    {
        private const int AutoCompleteThresholdPercent = 95;

        public async Task<ApiResponse<LessonDetailResponse>> GetLessonDetailAsync(
            int userId, int lessonId, CancellationToken cancellationToken)
        {
            try
            {
                var lesson = await unitOfWork.Lessons.GetByIdAsync(lessonId);
                if (lesson is null)
                    return ApiResponse<LessonDetailResponse>.NotFound($"Không tìm thấy bài học Id = {lessonId}.");

                // NOTE: cần ILessonProgressRepository.GetByUserAndLessonAsync(userId, lessonId).
                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(userId, lessonId);

                return ApiResponse<LessonDetailResponse>.Success(MapToResponse(lesson, progress));
            }
            catch (Exception ex)
            {
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Không thể tải chi tiết bài học.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<LessonDetailResponse>> UpdatePlaybackPositionAsync(
            int userId, int lessonId, UpdatePlaybackPositionRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.DurationSeconds <= 0)
                    return ApiResponse<LessonDetailResponse>.Fail("Thời lượng video không hợp lệ.");

                if (request.PositionSeconds < 0)
                    return ApiResponse<LessonDetailResponse>.Fail("Vị trí phát không hợp lệ.");

                var lesson = await unitOfWork.Lessons.GetByIdAsync(lessonId);
                if (lesson is null)
                    return ApiResponse<LessonDetailResponse>.NotFound($"Không tìm thấy bài học Id = {lessonId}.");

                var progress = await unitOfWork.LessonProgresses.GetByUserAndLessonAsync(userId, lessonId);

                var clampedPosition = Math.Min(request.PositionSeconds, request.DurationSeconds);
                var percent = (int)Math.Round(clampedPosition * 100.0 / request.DurationSeconds);
                percent = Math.Clamp(percent, 0, 100);

                if (progress is null)
                {
                    progress = new LessonProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        IsCompleted = false,
                        ProgressPercent = percent,
                        LastPositionSeconds = clampedPosition,
                        ModifiedDate = null,
                    };
                    await unitOfWork.LessonProgresses.AddAsync(progress);
                }
                else
                {
                    // Không lùi % tiến độ nếu user tua lại video xem lại đoạn cũ.
                    progress.ProgressPercent = Math.Max(progress.ProgressPercent, percent);
                    progress.LastPositionSeconds = clampedPosition;
                    unitOfWork.LessonProgresses.Update(progress);
                }

                if (progress.ProgressPercent >= AutoCompleteThresholdPercent && progress.ModifiedDate is null)
                {
                    progress.IsCompleted = true;
                    progress.ModifiedDate = DateTime.UtcNow;
                }

                await unitOfWork.SaveChangesAsync();

                return ApiResponse<LessonDetailResponse>.Success(MapToResponse(lesson, progress));
            }
            catch (Exception ex)
            {
                return ApiResponse<LessonDetailResponse>.Fail(
                    "Không thể lưu tiến độ xem video.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static LessonDetailResponse MapToResponse(Lesson lesson, LessonProgress? progress) => new()
        {
            LessonId = lesson.Id,
            CourseId = lesson.CourseId,
            Title = lesson.Title,
            Content = lesson.Description,
            VideoUrl = lesson.VideoUrl,
            AttachmentUrl = null,
            OrderIndex = lesson.SortOrder,
            ProgressPercent = progress?.ProgressPercent ?? 0,
            ResumePositionSeconds = progress?.LastPositionSeconds ?? 0,
            IsCompleted = progress?.IsCompleted ?? false,
            CompletedAt = progress?.ModifiedDate,
        };

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
