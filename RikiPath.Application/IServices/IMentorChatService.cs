using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorChatService
{
    Task<ApiResponse<List<MentorChatThreadResponse>>> GetThreadsAsync(CancellationToken cancellationToken);
    Task<ApiResponse<List<MentorChatMessageResponse>>> GetMessagesAsync(int bookingId, int? beforeMessageId, int pageSize, CancellationToken cancellationToken);
    Task<ApiResponse<MentorChatThreadResponse>> GetThreadAsync(int bookingId, CancellationToken cancellationToken);
    Task<ApiResponse<MentorChatMessageResponse>> SendAsync(int bookingId, string? text, CancellationToken cancellationToken);
    Task<ApiResponse<int>> MarkReadAsync(int bookingId, CancellationToken cancellationToken);
}
