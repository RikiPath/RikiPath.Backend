using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Reviews;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class ReviewController(IReviewService reviewService) : ControllerBase
    {
        [HttpGet("daily-queue")]
        public async Task<IActionResult> GetDailyQueue(CancellationToken cancellationToken)
        {
            var response = await reviewService.GetDailyReviewQueueAsync(GetCurrentUserId(), cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("submit-result")]
        public async Task<IActionResult> SubmitResult([FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
        {
            var response = await reviewService.SubmitReviewResultAsync(GetCurrentUserId(), request, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        private int GetCurrentUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
