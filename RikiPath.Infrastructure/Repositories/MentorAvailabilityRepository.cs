using RikiPath.Application.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Repositories
{
    public class MentorAvailabilityRepository : GenericRepository<MentorAvailability>, IMentorAvailabilityRepository
    {
        public MentorAvailabilityRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<List<MentorAvailability>> GetByMentorAsync(int mentorId)
            => await _context.MentorAvailabilities
                .Where(x => x.MentorId == mentorId)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

        public async Task<List<MentorAvailability>> GetAvailableSlotsAsync(int? mentorId, DateTime from, DateTime to)
        {
            IQueryable<MentorAvailability> query = _context.MentorAvailabilities
                .Where(x => !x.IsBooked && x.StartTime >= from && x.EndTime <= to);

            if (mentorId.HasValue)
                query = query.Where(x => x.MentorId == mentorId.Value);

            return await query.Include(x => x.Mentor).OrderBy(x => x.StartTime).ToListAsync();
        }
    }
}
