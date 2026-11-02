using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories;

// Triển khai Repository quản lý truy vấn và ghi sổ cái tín dụng MentorCreditLedger bằng EF Core.
public sealed class MentorCreditLedgerRepository : IMentorCreditLedgerRepository
{
    private readonly AppDbContext _context;

    public MentorCreditLedgerRepository(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách các nguồn cấp tín dụng còn hạn và còn số dư theo thứ tự FIFO (ExpiresAtUtc tăng dần).
    public async Task<List<MentorCreditGrant>> GetActiveGrantsForLearnerAsync(
        int learnerId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditGrants
            .Where(x => x.LearnerId == learnerId
                     && x.AvailableQuantity > 0
                     && x.EffectiveFromUtc <= asOfUtc
                     && x.ExpiresAtUtc > asOfUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<MentorCreditGrant?> GetGrantByIdAsync(long grantId, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditGrants
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.Id == grantId, cancellationToken);
    }

    public async Task<MentorCreditGrant?> GetGrantBySubscriptionIdAsync(
        int subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditGrants
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.UserSubscriptionId == subscriptionId, cancellationToken);
    }

    public async Task<MentorCreditEntry?> GetEntryByOperationKeyAsync(
        string operationKey, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditEntries
            .FirstOrDefaultAsync(x => x.OperationKey == operationKey, cancellationToken);
    }

    public async Task<List<MentorCreditEntry>> GetEntriesByBookingIdAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditEntries
            .Where(x => x.MentorBookingId == bookingId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MentorCreditEntry>> GetEntriesByLearnerIdAsync(
        int learnerId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditEntries
            .Where(x => x.LearnerId == learnerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MentorCreditGrant>> GetAllGrantsForLearnerAsync(
        int learnerId, CancellationToken cancellationToken = default)
    {
        return await _context.MentorCreditGrants
            .Include(x => x.Entries)
            .Where(x => x.LearnerId == learnerId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddGrantAsync(MentorCreditGrant grant, CancellationToken cancellationToken = default)
    {
        await _context.MentorCreditGrants.AddAsync(grant, cancellationToken);
    }

    public void UpdateGrant(MentorCreditGrant grant)
    {
        _context.MentorCreditGrants.Update(grant);
    }

    public async Task AddEntryAsync(MentorCreditEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.MentorCreditEntries.AddAsync(entry, cancellationToken);
    }
}
