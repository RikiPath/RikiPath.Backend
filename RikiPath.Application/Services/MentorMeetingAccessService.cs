using RikiPath.Application.IServices;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class MentorMeetingAccessService(IUnitOfWork unitOfWork) : IMentorMeetingAccessService
    {
        public async Task<MeetingAccessInfoResponse?> GetMeetingAsync(Guid roomId, CancellationToken cancellationToken)
        {
            // Method mới cần thêm vào IMentorBookingRepository/MentorBookingRepository (xem patch
            // kèm theo) - đây là nơi duy nhất còn gọi EF Core (.Include/.ThenInclude), đúng vị trí
            // của nó (Infrastructure), không phải trong Hub (WebApi).
            var booking = await unitOfWork.MentorBookings.GetActiveMeetingByRoomIdAsync(roomId, cancellationToken);
            if (booking is null)
                return null;

            var learnerAccount = booking.UserSubscription.UserAccount;
            var learner = new MeetingParticipantResponse
            {
                UserId = booking.UserSubscription.UserId,
                Name = learnerAccount is null
                     ? $"User #{booking.UserSubscription.UserId}"
                     : $"{learnerAccount.FirstName} {learnerAccount.LastName}".Trim(),
                IsMentor = false
            };

            // Giữ đúng guard cũ trong Hub: chỉ coi là Mentor hợp lệ nếu Role thực sự là Mentor
            // (phòng vệ trường hợp dữ liệu MentorAvailability bị sai/thiếu).
            var mentorAccount = booking.MentorAvailability?.Mentor;
            var mentorIsValid = mentorAccount?.Role == Role.Mentor;
            var mentor = new MeetingParticipantResponse
            {
                UserId = mentorIsValid ? booking.MentorAvailability!.MentorId : 0,
                Name = mentorIsValid
                     ? $"{mentorAccount!.FirstName} {mentorAccount.LastName}".Trim()
                     : "Mentor",
                IsMentor = true
            };

            return new MeetingAccessInfoResponse
            {
                Learner = learner,
                Mentor = mentor
            };
        }
    }
}