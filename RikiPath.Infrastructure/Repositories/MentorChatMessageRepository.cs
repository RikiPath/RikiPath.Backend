using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

public sealed class MentorChatMessageRepository(AppDbContext context)
    : GenericRepository<MentorChatMessage>(context), IMentorChatMessageRepository
{
    private IQueryable<MentorChatMessage> Conversation(int bookingId)
    {
        var anchor = _context.MentorBookings.Where(b => b.Id == bookingId);
        return _context.Set<MentorChatMessage>().Where(m => !m.IsDeleted && anchor.Any(a =>
            m.MentorBooking.UserSubscription.UserId == a.UserSubscription.UserId
            && ((int?)m.MentorBooking.MentorAvailability!.MentorId ?? m.MentorBooking.UserSubscription.MentorId)
                == ((int?)a.MentorAvailability!.MentorId ?? a.UserSubscription.MentorId)));
    }
    public Task<MentorChatMessage> GetWithSenderAsync(int messageId, CancellationToken cancellationToken)
        => _context.Set<MentorChatMessage>().AsNoTracking().Include(x => x.Sender)
            .SingleAsync(x => x.Id == messageId, cancellationToken);

    public Task<List<MentorChatMessage>> GetPageAsync(
        int bookingId, int? beforeMessageId, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<MentorChatMessage> query = Conversation(bookingId)
            .AsNoTracking()
            .Include(x => x.Sender)
            ;
        if (beforeMessageId is > 0)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        return query.OrderByDescending(x => x.Id).Take(pageSize).ToListAsync(cancellationToken);
    }

    public Task<MentorChatMessage?> GetLatestAsync(int bookingId, CancellationToken cancellationToken)
        => Conversation(bookingId).AsNoTracking()
            .Include(x => x.Sender)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(int bookingId, int readerUserId, CancellationToken cancellationToken)
        => Conversation(bookingId).CountAsync(
            x => x.SenderUserId != readerUserId && x.ReadAt == null,
            cancellationToken);

    public Task<int> MarkIncomingAsReadAsync(
        int bookingId, int readerUserId, DateTime readAt, CancellationToken cancellationToken)
        => Conversation(bookingId)
            .Where(x => x.SenderUserId != readerUserId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, readAt), cancellationToken);
}
