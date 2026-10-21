using RikiPath.Domain.Entities;

namespace RikiPath.Application.IRepositories;

public interface IMentorChatMessageRepository : IGenericRepository<MentorChatMessage>
{
    Task<MentorChatMessage> GetWithSenderAsync(int messageId, CancellationToken cancellationToken);
    Task<List<MentorChatMessage>> GetPageAsync(int bookingId, int? beforeMessageId, int pageSize, CancellationToken cancellationToken);
    Task<MentorChatMessage?> GetLatestAsync(int bookingId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(int bookingId, int readerUserId, CancellationToken cancellationToken);
    Task<int> MarkIncomingAsReadAsync(int bookingId, int readerUserId, DateTime readAt, CancellationToken cancellationToken);
}
