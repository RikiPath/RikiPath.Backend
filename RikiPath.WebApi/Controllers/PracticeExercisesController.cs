using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/practice-exercises")]
[Authorize(Roles = "Learner")]
public class PracticeExercisesController(IPracticeQuizService practiceQuizService) : ControllerBase
{
    [HttpGet("lessons/{lessonId:int}")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> GetForLesson(int lessonId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.GetLessonExercisesAsync(lessonId, cancellationToken);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpGet("assessment-roadmap/lessons/{lessonId:int}")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> GetForAssessmentRoadmapLesson(int lessonId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.GetAssessmentRoadmapExercisesAsync(lessonId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{practiceExerciseId:int}/attempts")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> StartAttempt(int practiceExerciseId, CancellationToken cancellationToken,
        [FromQuery] bool fromCatalog = false, [FromQuery] string? skill = null)
    {
        var response = await practiceQuizService.StartAttemptAsync(practiceExerciseId, cancellationToken, fromCatalog, skill);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPost("{practiceExerciseId:int}/assessment-roadmap/attempts")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> StartAssessmentRoadmapAttempt(int practiceExerciseId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.StartAssessmentRoadmapAttemptAsync(practiceExerciseId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("attempts/{attemptId:int}/submit")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> SubmitAttempt(
        int attemptId, [FromBody] SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.SubmitAttemptAsync(attemptId, request, cancellationToken);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPost("assessment-roadmap/attempts/{attemptId:int}/submit")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> SubmitAssessmentRoadmapAttempt(
        int attemptId, [FromBody] SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.SubmitAssessmentRoadmapAttemptAsync(attemptId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("checkpoints")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> GetCheckpoints(
        [FromQuery] int? topicId = null,
        [FromQuery] int? certificateLevelId = null,
        CancellationToken cancellationToken = default)
    {
        var response = await practiceQuizService.GetCheckpointsAsync(topicId, certificateLevelId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("checkpoints/{practiceExerciseId:int}/attempts")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> StartCheckpointAttempt(int practiceExerciseId, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.StartCheckpointAttemptAsync(practiceExerciseId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("checkpoints/attempts/{attemptId:int}/submit")]
    [RequireFeature(FeatureCodes.PracticeExercises)]
    public async Task<IActionResult> SubmitCheckpointAttempt(
        int attemptId, [FromBody] SubmitPracticeExerciseAttemptRequest request, CancellationToken cancellationToken)
    {
        var response = await practiceQuizService.SubmitCheckpointAttemptAsync(attemptId, request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
