using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices
{
    public interface IMentorMeetingAccessService
    {
        Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken);
    }
}
