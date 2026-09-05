using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeTests;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class PracticeTestController(IPracticeTestService practiceTestService) : ControllerBase
    {
        [HttpPost("{practiceTestId:int}/start")]
        public async Task<IActionResult> StartAttempt(int practiceTestId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.StartAttemptAsync(userId, practiceTestId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("attempts/{attemptId:int}/submit")]
        public async Task<IActionResult> SubmitAttempt(int attemptId, [FromBody] SubmitAttemptRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.SubmitAttemptAsync(userId, attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("attempts/{attemptId:int}")]
        public async Task<IActionResult> GetAttemptResult(int attemptId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.GetAttemptResultAsync(userId, attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("level/{jlptLevelId:int}/tests")]
        public async Task<IActionResult> GetTestsByLevel(int jlptLevelId, CancellationToken cancellationToken)
        {
            var result = await practiceTestService.GetTestsByLevelAsync(jlptLevelId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("attempts/{attemptId:int}/detailed")]
        public async Task<IActionResult> GetDetailedResult(int attemptId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await practiceTestService.GetDetailedResultAsync(userId, attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
