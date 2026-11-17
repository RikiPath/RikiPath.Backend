using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
namespace RikiPath.WebApi.Controllers;
[ApiController, Authorize, Route("api/mentor-schedule")]
public sealed class MentorWeeklyScheduleController(IMentorScheduleService service, IMentorCancellationService cancellationService) : ControllerBase
{
    [HttpGet("weekly"), Authorize(Roles = "Mentor,Admin")]
    public async Task<IActionResult> Get(DateOnly weekStart, CancellationToken ct) => Result(await service.GetWeekAsync(weekStart, ct));
    [HttpPost("slots/{id:int}/change"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Change(int id, ChangeMentorSlotRequest request, CancellationToken ct) => Result(await service.RequestChangeAsync(id, request.Reason, ct));
    [HttpPost("slots/{id:int}/resolve"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resolve(int id, ResolveMentorSlotRequest request, CancellationToken ct) => Result(await service.ResolveChangeAsync(id, request, ct));
    [HttpPost("bookings/{id:int}/cancel"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        // Alias cũ chỉ chuyển tiếp đến một luồng hủy chuẩn; tuyệt đối không tự hoàn vé.
        var result = await cancellationService.CancelBookingAsync(id, new CancelMentorBookingRequest(), ct);
        return StatusCode(result.StatusCode, result);
    }
    private IActionResult Result<T>(ApiResponse<T> response) => StatusCode(response.StatusCode, response);
}
