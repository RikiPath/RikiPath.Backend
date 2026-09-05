using RikiPath.Application.Requests.PracticeTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.PracticeTests;

namespace RikiPath.Application.IServices
{
    // NOTE: file này THAY THẾ IPracticeTestService cũ (thêm 2 method GetTestsByLevelAsync +
    // GetDetailedResultAsync ở cuối, 3 method đầu giữ nguyên y hệt bản trước).
    public interface IPracticeTestService
    {
        Task<ApiResponse<StartAttemptResponse>> StartAttemptAsync(
            int userId, int practiceTestId, CancellationToken cancellationToken);

        Task<ApiResponse<AttemptResultResponse>> SubmitAttemptAsync(
            int userId, int attemptId, SubmitAttemptRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<AttemptResultResponse>> GetAttemptResultAsync(
            int userId, int attemptId, CancellationToken cancellationToken);

        /// <summary>Danh sách đề thi thử theo cấp độ JLPT, cho màn hình chọn đề.</summary>
        Task<ApiResponse<List<TestSummaryResponse>>> GetTestsByLevelAsync(
            int jlptLevelId, CancellationToken cancellationToken);

        /// <summary>
        /// Kết quả chi tiết từng câu: đáp án đã chọn, đáp án đúng, và lời giải — chỉ xem được
        /// sau khi attempt đã Completed.
        /// </summary>
        Task<ApiResponse<DetailedAttemptResultResponse>> GetDetailedResultAsync(
            int userId, int attemptId, CancellationToken cancellationToken);
    }
}
