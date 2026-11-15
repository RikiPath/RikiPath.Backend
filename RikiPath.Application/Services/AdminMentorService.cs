using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services
{
    public class AdminMentorService(
        IUnitOfWork unitOfWork,
        IClaimService claimService,
        IFileStorageService? fileStorageService = null) : IAdminMentorService
    {
        public async Task<ApiResponse<MentorResponse>> CreateMentorAsync(
            CreateMentorRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return ApiResponse<MentorResponse>.Fail("Email không được để trống.", HttpStatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                    return ApiResponse<MentorResponse>.Fail("Mật khẩu phải có ít nhất 6 ký tự.", HttpStatusCode.BadRequest);

                var normalizedEmail = request.Email.Trim().ToLowerInvariant();

                var existing = (await unitOfWork.UserAccounts
                    .FindAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken)).FirstOrDefault();
                if (existing is not null)
                    return ApiResponse<MentorResponse>.Fail("Email này đã được sử dụng.", HttpStatusCode.BadRequest);

                CreatePasswordHash(request.Password, out var hash, out var salt);

                var mentor = new UserAccount
                {
                    Email = normalizedEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    Role = Role.Mentor,
                    IsEmailVerified = true,
                    CreatedDate = DateTime.UtcNow
                };

                await unitOfWork.UserAccounts.AddAsync(mentor, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var profile = new MentorProfile
                {
                    MentorId = mentor.Id,
                    VerificationStatus = MentorVerificationStatus.Unverified,
                    CreatedDate = DateTime.UtcNow
                };
                await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorResponse>.Success(MapToMentorResponse(mentor));
            }
            catch (Exception)
            {
                return ApiResponse<MentorResponse>.Fail("Không thể thêm cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MentorResponse>>> GetMentorsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var mentors = await unitOfWork.UserAccounts.FindAsync(u => u.Role == Role.Mentor && !u.IsDeleted, cancellationToken);
                var result = mentors
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .Select(MapToMentorResponse)
                    .ToList();

                return ApiResponse<List<MentorResponse>>.Success(result);
            }
            catch (Exception)
            {
                return ApiResponse<List<MentorResponse>>.Fail("Không thể tải danh sách cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorResponse>> SetMentorActiveAsync(
            int mentorId, bool isActive, CancellationToken cancellationToken)
        {
            try
            {
                var mentor = await unitOfWork.UserAccounts.GetByIdAsync(mentorId, cancellationToken);
                if (mentor is null || mentor.Role != Role.Mentor)
                    return ApiResponse<MentorResponse>.Fail("Không tìm thấy cố vấn.", HttpStatusCode.NotFound);

                mentor.IsDeleted = !isActive;
                mentor.ModifiedDate = DateTime.UtcNow;

                unitOfWork.UserAccounts.Update(mentor);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<MentorResponse>.Success(
                    MapToMentorResponse(mentor), message:
                    isActive ? "Đã kích hoạt cố vấn." : "Đã khóa tài khoản cố vấn.");
            }
            catch (Exception)
            {
                return ApiResponse<MentorResponse>.Fail("Không thể cập nhật trạng thái cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<AdminMentorProfileResponse>>> GetMentorProfilesAsync(
            MentorVerificationStatus? status, CancellationToken cancellationToken)
        {
            try
            {
                var query = unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .Where(u => !u.IsDeleted &&
                        (u.Role == Role.Mentor || (u.Role == Role.Learner && u.MentorProfile != null)));

                if (status.HasValue)
                {
                    if (status.Value == MentorVerificationStatus.Unverified)
                    {
                        query = query.Where(u => u.MentorProfile == null || u.MentorProfile.VerificationStatus == MentorVerificationStatus.Unverified);
                    }
                    else
                    {
                        query = query.Where(u => u.MentorProfile != null && u.MentorProfile.VerificationStatus == status.Value);
                    }
                }

                var list = await query
                    .OrderByDescending(u => u.MentorProfile != null ? u.MentorProfile.SubmittedAt ?? u.MentorProfile.CreatedDate : u.CreatedDate)
                    .ToListAsync(cancellationToken);

                var result = list.Select(u => MapToAdminMentorProfileResponse(u, u.MentorProfile)).ToList();
                return ApiResponse<List<AdminMentorProfileResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AdminMentorProfileResponse>>.Fail("Không thể tải danh sách hồ sơ cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AdminMentorProfileResponse>> GetMentorProfileByIdAsync(
            int mentorId, CancellationToken cancellationToken)
        {
            try
            {
                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == mentorId && !u.IsDeleted &&
                        (u.Role == Role.Mentor || (u.Role == Role.Learner && u.MentorProfile != null)), cancellationToken);

                if (mentor is null)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Không tìm thấy thông tin chuyên gia.", HttpStatusCode.NotFound);

                return ApiResponse<AdminMentorProfileResponse>.Success(
                    MapToAdminMentorProfileResponse(mentor, mentor.MentorProfile));
            }
            catch (Exception ex)
            {
                return ApiResponse<AdminMentorProfileResponse>.Fail("Không thể tải hồ sơ cố vấn. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AdminMentorProfileResponse>> ReviewVerificationAsync(
            int mentorId, ReviewMentorVerificationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Admin)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Chỉ Quản trị viên (Admin) mới có quyền xét duyệt hồ sơ chuyên gia.", HttpStatusCode.Forbidden);

                var admin = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                var adminName = admin is not null
                    ? $"{admin.FirstName} {admin.LastName}".Trim()
                    : "Admin System";

                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == mentorId && !u.IsDeleted &&
                        (u.Role == Role.Mentor || (u.Role == Role.Learner && u.MentorProfile != null)), cancellationToken);

                if (mentor is null)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Không tìm thấy thông tin chuyên gia.", HttpStatusCode.NotFound);

                var profile = mentor.MentorProfile;
                if (profile is null)
                {
                    profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = DateTime.UtcNow
                    };
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                }

                if (profile.VerificationStatus == MentorVerificationStatus.Unverified)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Chuyên gia chưa nộp hồ sơ xin xét duyệt.", HttpStatusCode.BadRequest);

                if (profile.VerificationStatus != MentorVerificationStatus.Pending)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Hồ sơ đã được xử lý hoặc chưa được gửi lại. Vui lòng làm mới danh sách.", HttpStatusCode.Conflict);

                string responseMessage;

                switch (request.Decision)
                {
                    case MentorVerificationDecision.Approve:
                        if (string.IsNullOrWhiteSpace(profile.VerificationDocumentUrl) || fileStorageService is null)
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Chứng chỉ riêng tư chưa sẵn sàng để xét duyệt.", HttpStatusCode.Conflict);
                        try
                        {
                            await fileStorageService.VerifyPrivateVerificationAsync(profile.VerificationDocumentUrl, cancellationToken);
                        }
                        catch (ArgumentException)
                        {
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Tài liệu cũ cần được tải lại vào kho bảo vệ.", HttpStatusCode.Conflict);
                        }
                        catch (InvalidOperationException)
                        {
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Kho chứng chỉ riêng tư chưa sẵn sàng.", HttpStatusCode.ServiceUnavailable);
                        }
                        catch (FileNotFoundException)
                        {
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Không tìm thấy chứng chỉ trong kho bảo vệ.", HttpStatusCode.Conflict);
                        }
                        profile.VerificationStatus = MentorVerificationStatus.Approved;
                        if (mentor.Role == Role.Learner)
                        {
                            mentor.Role = Role.Mentor;
                            mentor.ModifiedDate = DateTime.UtcNow;
                            unitOfWork.UserAccounts.Update(mentor);
                        }
                        profile.ReviewedAt = DateTime.UtcNow;
                        profile.ReviewedByAdminName = adminName;
                        profile.RejectionReason = null;
                        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
                            profile.ReviewNotes = request.ReviewNotes.Trim();
                        responseMessage = "Phê duyệt hồ sơ chuyên gia thành công.";
                        break;

                    case MentorVerificationDecision.Reject:
                        if (string.IsNullOrWhiteSpace(request.RejectionReason))
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Vui lòng cung cấp lý do từ chối hồ sơ.", HttpStatusCode.BadRequest);

                        profile.VerificationStatus = MentorVerificationStatus.Rejected;
                        profile.ReviewedAt = DateTime.UtcNow;
                        profile.ReviewedByAdminName = adminName;
                        profile.RejectionReason = request.RejectionReason.Trim();
                        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
                            profile.ReviewNotes = request.ReviewNotes.Trim();
                        responseMessage = "Đã từ chối hồ sơ chuyên gia.";
                        break;

                    case MentorVerificationDecision.RequestChanges:
                        if (string.IsNullOrWhiteSpace(request.ReviewNotes))
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Vui lòng cung cấp nội dung yêu cầu bổ sung thông tin.", HttpStatusCode.BadRequest);

                        profile.VerificationStatus = MentorVerificationStatus.ChangesRequested;
                        profile.ReviewedAt = DateTime.UtcNow;
                        profile.ReviewedByAdminName = adminName;
                        profile.ReviewNotes = request.ReviewNotes.Trim();
                        responseMessage = "Đã gửi yêu cầu bổ sung thông tin hồ sơ cho chuyên gia.";
                        break;

                    default:
                        return ApiResponse<AdminMentorProfileResponse>.Fail("Quyết định xét duyệt không hợp lệ.", HttpStatusCode.BadRequest);
                }

                profile.ModifiedDate = DateTime.UtcNow;
                unitOfWork.MentorProfiles.Update(profile);
                var nowUtc = DateTime.UtcNow;
                unitOfWork.AddMentorOutboxEvent(new MentorOutboxEvent
                {
                    EventKey = $"mentor-verification:reviewed:{mentor.Id}:{Guid.NewGuid():N}",
                    AggregateType = "MentorProfile",
                    AggregateId = mentor.Id.ToString(),
                    EventType = "MentorVerificationReviewed",
                    EventVersion = 1,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        MentorId = mentor.Id,
                        AdminId = claim.Id,
                        Decision = request.Decision.ToString(),
                        AdminIds = await unitOfWork.UserAccounts.Query()
                            .Where(u => u.Role == Role.Admin && !u.IsDeleted && u.Id != claim.Id)
                            .Select(u => u.Id)
                            .ToListAsync(cancellationToken)
                    }),
                    CreatedAtUtc = nowUtc,
                    AvailableAtUtc = nowUtc
                });
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<AdminMentorProfileResponse>.Success(
                    MapToAdminMentorProfileResponse(mentor, profile),
                    message: responseMessage);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiResponse<AdminMentorProfileResponse>.Fail("Hồ sơ vừa được quản trị viên khác xử lý. Vui lòng tải lại.", HttpStatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ApiResponse<AdminMentorProfileResponse>.Fail("Không thể xét duyệt hồ sơ. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            using var hmac = new HMACSHA512();
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private static MentorResponse MapToMentorResponse(UserAccount u) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            IsEmailVerified = u.IsEmailVerified,
            CreatedDate = u.CreatedDate
        };

        private static AdminMentorProfileResponse MapToAdminMentorProfileResponse(UserAccount u, MentorProfile? p) => new()
        {
            MentorId = u.Id,
            Email = u.Email ?? string.Empty,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            AvatarUrl = u.AvatarUrl,
            IsActive = !u.IsDeleted,
            Bio = p?.Bio,
            Expertise = p?.Expertise,
            JlptLevel = p?.JlptLevel,
            YearsOfExperience = p?.YearsOfExperience,
            Workplace = p?.Workplace,
            HasVerificationDocument = !string.IsNullOrWhiteSpace(p?.VerificationDocumentUrl),
            IntroductionVideoUrl = p?.IntroductionVideoUrl,
            VerificationStatus = p?.VerificationStatus ?? MentorVerificationStatus.Unverified,
            VerificationStatusDescription = GetStatusDescription(p?.VerificationStatus ?? MentorVerificationStatus.Unverified),
            IsBookable = (p?.VerificationStatus ?? MentorVerificationStatus.Unverified) == MentorVerificationStatus.Approved,
            SubmittedAt = p?.SubmittedAt,
            ReviewedAt = p?.ReviewedAt,
            ReviewedByAdminName = p?.ReviewedByAdminName,
            RejectionReason = p?.RejectionReason,
            ReviewNotes = p?.ReviewNotes,
            CreatedDate = u.CreatedDate
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
