using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices
{
    public interface IMentorMeetingAccessService
    {
        Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken);
        Task RecordAttendanceAsync(Guid roomId, int mentorId, int[] learnerIds, CancellationToken cancellationToken);
        void TrackChatParticipantJoined(Guid roomId);
        MeetingChatMessageResponse? AddChatMessage(Guid roomId, int senderUserId, string senderName, string role, string? text);
        IReadOnlyList<MeetingChatMessageResponse> GetChatHistory(Guid roomId);
    }
}
