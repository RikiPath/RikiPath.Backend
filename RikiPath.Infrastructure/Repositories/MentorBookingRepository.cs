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
                .Where(x => x.MentorAvailability != null && x.MentorAvailability.MentorId == mentorId)
                .OrderBy(x => x.ScheduledAt ?? x.CreatedDate)
                .ToListAsync();

        public async Task<List<MentorBooking>> GetPendingAssignmentAsync()
            => await _context.MentorBookings
                .Where(x => x.Status == ConsultationStatus.PendingAssignment)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync();
    }
}
