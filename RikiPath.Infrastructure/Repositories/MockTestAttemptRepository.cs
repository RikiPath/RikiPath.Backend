using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RikiPath.Infrastructure.Repositories
{
    public class MockTestAttemptRepository : GenericRepository<MockTestAttempt>, IMockTestAttemptRepository
    {
        public MockTestAttemptRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MockTestAttempt>> GetByUserAsync(int userId)
            => await _context.MockTestAttempts
                .Where(x => x.UserId == userId && !x.IsDeleted && x.AttemptKind == AttemptKind.MockTest)
                .OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

        public async Task<List<MockTestAttempt>> GetPlacementByUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => await _context.MockTestAttempts
                .Where(x => x.UserId == userId && !x.IsDeleted && x.AttemptKind == AttemptKind.Placement)
                .OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);

        public async Task<MockTestAttempt?> GetActivePlacementByUserAsync(
            int userId,
            DateTime serverNowUtc,
            CancellationToken cancellationToken = default)
            => await _context.MockTestAttempts
                .Where(x => x.UserId == userId &&
                            !x.IsDeleted &&
                            x.AttemptKind == AttemptKind.Placement &&
                            !x.IsCompleted &&
                            x.ExpiresAt.HasValue &&
                            x.ExpiresAt.Value > serverNowUtc)
                .OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<MockTestAttempt?> GetLatestCompletedPlacementByUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
            => await _context.MockTestAttempts
                .Where(x => x.UserId == userId &&
                            !x.IsDeleted &&
                            x.AttemptKind == AttemptKind.Placement &&
                            x.IsCompleted &&
                            x.TotalScore.HasValue)
                .OrderByDescending(x => x.SubmittedAt ?? x.StartedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<MockTestAttempt?> GetPlacementByIdAsync(
            int attemptId,
            CancellationToken cancellationToken = default)
            => await _context.MockTestAttempts
                .FirstOrDefaultAsync(
                    x => x.Id == attemptId && !x.IsDeleted && x.AttemptKind == AttemptKind.Placement,
                    cancellationToken);

        public async Task<MockTestAttempt?> GetPlacementForSubmissionAsync(
            int attemptId,
            CancellationToken cancellationToken = default)
        {
            MockTestAttempt? attempt;
            try
            {
                attempt = await _context.MockTestAttempts
                    .FromSqlInterpolated($"SELECT * FROM \"MockTestAttempts\" WHERE \"Id\" = {attemptId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (attempt is not null)
                {
                    await _context.Entry(attempt).ReloadAsync(cancellationToken);
                }
            }
            catch (InvalidOperationException)
            {
                attempt = await _context.MockTestAttempts
                    .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken);
            }

            if (attempt is null || attempt.AttemptKind != AttemptKind.Placement)
            {
                return null;
            }

            return attempt;
        }

        public async Task<MockTestAttempt?> GetWithBreakdownAsync(int attemptId)
            => await _context.MockTestAttempts
                .Where(x => !x.IsDeleted)
                .Include(x => x.Answers).ThenInclude(a => a.MockQuestion)
                .Include(x => x.SectionResults).ThenInclude(r => r.MockTestSection)
                .FirstOrDefaultAsync(x => x.Id == attemptId);

        public async Task<MockTestAttempt?> GetForSubmissionAsync(
            int attemptId,
            CancellationToken cancellationToken = default)
        {
            MockTestAttempt? attempt;
            try
            {
                attempt = await _context.MockTestAttempts
                    // Khóa attempt trong transaction để hai request đồng thời không thể cùng chấm một bài.
                    .FromSqlInterpolated($"SELECT * FROM \"MockTestAttempts\" WHERE \"Id\" = {attemptId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                attempt = await _context.MockTestAttempts
                    .FirstOrDefaultAsync(x => x.Id == attemptId, cancellationToken);
            }

            if (attempt is null || attempt.IsDeleted || attempt.AttemptKind != AttemptKind.MockTest)
            {
                return null;
            }

            return attempt;
        }

        public async Task<List<DateTime>> GetActivityDatesAsync(int userId)
        {
            return await _context.MockTestAttempts
                .Where(a => a.UserId == userId &&
                            !a.IsDeleted &&
                            a.IsCompleted &&
                            (a.AttemptKind == AttemptKind.MockTest || a.AttemptKind == AttemptKind.Placement))
                .Select(a => (a.SubmittedAt ?? a.StartedAt).Date)
                .Distinct()
                .ToListAsync();
        }
    }
}
