using RikiPath.Domain.Entities;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần thêm các cột sau vào UserAccount nếu chưa có:
    //   AvatarUrl (string?), DailyStudyMinutes (int, default 0),
    //   EmailNotificationsEnabled (bool, default true), SystemNotificationsEnabled (bool, default true).
    // TargetJlptLevelId/TargetJlptLevel đã tồn tại (dùng trong LearningPathService).
    public class UserProfileService(IUnitOfWork unitOfWork, IFileStorageService fileStorage, IClaimService claimService) : IUserProfileService
    {
        private static readonly string[] AllowedAvatarContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5MB

        public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể tải thông tin cá nhân.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                    return ApiResponse<UserProfileResponse>.Fail("Họ và tên không được để trống.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật thông tin cá nhân.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> UpdateAvatarAsync(IFormFile avatarFile, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (avatarFile is null || avatarFile.Length == 0)
                    return ApiResponse<UserProfileResponse>.Fail("Vui lòng chọn file ảnh.");

                if (avatarFile.Length > MaxAvatarSizeBytes)
                    return ApiResponse<UserProfileResponse>.Fail("Ảnh đại diện tối đa 5MB.");

                if (!AllowedAvatarContentTypes.Contains(avatarFile.ContentType))
                    return ApiResponse<UserProfileResponse>.Fail("Chỉ chấp nhận file JPEG, PNG hoặc WEBP.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                await using var stream = avatarFile.OpenReadStream();
                var uploaded = await fileStorage.UploadAsync(
                    stream, avatarFile.FileName, avatarFile.ContentType, "avatars", cancellationToken);

                user.AvatarUrl = uploaded.Url;
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật ảnh đại diện.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> SetJlptGoalAsync(SetJlptGoalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request.DailyStudyMinutes < 0 || request.DailyStudyMinutes > 1440)
                    return ApiResponse<UserProfileResponse>.Fail("Thời gian học/ngày không hợp lệ.");

                // Đã chuyển đổi sang CertificationLevels
                var certificationLevel = await unitOfWork.CertificationLevels.GetByIdAsync(request.TargetJlptLevelId, cancellationToken);
                if (certificationLevel is null)
                    return ApiResponse<UserProfileResponse>.NotFound(
                        $"Không tìm thấy cấp độ Id = {request.TargetJlptLevelId}.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.TargetCertificationLevelId = certificationLevel.Id;
                user.DailyStudyMinutes = request.DailyStudyMinutes;
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user, certificationLevel));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật mục tiêu học tập.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
                user.SystemNotificationsEnabled = request.SystemNotificationsEnabled;
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật cài đặt thông báo.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static UserProfileResponse MapToResponse(UserAccount user, CertificationLevel? level = null) => new()
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            TargetJlptLevelId = user.TargetCertificationLevelId,
            TargetJlptLevelName = level?.Code ?? user.TargetCertificationLevel?.Code,
            DailyStudyMinutes = user.DailyStudyMinutes,
            EmailNotificationsEnabled = user.EmailNotificationsEnabled,
            SystemNotificationsEnabled = user.SystemNotificationsEnabled,
        };

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
