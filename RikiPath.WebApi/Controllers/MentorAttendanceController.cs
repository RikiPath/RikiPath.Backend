using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
public sealed class MentorAttendanceController(IMentorAttendanceService attendanceService) : ControllerBase
{
    // Kiểm tra quyền vào phòng họp và trạng thái sẵn sàng của phòng
    [HttpGet("api/v1/mentor-meetings/rooms/{roomId:guid}/access")]
    [HttpGet("api/mentor-meetings/rooms/{roomId:guid}/access")]
    [ProducesResponseType(typeof(ApiResponse<RoomAccessCheckResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RoomAccessCheckResponse>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckRoomAccess(
        [FromRoute] Guid roomId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.CheckRoomAccessAsync(roomId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Lấy hồ sơ điểm danh bền vững của phòng họp
    [HttpGet("api/v1/mentor-meetings/rooms/{roomId:guid}/attendance")]
    [HttpGet("api/mentor-meetings/rooms/{roomId:guid}/attendance")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorAttendanceResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorAttendanceResponse>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoomAttendance(
        [FromRoute] Guid roomId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetRoomAttendanceAsync(roomId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Lấy thông tin hiện diện trực tuyến theo thời gian thực trong phòng họp
    [HttpGet("api/v1/mentor-meetings/rooms/{roomId:guid}/presence")]
    [HttpGet("api/mentor-meetings/rooms/{roomId:guid}/presence")]
    [ProducesResponseType(typeof(ApiResponse<RoomPresenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<RoomPresenceResponse>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoomPresence(
        [FromRoute] Guid roomId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetRoomPresenceAsync(roomId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Lấy lịch sử điểm danh theo mã lịch hẹn tư vấn
    [HttpGet("api/v1/mentor-meetings/bookings/{bookingId:int}/attendance")]
    [HttpGet("api/mentor-meetings/bookings/{bookingId:int}/attendance")]
    [ProducesResponseType(typeof(ApiResponse<List<MentorAttendanceResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorAttendanceResponse>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<List<MentorAttendanceResponse>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingAttendance(
        [FromRoute] int bookingId,
        CancellationToken cancellationToken)
    {
        var result = await attendanceService.GetBookingAttendanceAsync(bookingId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
