using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IRepositories;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;

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

        public async Task<Note?> GetByRoomAndAuthorAsync(Guid roomId, int authorUserId, CancellationToken ct = default)
        {
            return await _context.Notes
                .FirstOrDefaultAsync(n => n.MentorBooking.MentorAvailability.RoomId == roomId && n.UserId == authorUserId, ct);
        }

        public async Task<IReadOnlyList<MeetingNoteResponse>> GetByRoomAsync(Guid roomId, CancellationToken ct = default)
        {
            return await _context.Notes
                .Where(n => n.MentorBooking.MentorAvailability.RoomId == roomId)
                .OrderBy(n => n.AuthorRole == "Mentor" ? 0 : 1)
                .ThenBy(n => n.UserId)
                .Select(n => new MeetingNoteResponse
                {
                    UserId = n.UserId,
                    Name = n.UserAccount != null ? n.UserAccount.LastName + ", " + n.UserAccount.FirstName : string.Empty,
                    Role = n.AuthorRole,
                    Content = n.Content,
                })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<MeetingNoteResponse>> GetByBookingAsync(int mentorBookingId, CancellationToken ct = default)
        {
            return await _context.Notes
                .Where(n => n.MentorBookingId == mentorBookingId)
                .OrderBy(n => n.AuthorRole == "Mentor" ? 0 : 1)
                .ThenBy(n => n.UserId)
                .Select(n => new MeetingNoteResponse
                {
                    UserId = n.UserId,
                    Name = n.UserAccount != null ? n.UserAccount.LastName + ", " + n.UserAccount.FirstName : string.Empty,
                    Role = n.AuthorRole,
                    Content = n.Content
                })
                .ToListAsync(ct);
        }
    }
}
