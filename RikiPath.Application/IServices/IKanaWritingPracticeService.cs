using RikiPath.Application.Requests.KanaWriting;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.KanaWriting;
using RikiPath.Domain.Enums;
namespace RikiPath.Application.IServices
{
    public interface IKanaWritingPracticeService
    {
        Task<ApiResponse<List<KanaWritingQueueItem>>> GetDueForPracticeAsync(int count, KanaCharacterType? type, CancellationToken cancellationToken);
        Task<ApiResponse<SubmitKanaWritingResultResponse>> SubmitResultAsync(SubmitKanaWritingResultRequest request, CancellationToken cancellationToken);
    }
}
