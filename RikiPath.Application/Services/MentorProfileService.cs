using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    // // Dịch vụ quản lý hồ sơ chuyên môn và quy trình nộp hồ sơ xác minh của Chuyên gia.
    public class MentorProfileService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        ILogger<MentorProfileService> logger,
        IFileStorageService? fileStorageService = null) : IMentorProfileService
    {
        public async Task<ApiResponse<MentorProfileResponse>> GetMyProfileAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role is not (Role.Mentor or Role.Learner))
                    return ApiResponse<MentorProfileResponse>.Fail("Tài khoản không có quyền truy cập hồ sơ ứng tuyển cố vấn.", HttpStatusCode.Forbidden);

                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == claim.Id && !u.IsDeleted, cancellationToken);

                if (mentor is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Không tìm thấy thông tin tài khoản chuyên gia.", HttpStatusCode.NotFound);

                var profile = mentor.MentorProfile ?? await unitOfWork.MentorProfiles.GetByMentorIdAsync(mentor.Id, cancellationToken);
                if (profile is null)
                {
                    profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = DateTime.UtcNow
                    };
                    mentor.MentorProfile = profile;
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    mentor.MentorProfile = profile;
                }

                return ApiResponse<MentorProfileResponse>.Success(MapToMentorProfileResponse(mentor, profile));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi lấy thông tin hồ sơ chuyên gia.");
                return ApiResponse<MentorProfileResponse>.Fail("Không thể tải hồ sơ cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorProfileResponse>> UpdateMyProfileAsync(
            UpdateMentorProfileRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role is not (Role.Mentor or Role.Learner))
                    return ApiResponse<MentorProfileResponse>.Fail("Tài khoản không có quyền cập nhật hồ sơ ứng tuyển cố vấn.", HttpStatusCode.Forbidden);

                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == claim.Id && !u.IsDeleted, cancellationToken);

                if (mentor is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Không tìm thấy thông tin tài khoản chuyên gia.", HttpStatusCode.NotFound);

                var isNew = false;
                var profile = mentor.MentorProfile ?? await unitOfWork.MentorProfiles.GetByMentorIdAsync(mentor.Id, cancellationToken);
                if (profile is null)
                {
                    isNew = true;
                    profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = DateTime.UtcNow
                    };
                    mentor.MentorProfile = profile;
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                }
                else
                {
                    mentor.MentorProfile = profile;
                }

                if (profile.VerificationStatus == MentorVerificationStatus.Pending)
                    return ApiResponse<MentorProfileResponse>.Fail("Hồ sơ đang chờ xét duyệt. Vui lòng đợi kết quả trước khi chỉnh sửa.", HttpStatusCode.Conflict);

                if (profile.VerificationStatus == MentorVerificationStatus.Approved &&
                    ((request.Expertise is not null && request.Expertise.Trim() != profile.Expertise) ||
                     (request.JlptLevel is not null && request.JlptLevel.Trim() != profile.JlptLevel) ||
                     (request.VerificationDocumentUrl is not null && request.VerificationDocumentUrl.Trim() != profile.VerificationDocumentUrl) ||
                     (request.YearsOfExperience.HasValue && request.YearsOfExperience != profile.YearsOfExperience) ||
                     (request.Workplace is not null && request.Workplace.Trim() != profile.Workplace)))
                    return ApiResponse<MentorProfileResponse>.Fail("Không thể đổi thông tin chuyên môn đã xác minh khi hồ sơ đang được phê duyệt.", HttpStatusCode.Conflict);

                if (request.VerificationDocumentUrl is not null)
                    return ApiResponse<MentorProfileResponse>.Fail("Vui lòng tải chứng chỉ qua chức năng tải tài liệu bảo vệ.", HttpStatusCode.BadRequest);

                if (request.Bio is not null) profile.Bio = request.Bio.Trim();
                if (request.Expertise is not null) profile.Expertise = request.Expertise.Trim();
                if (request.JlptLevel is not null) profile.JlptLevel = request.JlptLevel.Trim();
                if (request.YearsOfExperience.HasValue) profile.YearsOfExperience = request.YearsOfExperience.Value;
                if (request.Workplace is not null) profile.Workplace = request.Workplace.Trim();
                if (request.IntroductionVideoUrl is not null) profile.IntroductionVideoUrl = request.IntroductionVideoUrl.Trim();

                profile.ModifiedDate = DateTime.UtcNow;
                if (!isNew)
                {
                    unitOfWork.MentorProfiles.Update(profile);
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorProfileResponse>.Success(
                    MapToMentorProfileResponse(mentor, profile),
                    message: "Cập nhật hồ sơ chuyên gia thành công.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi cập nhật hồ sơ chuyên gia.");
                return ApiResponse<MentorProfileResponse>.Fail("Không thể cập nhật hồ sơ cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorProfileResponse>> SubmitVerificationAsync(
            SubmitMentorVerificationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role is not (Role.Mentor or Role.Learner))
                    return ApiResponse<MentorProfileResponse>.Fail("Tài khoản không có quyền gửi hồ sơ ứng tuyển cố vấn.", HttpStatusCode.Forbidden);

                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == claim.Id && !u.IsDeleted, cancellationToken);

                if (mentor is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Không tìm thấy thông tin tài khoản chuyên gia.", HttpStatusCode.NotFound);

                var isNew = false;
                var profile = mentor.MentorProfile ?? await unitOfWork.MentorProfiles.GetByMentorIdAsync(mentor.Id, cancellationToken);
                if (profile is null)
                {
                    isNew = true;
                    profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = DateTime.UtcNow
                    };
                    mentor.MentorProfile = profile;
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                }
                else
                {
                    mentor.MentorProfile = profile;
                }

                if (profile.VerificationStatus == MentorVerificationStatus.Approved)
                    return ApiResponse<MentorProfileResponse>.Fail("Hồ sơ của bạn đã được phê duyệt, không cần gửi lại yêu cầu xác minh.", HttpStatusCode.BadRequest);

                if (profile.VerificationStatus == MentorVerificationStatus.Pending)
                    return ApiResponse<MentorProfileResponse>.Fail("Hồ sơ của bạn đang trong quá trình xét duyệt, vui lòng chờ phản hồi từ Quản trị viên.", HttpStatusCode.BadRequest);

                if (request.VerificationDocumentUrl is not null)
                    return ApiResponse<MentorProfileResponse>.Fail("Vui lòng tải chứng chỉ qua chức năng tải tài liệu bảo vệ.", HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(profile.Bio) ||
                    string.IsNullOrWhiteSpace(profile.Expertise) ||
                    string.IsNullOrWhiteSpace(profile.JlptLevel) ||
                    string.IsNullOrWhiteSpace(profile.VerificationDocumentUrl))
                {
                    return ApiResponse<MentorProfileResponse>.Fail(
                        "Vui lòng điền đầy đủ Giới thiệu bản thân, Chuyên môn, Trình độ JLPT và Tài liệu chứng chỉ trước khi gửi yêu cầu xác minh.", HttpStatusCode.BadRequest);
                }

                if (fileStorageService is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Kho tài liệu xác minh chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
                try
                {
                    await fileStorageService.VerifyPrivateVerificationAsync(profile.VerificationDocumentUrl, cancellationToken);
                }
                catch (ArgumentException)
                {
                    return ApiResponse<MentorProfileResponse>.Fail("Chứng chỉ cũ cần được tải lại vào kho bảo vệ trước khi gửi xét duyệt.", HttpStatusCode.Conflict);
                }
                catch (InvalidOperationException)
                {
                    return ApiResponse<MentorProfileResponse>.Fail("Kho tài liệu xác minh chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
                }
                catch (FileNotFoundException)
                {
                    return ApiResponse<MentorProfileResponse>.Fail("Chứng chỉ không còn trong kho bảo vệ. Vui lòng tải lại.", HttpStatusCode.Conflict);
                }

                var previousStatus = profile.VerificationStatus;
                var nowUtc = DateTime.UtcNow;
                profile.VerificationStatus = MentorVerificationStatus.Pending;
                profile.SubmittedAt = nowUtc;
                profile.RejectionReason = null;
                if (!string.IsNullOrWhiteSpace(request.Notes))
                    profile.ReviewNotes = request.Notes.Trim();

                profile.ModifiedDate = nowUtc;
                if (!isNew)
                {
                    unitOfWork.MentorProfiles.Update(profile);
                }
                var adminIds = await unitOfWork.UserAccounts.Query()
                    .Where(u => u.Role == Role.Admin && !u.IsDeleted)
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);
                unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
                {
                    EventKey = $"mentor-verification:submitted:{mentor.Id}:{Guid.NewGuid():N}",
                    AggregateType = "MentorProfile",
                    AggregateId = mentor.Id.ToString(),
                    EventType = previousStatus == MentorVerificationStatus.Unverified
                        ? "MentorVerificationSubmitted" : "MentorVerificationResubmitted",
                    EventVersion = 1,
                    PayloadJson = JsonSerializer.Serialize(new { MentorId = mentor.Id, AdminIds = adminIds }),
                    CreatedAtUtc = nowUtc,
                    AvailableAtUtc = nowUtc
                });
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorProfileResponse>.Success(
                    MapToMentorProfileResponse(mentor, profile),
                    message: "Gửi yêu cầu xác minh hồ sơ thành công. Quản trị viên sẽ xét duyệt trong thời gian sớm nhất.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<MentorProfileResponse>.Fail("Hồ sơ vừa được cập nhật ở phiên khác. Vui lòng tải lại.", HttpStatusCode.Conflict);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi nộp hồ sơ xác minh chuyên gia.");
                return ApiResponse<MentorProfileResponse>.Fail("Không thể nộp hồ sơ xác minh. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorProfileResponse>> UploadVerificationDocumentAsync(
            Stream document, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role is not (Role.Learner or Role.Mentor))
                    return ApiResponse<MentorProfileResponse>.Fail("Tài khoản không có quyền nộp chứng chỉ cố vấn.", HttpStatusCode.Forbidden);
                if (fileStorageService is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Kho tài liệu xác minh chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);

                var user = await unitOfWork.UserAccounts.Query().Include(u => u.MentorProfile)
                    .SingleOrDefaultAsync(u => u.Id == claim.Id && !u.IsDeleted, cancellationToken);
                if (user is null)
                    return ApiResponse<MentorProfileResponse>.Fail("Không tìm thấy tài khoản.", HttpStatusCode.NotFound);
                var profile = user.MentorProfile ?? new MentorProfile
                {
                    MentorId = user.Id,
                    VerificationStatus = MentorVerificationStatus.Unverified,
                    CreatedDate = DateTime.UtcNow
                };
                if (profile.VerificationStatus is MentorVerificationStatus.Pending or MentorVerificationStatus.Approved)
                    return ApiResponse<MentorProfileResponse>.Fail("Không thể thay chứng chỉ khi hồ sơ đang hoặc đã được duyệt.", HttpStatusCode.Conflict);

                var result = await fileStorageService.UploadPrivateVerificationAsync(
                    document, contentType, user.Id, cancellationToken);
                if (user.MentorProfile is null)
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                profile.VerificationDocumentUrl = result.Url;
                profile.ModifiedDate = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse<MentorProfileResponse>.Success(MapToMentorProfileResponse(user, profile));
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Không thể dùng kho chứng chỉ private.");
                return ApiResponse<MentorProfileResponse>.Fail("Kho tài liệu xác minh chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
            }
            catch (ArgumentException)
            {
                return ApiResponse<MentorProfileResponse>.Fail("Tài liệu chứng chỉ không hợp lệ.", HttpStatusCode.BadRequest);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<MentorProfileResponse>.Fail("Hồ sơ đã thay đổi. Vui lòng tải lại.", HttpStatusCode.Conflict);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi tải chứng chỉ cố vấn.");
                return ApiResponse<MentorProfileResponse>.Fail("Không thể lưu chứng chỉ. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<PublicMentorProfileResponse>> GetPublicProfileAsync(
            int mentorId, CancellationToken cancellationToken = default)
        {
            try
            {
                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);

                if (mentor is null)
                    return ApiResponse<PublicMentorProfileResponse>.Fail("Không tìm thấy thông tin chuyên gia.", HttpStatusCode.NotFound);

                var profile = mentor.MentorProfile ?? await unitOfWork.MentorProfiles.GetByMentorIdAsync(mentor.Id, cancellationToken);
                var isBookable = profile is not null && profile.VerificationStatus == MentorVerificationStatus.Approved;

                var response = new PublicMentorProfileResponse
                {
                    MentorId = mentor.Id,
                    Name = $"{mentor.FirstName} {mentor.LastName}".Trim(),
                    AvatarUrl = mentor.AvatarUrl,
                    Bio = profile?.Bio,
                    Expertise = profile?.Expertise,
                    JlptLevel = profile?.JlptLevel,
                    YearsOfExperience = profile?.YearsOfExperience,
                    Workplace = profile?.Workplace,
                    IntroductionVideoUrl = profile?.IntroductionVideoUrl,
                    IsBookable = isBookable
                };

                return ApiResponse<PublicMentorProfileResponse>.Success(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi lấy thông tin công khai của chuyên gia {MentorId}.", mentorId);
                return ApiResponse<PublicMentorProfileResponse>.Fail("Không thể tải hồ sơ cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> IsMentorEligibleForSchedulingAsync(int mentorId, CancellationToken cancellationToken = default)
        {
            var mentor = await unitOfWork.UserAccounts.Query()
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Id == mentorId && u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);

            if (mentor is null) return false;
            var profile = mentor.MentorProfile ?? await unitOfWork.MentorProfiles.GetByMentorIdAsync(mentor.Id, cancellationToken);
            // Chỉ tài khoản Chuyên gia có hồ sơ ĐÃ ĐƯỢC PHÊ DUYỆT (Approved) mới đủ điều kiện tạo hoặc công bố lịch mới.
            // Legacy chuyên gia không có profile hoặc profile chưa được duyệt tuyệt đối không được phép tạo/công bố slot mới.
            return profile is not null && profile.VerificationStatus == MentorVerificationStatus.Approved;
        }

        private static MentorProfileResponse MapToMentorProfileResponse(UserAccount u, MentorProfile p) => new()
        {
            MentorId = u.Id,
            Email = u.Email ?? string.Empty,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            AvatarUrl = u.AvatarUrl,
            Bio = p.Bio,
            Expertise = p.Expertise,
            JlptLevel = p.JlptLevel,
            YearsOfExperience = p.YearsOfExperience,
            Workplace = p.Workplace,
            HasVerificationDocument = !string.IsNullOrWhiteSpace(p.VerificationDocumentUrl),
            IntroductionVideoUrl = p.IntroductionVideoUrl,
            VerificationStatus = p.VerificationStatus,
            VerificationStatusDescription = GetStatusDescription(p.VerificationStatus),
            IsBookable = p.VerificationStatus == MentorVerificationStatus.Approved,
            SubmittedAt = p.SubmittedAt,
            ReviewedAt = p.ReviewedAt,
            RejectionReason = p.RejectionReason,
            ReviewNotes = p.ReviewNotes
        };

        private static string GetStatusDescription(MentorVerificationStatus status) => status switch
        {
            MentorVerificationStatus.Unverified => "Chưa nộp hồ sơ",
            MentorVerificationStatus.Pending => "Đang chờ duyệt",
            MentorVerificationStatus.Approved => "Đã phê duyệt",
            MentorVerificationStatus.Rejected => "Đã bị từ chối",
            MentorVerificationStatus.ChangesRequested => "Yêu cầu bổ sung thông tin",
            _ => status.ToString()
        };
    }
}
