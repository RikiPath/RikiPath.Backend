using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
namespace RikiPath.WebApi.Controllers;
[ApiController, Authorize(Roles = "Admin"), Route("api/admin/mentor-plans")]
public sealed class AdminMentorPlansController(IMentorPlanService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) { var r = await service.GetAsync(ct); return StatusCode(r.StatusCode, r); }
    [HttpPost]
    public async Task<IActionResult> Create(SaveMentorPlanRequest request, CancellationToken ct) { var r = await service.SaveAsync(null, request, ct); return StatusCode(r.StatusCode, r); }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveMentorPlanRequest request, CancellationToken ct) { var r = await service.SaveAsync(id, request, ct); return StatusCode(r.StatusCode, r); }
}
