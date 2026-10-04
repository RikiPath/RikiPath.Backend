using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class MentorMeetingAccessService(IUnitOfWork unitOfWork) : IMentorMeetingAccessService
    {
        public async Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken)
        {
            var slot = await unitOfWork.MentorAvailabilities.GetActiveSlotByRoomIdAsync(roomId, cancellationToken);
            if (slot is null)
                return null;

            // Giữ đúng guard cũ: chỉ coi là Mentor hợp lệ nếu Role thực sự là Mentor
            // (phòng vệ trường hợp dữ liệu Mentor bị sai/thiếu).
            var mentorIsValid = slot.Mentor?.Role == Role.Mentor;
            var mentor = new MeetingParticipantResponse
            {
                UserId = mentorIsValid ? slot.MentorId : 0,
                Name = mentorIsValid
                    ? $"{slot.Mentor!.FirstName} {slot.Mentor.LastName}".Trim()
                    : "Mentor",
                IsMentor = true
            };

            // Mỗi booking hợp lệ (Paid + Assigned/Accepted) = 1 learner đã đặt chung slot này.
            // GroupBy phòng hờ trường hợp hiếm: cùng 1 learner lỡ có 2 booking cho cùng 1 slot.
            var learners = (slot.MentorBookings ?? [])
                .Select(b => ToLearner(b.UserSubscription))
                .GroupBy(l => l.UserId)
                .Select(g => g.First())
                .ToList();

            return new MeetingAccessInfoResponse
            {
                Mentor = mentor,
                Learners = learners
            };
        }

        private static MeetingParticipantResponse ToLearner(UserSubscription subscription)
        {
            var account = subscription.UserAccount;
            return new MeetingParticipantResponse
            {
                UserId = subscription.UserId,
                Name = account is null
                    ? $"User #{subscription.UserId}"
                    : $"{account.FirstName} {account.LastName}".Trim(),
                IsMentor = false
            };
        }
    }
}