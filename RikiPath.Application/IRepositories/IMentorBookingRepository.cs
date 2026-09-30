using RikiPath.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMentorBookingRepository : IGenericRepository<MentorBooking>
    {
        Task<List<MentorBooking>> GetQueueForMentorAsync(int mentorId);
        Task<List<MentorBooking>> GetPendingAssignmentAsync();
    }
}
