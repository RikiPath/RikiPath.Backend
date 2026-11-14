using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Learner")]
public sealed class StandardRoadmapController(
    IStandardRoadmapService service, ILessonProgressService lessonProgress) : ControllerBase
{
    /// <summary>Lộ trình JLPT tiêu chuẩn từ cấp độ Learner tự chọn; không gọi AI và không yêu cầu Placement.</summary>
    /// <remarks>Danh sách rỗng là trạng thái nội dung hợp lệ. Tiến độ lấy từ LessonProgress của JWT owner.</remarks>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Lộ trình tự học theo điểm từng kỹ năng của bài đánh giá gần nhất.</summary>
    [HttpGet("assessment")]
    public async Task<IActionResult> GetAssessmentRoadmap(CancellationToken cancellationToken)
    {
        var result = await service.GetAssessmentRoadmapAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("assessment/lessons/{lessonId:int}")]
    [RequireSelfStudy]
    public async Task<IActionResult> GetAssessmentLesson(int lessonId, CancellationToken cancellationToken)
    {
        var access = await service.CheckAssessmentLessonAsync(lessonId, cancellationToken);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);

        var result = await lessonProgress.GetLessonDetailAsync(lessonId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("assessment/lessons/{lessonId:int}/complete")]
    [RequireSelfStudy]
    public async Task<IActionResult> CompleteAssessmentLesson(int lessonId, CancellationToken cancellationToken)
    {
        var access = await service.CheckAssessmentLessonAsync(lessonId, cancellationToken);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);

        var result = await lessonProgress.CompleteLessonAsync(lessonId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
