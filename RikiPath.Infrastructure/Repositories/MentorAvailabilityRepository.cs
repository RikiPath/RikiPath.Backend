using RikiPath.Application.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

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
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.StartTime)
                .ToListAsync();

        public async Task<List<MentorAvailability>> GetAvailableSlotsAsync(int? mentorId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            IQueryable<MentorAvailability> query = _context.MentorAvailabilities
                .Where(x => !x.IsBooked && !x.IsDeleted && x.StartTime >= from && x.EndTime <= to
                    && x.EndTime > DateTime.UtcNow && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor);

            if (mentorId.HasValue)
                query = query.Where(x => x.MentorId == mentorId.Value);

            return await query.Include(x => x.Mentor).OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
        }

        public Task<MentorAvailability?> GetAvailableSlotByIdAsync(int id, DateTime after, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.Include(x => x.Mentor)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && !x.IsBooked && x.StartTime > after
                    && x.Mentor != null && !x.Mentor.IsDeleted && x.Mentor.Role == Role.Mentor, cancellationToken);

        public Task<bool> HasOverlappingSlotAsync(int mentorId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
            => _context.MentorAvailabilities.AnyAsync(x => x.MentorId == mentorId && !x.IsDeleted
                && x.StartTime < endTime && startTime < x.EndTime, cancellationToken);
    }
}
