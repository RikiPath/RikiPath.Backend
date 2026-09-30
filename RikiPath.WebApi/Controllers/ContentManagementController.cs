using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.DTOs.Content;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;

namespace RikiPath.WebApi.Controllers;

/// <summary>Các API import, gửi duyệt và kiểm duyệt nội dung.</summary>
[ApiController]
[Route("api/content-management")]
[Authorize]
public class ContentManagementController(IContentManagementService contentManagementService, IContentAuthorService contentAuthorService) : ControllerBase
{
    /// <summary>Admin lấy toàn bộ thông tin một nội dung cùng cấu trúc con để kiểm tra trước khi duyệt.</summary>
    /// <param name="entityType">Loại nội dung.</param>
    /// <param name="entityId">ID nội dung.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpGet("admin/details/{entityType}/{entityId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetContentForReview(ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
    {
        var result = await contentAuthorService.GetForAdminAsync(entityType, entityId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Import nhiều loại nội dung từ một sheet Excel; cột Type xác định loại của từng dòng.</summary>
    /// <remarks>
    /// Type nhận Lesson, Kanji, KanaCharacter, Vocabulary, GrammarPattern, MockTest, MockQuestion hoặc PracticeExercise.
    /// Các loại Lesson, Kanji, Vocabulary, GrammarPattern, MockTest và PracticeExercise cần cột CertificateType (tên loại trong DB)
    /// và CertificateLevel (mã cấp độ thuộc loại đó, ví dụ JLPT/N5). Cột bắt buộc khác: Lesson = Title, LanguageSkillId;
    /// Kanji = Character, Meaning, StrokeCount; KanaCharacter = Character, KanaType (Hiragana/Katakana), Romaji, StrokeCount;
    /// Vocabulary = Word, Reading, Meaning; GrammarPattern = Title, Structure.
    /// Các dòng Kanji, Vocabulary, GrammarPattern và PracticeExercise được gắn với dòng Lesson gần nhất phía trên
    /// trong cùng file, cho đến khi gặp dòng Lesson tiếp theo. Hệ thống tự dùng ID Lesson do database sinh ra;
    /// không cần cột LessonId trong Excel. Các dòng nội dung này đặt trước Lesson sẽ không được gắn vào Lesson.
    /// MockTest = Title, TimeLimitMinutes; PracticeExercise = Title, LanguageSkillId.
    /// MockQuestion = QuestionText, MockTestSectionId (lấy cấp độ từ đề thi cha).
    /// LanguageSkillId và MockTestSectionId phải là ID đã có trong DB.
    /// MockQuestion dùng MockTestSectionId có sẵn thuộc đề thi của tác giả.
    /// Dữ liệu import ở trạng thái Draft. Các cột không áp dụng cho loại dòng đó được bỏ qua.
    /// </remarks>
    /// <param name="file">File Excel .xlsx cần import.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpPost("bulk-import")]
    [Authorize(Roles = "ContentAuthor")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkImport(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn file import." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Chỉ hỗ trợ file Excel .xlsx." });

        await using var stream = file.OpenReadStream();
        var result = await contentManagementService.BulkImportAsync(new BulkImportRequest
        {
            FileStream = stream
        }, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Content Author lấy danh sách nội dung của mình theo loại nội dung.</summary>
    /// <param name="entityType">Loại nội dung.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpGet("author/mine/{entityType}")]
    [Authorize(Roles = "ContentAuthor")]
    public async Task<IActionResult> GetMyContent(ContentEntityType entityType, CancellationToken cancellationToken)
    {
        var result = await contentManagementService.GetMyContentAsync(entityType, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Gửi nội dung của Content Author sang hàng chờ Admin kiểm duyệt.</summary>
    /// <param name="entityType">Loại nội dung.</param>
    /// <param name="entityId">ID nội dung cần gửi duyệt.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpPost("author/submit/{entityType}/{entityId:int}")]
    [Authorize(Roles = "ContentAuthor")]
    public async Task<IActionResult> SubmitForReview(
        ContentEntityType entityType, int entityId, CancellationToken cancellationToken)
    {
        var result = await contentManagementService.SubmitForReviewAsync(entityType, entityId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Admin lấy danh sách nội dung đang chờ kiểm duyệt theo loại.</summary>
    /// <param name="entityType">Loại nội dung cần xem.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpGet("admin/pending/{entityType}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPendingReview(ContentEntityType entityType, CancellationToken cancellationToken)
    {
        var result = await contentManagementService.GetPendingReviewAsync(entityType, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Admin duyệt hoặc từ chối nội dung; khi từ chối có thể kèm lý do.</summary>
    /// <param name="entityType">Loại nội dung.</param>
    /// <param name="entityId">ID nội dung cần kiểm duyệt.</param>
    /// <param name="request">Kết quả duyệt và lý do từ chối nếu có.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpPost("admin/review/{entityType}/{entityId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review(
        ContentEntityType entityType,
        int entityId,
        [FromBody] ReviewContentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contentManagementService.ReviewAsync(entityType, entityId, request, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
