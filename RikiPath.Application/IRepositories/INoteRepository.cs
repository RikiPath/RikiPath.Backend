using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using System.Threading.Tasks;

namespace RikiPath.Application.IRepositories
{
    public interface INoteRepository : IGenericRepository<Note>
    {
        Task<Note?> GetByRequestIdAsync(int consultationRequestId);
        Task<Note?> GetByRoomAndAuthorAsync(Guid roomId, int authorUserId, CancellationToken ct = default);
        Task<IReadOnlyList<MeetingNoteResponse>> GetByRoomAsync(Guid roomId, CancellationToken ct = default);
        Task<IReadOnlyList<MeetingNoteResponse>> GetByBookingAsync(int mentorBookingId, CancellationToken ct = default);
    }
}
