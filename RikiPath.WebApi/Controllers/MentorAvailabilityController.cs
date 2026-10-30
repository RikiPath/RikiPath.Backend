using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

/// <summary>
/// HTTP endpoints dành cho Mentor quản lý lịch trống và danh sách buổi hẹn tư vấn.
/// </summary>
[ApiController]
[Route("api/mentor/availability")]
[Authorize(Roles = "Mentor")]
public sealed class MentorAvailabilityController(IMentorMeetingService service) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách các khung giờ trống của Mentor đang đăng nhập.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyAvailabilityAsync(cancellationToken));

    /// <summary>
    /// Tạo khung giờ trống mới cho Mentor trong tương lai.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMentorAvailabilityRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.CreateAvailabilityAsync(request, cancellationToken));

    /// <summary>
    /// Xóa/Đóng khung giờ trống chưa có người đặt.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => ToActionResult(await service.DeleteAvailabilityAsync(id, cancellationToken));

    /// <summary>
    /// Gửi yêu cầu hủy/xóa một hoặc nhiều slot trống đã tạo.
    /// </summary>
    [HttpPost("request-cancel")]
    public async Task<IActionResult> RequestCancel(
        [FromBody] CancelAvailabilitiesRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await service.RequestCancelAvailabilitiesAsync(request, cancellationToken));

    /// <summary>
    /// Lấy danh sách các buổi hẹn tư vấn 1-1 đã được Học viên đặt.
    /// </summary>
    [HttpGet("/api/mentor/meetings")]
    public async Task<IActionResult> GetMyMeetings(CancellationToken cancellationToken)
        => ToActionResult(await service.GetMyMeetingsAsync(cancellationToken));

    /// <summary>
    /// Xem chi tiết một buổi hẹn tư vấn theo ID.
    /// </summary>
    [HttpGet("/api/mentor/meetings/{bookingId:int}")]
    public async Task<IActionResult> GetBooking(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await service.GetMentorBookingAsync(bookingId, cancellationToken));

    /// <summary>
    /// Xác nhận chấp nhận lịch hẹn từ Học viên.
    /// </summary>
    [HttpPost("/api/mentor/meetings/{bookingId:int}/accept")]
    public async Task<IActionResult> AcceptBooking(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await service.AcceptMentorBookingAsync(bookingId, cancellationToken));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode((int)response.StatusCode, response);
}