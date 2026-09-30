using RikiPath.Application.IRepositories;
using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Infrastructure.Repositories
{
    public class NoteRepository : GenericRepository<Note>, INoteRepository
    {
        public NoteRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Note?> GetByRequestIdAsync(int consultationRequestId)
            => await _context.Notes
                .FirstOrDefaultAsync(x => x.MentorBookingId == consultationRequestId);
    }
}
