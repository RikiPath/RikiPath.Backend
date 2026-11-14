using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.StudyCourses;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController, Authorize(Roles = "Admin,Learner,Mentor"), Route("api/study-courses")]
public sealed class StudyCoursesController(IStudyCourseService service) : ControllerBase
{
    private IActionResult Result(ApiResponse<object> r) => StatusCode(r.StatusCode, r);
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct) => Result(await service.CatalogAsync(ct));
    [HttpGet("admin-catalog"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCatalog(CancellationToken ct) => Result(await service.AdminCatalogAsync(ct));
    [HttpGet("mentors"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Mentors(CancellationToken ct) => Result(await service.MentorsAsync(ct));
    [HttpPost("certificates"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCertificate(SaveCourseCertificate r, CancellationToken ct) => Result(await service.SaveCertificateAsync(null, r, ct));
    [HttpPut("certificates/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveCertificate(int id, SaveCourseCertificate r, CancellationToken ct) => Result(await service.SaveCertificateAsync(id, r, ct));
    [HttpPost("levels"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLevel(SaveCourseLevel r, CancellationToken ct) => Result(await service.SaveLevelAsync(null, r, ct));
    [HttpPut("levels/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveLevel(int id, SaveCourseLevel r, CancellationToken ct) => Result(await service.SaveLevelAsync(id, r, ct));
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) => Result(await service.PlansAsync(ct));
    [HttpPost("plans"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(SaveCoursePlan r, CancellationToken ct) => Result(await service.SavePlanAsync(null, r, ct));
    [HttpPut("plans/{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(int id, SaveCoursePlan r, CancellationToken ct) => Result(await service.SavePlanAsync(id, r, ct));
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Result(await service.CoursesAsync(ct));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => Result(await service.DetailAsync(id, ct));
    [HttpPost("purchase"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> Purchase(PurchaseCourse r, CancellationToken ct) => Result(await service.PurchaseAsync(r, ct));
    [HttpPost("{id:int}/activate"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id, ActivateCourse r, CancellationToken ct) => Result(await service.ActivateAsync(id, r, ct));
    [HttpPost("{id:int}/sessions"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Schedule(int id, ScheduleCourseSession r, CancellationToken ct) => Result(await service.ScheduleAsync(id, r, ct));
    [HttpPost("{id:int}/sessions/{sessionId:int}/attendance"), Authorize(Roles = "Admin,Mentor")]
    public async Task<IActionResult> Attendance(int id, int sessionId, ConfirmCourseAttendance r, CancellationToken ct) => Result(await service.AttendanceAsync(id, sessionId, r, ct));
    [HttpPost("{id:int}/homework"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Assign(int id, AssignCourseHomework r, CancellationToken ct) => Result(await service.AssignAsync(id, r, ct));
    [HttpPost("{id:int}/homework/{homeworkId:int}/submit"), Authorize(Roles = "Learner")]
    public async Task<IActionResult> Submit(int id, int homeworkId, SubmitCourseHomework r, CancellationToken ct) => Result(await service.SubmitHomeworkAsync(id, homeworkId, r, ct));
    [HttpPost("{id:int}/homework/{homeworkId:int}/grade"), Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Grade(int id, int homeworkId, GradeCourseHomework r, CancellationToken ct) => Result(await service.GradeAsync(id, homeworkId, r, ct));
    [HttpPost("{id:int}/complete"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct) => Result(await service.CompleteAsync(id, ct));
    [HttpPost("{id:int}/retake"), Authorize(Roles = "Learner"), RequestSizeLimit(11000000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RequestRetake(int id, [FromForm] DateTime examDate,
        [FromForm] DateTime resultPublishedAt, IFormFile evidence, CancellationToken ct)
        => Result(await service.RequestRetakeAsync(id, examDate, resultPublishedAt, evidence, ct));
    [HttpPost("{id:int}/retake/{requestId:int}/review"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(int id, int requestId, ReviewCourseRetake r, CancellationToken ct)
        => Result(await service.ReviewRetakeAsync(id, requestId, r, ct));
    [HttpGet("{id:int}/retake/{requestId:int}/evidence"), Authorize(Roles = "Admin,Learner")]
    public async Task<IActionResult> Evidence(int id, int requestId, CancellationToken ct)
    {
        var file = await service.EvidenceAsync(id, requestId, ct);
        Response.Headers.CacheControl = "no-store";
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }
}
