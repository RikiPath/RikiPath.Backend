using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;

namespace RikiPath.Application.IServices
{
    public interface IAdminMentorService
    {
        Task<ApiResponse<MentorResponse>> CreateMentorAsync(
            CreateMentorRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<List<MentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken);

        Task<ApiResponse<MentorResponse>> SetMentorActiveAsync(
            int mentorId, bool isActive, CancellationToken cancellationToken);

        // ---- Consultation package management ----

    }
}
