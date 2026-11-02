using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorDiscovery;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorDiscovery;
using RikiPath.Application.Responses.MentorProfile;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/v1/mentors")]
[Route("api/mentors")]
[AllowAnonymous]
public sealed class MentorDiscoveryController(IMentorDiscoveryService discoveryService) : ControllerBase
{
    // Học viên và khách vãng lai tìm kiếm, lọc danh sách chuyên gia đã được phê duyệt công khai.
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PublicMentorCardResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMentors(
        [FromQuery] MentorDiscoveryFilterRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await discoveryService.SearchMentorsAsync(request, cancellationToken));

    // Xem hồ sơ công khai chi tiết của chuyên gia (đã loại bỏ toàn bộ dữ liệu xác minh nội bộ).
    [HttpGet("{mentorId:int}/profile")]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorProfileResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMentorProfile(
        [FromRoute] int mentorId,
        CancellationToken cancellationToken)
        => ToActionResult(await discoveryService.GetMentorProfileAsync(mentorId, cancellationToken));

    // Duyệt danh sách các khung giờ 90 phút khả dụng của chuyên gia theo khoảng ngày (tối đa 30 ngày).
    [HttpGet("{mentorId:int}/slots")]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorSlotsGroupedResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorSlotsGroupedResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorSlotsGroupedResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSlots(
        [FromRoute] int mentorId,
        [FromQuery] MentorSlotBrowsingRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await discoveryService.GetPublicSlotsAsync(mentorId, request, cancellationToken));

    // Lấy danh sách các ngày có ít nhất một khung giờ khả dụng để học viên chọn trên lịch.
    [HttpGet("{mentorId:int}/available-dates")]
    [ProducesResponseType(typeof(ApiResponse<List<DateOnly>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<DateOnly>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<List<DateOnly>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableDates(
        [FromRoute] int mentorId,
        [FromQuery] MentorSlotBrowsingRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await discoveryService.GetAvailableDatesAsync(mentorId, request, cancellationToken));

    // Kiểm tra trực tiếp một khung giờ công khai theo SlotId (kiểm tra trạng thái duyệt và giữ chỗ).
    [HttpGet("slots/{slotId:int}")]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PublicMentorSlotResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicSlotById(
        [FromRoute] int slotId,
        [FromQuery] string? timeZoneId,
        CancellationToken cancellationToken)
        => ToActionResult(await discoveryService.GetPublicSlotByIdAsync(slotId, timeZoneId, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
