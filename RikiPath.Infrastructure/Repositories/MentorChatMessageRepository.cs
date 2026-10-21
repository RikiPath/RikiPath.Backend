using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public sealed class MentorChatMessageRepository(AppDbContext context)
    : GenericRepository<MentorChatMessage>(context), IMentorChatMessageRepository
{
    public Task<List<MentorChatMessage>> GetPageAsync(
        int bookingId, int? beforeMessageId, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.Set<MentorChatMessage>()
            .AsNoTracking()
            .Include(x => x.Sender)
            .Where(x => x.MentorBookingId == bookingId);
        if (beforeMessageId is > 0)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        return query.OrderByDescending(x => x.Id).Take(pageSize).ToListAsync(cancellationToken);
    }

    public Task<MentorChatMessage?> GetLatestAsync(int bookingId, CancellationToken cancellationToken)
        => _context.Set<MentorChatMessage>().AsNoTracking()
            .Include(x => x.Sender)
            .Where(x => x.MentorBookingId == bookingId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(int bookingId, int readerUserId, CancellationToken cancellationToken)
        => _context.Set<MentorChatMessage>().CountAsync(
            x => x.MentorBookingId == bookingId && x.SenderUserId != readerUserId && x.ReadAt == null,
            cancellationToken);

    public Task<int> MarkIncomingAsReadAsync(
        int bookingId, int readerUserId, DateTime readAt, CancellationToken cancellationToken)
        => _context.Set<MentorChatMessage>()
            .Where(x => x.MentorBookingId == bookingId && x.SenderUserId != readerUserId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, readAt), cancellationToken);
}
