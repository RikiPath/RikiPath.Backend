using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Helpers;
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
        IFileStorageService? fileStorageService = null,
        ILogger<AdminMentorService>? logger = null) : IAdminMentorService
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
                var actor = claimService.GetUserClaim();

                var existing = await unitOfWork.UserAccounts.Query()
                    .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
                if (existing)
                    return ApiResponse<MentorResponse>.Fail("Email này đã được sử dụng.", HttpStatusCode.Conflict);

                CreatePasswordHash(request.Password, out var hash, out var salt);
                var (firstName, lastName) = UserNameHelper.SplitFullName(request.FullName);
                var now = DateTime.UtcNow;

                var mentor = new UserAccount
                {
                    Email = normalizedEmail,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = request.PhoneNumber?.Trim(),
                    Role = Role.Mentor,
                    IsEmailVerified = true,
                    CreatedDate = now,
                };

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await unitOfWork.UserAccounts.AddAsync(mentor, token);
                    await unitOfWork.SaveChangesAsync(token);

                    var profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = now,
                    };
                    await unitOfWork.MentorProfiles.AddAsync(profile, token);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = actor.Id,
                        EntityType = "MentorAccount",
                        EntityId = mentor.Id,
                        Action = "Created",
                        AfterJson = JsonSerializer.Serialize(new { mentor.Id, mentor.Email, FullName = UserNameHelper.FormatFullName(mentor.FirstName, mentor.LastName) }),
                        CreatedAtUtc = now,
                    }, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                return ApiResponse<MentorResponse>.Success(MapToMentorResponse(mentor, null, []));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không thể tạo tài khoản Giáo viên {Email}", request.Email);
                return ApiResponse<MentorResponse>.Fail("Không thể thêm Giáo viên. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<List<MentorResponse>>> GetMentorsAsync(
            CancellationToken cancellationToken,
            bool includeInactive = false)
        {
            try
            {
                var query = unitOfWork.UserAccounts.Query()
                    .AsNoTracking()
                    .Include(u => u.MentorProfile)
                    .Where(u => u.Role == Role.Mentor);

                if (!includeInactive)
                    query = query.Where(u => !u.IsDeleted);

                var mentors = await query
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync(cancellationToken);

                var mentorIds = mentors.Select(m => m.Id).ToList();
                var quals = mentorIds.Count == 0 || unitOfWork.MentorTeachingQualifications == null
                    ? []
                    : await unitOfWork.MentorTeachingQualifications.Query()
                        .AsNoTracking()
                        .Include(q => q.CertificateLevel)
                        .Where(q => mentorIds.Contains(q.MentorUserId) && q.IsActive)
                        .ToListAsync(cancellationToken);

                var levelsByMentor = quals
                    .GroupBy(q => q.MentorUserId)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<string>)g
                            .OrderBy(q => q.CertificateLevel.SortOrder)
                            .Select(q => q.CertificateLevel.Code)
                            .ToList());

                var result = mentors
                    .Select(u => MapToMentorResponse(
                        u,
                        u.MentorProfile,
                        levelsByMentor.GetValueOrDefault(u.Id, [])))
                    .ToList();

                return ApiResponse<List<MentorResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không thể tải danh sách Giáo viên");
                return ApiResponse<List<MentorResponse>>.Fail("Không thể tải danh sách Giáo viên. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorResponse>> SetMentorActiveAsync(
            int mentorId, bool isActive, CancellationToken cancellationToken)
        {
            try
            {
                var actor = claimService.GetUserClaim();
                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == mentorId, cancellationToken);
                if (mentor is null || mentor.Role != Role.Mentor)
                    return ApiResponse<MentorResponse>.Fail("Không tìm thấy Giáo viên.", HttpStatusCode.NotFound);

                if (!isActive && !mentor.IsDeleted)
                {
                    var now = DateTime.UtcNow;
                    var hasActiveCohorts = await unitOfWork.StudyCohorts.Query()
                        .AnyAsync(c => c.MentorId == mentorId && (c.Status == "Published" || c.Status == "Draft") && c.EndsAt > now, cancellationToken);
                    var hasBookedSlots = await unitOfWork.MentorAvailabilities.Query()
                        .AnyAsync(a => a.MentorId == mentorId && !a.IsDeleted && a.IsApproved && a.BookedCount > 0 && a.StartTime > now, cancellationToken);
                    if (hasActiveCohorts || hasBookedSlots)
                    {
                        return ApiResponse<MentorResponse>.Fail(
                            "Giáo viên đang phụ trách lớp học hoặc lịch đã có học viên đặt. Hãy điều phối thay thế trước khi tạm khóa.",
                            HttpStatusCode.Conflict);
                    }
                }

                var nowUtc = DateTime.UtcNow;
                var wasActive = !mentor.IsDeleted;
                mentor.IsDeleted = !isActive;
                mentor.ModifiedDate = nowUtc;

                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    unitOfWork.UserAccounts.Update(mentor);
                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = actor.Id,
                        EntityType = "MentorAccount",
                        EntityId = mentor.Id,
                        Action = isActive ? "Activated" : "Deactivated",
                        BeforeJson = JsonSerializer.Serialize(new { IsActive = wasActive }),
                        AfterJson = JsonSerializer.Serialize(new { IsActive = isActive }),
                        CreatedAtUtc = nowUtc,
                    }, token);
                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                var teachingLevels = unitOfWork.MentorTeachingQualifications == null
                    ? (IReadOnlyList<string>)[]
                    : await unitOfWork.MentorTeachingQualifications.Query()
                        .AsNoTracking()
                        .Include(q => q.CertificateLevel)
                        .Where(q => q.MentorUserId == mentorId && q.IsActive)
                        .OrderBy(q => q.CertificateLevel.SortOrder)
                        .Select(q => q.CertificateLevel.Code)
                        .ToListAsync(cancellationToken);

                return ApiResponse<MentorResponse>.Success(
                    MapToMentorResponse(mentor, mentor.MentorProfile, teachingLevels),
                    message: isActive ? "Đã kích hoạt tài khoản Giáo viên." : "Đã khóa tài khoản Giáo viên.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không thể cập nhật trạng thái Giáo viên {MentorId}", mentorId);
                return ApiResponse<MentorResponse>.Fail("Không thể cập nhật trạng thái Giáo viên. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
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
                logger?.LogError(ex, "Không thể tải danh sách hồ sơ Giáo viên");
                return ApiResponse<List<AdminMentorProfileResponse>>.Fail("Không thể tải danh sách hồ sơ Giáo viên. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
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
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Không tìm thấy thông tin Giáo viên.", HttpStatusCode.NotFound);

                return ApiResponse<AdminMentorProfileResponse>.Success(
                    MapToAdminMentorProfileResponse(mentor, mentor.MentorProfile));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không thể tải hồ sơ Giáo viên {MentorId}", mentorId);
                return ApiResponse<AdminMentorProfileResponse>.Fail("Không thể tải hồ sơ Giáo viên. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<AdminMentorProfileResponse>> ReviewVerificationAsync(
            int mentorId, ReviewMentorVerificationRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                if (claim.Role != Role.Admin)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Chỉ Quản trị viên (Admin) mới có quyền xét duyệt hồ sơ Giáo viên.", HttpStatusCode.Forbidden);

                var admin = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                var adminName = admin is not null
                    ? $"{admin.FirstName} {admin.LastName}".Trim()
                    : "Admin System";

                var mentor = await unitOfWork.UserAccounts.Query()
                    .Include(u => u.MentorProfile)
                    .FirstOrDefaultAsync(u => u.Id == mentorId && !u.IsDeleted &&
                        (u.Role == Role.Mentor || (u.Role == Role.Learner && u.MentorProfile != null)), cancellationToken);

                if (mentor is null)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Không tìm thấy thông tin Giáo viên.", HttpStatusCode.NotFound);

                var profile = mentor.MentorProfile;
                if (profile is null)
                {
                    profile = new MentorProfile
                    {
                        MentorId = mentor.Id,
                        VerificationStatus = MentorVerificationStatus.Unverified,
                        CreatedDate = DateTime.UtcNow,
                    };
                    await unitOfWork.MentorProfiles.AddAsync(profile, cancellationToken);
                }

                if (profile.VerificationStatus == MentorVerificationStatus.Unverified)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Giáo viên chưa nộp hồ sơ xin xét duyệt.", HttpStatusCode.BadRequest);

                if (profile.VerificationStatus != MentorVerificationStatus.Pending)
                    return ApiResponse<AdminMentorProfileResponse>.Fail("Hồ sơ đã được xử lý hoặc chưa được gửi lại. Vui lòng làm mới danh sách.", HttpStatusCode.Conflict);

                var previousStatus = profile.VerificationStatus.ToString();
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
                        responseMessage = "Phê duyệt hồ sơ Giáo viên thành công.";
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
                        responseMessage = "Đã từ chối hồ sơ Giáo viên.";
                        break;

                    case MentorVerificationDecision.RequestChanges:
                        if (string.IsNullOrWhiteSpace(request.ReviewNotes))
                            return ApiResponse<AdminMentorProfileResponse>.Fail("Vui lòng cung cấp nội dung yêu cầu bổ sung thông tin.", HttpStatusCode.BadRequest);

                        profile.VerificationStatus = MentorVerificationStatus.ChangesRequested;
                        profile.ReviewedAt = DateTime.UtcNow;
                        profile.ReviewedByAdminName = adminName;
                        profile.ReviewNotes = request.ReviewNotes.Trim();
                        responseMessage = "Đã gửi yêu cầu bổ sung thông tin hồ sơ cho Giáo viên.";
                        break;

                    default:
                        return ApiResponse<AdminMentorProfileResponse>.Fail("Quyết định xét duyệt không hợp lệ.", HttpStatusCode.BadRequest);
                }

                var nowUtc = DateTime.UtcNow;
                profile.ModifiedDate = nowUtc;
                unitOfWork.MentorProfiles.Update(profile);
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
                    AvailableAtUtc = nowUtc,
                });

                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = claim.Id,
                    EntityType = "MentorVerification",
                    EntityId = mentor.Id,
                    Action = request.Decision.ToString(),
                    BeforeJson = JsonSerializer.Serialize(new { VerificationStatus = previousStatus }),
                    AfterJson = JsonSerializer.Serialize(new { VerificationStatus = profile.VerificationStatus.ToString(), profile.RejectionReason, profile.ReviewNotes }),
                    Reason = request.RejectionReason?.Trim() ?? request.ReviewNotes?.Trim(),
                    CreatedAtUtc = nowUtc,
                }, cancellationToken);

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
                logger?.LogError(ex, "Không thể xét duyệt hồ sơ Giáo viên {MentorId}", mentorId);
                return ApiResponse<AdminMentorProfileResponse>.Fail("Không thể xét duyệt hồ sơ. Vui lòng thử lại.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorQualificationsWorkspaceResponse>> GetTeachingQualificationsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var levels = await unitOfWork.CertificateLevels.Query()
                    .AsNoTracking()
                    .Include(l => l.CertificateType)
                    .Where(l => !l.IsDeleted && !l.CertificateType.IsDeleted && l.CertificateType.Name.ToUpper() == "JLPT")
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new MentorQualificationLevelOptionResponse
                    {
                        Id = l.Id,
                        Code = l.Code,
                        CertificateCode = l.CertificateType.Name,
                        SortOrder = l.SortOrder,
                    })
                    .ToListAsync(cancellationToken);

                var mentors = await unitOfWork.UserAccounts.Query()
                    .AsNoTracking()
                    .Include(u => u.MentorProfile)
                    .Where(u => !u.IsDeleted && u.Role == Role.Mentor)
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync(cancellationToken);

                var mentorIds = mentors.Select(m => m.Id).ToList();
                var allQuals = mentorIds.Count == 0
                    ? []
                    : await unitOfWork.MentorTeachingQualifications.Query()
                        .AsNoTracking()
                        .Include(q => q.CertificateLevel)
                        .Include(q => q.VerifiedByAdmin)
                        .Where(q => mentorIds.Contains(q.MentorUserId))
                        .ToListAsync(cancellationToken);

                var summaries = mentors.Select(m =>
                {
                    var items = allQuals.Where(q => q.MentorUserId == m.Id).ToList();
                    var active = items
                        .Where(q => q.IsActive && !q.CertificateLevel.IsDeleted)
                        .OrderBy(q => q.CertificateLevel.SortOrder)
                        .Select(q => new MentorQualificationBadgeResponse
                        {
                            CertificateLevelId = q.CertificateLevelId,
                            LevelCode = q.CertificateLevel.Code,
                            IsActive = true,
                            VerifiedAtUtc = q.VerifiedAtUtc,
                            VerifiedByAdminName = UserNameHelper.FormatFullName(q.VerifiedByAdmin.FirstName, q.VerifiedByAdmin.LastName),
                            RevokedAtUtc = null,
                        })
                        .ToList();

                    return new MentorQualificationSummaryResponse
                    {
                        MentorId = m.Id,
                        FullName = UserNameHelper.FormatFullName(m.FirstName, m.LastName),
                        Email = m.Email ?? string.Empty,
                        IsActive = !m.IsDeleted,
                        VerificationStatus = (m.MentorProfile?.VerificationStatus ?? MentorVerificationStatus.Unverified).ToString(),
                        DeclaredJlptLevel = m.MentorProfile?.JlptLevel,
                        ActiveQualifications = active,
                        RevokedCount = items.Count(q => !q.IsActive),
                    };
                }).ToList();

                return ApiResponse<MentorQualificationsWorkspaceResponse>.Success(new MentorQualificationsWorkspaceResponse
                {
                    Levels = levels,
                    Mentors = summaries,
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không thể tải danh sách phân quyền giảng dạy JLPT");
                return ApiResponse<MentorQualificationsWorkspaceResponse>.Fail("Không thể tải danh sách phân quyền JLPT.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<MentorQualificationBadgeResponse>> GrantTeachingQualificationAsync(
            int mentorId, GrantMentorQualificationRequest request, CancellationToken cancellationToken)
        {
            var actor = claimService.GetUserClaim();
            if (actor.Role != Role.Admin)
                return ApiResponse<MentorQualificationBadgeResponse>.Fail("Chỉ Admin mới được cấp quyền giảng dạy.", HttpStatusCode.Forbidden);

            var mentor = await unitOfWork.UserAccounts.Query()
                .Include(u => u.MentorProfile)
                .FirstOrDefaultAsync(u => u.Id == mentorId && !u.IsDeleted && u.Role == Role.Mentor, cancellationToken);
            if (mentor is null)
                return ApiResponse<MentorQualificationBadgeResponse>.Fail("Không tìm thấy Giáo viên đang hoạt động.", HttpStatusCode.NotFound);

            if (mentor.MentorProfile?.VerificationStatus != MentorVerificationStatus.Approved)
                return ApiResponse<MentorQualificationBadgeResponse>.Fail("Chỉ được cấp quyền giảng dạy cho Giáo viên đã được phê duyệt hồ sơ.", HttpStatusCode.Conflict);

            var level = await unitOfWork.CertificateLevels.Query()
                .Include(l => l.CertificateType)
                .FirstOrDefaultAsync(l => l.Id == request.CertificateLevelId && !l.IsDeleted && !l.CertificateType.IsDeleted, cancellationToken);
            if (level is null)
                return ApiResponse<MentorQualificationBadgeResponse>.Fail("Không tìm thấy cấp độ chứng chỉ hợp lệ.", HttpStatusCode.NotFound);

            if (string.Equals(level.CertificateType.Name, "JLPT", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(mentor.MentorProfile.JlptLevel))
            {
                var declaredRank = JlptCurriculum.Rank(mentor.MentorProfile.JlptLevel);
                var targetRank = JlptCurriculum.Rank(level.Code);
                if (declaredRank > 0 && targetRank > declaredRank)
                {
                    return ApiResponse<MentorQualificationBadgeResponse>.Fail(
                        $"Không thể cấp quyền dạy {level.Code} khi chứng chỉ JLPT đã xác minh của Giáo viên là {mentor.MentorProfile.JlptLevel}.",
                        HttpStatusCode.BadRequest);
                }
            }

            var existing = await unitOfWork.MentorTeachingQualifications.Query()
                .FirstOrDefaultAsync(q => q.MentorUserId == mentorId && q.CertificateLevelId == level.Id, cancellationToken);
            if (existing is not null && existing.IsActive)
                return ApiResponse<MentorQualificationBadgeResponse>.Fail("Giáo viên đã có quyền giảng dạy cấp độ này.", HttpStatusCode.Conflict);

            var now = DateTime.UtcNow;
            var adminUser = await unitOfWork.UserAccounts.GetByIdAsync(actor.Id, cancellationToken);
            var adminName = adminUser is null ? $"Admin #{actor.Id}" : UserNameHelper.FormatFullName(adminUser.FirstName, adminUser.LastName);

            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                if (existing is null)
                {
                    existing = new MentorTeachingQualification
                    {
                        MentorUserId = mentorId,
                        CertificateLevelId = level.Id,
                        IsActive = true,
                        VerifiedByAdminId = actor.Id,
                        VerifiedAtUtc = now,
                    };
                    await unitOfWork.MentorTeachingQualifications.AddAsync(existing, token);
                }
                else
                {
                    existing.IsActive = true;
                    existing.VerifiedByAdminId = actor.Id;
                    existing.VerifiedAtUtc = now;
                    existing.RevokedAtUtc = null;
                    unitOfWork.MentorTeachingQualifications.Update(existing);
                }

                await unitOfWork.SaveChangesAsync(token);
                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = actor.Id,
                    EntityType = "MentorTeachingQualification",
                    EntityId = existing.Id,
                    Action = "Granted",
                    AfterJson = JsonSerializer.Serialize(new { MentorId = mentorId, CertificateLevelId = level.Id, LevelCode = level.Code }),
                    Reason = request.Reason?.Trim(),
                    CreatedAtUtc = now,
                }, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            return ApiResponse<MentorQualificationBadgeResponse>.Success(new MentorQualificationBadgeResponse
            {
                CertificateLevelId = level.Id,
                LevelCode = level.Code,
                IsActive = true,
                VerifiedAtUtc = now,
                VerifiedByAdminName = adminName,
            }, message: $"Đã cấp quyền giảng dạy {level.Code} cho Giáo viên.");
        }

        public async Task<ApiResponse<bool>> RevokeTeachingQualificationAsync(
            int mentorId, int certificateLevelId, RevokeMentorQualificationRequest request, CancellationToken cancellationToken)
        {
            var actor = claimService.GetUserClaim();
            if (actor.Role != Role.Admin)
                return ApiResponse<bool>.Fail("Chỉ Admin mới được thu hồi quyền giảng dạy.", HttpStatusCode.Forbidden);

            if (string.IsNullOrWhiteSpace(request.Reason))
                return ApiResponse<bool>.Fail("Vui lòng nhập lý do thu hồi quyền giảng dạy.", HttpStatusCode.BadRequest);

            var existing = await unitOfWork.MentorTeachingQualifications.Query()
                .Include(q => q.CertificateLevel)
                .FirstOrDefaultAsync(q => q.MentorUserId == mentorId && q.CertificateLevelId == certificateLevelId, cancellationToken);
            if (existing is null || !existing.IsActive)
                return ApiResponse<bool>.Fail("Không tìm thấy quyền giảng dạy đang hoạt động cho cấp độ này.", HttpStatusCode.NotFound);

            var now = DateTime.UtcNow;
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                existing.IsActive = false;
                existing.RevokedAtUtc = now;
                unitOfWork.MentorTeachingQualifications.Update(existing);

                await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                {
                    ActorUserId = actor.Id,
                    EntityType = "MentorTeachingQualification",
                    EntityId = existing.Id,
                    Action = "Revoked",
                    BeforeJson = JsonSerializer.Serialize(new { MentorId = mentorId, CertificateLevelId = certificateLevelId, LevelCode = existing.CertificateLevel.Code, IsActive = true }),
                    AfterJson = JsonSerializer.Serialize(new { MentorId = mentorId, CertificateLevelId = certificateLevelId, LevelCode = existing.CertificateLevel.Code, IsActive = false }),
                    Reason = request.Reason.Trim(),
                    CreatedAtUtc = now,
                }, token);
                await unitOfWork.SaveChangesAsync(token);
            }, cancellationToken);

            return ApiResponse<bool>.Success(true, message: $"Đã thu hồi quyền giảng dạy {existing.CertificateLevel.Code} (dữ liệu lớp lịch sử được bảo toàn).");
        }

        private static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
        {
            using var hmac = new HMACSHA512();
            salt = hmac.Key;
            hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private static MentorResponse MapToMentorResponse(
            UserAccount u, MentorProfile? profile, IReadOnlyList<string> teachingLevels) => new()
        {
            Id = u.Id,
            Email = u.Email,
            FullName = UserNameHelper.FormatFullName(u.FirstName, u.LastName),
            PhoneNumber = u.PhoneNumber,
            IsEmailVerified = u.IsEmailVerified,
            IsActive = !u.IsDeleted,
            VerificationStatus = (profile?.VerificationStatus ?? MentorVerificationStatus.Unverified).ToString(),
            JlptLevel = profile?.JlptLevel,
            TeachingLevels = teachingLevels,
            CreatedDate = u.CreatedDate,
        };

        private static AdminMentorProfileResponse MapToAdminMentorProfileResponse(UserAccount u, MentorProfile? p) => new()
        {
            MentorId = u.Id,
            Email = u.Email ?? string.Empty,
            FullName = UserNameHelper.FormatFullName(u.FirstName, u.LastName),
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
            CreatedDate = u.CreatedDate,
        };

        private static string GetStatusDescription(MentorVerificationStatus status) => status switch
        {
            MentorVerificationStatus.Unverified => "Chưa nộp hồ sơ",
            MentorVerificationStatus.Pending => "Đang chờ duyệt",
            MentorVerificationStatus.Approved => "Đã phê duyệt",
            MentorVerificationStatus.Rejected => "Đã bị từ chối",
            MentorVerificationStatus.ChangesRequested => "Yêu cầu bổ sung thông tin",
            _ => status.ToString(),
        };
    }
}
