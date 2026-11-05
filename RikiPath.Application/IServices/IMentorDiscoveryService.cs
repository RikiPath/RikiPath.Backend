using RikiPath.Application.Common;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorDiscovery;
using RikiPath.Application.Responses.MentorProfile;

namespace RikiPath.Application.IServices;

public interface IMentorDiscoveryService
{
    Task<ApiResponse<PagedResult<PublicMentorCardResponse>>> SearchMentorsAsync(
        MentorDiscoveryFilterRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PublicMentorProfileResponse>> GetMentorProfileAsync(
        int mentorId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PublicMentorSlotsGroupedResponse>> GetPublicSlotsAsync(
        int mentorId, MentorSlotBrowsingRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<List<DateOnly>>> GetAvailableDatesAsync(
        int mentorId, MentorSlotBrowsingRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PublicMentorSlotResponse>> GetPublicSlotByIdAsync(
        int slotId, string? timeZoneId = null, CancellationToken cancellationToken = default);
}
