using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
public sealed class ContentTemplatesAndExportController(
    ISkillTemplateService templateService,
    IUnifiedImportService unifiedImportService,
    IContentExportService exportService,
    IClaimService claimService) : ControllerBase
{
    [HttpGet("api/content-author/templates/{templateKind}")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public IActionResult DownloadTemplate(string templateKind, [FromQuery] string format = "xlsx")
    {
        try
        {
            var file = templateService.GenerateTemplate(templateKind, format);
            return File(file.Bytes, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("api/content-import/unified/preview")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> PreviewUnifiedImport(
        IFormFile? file,
        [FromQuery] UnifiedImportKind expectedKind = UnifiedImportKind.Auto,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<UnifiedImportPreviewResponse>.Fail("Vui lòng chọn tệp để xem trước."));

        await using var stream = file.OpenReadStream();
        var res = await unifiedImportService.PreviewAsync(stream, file.FileName, expectedKind, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("api/content-import/unified/confirm")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> ConfirmUnifiedImport(
        [FromBody] ConfirmUnifiedImportRequest request,
        CancellationToken ct)
    {
        var res = await unifiedImportService.ConfirmAsync(request, null, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("api/content-import/unified/confirm-archive")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> ConfirmUnifiedArchive(
        IFormFile? file,
        [FromQuery] UnifiedImportKind expectedKind = UnifiedImportKind.Auto,
        [FromQuery] string? idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<ConfirmUnifiedImportResponse>.Fail("Vui lòng chọn file .zip hợp lệ."));

        await using var stream = file.OpenReadStream();
        var res = await unifiedImportService.ConfirmFromArchiveAsync(
            stream, file.FileName, expectedKind, idempotencyKey, claimService.GetUserClaim().Id, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpPost("api/content-import/verify-roundtrip/{examId:int}")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> VerifyRoundTrip(
        int examId,
        [FromQuery] string format = "xlsx",
        CancellationToken ct = default)
    {
        var isPrivileged = User.IsInRole("ContentAuthor") || User.IsInRole("Admin");
        var res = await exportService.VerifyExamRoundTripAsync(examId, format, claimService.GetUserClaim().Id, isPrivileged, ct);
        return StatusCode(res.StatusCode, res);
    }

    [HttpGet("api/content-export/exams/{examId:int}")]
    [Authorize]
    public async Task<IActionResult> ExportExam(
        int examId,
        [FromQuery] string format = "pdf",
        [FromQuery] bool includeAnswers = false,
        CancellationToken ct = default)
    {
        var isAuthorOrAdmin = User.IsInRole("ContentAuthor") || User.IsInRole("Admin");
        var res = await exportService.ExportExamAsync(examId, format, includeAnswers, claimService.GetUserClaim().Id, isAuthorOrAdmin, ct);
        if (!res.IsSuccess || res.Result is null)
            return StatusCode(res.StatusCode, ApiResponse<object>.Fail(res.ErrorMessage ?? "Không thể xuất đề.", (System.Net.HttpStatusCode)res.StatusCode, errorCode: res.ErrorCode));

        return File(res.Result.Bytes, res.Result.ContentType, res.Result.FileName);
    }

    [HttpGet("api/content-export/question-bank")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public async Task<IActionResult> ExportQuestionBank(
        [FromQuery] int? certificateLevelId,
        [FromQuery] int? languageSkillId,
        [FromQuery] string? questionType,
        [FromQuery] string? keyword,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool includeAnswers = true,
        CancellationToken ct = default)
    {
        var filter = new QuestionBankQueryFilter
        {
            CertificateLevelId = certificateLevelId,
            LanguageSkillId = languageSkillId,
            QuestionType = questionType,
            Keyword = keyword,
        };
        var res = await exportService.ExportQuestionBankAsync(
            filter, format, includeAnswers, claimService.GetUserClaim().Id, true, ct);
        if (!res.IsSuccess || res.Result is null)
            return StatusCode(res.StatusCode, ApiResponse<object>.Fail(res.ErrorMessage ?? "Không thể xuất ngân hàng câu hỏi.", (System.Net.HttpStatusCode)res.StatusCode, errorCode: res.ErrorCode));

        return File(res.Result.Bytes, res.Result.ContentType, res.Result.FileName);
    }
}
