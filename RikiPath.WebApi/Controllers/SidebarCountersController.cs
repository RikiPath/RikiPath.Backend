using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/me/sidebar-counters")]
[Authorize]
public sealed class SidebarCountersController(ISidebarCounterService sidebarCounterService) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetMyCounters(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        var response = await sidebarCounterService.GetMyCountersAsync(cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
