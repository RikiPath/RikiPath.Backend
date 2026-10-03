using RikiPath.Application.Requests.KanjiWriting;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.KanjiWriting;

namespace RikiPath.Application.IServices
{
    public interface IKanjiWritingPracticeService
    {
        /// <summary>Danh sách chữ Kanji cần luyện viết hôm nay: ưu tiên chữ đã đến hạn ôn lại
        /// (NextReviewDate &lt;= hôm nay), rồi mới bổ sung chữ mới nếu chưa đủ count.</summary>
        Task<ApiResponse<List<KanjiWritingQueueItem>>> GetDueForPracticeAsync(int count, CancellationToken cancellationToken);
        Task<ApiResponse<List<KanjiWritingQueueItem>>> GetAllForPracticeAsync(int count, CancellationToken cancellationToken);

        /// <summary>Ghi nhận kết quả 1 lượt luyện viết, tính lại lịch ôn theo SM-2.</summary>
        Task<ApiResponse<SubmitKanjiWritingResultResponse>> SubmitResultAsync(
            SubmitKanjiWritingResultRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<List<KanjiWritingScoreResponse>>> GetScoresAsync(CancellationToken cancellationToken);
    }
}