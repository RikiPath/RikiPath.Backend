using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
public sealed class MentorDisputeController(IMentorDisputeService disputeService) : ControllerBase
{
    // Học viên khởi tạo đơn khiếu nại cho lịch hẹn tư vấn
    [HttpPost("api/mentor-disputes")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDispute(
        [FromBody] CreateMentorDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await disputeService.CreateDisputeAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên xem danh sách các đơn khiếu nại của mình
    [HttpGet("api/learner/mentor-disputes")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorDisputeResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLearnerDisputes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await disputeService.GetLearnerDisputesAsync(page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Mentor xem danh sách các khiếu nại liên quan đến mình
    [HttpGet("api/mentor/mentor-disputes")]
    [Authorize(Roles = "Mentor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorDisputeResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentorDisputes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await disputeService.GetMentorDisputesAsync(page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Xem chi tiết hồ sơ khiếu nại kèm bằng chứng điểm danh (Stage 09) và trạng thái hủy (Stage 10)
    [HttpGet("api/mentor-disputes/{disputeId:int}")]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeDetailResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeDetailResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDisputeDetail(
        [FromRoute] int disputeId,
        CancellationToken cancellationToken)
    {
        var result = await disputeService.GetDisputeDetailAsync(disputeId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Mentor nộp bản giải trình phản hồi khiếu nại
    [HttpPost("api/mentor-disputes/{disputeId:int}/respond")]
    [Authorize(Roles = "Mentor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MentorRespond(
        [FromRoute] int disputeId,
        [FromBody] MentorResponseDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await disputeService.MentorRespondDisputeAsync(disputeId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên xem hàng đợi các đơn khiếu nại cần xử lý
    [HttpGet("api/admin/mentor-disputes/queue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorDisputeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorDisputeResponse>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AdminGetQueue(
        [FromQuery] MentorDisputeStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await disputeService.AdminGetDisputeQueueAsync(status, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên ra phán quyết giải quyết tranh chấp (hoàn tín dụng hoặc từ chối)
    [HttpPost("api/admin/mentor-disputes/{disputeId:int}/resolve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdminResolve(
        [FromRoute] int disputeId,
        [FromBody] ResolveMentorDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await disputeService.AdminResolveDisputeAsync(disputeId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên chuyển đổi trạng thái vòng đời của đơn khiếu nại
    [HttpPost("api/admin/mentor-disputes/{disputeId:int}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorDisputeResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminUpdateStatus(
        [FromRoute] int disputeId,
        [FromBody] UpdateDisputeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await disputeService.AdminUpdateDisputeStatusAsync(disputeId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên xem báo cáo các chỉ số chất lượng hoạt động của các Chuyên gia
    [HttpGet("api/admin/mentor-quality/metrics")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorQualityMetricResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorQualityMetricResponse>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AdminGetQualityMetrics(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await disputeService.AdminGetQualityMetricsAsync(page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
