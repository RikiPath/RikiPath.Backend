using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Homework;
using RikiPath.Application.Requests.PracticeExercises;
using RikiPath.Application.Responses;
using RikiPath.Application.Services;

namespace RikiPath.WebApi.Controllers;

// PR #70 integration: các route của teammate là canonical (scan, ai-draft, PUT grade, bookings/*).
// Route Task 02/05 (cohorts, submit-scan, submission-scan, POST grade) giữ làm alias, gọi cùng một method canonical.
[ApiController]
[Route("api/homework")]
[Authorize(Roles = "Learner,Mentor,Admin")]
public sealed class HomeworkController(HomeworkService homework, IPracticeQuizService practice) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? learnerId, [FromQuery] int? bookingId, [FromQuery] int? cohortId,
        CancellationToken ct)
    {
        if (User.IsInRole("Admin") && bookingId is not > 0 && learnerId is not > 0 && cohortId is not > 0)
            return BadRequest(ApiResponse<object>.Fail("Chọn lịch ôn luyện, lớp học hoặc học viên để xem bài tập."));
        return Ok(ApiResponse<object>.Success(await homework.GetAssignmentsAsync(learnerId, ct, cohortId, bookingId)));
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings(CancellationToken ct)
        => Ok(ApiResponse<object>.Success(await homework.ListBookingsAsync(ct)));

    [HttpPut("bookings/{bookingId:int}/evaluation"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Evaluate(int bookingId, EvaluateMentorBookingRequest request, CancellationToken ct)
        => Send(await homework.EvaluateBookingAsync(bookingId, request, ct));

    [HttpGet("bookings/{bookingId:int}/progress")]
    public async Task<IActionResult> Progress(int bookingId, CancellationToken ct)
        => Send(await homework.GetPracticeProgressAsync(bookingId, ct));

    [HttpPost("bookings/{bookingId:int}/progress"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> Record(int bookingId, RecordMentorPracticeResultRequest request, CancellationToken ct)
        => Send(await homework.RecordPracticeProgressAsync(bookingId, request, ct));

    [HttpPut("bookings/{bookingId:int}/review-plan"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> ReviewPlan(int bookingId, SaveMentorReviewPlanRequest request, CancellationToken ct)
        => Send(await homework.SaveReviewPlanAsync(bookingId, request, ct));

    [HttpPost("{assignmentId:int}/scan"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitScan(int assignmentId, IFormFile? file, [FromForm] string? textContent, CancellationToken ct)
        => Send(await homework.SubmitScanAsync(assignmentId, file, ct, textContent));

    // Alias Task 02: form "scan" + "textContent".
    [HttpPost("{assignmentId:int}/submit-scan"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitScanLegacy(int assignmentId, [FromForm] string? textContent, IFormFile? scan,
        CancellationToken ct)
        => Send(await homework.SubmitWithScanAsync(assignmentId, textContent, scan, ct));

    [HttpGet("{assignmentId:int}/scan")]
    [HttpGet("{assignmentId:int}/submission-scan")]
    public async Task<IActionResult> Scan(int assignmentId, CancellationToken ct)
    {
        var file = await homework.ReadScanAsync(assignmentId, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }

    [HttpPost("{assignmentId:int}/ai-draft"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Draft(int assignmentId, CancellationToken ct)
        => Send(await homework.CreateDraftAsync(assignmentId, ct));

    [HttpPut("{assignmentId:int}/grade"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Grade(int assignmentId, PublishHomeworkGradeRequest request, CancellationToken ct)
        => Send(await homework.PublishGradeAsync(assignmentId, request, ct));

    // Alias Task 02: GradeHomeworkAssignmentRequest (Passed/Score/Feedback/PublishAiDraft) → PublishGradeAsync.
    [HttpPost("{assignmentId:int}/grade"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GradeAndPublish(int assignmentId, GradeHomeworkAssignmentRequest request, CancellationToken ct)
        => Send(await homework.GradeAndPublishAsync(assignmentId, request, ct));

    [HttpGet("learners")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Learners(CancellationToken ct, [FromQuery] bool includeAllBookings = false)
        => Ok(ApiResponse<object>.Success(await homework.GetLearnersAsync(ct, includeAllBookings)));

    [HttpGet("catalog")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Catalog([FromQuery] string? level, [FromQuery] string? search, CancellationToken ct,
        [FromQuery] int page = 1)
        => Ok(ApiResponse<object>.Success(await homework.GetCatalogAsync(level, search, page, ct)));

    [HttpPost]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Create(CreateHomeworkRequest request, CancellationToken ct)
        => Send(await homework.CreateAsync(request, ct));

    [HttpPost("cohorts/{cohortId:int}")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> CreateForCohort(int cohortId, CreateHomeworkRequest request, CancellationToken ct)
        => Send(await homework.CreateCohortAssignmentsAsync(cohortId, request, ct));

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
    [HttpPut("{assignmentId:int}/submit")]
    [Authorize(Roles = "Learner")]
    public async Task<IActionResult> Submit(int assignmentId, SubmitHomeworkRequest request, CancellationToken ct)
        => Send(await homework.SubmitAsync(assignmentId, request, ct));

    [HttpGet("report")]
    [Authorize(Roles = "Learner,Mentor")]
    public async Task<IActionResult> Report([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? learnerId,
        CancellationToken ct)
        => Send(await homework.GetReportAsync(from, to, learnerId, ct));

    private IActionResult Send<T>(ApiResponse<T> response) => StatusCode(response.StatusCode, response);
}
