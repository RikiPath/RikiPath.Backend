using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.Application.IServices;

public interface IMentorLearnerSkillProfileService
{
    Task<ApiResponse<MentorLearnerSkillProfileResponse>> GetSkillProfileAsync(
        int learnerId, int? cohortId = null, CancellationToken cancellationToken = default);
}
