using RikiPath.Domain.Entities;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;
using System.Net;
using RikiPath.Domain.Enums;

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

        public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

        public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
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

                var utcNow = BusinessDateHelper.EnsureUtc(UtcNowProvider());
                if (request.TargetExamDate.HasValue
                    && !BusinessDateHelper.IsFutureExamDate(request.TargetExamDate.Value, utcNow))
                    return ApiResponse<UserProfileResponse>.Fail("Ngày dự định thi phải sau hôm nay.");

                // Đã chuyển đổi sang CertificateLevels
                if (request.ClearTargetExamDate && request.TargetExamDate.HasValue)
                    return ApiResponse<UserProfileResponse>.Fail("Không thể vừa đặt vừa xóa ngày thi.");

                var certificationLevel = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(request.TargetJlptLevelId, cancellationToken);
                if (certificationLevel is null)
                    return ApiResponse<UserProfileResponse>.NotFound(
                        $"Không tìm thấy cấp độ Id = {request.TargetJlptLevelId}.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                var active = await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(userId, cancellationToken);
                if (active is not null && active.TargetCertificateLevelId != certificationLevel.Id)
                    return ApiResponse<UserProfileResponse>.Fail(
                        "Mục tiêu của lộ trình đang hoạt động chỉ có thể đổi qua xem trước và xác nhận.",
                        HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_REQUIRED");

                user.TargetCertificateLevelId = certificationLevel.Id;
                user.DailyStudyMinutes = request.DailyStudyMinutes;
                if (request.ClearTargetExamDate)
                    user.TargetExamDate = null;
                else if (request.TargetExamDate.HasValue)
                    user.TargetExamDate = BusinessDateHelper.NormalizeExamDate(request.TargetExamDate.Value);
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

        public async Task<ApiResponse<UserProfileResponse>> SetStudyLevelAsync(
            SetStudyLevelRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (!request.Confirmed)
                    return ApiResponse<UserProfileResponse>.Fail(
                        "Vui lòng xác nhận thay đổi cấp độ học.", HttpStatusCode.BadRequest,
                        errorCode: "STUDY_LEVEL_CONFIRMATION_REQUIRED");
                var userId = claimService.GetUserClaim().Id;
                var level = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(
                    request.CurrentStudyLevelId, cancellationToken);
                if (level is null || JlptCurriculum.Rank(level.Code) == 0)
                    return ApiResponse<UserProfileResponse>.Fail(
                        "Cấp độ học không khả dụng; lộ trình tiêu chuẩn hiện hỗ trợ N5–N3.",
                        HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_UNSUPPORTED");

                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.StartingCertificateLevelId = level.Id;
                user.StartingCertificateLevel = level;
                user.LearningMode = LearningMode.Standard;
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật cấp độ học.", HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
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

        private static UserProfileResponse MapToResponse(UserAccount user, CertificateLevel? level = null) => new()
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            TargetJlptLevelId = user.TargetCertificateLevelId,
            TargetJlptLevelName = level?.Code ?? user.TargetCertificateLevel?.Code,
            // Legacy profile: an old target remains a readable starting level until the learner confirms a new one.
            StartingJlptLevelId = user.StartingCertificateLevelId ?? user.TargetCertificateLevelId,
            StartingJlptLevelName = user.StartingCertificateLevel?.Code ?? level?.Code ?? user.TargetCertificateLevel?.Code,
            CurrentStudyLevelId = user.StartingCertificateLevelId ?? user.TargetCertificateLevelId,
            CurrentStudyLevelName = user.StartingCertificateLevel?.Code ?? level?.Code ?? user.TargetCertificateLevel?.Code,
            LearningMode = user.LearningMode == LearningMode.AiPersonalized ? "AI_PERSONALIZED" : "STANDARD",
            AssessedJlptLevelId = user.AssessedCertificateLevelId,
            AssessedJlptLevelName = user.AssessedCertificateLevel?.Code,
            PlacementDecision = user.PlacementDecision switch
            {
                PlacementDecision.Skipped => "SKIPPED",
                PlacementDecision.Completed => "COMPLETED",
                _ => "NOT_TAKEN",
            },
            DailyStudyMinutes = user.DailyStudyMinutes,
            TargetExamDate = user.TargetExamDate,
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
