using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Learner")]
public sealed class StandardRoadmapController(IStandardRoadmapService service) : ControllerBase
{
    /// <summary>Lộ trình JLPT tiêu chuẩn từ cấp độ Learner tự chọn; không gọi AI và không yêu cầu Placement.</summary>
    /// <remarks>Danh sách rỗng là trạng thái nội dung hợp lệ. Tiến độ lấy từ LessonProgress của JWT owner.</remarks>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
}
