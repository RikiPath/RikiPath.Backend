using Microsoft.AspNetCore.Http;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;

namespace RikiPath.Application.IServices
{
    public interface IUserProfileService
    {
        Task<ApiResponse<UserProfileResponse>> GetProfileAsync(CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateAvatarAsync(IFormFile avatarFile, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> SetJlptGoalAsync(SetJlptGoalRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken);
    }
}
