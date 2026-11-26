using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Domain.Enums;

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
    [RequireFeature(FeatureCodes.StandardRoadmap)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    /// <summary>Lộ trình tự học thích ứng theo mục tiêu, điểm mạnh/yếu từng kỹ năng và tiến độ học tập.</summary>
    [HttpGet("assessment")]
    [HttpGet("adaptive")]
    [RequireFeature(FeatureCodes.StandardRoadmap)]
    public async Task<IActionResult> GetAssessmentRoadmap(CancellationToken cancellationToken)
    {
        var result = await service.GetAssessmentRoadmapAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Khởi tạo / kích hoạt Lộ trình tiêu chuẩn một cách idempotent sau khi đã đủ điều kiện tiên quyết.</summary>
    [HttpPost("activate")]
    [RequireFeature(FeatureCodes.StandardRoadmap)]
    public async Task<IActionResult> Activate(CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("assessment/lessons/{lessonId:int}")]
    [RequireFeature(FeatureCodes.LessonsContent)]
    public async Task<IActionResult> GetAssessmentLesson(int lessonId, CancellationToken cancellationToken)
    {
        var access = await service.CheckAssessmentLessonAsync(lessonId, cancellationToken);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);

        var result = await lessonProgress.GetLessonDetailAsync(lessonId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("assessment/lessons/{lessonId:int}/complete")]
    [RequireFeature(FeatureCodes.LessonsContent)]
    public async Task<IActionResult> CompleteAssessmentLesson(int lessonId, CancellationToken cancellationToken)
    {
        var access = await service.CheckAssessmentLessonAsync(lessonId, cancellationToken);
        if (!access.IsSuccess) return StatusCode(access.StatusCode, access);

        var result = await lessonProgress.CompleteLessonAsync(lessonId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
