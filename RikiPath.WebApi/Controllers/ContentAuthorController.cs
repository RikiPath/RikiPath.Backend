using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application;

namespace RikiPath.WebApi.Controllers;

/// <summary>Các API để Content Author tra cứu dữ liệu và quản lý nội dung do chính mình tạo.</summary>
[ApiController]
[Route("api/content-author")]
[Authorize(Roles = "ContentAuthor")]
public class ContentAuthorController(IContentAuthorService authorService, IContentManagementService workflowService, IUnitOfWork uow) : ControllerBase
{
    /// <summary>Lấy danh mục loại chứng chỉ, cấp độ chứng chỉ và kỹ năng ngôn ngữ dùng khi tạo nội dung.</summary>
    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
    {
        var types = (await uow.CertificateTypes.GetAllAsync(cancellationToken)).Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Name }).ToList();
        var levels = (await uow.CertificateLevels.GetAllAsync(cancellationToken)).Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Code, x.CertificateTypeId }).ToList();
        var skills = (await uow.LanguageSkills.GetAllAsync(cancellationToken)).Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Name }).ToList();
        return Ok(new { CertificateTypes = types, CertificateLevels = levels, LanguageSkills = skills });
    }
    /// <summary>Lấy danh sách nội dung của Content Author hiện tại theo loại nội dung.</summary>
    /// <param name="type">Loại nội dung, ví dụ Lesson, Kanji, Vocabulary, GrammarPattern, MockTest hoặc PracticeExercise.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpGet("content/{type}")]
    public async Task<IActionResult> GetMine(ContentEntityType type, CancellationToken cancellationToken)
    {
        var result = await workflowService.GetMyContentAsync(type, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Lấy chi tiết một nội dung thuộc sở hữu của Content Author hiện tại.</summary>
    /// <param name="type">Loại nội dung.</param>
    /// <param name="id">ID nội dung.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpGet("content/{type}/{id:int}")]
    public async Task<IActionResult> GetMineById(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        var result = await authorService.GetByIdAsync(type, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Tạo nội dung mới ở trạng thái Draft.</summary>
    /// <param name="type">Loại nội dung cần tạo.</param>
    /// <param name="request">Thông tin nội dung; các trường áp dụng tùy theo loại nội dung.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpPost("content/{type}")]
    public async Task<IActionResult> Create(ContentEntityType type, [FromBody] AuthorContentRequest request, CancellationToken cancellationToken)
    {
        var result = await authorService.SaveAsync(type, null, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Cập nhật nội dung của Content Author khi nội dung còn Draft hoặc bị từ chối.</summary>
    /// <param name="type">Loại nội dung.</param>
    /// <param name="id">ID nội dung cần cập nhật.</param>
    /// <param name="request">Thông tin nội dung mới.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpPut("content/{type}/{id:int}")]
    public async Task<IActionResult> Update(ContentEntityType type, int id, [FromBody] AuthorContentRequest request, CancellationToken cancellationToken)
    {
        var result = await authorService.SaveAsync(type, id, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Xóa mềm nội dung thuộc Content Author hiện tại.</summary>
    /// <param name="type">Loại nội dung.</param>
    /// <param name="id">ID nội dung cần xóa.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpDelete("content/{type}/{id:int}")]
    public async Task<IActionResult> Delete(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        var result = await authorService.DeleteAsync(type, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
