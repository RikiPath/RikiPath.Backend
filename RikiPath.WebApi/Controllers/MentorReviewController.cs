using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorQuality;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorQuality;

namespace RikiPath.WebApi.Controllers;

[ApiController]
public sealed class MentorReviewController(IMentorReviewService reviewService) : ControllerBase
{
    // Học viên tạo đánh giá cho buổi tư vấn đã hoàn thành
    [HttpPost("api/mentor-reviews")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateMentorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.CreateReviewAsync(request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên chỉnh sửa đánh giá của chính mình
    [HttpPut("api/mentor-reviews/{reviewId:int}")]
    [Authorize(Roles = "Learner,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(
        [FromRoute] int reviewId,
        [FromBody] UpdateMentorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.UpdateReviewAsync(reviewId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Khách hoặc học viên xem danh sách đánh giá công khai của Mentor
    [HttpGet("api/mentors/{mentorId:int}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<MentorReviewResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorReviewResponse>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMentorReviews(
        [FromRoute] int mentorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await reviewService.GetMentorReviewsAsync(mentorId, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Lấy thông tin xếp hạng tổng hợp (sao trung bình, biểu đồ sao) của Mentor
    [HttpGet("api/mentors/{mentorId:int}/rating-summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<MentorRatingSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorRatingSummaryResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMentorRatingSummary(
        [FromRoute] int mentorId,
        CancellationToken cancellationToken = default)
    {
        var result = await reviewService.GetMentorRatingSummaryAsync(mentorId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Mentor nộp phản hồi chính thức cho đánh giá
    [HttpPost("api/mentor-reviews/{reviewId:int}/reply")]
    [Authorize(Roles = "Mentor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplyReview(
        [FromRoute] int reviewId,
        [FromBody] MentorReplyReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.ReplyReviewAsync(reviewId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên kiểm duyệt đánh giá (ẩn/hiện, phê duyệt)
    [HttpPost("api/admin/mentor-reviews/{reviewId:int}/moderate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<MentorReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateReview(
        [FromRoute] int reviewId,
        [FromBody] ModerateMentorReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reviewService.ModerateReviewAsync(reviewId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
