using Microsoft.AspNetCore.Http;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;

namespace RikiPath.Application.IServices
{
    public interface IUserProfileService
    {
        Task<ApiResponse<UserProfileResponse>> GetProfileAsync(int userId, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateProfileAsync(
            int userId, UpdateProfileRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateAvatarAsync(
            int userId, IFormFile avatarFile, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> SetJlptGoalAsync(
            int userId, SetJlptGoalRequest request, CancellationToken cancellationToken);

        Task<ApiResponse<UserProfileResponse>> UpdateNotificationSettingsAsync(
            int userId, UpdateNotificationSettingsRequest request, CancellationToken cancellationToken);
    }
}
