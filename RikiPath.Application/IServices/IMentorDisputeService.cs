using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.IServices;

public interface IMentorDisputeService
{
    // Học viên khởi tạo đơn khiếu nại cho lịch hẹn tư vấn
    Task<ApiResponse<MentorDisputeResponse>> CreateDisputeAsync(
        CreateMentorDisputeRequest request,
        CancellationToken cancellationToken = default);

    // Học viên xem danh sách các khiếu nại do mình tạo
    Task<ApiResponse<List<MentorDisputeResponse>>> GetLearnerDisputesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    // Chuyên gia tư vấn xem danh sách khiếu nại liên quan đến mình
    Task<ApiResponse<List<MentorDisputeResponse>>> GetMentorDisputesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    // Xem chi tiết hồ sơ khiếu nại kèm bằng chứng điểm danh và lịch sử xử lý
    Task<ApiResponse<MentorDisputeDetailResponse>> GetDisputeDetailAsync(
        int disputeId,
        CancellationToken cancellationToken = default);

    // Chuyên gia tư vấn nộp phản hồi / giải trình cho khiếu nại
    Task<ApiResponse<MentorDisputeResponse>> MentorRespondDisputeAsync(
        int disputeId,
        MentorResponseDisputeRequest request,
        CancellationToken cancellationToken = default);

    // Quản trị viên xem hàng đợi các khiếu nại cần xử lý
    Task<ApiResponse<List<MentorDisputeResponse>>> AdminGetDisputeQueueAsync(
        MentorDisputeStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    // Quản trị viên ra phán quyết giải quyết khiếu nại (hoàn tín dụng hoặc bác bỏ)
    Task<ApiResponse<MentorDisputeResponse>> AdminResolveDisputeAsync(
        int disputeId,
        ResolveMentorDisputeRequest request,
        CancellationToken cancellationToken = default);

    // Quản trị viên cập nhật trạng thái vòng đời của đơn khiếu nại
    Task<ApiResponse<MentorDisputeResponse>> AdminUpdateDisputeStatusAsync(
        int disputeId,
        UpdateDisputeStatusRequest request,
        CancellationToken cancellationToken = default);

    // Quản trị viên xem báo cáo chỉ số chất lượng tổng hợp của các Mentor
    Task<ApiResponse<List<MentorQualityMetricResponse>>> AdminGetQualityMetricsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
