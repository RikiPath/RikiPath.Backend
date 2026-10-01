using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMentorBookingRepository : IGenericRepository<MentorBooking>
    {
        Task<List<MentorBooking>> GetQueueForMentorAsync(int mentorId);
        Task<List<MentorBooking>> GetPendingAssignmentAsync();
        Task<List<MentorBooking>> GetExpiredPaymentHoldsAsync(DateTime now, CancellationToken cancellationToken = default);
        Task<List<MentorBooking>> GetByLearnerAsync(int learnerId, CancellationToken cancellationToken = default);
        Task<List<MentorBooking>> GetPaidBookingsForMentorAsync(int mentorId, CancellationToken cancellationToken = default);
    }
}
