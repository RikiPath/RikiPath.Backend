using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices
{
    public interface IMeetingNoteService
    {
        /// <summary>Tạo mới hoặc cập nhật note của 1 tác giả trong 1 phòng (unique theo RoomId + AuthorUserId).</summary>
        Task<MeetingNoteResponse> UpsertAsync(
            Guid roomId,
            int mentorBookingId,
            int authorUserId,
            string authorName,
            string authorRole,
            string? content,
            CancellationToken ct);

        /// <summary>Toàn bộ note trong 1 phòng đang họp (dùng khi vào phòng / F5 qua Hub).</summary>
        Task<IReadOnlyList<MeetingNoteResponse>> GetByRoomAsync(Guid roomId, CancellationToken ct);

        /// <summary>Toàn bộ note của 1 booking (dùng để xem lại SAU buổi họp, qua REST API bình thường).</summary>
        Task<IReadOnlyList<MeetingNoteResponse>> GetByBookingAsync(int mentorBookingId, CancellationToken ct);
    }
}
