using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/homework")]
[Authorize(Roles = "Learner,Mentor")]
public sealed class HomeworkController(HomeworkService homework, IPracticeQuizService practice) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? learnerId, CancellationToken ct)
        => Ok(ApiResponse<object>.Success(await homework.GetAssignmentsAsync(learnerId, ct)));

    [HttpGet("learners")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Learners(CancellationToken ct)
        => Ok(ApiResponse<object>.Success(await homework.GetLearnersAsync(ct)));

    [HttpGet("catalog")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Catalog([FromQuery] string? level, [FromQuery] string? search, CancellationToken ct,
        [FromQuery] int page = 1)
        => Ok(ApiResponse<object>.Success(await homework.GetCatalogAsync(level, search, page, ct)));

    [HttpPost]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Create(CreateHomeworkRequest request, CancellationToken ct)
        => Send(await homework.CreateAsync(request, ct));

    [HttpGet("{assignmentId:int}/lessons/{lessonId:int}")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Lesson(int assignmentId, int lessonId, CancellationToken ct)
        => Send(await homework.GetAssignedLessonAsync(assignmentId, lessonId, ct));

    [HttpPost("{assignmentId:int}/exercises/{exerciseId:int}/start")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> StartExercise(int assignmentId, int exerciseId, CancellationToken ct)
        => Send(await practice.StartHomeworkAttemptAsync(assignmentId, exerciseId, ct));

    [HttpPost("{assignmentId:int}/exercise-attempts/{attemptId:int}/submit")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> SubmitExercise(int assignmentId, int attemptId, SubmitPracticeExerciseAttemptRequest request,
        CancellationToken ct)
        => Send(await practice.SubmitHomeworkAttemptAsync(assignmentId, attemptId, request, ct));

    [HttpPost("{assignmentId:int}/submit")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Submit(int assignmentId, SubmitHomeworkRequest request, CancellationToken ct)
        => Send(await homework.SubmitAsync(assignmentId, request, ct));

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? learnerId,
        CancellationToken ct)
        => Send(await homework.GetReportAsync(from, to, learnerId, ct));

    private IActionResult Send<T>(ApiResponse<T> response) => StatusCode(response.StatusCode, response);
}
