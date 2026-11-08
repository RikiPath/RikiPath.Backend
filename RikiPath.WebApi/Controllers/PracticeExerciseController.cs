using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class PracticeExerciseController(
    IPracticeExerciseService practiceExerciseService,
    IClaimService claimService) : ControllerBase
    {
        [HttpGet("lesson/{lessonId:int}")]
        public async Task<IActionResult> GetByLesson(int lessonId, CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var result = await practiceExerciseService.GetByLessonAsync(lessonId, userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("{practiceExerciseId:int}/start")]
        public async Task<IActionResult> StartAttempt(int practiceExerciseId, CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var result = await practiceExerciseService.StartAttemptAsync(practiceExerciseId, userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("attempt/{attemptId:int}/submit")]
        public async Task<IActionResult> SubmitAttempt(
            int attemptId,
            [FromBody] SubmitPracticeAttemptRequest request,
            CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var result = await practiceExerciseService.SubmitAttemptAsync(attemptId, userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
