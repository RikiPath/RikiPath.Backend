using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorKpi;

namespace RikiPath.Application.IServices;

/// <summary>
/// Tổng hợp chấm công, thời lượng giảng dạy và chỉ số KPI thực tế của Cố vấn (không tính lương/thù lao).
/// </summary>
public interface IMentorKpiService
{
    Task<ApiResponse<MentorKpiSummaryResponse>> GetMyKpiSummaryAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorKpiSummaryResponse>>> GetAdminMentorKpisAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        int? mentorId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorKpiSummaryResponse>> GetAdminMentorKpiByMentorAsync(
        int mentorId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorBookingAttendanceDetailResponse>> GetAdminBookingAttendanceAsync(
        int bookingId,
        CancellationToken cancellationToken = default);
}
