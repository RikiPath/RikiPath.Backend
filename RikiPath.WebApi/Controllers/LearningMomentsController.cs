using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningMoments;

namespace RikiPath.WebApi.Controllers
{
    /// <summary>
    /// Learning Moments &amp; Achievements for the signed-in learner. Moments are derived server-side from verified
    /// learning data; the learner can only claim (acknowledge) a moment that is currently eligible for them.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LearningMomentsController(ILearningMomentService learningMomentService) : ControllerBase
    {
        /// <summary>Recent, not-yet-shown celebration moments, ordered by priority.</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
        {
            var result = await learningMomentService.GetPendingAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Atomically claims one moment right before it is shown (other tabs/devices receive claimed = false).</summary>
        [HttpPost("claim")]
        public async Task<IActionResult> Claim([FromBody] ClaimLearningMomentRequest request, CancellationToken cancellationToken)
        {
            var result = await learningMomentService.ClaimAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Achievements already earned (from real data) and the nearest next goals.</summary>
        [HttpGet("achievements")]
        public async Task<IActionResult> GetAchievements(CancellationToken cancellationToken)
        {
            var result = await learningMomentService.GetAchievementsAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
