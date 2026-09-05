using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Lessons;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LessonProgressController(ILessonProgressService lessonProgressService) : ControllerBase
    {
        [HttpGet("lessons/{lessonId:int}")]
        public async Task<IActionResult> GetLessonDetail(int lessonId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await lessonProgressService.GetLessonDetailAsync(userId, lessonId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPatch("lessons/{lessonId:int}/playback-position")]
        public async Task<IActionResult> UpdatePlaybackPosition(int lessonId, [FromBody] UpdatePlaybackPositionRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await lessonProgressService.UpdatePlaybackPositionAsync(userId, lessonId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
