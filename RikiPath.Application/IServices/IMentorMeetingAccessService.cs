using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices
{
    public interface IMentorMeetingAccessService
    {
        Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken);
        void TrackChatParticipantJoined(Guid roomId);
        MeetingChatMessageResponse? AddChatMessage(Guid roomId, int senderUserId, string senderName, string role, string? text);
        IReadOnlyList<MeetingChatMessageResponse> GetChatHistory(Guid roomId);
    }
}
