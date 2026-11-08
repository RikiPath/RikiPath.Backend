using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/practice-exercises")]
[Authorize(Roles = "Learner")]
public class PracticeExercisesController(IPracticeQuizService practiceQuizService) : ControllerBase
{
    [HttpGet("lessons/{lessonId:int}")]
    public async Task<IActionResult> GetForLesson(int lessonId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.GetLessonExercisesAsync(lessonId, cancellationToken);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPost("{practiceExerciseId:int}/attempts")]
    public async Task<IActionResult> StartAttempt(int practiceExerciseId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.StartAttemptAsync(practiceExerciseId, cancellationToken);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPost("attempts/{attemptId:int}/submit")]
    public async Task<IActionResult> SubmitAttempt(
        int attemptId, [FromBody] SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.SubmitAttemptAsync(attemptId, request, cancellationToken);
        return StatusCode((int)response.StatusCode, response);
    }
}
