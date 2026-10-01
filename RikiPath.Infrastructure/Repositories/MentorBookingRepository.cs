using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Repositories
{
    public class MentorBookingRepository : GenericRepository<MentorBooking>, IMentorBookingRepository
    {
        public MentorBookingRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MentorBooking>> GetQueueForMentorAsync(int mentorId)
            => await _context.MentorBookings
                .Include(x => x.MentorAvailability)
                .Include(x => x.UserSubscription).ThenInclude(x => x.UserAccount)
                .Where(x => x.MentorAvailability != null && x.MentorAvailability.MentorId == mentorId)
                .OrderBy(x => x.ScheduledAt ?? x.CreatedDate)
                .ToListAsync();

        public async Task<List<MentorBooking>> GetPendingAssignmentAsync()
            => await _context.MentorBookings
                .Where(x => x.Status == ConsultationStatus.PendingAssignment)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();

        public Task<List<MentorBooking>> GetExpiredPaymentHoldsAsync(DateTime now, CancellationToken cancellationToken = default)
            => _context.MentorBookings
                .Include(x => x.UserSubscription)
                .Include(x => x.MentorAvailability)
                .Where(x => x.Status == ConsultationStatus.AwaitingPayment
                    && x.UserSubscription.PaymentStatus == PaymentStatus.Pending
                    && x.UserSubscription.PaymentExpiresAt <= now)
                .ToListAsync(cancellationToken);

        public Task<List<MentorBooking>> GetByLearnerAsync(int learnerId, CancellationToken cancellationToken = default)
            => _context.MentorBookings.AsNoTracking()
                .Include(x => x.UserSubscription)
                .Include(x => x.MentorAvailability).ThenInclude(x => x!.Mentor)
                .Where(x => x.UserSubscription.UserId == learnerId && !x.IsDeleted)
                .OrderBy(x => x.ScheduledAt)
                .ToListAsync(cancellationToken);

        public Task<List<MentorBooking>> GetPaidBookingsForMentorAsync(int mentorId, CancellationToken cancellationToken = default)
            => _context.MentorBookings.AsNoTracking()
                .Include(x => x.UserSubscription).ThenInclude(x => x.UserAccount)
                .Include(x => x.MentorAvailability)
                .Where(x => x.MentorAvailability != null && x.MentorAvailability.MentorId == mentorId
                    && !x.IsDeleted && x.UserSubscription.PaymentStatus == PaymentStatus.Paid
                    && x.Status != ConsultationStatus.Cancelled)
                .OrderBy(x => x.ScheduledAt)
                .ToListAsync(cancellationToken);
    }
}
