using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    public interface IAdminMentorService
    {
        Task<ApiResponse<MentorResponse>> CreateMentorAsync(
            CreateMentorRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<MentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken);

        Task<ApiResponse<MentorResponse>> SetMentorActiveAsync(
            int mentorId, bool isActive, CancellationToken cancellationToken);

        Task<ApiResponse<List<AdminMentorProfileResponse>>> GetMentorProfilesAsync(
            MentorVerificationStatus? status, CancellationToken cancellationToken);

        Task<ApiResponse<AdminMentorProfileResponse>> GetMentorProfileByIdAsync(
            int mentorId, CancellationToken cancellationToken);

        Task<ApiResponse<AdminMentorProfileResponse>> ReviewVerificationAsync(
            int mentorId, ReviewMentorVerificationRequest request, CancellationToken cancellationToken);
    }
}
