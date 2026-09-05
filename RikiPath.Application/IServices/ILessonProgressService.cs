using RikiPath.Application.Requests.Lessons;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Lessons;

namespace RikiPath.Application.IServices
{
    public interface ILessonProgressService
    {
        Task<ApiResponse<LessonDetailResponse>> GetLessonDetailAsync(
            int userId, int lessonId, CancellationToken cancellationToken);

        /// <summary>
        /// Lưu vị trí phát video hiện tại (resume), tự tính lại % tiến độ, và tự động đánh dấu
        /// hoàn thành khi progress &gt;= 95% (ngưỡng tránh trường hợp video kết thúc lệch vài giây).
        /// </summary>
        Task<ApiResponse<LessonDetailResponse>> UpdatePlaybackPositionAsync(
            int userId, int lessonId, UpdatePlaybackPositionRequest request, CancellationToken cancellationToken);
    }
}
