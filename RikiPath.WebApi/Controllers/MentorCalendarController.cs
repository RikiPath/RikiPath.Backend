using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.WebApi.Controllers;

[ApiController, Route("api/mentor/calendar"), Authorize(Roles = "Mentor")]
public sealed class MentorCalendarController(IMentorCalendarService service, IClaimService claims) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(new { Result = await service.StatusAsync(claims.GetUserClaim().Id, ct) });
    [HttpPost("connect")]
    public async Task<IActionResult> Connect(CancellationToken ct)
    {
        if (!service.IsConfigured) return Conflict(new { ErrorMessage = "Tính năng kết nối Google Calendar hiện chưa sẵn sàng." });
        return Ok(new { Result = new { Url = await service.ConnectAsync(claims.GetUserClaim().Id, ct) } });
    }
    [HttpPost("callback")]
    public async Task<IActionResult> Callback(CalendarCallback request, CancellationToken ct)
    {
        try { await service.CompleteAsync(claims.GetUserClaim().Id, request.State, request.Code, ct); return Ok(new { Result = true }); }
        catch (Exception) when (!ct.IsCancellationRequested) { return BadRequest(new { ErrorMessage = "Không kết nối được Google Calendar. Phiên có thể đã hết hạn; vui lòng kết nối lại." }); }
    }
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        if (!service.IsConfigured) return Conflict(new { ErrorMessage = "Chưa cấu hình Google Calendar." });
        try { await service.SyncAsync(claims.GetUserClaim().Id, ct); return Ok(new { Result = true }); }
        catch (Exception) when (!ct.IsCancellationRequested) { return StatusCode(502, new { ErrorMessage = "Chưa đồng bộ được Google Calendar. Vui lòng kiểm tra quyền kết nối và thử lại." }); }
    }
    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await service.DisconnectAsync(claims.GetUserClaim().Id, ct); return Ok(new { Result = true });
    }
}

public sealed record CalendarCallback(string State, string Code);
