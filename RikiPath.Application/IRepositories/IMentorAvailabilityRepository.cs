using RikiPath.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface IMentorAvailabilityRepository : IGenericRepository<MentorAvailability>
    {
        Task<List<MentorAvailability>> GetByMentorAsync(int mentorId);
        Task<List<MentorAvailability>> GetAvailableSlotsAsync(int? mentorId, DateTime from, DateTime to);
    }
}
