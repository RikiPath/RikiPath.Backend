using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices
{
    // // Giao diện dịch vụ quản lý hồ sơ chuyên môn và quy trình xác minh chuyên gia.
    public interface IMentorProfileService
    {
        // Chuyên gia đã đăng nhập lấy hồ sơ cá nhân của mình.
        Task<ApiResponse<MentorProfileResponse>> GetMyProfileAsync(CancellationToken cancellationToken = default);

        // Chuyên gia cập nhật thông tin hồ sơ chuyên môn.
        Task<ApiResponse<MentorProfileResponse>> UpdateMyProfileAsync(UpdateMentorProfileRequest request, CancellationToken cancellationToken = default);

        // Chuyên gia gửi hồ sơ lên Quản trị viên để xét duyệt xác minh.
        Task<ApiResponse<MentorProfileResponse>> SubmitVerificationAsync(SubmitMentorVerificationRequest request, CancellationToken cancellationToken = default);
        Task<ApiResponse<MentorProfileResponse>> UploadVerificationDocumentAsync(
            Stream document, string contentType, CancellationToken cancellationToken = default);

        // Học viên hoặc khách xem hồ sơ công khai của chuyên gia (đã che thông tin nhạy cảm).
        Task<ApiResponse<PublicMentorProfileResponse>> GetPublicProfileAsync(int mentorId, CancellationToken cancellationToken = default);

        // Kiểm tra xem chuyên gia có đủ điều kiện nhận đặt lịch (Approved & Active) hay không.
        Task<bool> IsMentorEligibleForSchedulingAsync(int mentorId, CancellationToken cancellationToken = default);
    }
}
