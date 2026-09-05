using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
    {
        [HttpGet("study-streak")]
        public async Task<IActionResult> GetStudyStreak(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetStudyStreakAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("completion-stats")]
        public async Task<IActionResult> GetCompletionStats(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetCompletionStatsAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("skill-breakdown")]
        public async Task<IActionResult> GetSkillBreakdown(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetSkillBreakdownAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
