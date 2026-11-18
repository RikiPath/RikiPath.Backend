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

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
