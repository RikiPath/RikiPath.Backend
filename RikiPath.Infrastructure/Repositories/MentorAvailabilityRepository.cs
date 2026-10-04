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
                    && x.BookedCount < x.MaxLearners && !x.IsDeleted
                    && x.StartTime >= from && x.EndTime <= to
                    && x.EndTime > DateTime.UtcNow && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor);

            if (mentorId.HasValue)
                query = query.Where(x => x.MentorId == mentorId.Value);

            return await query.Include(x => x.Mentor).OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
        }

        public Task<MentorAvailability?> GetAvailableSlotByIdAsync(int id, DateTime after, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.Include(x => x.Mentor)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted
                    && x.IsApproved && x.RejectedAt == null && x.BookedCount < x.MaxLearners
                    && x.StartTime > after
                    && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor, cancellationToken);

        // Lịch bị từ chối không còn chiếm khung giờ của mentor.
        public Task<bool> HasOverlappingSlotAsync(int mentorId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.AnyAsync(x => x.MentorId == mentorId && !x.IsDeleted && x.RejectedAt == null
                && x.StartTime < endTime && startTime < x.EndTime, cancellationToken);

        /// <summary>Phòng họp theo RoomId của lịch (hub dùng để kiểm tra quyền vào phòng).</summary>
        public Task<MentorAvailability?> GetMeetingByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities
                .AsNoTracking()
                .Include(x => x.Mentor)
                .Include(x => x.MentorBookings.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.UserSubscription)
                        .ThenInclude(s => s.UserAccount)
                .FirstOrDefaultAsync(x => x.RoomId == roomId && !x.IsDeleted
                    && x.IsApproved && x.RejectedAt == null, cancellationToken);

        /// <summary>
        /// Đặt chỗ NGUYÊN TỬ: tăng BookedCount bằng một câu UPDATE có điều kiện rồi thêm booking trong cùng transaction.
        /// Hai learner giành chỗ cuối cùng cùng lúc thì chỉ một người thành công. Trả false nếu lịch hết chỗ / chưa duyệt / đã bắt đầu.
        /// Đặt trùng (cùng subscription, cùng lịch) bị unique index chặn: ném DbUpdateException và rollback.
        /// </summary>
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

        /// <summary>Trả lại 1 chỗ khi learner huỷ. Gọi cùng lúc với việc đổi Status của booking sang Cancelled.</summary>
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
    }
}