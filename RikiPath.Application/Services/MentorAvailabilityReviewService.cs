using System.Net;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public sealed class MentorAvailabilityReviewService(IUnitOfWork unitOfWork, IClaimService claimService)
    : IMentorAvailabilityReviewService
{
    public async Task<ApiResponse<List<MentorAvailabilityResponse>>> GetSlotsAsync(CancellationToken cancellationToken)
    {
        if (claimService.GetUserClaim().Role != Role.Admin)
            return ApiResponse<List<MentorAvailabilityResponse>>.Fail("Chỉ Admin mới được duyệt lịch Mentor.", HttpStatusCode.Forbidden);
        var slots = await unitOfWork.MentorAvailabilities.FindAsync(x => !x.IsDeleted, cancellationToken);
        var mentors = await unitOfWork.UserAccounts.FindAsync(x => x.Role == Role.Mentor, cancellationToken);
        var names = mentors.ToDictionary(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim());
        return ApiResponse<List<MentorAvailabilityResponse>>.Success(slots
            .OrderBy(x => x.IsApproved || x.RejectedAt != null).ThenBy(x => x.StartTime)
            .Select(x => Map(x, names.GetValueOrDefault(x.MentorId) ?? $"Mentor #{x.MentorId}")).ToList());
    }

    public async Task<ApiResponse<MentorAvailabilityResponse>> ReviewAsync(int id, bool approve, string? reason, CancellationToken cancellationToken)
    {
        var claims = claimService.GetUserClaim();
        if (claims.Role != Role.Admin)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Chỉ Admin mới được duyệt lịch Mentor.", HttpStatusCode.Forbidden);
        var normalizedReason = reason?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(normalizedReason))
            return ApiResponse<MentorAvailabilityResponse>.Fail("Vui lòng nhập lý do từ chối.");
        if (normalizedReason?.Length > 1000)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lý do tối đa 1000 ký tự.");
        var slot = await unitOfWork.MentorAvailabilities.GetByIdAsync(id, cancellationToken);
        if (slot is null || slot.IsDeleted)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không tìm thấy lịch Mentor.", HttpStatusCode.NotFound);
        if (slot.IsApproved || slot.RejectedAt != null)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Lịch đã được xử lý. Vui lòng tải lại danh sách.", HttpStatusCode.Conflict);
        if (slot.StartTime <= DateTime.UtcNow)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể duyệt lịch đã bắt đầu hoặc hết hạn.", HttpStatusCode.Conflict);
        if (slot.BookedCount > 0)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Không thể thay đổi duyệt lịch đã có học viên đặt.", HttpStatusCode.Conflict);
        var mentor = await unitOfWork.UserAccounts.GetByIdAsync(slot.MentorId, cancellationToken);
        if (mentor is null || mentor.IsDeleted || mentor.Role != Role.Mentor)
            return ApiResponse<MentorAvailabilityResponse>.Fail("Mentor không còn hoạt động.", HttpStatusCode.Conflict);
        var admin = await unitOfWork.UserAccounts.GetByIdAsync(claims.Id, cancellationToken);
        slot.IsApproved = approve;
        slot.AdminName = admin is null ? $"Admin #{claims.Id}" : $"{admin.FirstName} {admin.LastName}".Trim();
        slot.RejectedAt = approve ? null : DateTime.UtcNow;
        slot.RejectionReason = approve ? null : normalizedReason;
        slot.ModifiedDate = DateTime.UtcNow;
        unitOfWork.MentorAvailabilities.Update(slot);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<MentorAvailabilityResponse>.Success(Map(slot, $"{mentor.FirstName} {mentor.LastName}".Trim()));
    }

    private static MentorAvailabilityResponse Map(MentorAvailability slot, string mentorName) => new()
    {
        Id = slot.Id, MentorId = slot.MentorId, MentorName = mentorName,
        StartTime = slot.StartTime, EndTime = slot.EndTime, IsApproved = slot.IsApproved,
        RejectedAt = slot.RejectedAt, RejectionReason = slot.RejectionReason,
        IsBooked = slot.IsFull, BookedCount = slot.BookedCount, MaxLearners = slot.MaxLearners,
        AdminName = slot.AdminName
    };
}
