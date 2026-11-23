using RikiPath.Domain.Entities;
using Microsoft.AspNetCore.Http;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Profile;
using System.Net;
using RikiPath.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace RikiPath.Application.Services
{
    // NOTE: cần thêm các cột sau vào UserAccount nếu chưa có:
    //   AvatarUrl (string?), DailyStudyMinutes (int, default 0),
    //   EmailNotificationsEnabled (bool, default true), SystemNotificationsEnabled (bool, default true).
    // TargetJlptLevelId/TargetJlptLevel đã tồn tại (dùng trong LearningPathService).
    public class UserProfileService(IUnitOfWork unitOfWork, IFileStorageService fileStorage, IClaimService claimService) : IUserProfileService
    {
        private static readonly string[] AllowedAvatarContentTypes = { "image/jpeg", "image/png", "image/webp" };
        private static readonly string[] AllowedAvatarExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024; // 5MB

        public Func<DateTime> UtcNowProvider { get; init; } = static () => DateTime.UtcNow;

        public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
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
                var normalizedName = string.Join(' ', (request.FullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (normalizedName.Length > 200)
                    return ApiResponse<UserProfileResponse>.Fail("Họ và tên tối đa 200 ký tự.");

                var (firstName, lastName) = UserNameHelper.SplitFullName(normalizedName);
                if (string.IsNullOrWhiteSpace(firstName))
                    return ApiResponse<UserProfileResponse>.Fail("Họ và tên không được để trống.");

                string? cleanedPhone = null;
                if (request.UpdatePhoneNumber)
                {
                    var rawPhone = request.PhoneNumber?.Trim();
                    if (!string.IsNullOrEmpty(rawPhone))
                    {
                        var compact = rawPhone.Replace(" ", string.Empty).Replace("-", string.Empty);
                        if (!System.Text.RegularExpressions.Regex.IsMatch(compact, @"^(\+?[1-9]\d{7,14}|0\d{8,10})$"))
                            return ApiResponse<UserProfileResponse>.Fail("Số điện thoại không đúng định dạng (ví dụ: 0912345678 hoặc +84912345678).");
                        cleanedPhone = compact;
                    }
                }

                UserProfileResponse? response = null;
                ApiResponse<UserProfileResponse>? rejection = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, ct);
                    if (user is null || user.IsDeleted)
                    {
                        rejection = ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");
                        return;
                    }

                    user.FirstName = firstName;
                    user.LastName = lastName;
                    if (request.UpdatePhoneNumber)
                        user.PhoneNumber = cleanedPhone;
                    if (request.StudyTimePreference is not null)
                    {
                        var pref = request.StudyTimePreference.Trim();
                        user.StudyTimePreference = string.IsNullOrEmpty(pref) ? null : pref;
                    }
                    user.ModifiedDate = UtcNowProvider();
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(ct);

                    var full = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, ct);
                    response = MapToResponse(full ?? user);
                }, cancellationToken);

                return rejection ?? ApiResponse<UserProfileResponse>.Success(response!);
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật thông tin cá nhân.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> UpdateAvatarAsync(IFormFile avatarFile, CancellationToken cancellationToken)
        {
            UploadedFileResult? uploaded = null;
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (avatarFile is null || avatarFile.Length == 0)
                    return ApiResponse<UserProfileResponse>.Fail("Vui lòng chọn file ảnh.");

                if (avatarFile.Length > MaxAvatarSizeBytes)
                    return ApiResponse<UserProfileResponse>.Fail("Ảnh đại diện tối đa 5MB.");

                var contentType = avatarFile.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
                if (!AllowedAvatarContentTypes.Contains(contentType))
                    return ApiResponse<UserProfileResponse>.Fail("Chỉ chấp nhận file JPEG, PNG hoặc WEBP.");

                var ext = Path.GetExtension(avatarFile.FileName)?.ToLowerInvariant() ?? string.Empty;
                if (!AllowedAvatarExtensions.Contains(ext))
                    return ApiResponse<UserProfileResponse>.Fail("Phần mở rộng file không hợp lệ (chỉ chấp nhận .jpg, .jpeg, .png, .webp).");

                await using var buffer = new MemoryStream();
                await using (var incoming = avatarFile.OpenReadStream())
                {
                    var chunk = new byte[81920];
                    int read;
                    while ((read = await incoming.ReadAsync(chunk, cancellationToken)) > 0)
                    {
                        if (buffer.Length + read > MaxAvatarSizeBytes)
                            return ApiResponse<UserProfileResponse>.Fail("Ảnh đại diện tối đa 5MB.");
                        await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
                    }
                }

                if (!MatchesImageSignature(buffer.GetBuffer(), buffer.Length, contentType, ext))
                    return ApiResponse<UserProfileResponse>.Fail("Nội dung file ảnh không khớp định dạng JPEG, PNG hoặc WEBP.");

                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                var previousAvatarUrl = user.AvatarUrl;
                buffer.Position = 0;
                var safeFileName = $"avatar-{userId}{NormalizeAvatarExt(contentType)}";
                uploaded = await fileStorage.UploadAsync(buffer, safeFileName, contentType, "avatars", cancellationToken);

                user.AvatarUrl = uploaded.Url;
                user.ModifiedDate = UtcNowProvider();
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                await TryDeleteAvatarObjectAsync(previousAvatarUrl, uploaded.Url, cancellationToken);
                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                if (uploaded is not null)
                {
                    try { await fileStorage.DeleteAsync(uploaded.StoredFileName, "avatars", cancellationToken); }
                    catch { /* best-effort rollback */ }
                }
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật ảnh đại diện.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> DeleteAvatarAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                var previousAvatarUrl = user.AvatarUrl;
                user.AvatarUrl = null;
                user.ModifiedDate = UtcNowProvider();
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                await TryDeleteAvatarObjectAsync(previousAvatarUrl, null, cancellationToken);
                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể xóa ảnh đại diện.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> SetJlptGoalAsync(SetJlptGoalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (request.DailyStudyMinutes < 0 || request.DailyStudyMinutes > 480)
                    return ApiResponse<UserProfileResponse>.Fail("Thời gian học/ngày không hợp lệ.");

                var hasRange = request.DailyStudyMinutesMin.HasValue || request.DailyStudyMinutesMax.HasValue;
                if (hasRange && (!request.DailyStudyMinutesMin.HasValue || !request.DailyStudyMinutesMax.HasValue ||
                    request.DailyStudyMinutesMin < 15 || request.DailyStudyMinutesMax > 480 ||
                    request.DailyStudyMinutesMin > request.DailyStudyMinutesMax))
                    return ApiResponse<UserProfileResponse>.Fail("Thời gian học phải từ 15 đến 480 phút, tối thiểu không lớn hơn tối đa.");

                if (!hasRange && request.DailyStudyMinutes < 15)
                    return ApiResponse<UserProfileResponse>.Fail("Thời gian học phải từ 15 đến 480 phút/ngày.");
                // Accept the legacy client that repeats the minimum in DailyStudyMinutes.
                if (hasRange && request.DailyStudyMinutes != 0 && request.DailyStudyMinutes != request.DailyStudyMinutesMin)
                    return ApiResponse<UserProfileResponse>.Fail("Chọn số phút cố định hoặc khoảng thời gian; không gửi hai giá trị khác nhau.");
                if (request.TargetCertificateLevelId.HasValue && request.TargetJlptLevelId != 0 &&
                    request.TargetCertificateLevelId.Value != request.TargetJlptLevelId)
                    return ApiResponse<UserProfileResponse>.Fail("Hai trường cấp độ mục tiêu không khớp nhau.");
                var targetLevelId = request.TargetCertificateLevelId ?? request.TargetJlptLevelId;
                if (targetLevelId <= 0)
                    return ApiResponse<UserProfileResponse>.Fail("Vui lòng chọn cấp độ mục tiêu.");

                var utcNow = BusinessDateHelper.EnsureUtc(UtcNowProvider());
                if (request.TargetExamDate.HasValue
                    && !BusinessDateHelper.IsFutureExamDate(request.TargetExamDate.Value, utcNow))
                    return ApiResponse<UserProfileResponse>.Fail("Ngày dự định thi phải sau hôm nay.");
                if (request.TargetExamDate is { } requestedDate && !BusinessDateHelper.IsJlptExamMonth(requestedDate))
                    return ApiResponse<UserProfileResponse>.Fail(BusinessDateHelper.InvalidJlptExamMonthMessage,
                        HttpStatusCode.BadRequest, errorCode: BusinessDateHelper.InvalidJlptExamMonthCode);
                if (request.TargetExamDate is { } jlptDate && !BusinessDateHelper.IsJlptExamDate(jlptDate))
                    return ApiResponse<UserProfileResponse>.Fail(
                        "Ngày thi JLPT phải là Chủ nhật đầu tiên của tháng 7 hoặc tháng 12.",
                        HttpStatusCode.BadRequest, "JLPT_EXAM_DATE_INVALID");

                // Đã chuyển đổi sang CertificateLevels
                if (request.ClearTargetExamDate && request.TargetExamDate.HasValue)
                    return ApiResponse<UserProfileResponse>.Fail("Không thể vừa đặt vừa xóa ngày thi.");

                var certificationLevel = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(targetLevelId, cancellationToken);
                if (certificationLevel is null)
                    return ApiResponse<UserProfileResponse>.NotFound(
                        $"Không tìm thấy cấp độ Id = {targetLevelId}.");

                if (request.CertificateTypeId.HasValue && request.CertificateTypeId.Value != certificationLevel.CertificateTypeId)
                    return ApiResponse<UserProfileResponse>.Fail("Cấp độ mục tiêu không thuộc loại chứng chỉ đã chọn.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                var targetCode = certificationLevel.Code.Trim().ToUpperInvariant();
                if (request.DeclaredJlptCertificateCode is { } declared)
                {
                    var code = declared.Trim().ToUpperInvariant();
                    if (!PlacementAssessmentModePolicy.IsValidDeclaration(targetCode, code))
                        return ApiResponse<UserProfileResponse>.Fail(
                            "Với mục tiêu N4, N3 hoặc N2, hãy chọn chứng chỉ đã có thấp hơn mục tiêu hoặc chọn NONE.",
                            HttpStatusCode.BadRequest, errorCode: "INVALID_HELD_JLPT_CERTIFICATE");
                    user.DeclaredJlptCertificateCode = code;
                }

                // Save the next learning goal independently; replacing an active path still uses preview/confirm.
                user.TargetCertificateLevelId = certificationLevel.Id;
                user.DailyStudyMinutesMin = request.DailyStudyMinutesMin;
                user.DailyStudyMinutesMax = request.DailyStudyMinutesMax;
                // Dùng mức tối thiểu để ước tính kế hoạch bền vững, không giả định ngày nào cũng học tối đa.
                user.DailyStudyMinutes = request.DailyStudyMinutesMin ?? request.DailyStudyMinutes;
                user.LearningGoalVersion++;
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
                if (level is null || !JlptCurriculum.IsSupportedStudyLevel(level.Code))
                    return ApiResponse<UserProfileResponse>.Fail(
                        "Cấp độ học không khả dụng; lộ trình tiêu chuẩn hiện hỗ trợ N5–N2.",
                        HttpStatusCode.BadRequest, errorCode: "STUDY_LEVEL_UNSUPPORTED");

                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.StartingCertificateLevelId = level.Id;
                user.StartingCertificateLevel = level;
                user.LearningMode = LearningMode.Standard;
                user.LearningGoalVersion++;
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

        public async Task<ApiResponse<UserProfileResponse>> SetRoadmapModeAsync(
            SetRoadmapModeRequest request, CancellationToken cancellationToken)
        {
            var normalized = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
            LearningMode? targetMode = normalized switch
            {
                "STANDARD" => LearningMode.Standard,
                "AI" or "AI_PERSONALIZED" or "AIPERSONALIZED" => LearningMode.AiPersonalized,
                _ => null,
            };
            if (!targetMode.HasValue)
                return ApiResponse<UserProfileResponse>.Fail(
                    "Chế độ lộ trình không hợp lệ. Chỉ hỗ trợ STANDARD hoặc AI_PERSONALIZED.",
                    HttpStatusCode.BadRequest, errorCode: "ROADMAP_MODE_INVALID");

            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                if (user.LearningMode != targetMode.Value)
                {
                    user.LearningMode = targetMode.Value;
                    user.LearningGoalVersion++;
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể chuyển chế độ lộ trình.", HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<UserProfileResponse>> SetLearningGoalAsync(
            SetLearningGoalRequest request, CancellationToken cancellationToken)
        {
            if (request.DailyStudyMinutes < 1 || request.DailyStudyMinutes > 1440)
                return ApiResponse<UserProfileResponse>.Fail("Thời gian học mỗi ngày phải từ 1 đến 1.440 phút.");
            if (request.HasJlptCertificate != request.CertifiedJlptLevelId.HasValue)
                return ApiResponse<UserProfileResponse>.Fail("Vui lòng chọn cấp chứng chỉ JLPT đã đạt hoặc bỏ chọn chứng chỉ.");
            if (request.ExpectedVersion < 0)
                return ApiResponse<UserProfileResponse>.Fail("Phiên bản mục tiêu học tập không hợp lệ.");

            var utcNow = BusinessDateHelper.EnsureUtc(UtcNowProvider());
            if (request.TargetExamDate.HasValue &&
                !BusinessDateHelper.IsFutureExamDate(request.TargetExamDate.Value, utcNow))
                return ApiResponse<UserProfileResponse>.Fail("Ngày dự định thi phải sau hôm nay.");

            try
            {
                var current = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(request.CurrentStudyLevelId, cancellationToken);
                var target = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(request.TargetJlptLevelId, cancellationToken);
                CertificateLevel? certified = null;
                if (request.CertifiedJlptLevelId.HasValue)
                    certified = await unitOfWork.CertificateLevels.GetActiveJlptLevelByIdAsync(request.CertifiedJlptLevelId.Value, cancellationToken);

                if (current is null || !JlptCurriculum.IsSupportedStudyLevel(current.Code))
                    return ApiResponse<UserProfileResponse>.Fail("Cấp độ đang học không khả dụng; hiện hỗ trợ N5–N2.");
                if (target is null || !IsSupportedGoalLevel(target.Code))
                    return ApiResponse<UserProfileResponse>.Fail("Mục tiêu JLPT phải thuộc N5–N1.");
                if (request.HasJlptCertificate && (certified is null || !IsSupportedGoalLevel(certified.Code)))
                    return ApiResponse<UserProfileResponse>.Fail("Cấp chứng chỉ JLPT phải thuộc N5–N1.");

                var userId = claimService.GetUserClaim().Id;
                UserProfileResponse? result = null;
                ApiResponse<UserProfileResponse>? rejection = null;
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, ct);
                    if (user is null)
                    {
                        rejection = ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");
                        return;
                    }
                    if (user.LearningGoalVersion != request.ExpectedVersion)
                    {
                        rejection = ApiResponse<UserProfileResponse>.Fail(
                            "Mục tiêu đã được thay đổi ở nơi khác. Vui lòng tải lại hồ sơ.",
                            HttpStatusCode.Conflict, errorCode: "GOAL_VERSION_CONFLICT");
                        return;
                    }

                    var active = await unitOfWork.RecommendedLearningPaths.GetActiveByUserIdAsync(userId, ct);
                    if (active is not null && active.TargetCertificateLevelId != target.Id)
                    {
                        rejection = ApiResponse<UserProfileResponse>.Fail(
                            "Mục tiêu của lộ trình đang hoạt động chỉ có thể đổi qua xem trước và xác nhận.",
                            HttpStatusCode.Conflict, errorCode: "PATH_PREVIEW_REQUIRED");
                        return;
                    }

                    user.StartingCertificateLevelId = current.Id;
                    user.TargetCertificateLevelId = target.Id;
                    user.DailyStudyMinutes = request.DailyStudyMinutes;
                    user.TargetExamDate = request.TargetExamDate.HasValue
                        ? BusinessDateHelper.NormalizeExamDate(request.TargetExamDate.Value)
                        : null;
                    user.SelfDeclaredCertificateLevelId = certified?.Id;
                    user.LearningGoalVersion++;
                    unitOfWork.UserAccounts.Update(user);
                    await unitOfWork.SaveChangesAsync(ct);
                    result = MapToResponse(user, target);
                    result.CurrentStudyLevelName = current.Code;
                    result.StartingJlptLevelName = current.Code;
                    result.CertifiedJlptLevelName = certified?.Code;
                }, cancellationToken);

                return rejection ?? ApiResponse<UserProfileResponse>.Success(result!);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Mục tiêu đã được thay đổi ở nơi khác. Vui lòng tải lại hồ sơ.",
                    HttpStatusCode.Conflict, errorCode: "GOAL_VERSION_CONFLICT");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật mục tiêu học tập.", HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        private static bool IsSupportedGoalLevel(string? code) => JlptCurriculum.Rank(code) is >= 1 and <= 5;

        public async Task<ApiResponse<UserProfileResponse>> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var user = await unitOfWork.UserAccounts.GetWithLearningGoalAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<UserProfileResponse>.NotFound("Không tìm thấy người dùng.");

                user.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
                user.SystemNotificationsEnabled = request.SystemNotificationsEnabled;
                if (request.NotificationCategories is not null)
                {
                    user.NotificationPreferencesJson = NotificationPreferenceHelper.SerializeMerged(
                        user.Role, user.NotificationPreferencesJson, request.NotificationCategories);
                }
                user.ModifiedDate = UtcNowProvider();
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<UserProfileResponse>.Success(MapToResponse(user));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserProfileResponse>.Fail(
                    "Không thể cập nhật cài đặt thông báo.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private static UserProfileResponse MapToResponse(UserAccount user, CertificateLevel? level = null)
        {
            var hasPassword = user.PasswordHash is { Length: 64 };
            var googleLinked = !string.IsNullOrWhiteSpace(user.GoogleSubject);
            var providers = new List<string>(2);
            if (hasPassword) providers.Add("Password");
            if (googleLinked) providers.Add("Google");

            return new UserProfileResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                PendingEmail = user.PendingEmail,
                FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role.ToString(),
                IsEmailVerified = user.IsEmailVerified,
                IsPhoneVerified = user.IsPhoneVerified,
                HasPassword = hasPassword,
                GoogleLinked = googleLinked,
                AuthProviders = providers,
                StudyTimePreference = user.StudyTimePreference,
                CurrentStreak = user.CurrentStreak,
                LongestStreak = user.LongestStreak,
                TargetJlptLevelId = user.TargetCertificateLevelId,
                TargetJlptLevelName = level?.Code ?? user.TargetCertificateLevel?.Code,
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
                DeclaredJlptCertificateCode = user.DeclaredJlptCertificateCode,
                DailyStudyMinutes = user.DailyStudyMinutes,
                DailyStudyMinutesMin = user.DailyStudyMinutesMin ?? user.DailyStudyMinutes,
                DailyStudyMinutesMax = user.DailyStudyMinutesMax ?? user.DailyStudyMinutes,
                TargetCertificateTypeId = level?.CertificateTypeId ?? user.TargetCertificateLevel?.CertificateTypeId,
                LearningGoalVersion = user.LearningGoalVersion,
                HasJlptCertificate = user.SelfDeclaredCertificateLevelId.HasValue,
                CertifiedJlptLevelId = user.SelfDeclaredCertificateLevelId,
                CertifiedJlptLevelName = user.SelfDeclaredCertificateLevel?.Code,
                TargetExamDate = user.TargetExamDate,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled,
                SystemNotificationsEnabled = user.SystemNotificationsEnabled,
                NotificationCategories = NotificationPreferenceHelper.ParseForRole(user.Role, user.NotificationPreferencesJson),
            };
        }

        private async Task TryDeleteAvatarObjectAsync(string? previousUrl, string? newUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(previousUrl) || string.Equals(previousUrl, newUrl, StringComparison.Ordinal))
                return;
            if (!Uri.TryCreate(previousUrl, UriKind.Absolute, out var uri))
                return;
            const string marker = "/avatars/";
            var idx = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return;
            var fileName = Uri.UnescapeDataString(uri.AbsolutePath[(idx + marker.Length)..]);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains(".."))
                return;
            try
            {
                await fileStorage.DeleteAsync(fileName, "avatars", cancellationToken);
            }
            catch
            {
                // Keep profile update successful even if old storage cleanup fails.
            }
        }

        private static string NormalizeAvatarExt(string contentType) => contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg",
        };

        private static bool MatchesImageSignature(byte[] bytes, long length, string contentType, string ext) => (contentType, ext) switch
        {
            ("image/jpeg", ".jpg" or ".jpeg") => length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ("image/png", ".png") => length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
                && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ("image/webp", ".webp") => length >= 12
                && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => false,
        };

        private static List<string>? BuildDebugErrors(Exception ex)
        {
#if DEBUG
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
            return errors;
#else
            _ = ex;
            return null;
#endif
        }
    }
}
