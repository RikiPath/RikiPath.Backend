using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LearningPathController(ILearningPathService learningPathService) : ControllerBase
    {
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await learningPathService.GenerateLearningPathAsync(userId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }
    }
}
