using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices
{
    public interface IMeetingNoteService
    {
        Task<MeetingNoteResponse> UpsertAsync(
            Guid roomId,
            int mentorBookingId,
            int authorUserId,
            string authorName,
            string authorRole,
            string? content,
            CancellationToken ct);

        Task<IReadOnlyList<MeetingNoteResponse>> GetByRoomAsync(Guid roomId, CancellationToken ct);

        Task<IReadOnlyList<MeetingNoteResponse>> GetByBookingAsync(int mentorBookingId, CancellationToken ct);
    }
}
