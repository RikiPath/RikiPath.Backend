using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMentorAvailabilityRepository : IGenericRepository<MentorAvailability>
    {
        Task<List<MentorAvailability>> GetByMentorAsync(int mentorId);
        Task<List<MentorAvailability>> GetAvailableSlotsAsync(int? mentorId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task<MentorAvailability?> GetAvailableSlotByIdAsync(int id, DateTime after, CancellationToken cancellationToken = default);
        Task<bool> HasOverlappingSlotAsync(int mentorId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
    }
}
