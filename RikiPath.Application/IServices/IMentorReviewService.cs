using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;

namespace RikiPath.Application.IServices;

public interface IMentorReviewService
{
    // Học viên gửi đánh giá cho buổi tư vấn đã hoàn thành
    Task<ApiResponse<MentorReviewResponse>> CreateReviewAsync(
        CreateMentorReviewRequest request,
        CancellationToken cancellationToken = default);

    // Học viên cập nhật lại đánh giá của chính mình
    Task<ApiResponse<MentorReviewResponse>> UpdateReviewAsync(
        int reviewId,
        UpdateMentorReviewRequest request,
        CancellationToken cancellationToken = default);

    // Lấy danh sách đánh giá công khai của Mentor kèm phân trang
    Task<ApiResponse<List<MentorReviewResponse>>> GetMentorReviewsAsync(
        int mentorId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    // Lấy thông tin xếp hạng tổng hợp (số sao trung bình, phân bổ sao) của Mentor
    Task<ApiResponse<MentorRatingSummaryResponse>> GetMentorRatingSummaryAsync(
        int mentorId,
        CancellationToken cancellationToken = default);

    // Chuyên gia tư vấn gửi phản hồi cho đánh giá
    Task<ApiResponse<MentorReviewResponse>> ReplyReviewAsync(
        int reviewId,
        MentorReplyReviewRequest request,
        CancellationToken cancellationToken = default);

    // Quản trị viên kiểm duyệt đánh giá (ẩn/hiện, phê duyệt)
    Task<ApiResponse<MentorReviewResponse>> ModerateReviewAsync(
        int reviewId,
        ModerateMentorReviewRequest request,
        CancellationToken cancellationToken = default);
}
