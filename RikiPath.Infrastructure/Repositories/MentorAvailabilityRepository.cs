using RikiPath.Application.IRepositories;
using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class MentorAvailabilityRepository(AppDbContext context) : GenericRepository<MentorAvailability>(context), IMentorAvailabilityRepository
    {
        public async Task<List<MentorAvailability>> GetByMentorAsync(int mentorId)
            => await _context.MentorAvailabilities
                .Where(x => x.MentorId == mentorId)
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

        public async Task<List<MentorAvailability>> GetAvailableSlotsAsync(int? mentorId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            IQueryable<MentorAvailability> query = _context.MentorAvailabilities
                .Where(x => x.IsApproved && x.RejectedAt == null
                    && x.MaxLearners == 1 && x.BookedCount < x.MaxLearners && !x.IsDeleted
                    && x.StartTime >= from && x.EndTime <= to
                    && x.StartTime > DateTime.UtcNow && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor
                    && (x.PolicyVersion != null ? (x.Mentor.MentorProfile != null && x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved) : (x.Mentor.MentorProfile == null || x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved)));

            if (mentorId.HasValue)
                query = query.Where(x => x.MentorId == mentorId.Value);

            return await query.Include(x => x.Mentor).OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
        }

        public Task<MentorAvailability?> GetAvailableSlotByIdAsync(int id, DateTime after, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.Include(x => x.Mentor)
                .ThenInclude(m => m!.MentorProfile)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted
                    && x.IsApproved && x.RejectedAt == null && x.MaxLearners == 1 && x.BookedCount < x.MaxLearners
                    && x.StartTime > after
                    && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor
                    && (x.PolicyVersion != null ? (x.Mentor.MentorProfile != null && x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved) : (x.Mentor.MentorProfile == null || x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved)), cancellationToken);

        // Lịch bị từ chối không còn chiếm khung giờ của mentor.
        public Task<bool> HasOverlappingSlotAsync(int mentorId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.AnyAsync(x => x.MentorId == mentorId && !x.IsDeleted && x.RejectedAt == null
                && x.StartTime < endTime && startTime < x.EndTime, cancellationToken);

        // Phòng họp theo RoomId của lịch (hub dùng để kiểm tra quyền vào phòng).
        public Task<MentorAvailability?> GetMeetingByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .AsNoTracking()
                .Include(x => x.Mentor)
                .Include(x => x.MentorBookings.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.UserSubscription)
                        .ThenInclude(s => s.UserAccount)
                .FirstOrDefaultAsync(x => x.RoomId == roomId && !x.IsDeleted
                    && x.IsApproved && x.RejectedAt == null, cancellationToken);

        // // Đặt chỗ NGUYÊN TỬ: tăng BookedCount bằng một câu UPDATE có điều kiện rồi thêm booking trong cùng transaction.
        // Hai learner giành chỗ cuối cùng cùng lúc thì chỉ một người thành công. Trả false nếu lịch hết chỗ / chưa duyệt / đã bắt đầu.
        // Đặt trùng (cùng subscription, cùng lịch) bị unique index chặn: ném DbUpdateException và rollback.
        public async Task<bool> TryAddBookingAsync(MentorBooking booking, CancellationToken cancellationToken = default)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

                var taken = await _context.MentorAvailabilities
                    .Where(x => x.Id == booking.MentorAvailabilityId && !x.IsDeleted
                        && x.IsApproved && x.RejectedAt == null
                        && x.StartTime > DateTime.UtcNow
                        && x.BookedCount < x.MaxLearners)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, x => x.BookedCount + 1), cancellationToken);
                if (taken == 0) return false;

                _context.MentorBookings.Add(booking);
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return true;
            });
        }

        // Trả lại 1 chỗ khi learner huỷ. Gọi cùng lúc với việc đổi Status của booking sang Cancelled.
        public Task<int> ReleaseSeatAsync(int availabilityId, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .Where(x => x.Id == availabilityId && x.BookedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.BookedCount, x => x.BookedCount - 1), cancellationToken);

        public async Task<MentorAvailability?> GetActiveSlotByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
            => await _context.MentorAvailabilities
                .Include(x => x.Mentor)
                .Include(x => x.MentorBookings!.Where(b =>
                    !b.IsDeleted
                    && b.UserSubscription.PaymentStatus == PaymentStatus.Paid
                    && (b.Status == MentorStatus.Assigned || b.Status == MentorStatus.Accepted)))
                    .ThenInclude(b => b.UserSubscription).ThenInclude(s => s.UserAccount)
                .FirstOrDefaultAsync(x => x.RoomId == roomId, cancellationToken);

        public Task<List<MentorAvailability>> GetSlotsByGenerationKeysAsync(int mentorId, IEnumerable<string> generationKeys, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .Where(x => x.MentorId == mentorId && x.GenerationKey != null && generationKeys.Contains(x.GenerationKey))
                .ToListAsync(cancellationToken);

        public Task<List<MentorAvailability>> GetSlotsInRangeAsync(int mentorId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .Include(x => x.MentorBookings)
                .Where(x => x.MentorId == mentorId && !x.IsDeleted && x.StartTime < toUtc && fromUtc < x.EndTime)
                .OrderBy(x => x.StartTime)
                .ToListAsync(cancellationToken);

        public Task<MentorAvailability?> GetByGenerationKeyAsync(string generationKey, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .Include(x => x.MentorBookings)
                .FirstOrDefaultAsync(x => x.GenerationKey == generationKey, cancellationToken);

        public async Task<List<MentorAvailability>> GetPublicBookableSlotsAsync(
            int mentorId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTime.UtcNow;
            var minStartUtc = nowUtc.AddHours(2);

            var query = _context.MentorAvailabilities
                .Include(x => x.Mentor)
                    .ThenInclude(m => m!.MentorProfile)
                .Where(x => x.MentorId == mentorId
                    && !x.IsDeleted
                    && x.IsApproved
                    && x.RejectedAt == null
                    && x.MaxLearners == 1
                    && x.BookedCount == 0
                    && x.StartTime >= minStartUtc
                    && x.StartTime >= fromUtc
                    && x.EndTime <= toUtc
                    && x.Mentor != null
                    && !x.Mentor.IsDeleted
                    && x.Mentor.Role == Role.Mentor
                    && x.Mentor.MentorProfile != null
                    && x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved
                    && !_context.MentorBookingReservations.Any(r =>
                        r.MentorAvailabilityId == x.Id
                        && r.ReleasedAtUtc == null
                        && r.EndUtc > nowUtc));

            var slots = await query.OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
            return slots.Where(x => (x.EndTime - x.StartTime) == TimeSpan.FromMinutes(90)).ToList();
        }

        public async Task<MentorAvailability?> GetPublicBookableSlotByIdAsync(int slotId, CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTime.UtcNow;
            var minStartUtc = nowUtc.AddHours(2);

            var slot = await _context.MentorAvailabilities
                .Include(x => x.Mentor)
                    .ThenInclude(m => m!.MentorProfile)
                .FirstOrDefaultAsync(x => x.Id == slotId
                    && !x.IsDeleted
                    && x.IsApproved
                    && x.RejectedAt == null
                    && x.MaxLearners == 1
                    && x.BookedCount == 0
                    && x.StartTime >= minStartUtc
                    && x.Mentor != null
                    && !x.Mentor.IsDeleted
                    && x.Mentor.Role == Role.Mentor
                    && x.Mentor.MentorProfile != null
                    && x.Mentor.MentorProfile.VerificationStatus == MentorVerificationStatus.Approved
                    && !_context.MentorBookingReservations.Any(r =>
                        r.MentorAvailabilityId == x.Id
                        && r.ReleasedAtUtc == null
                        && r.EndUtc > nowUtc), cancellationToken);

            if (slot != null && (slot.EndTime - slot.StartTime) != TimeSpan.FromMinutes(90))
                return null;

            return slot;
        }
    }
}
