using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.Helpers;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.WebApi.Services;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FileStorageController(IFileStorageService fileStorageService,
        IContentAudioTranscriptionService? transcriptions = null, IClaimService? claims = null,
        ILogger<FileStorageController>? logger = null) : ControllerBase
    {
        private const long MaxUploadBytes = 25 * 1024 * 1024; // 25 MB

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
            ".pdf", ".mp3", ".wav", ".m4a", ".ogg", ".webm", ".mp4",
            ".xlsx", ".xls", ".csv", ".txt", ".docx", ".doc"
        };

        /// <summary>
        /// Upload một file lên storage (kiểm soát kích thước, phần mở rộng và đường dẫn an toàn).
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
                return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp hợp lệ."));
            if (file.Length > MaxUploadBytes)
                return BadRequest(ApiResponse<object>.Fail("Dung lượng tệp vượt quá giới hạn 25 MB."));

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                return BadRequest(ApiResponse<object>.Fail("Định dạng tệp không được phép tải lên."));

            if (!IsSafeSegment(folder, allowEmpty: true))
                return BadRequest(ApiResponse<object>.Fail("Đường dẫn thư mục không hợp lệ."));

            var originalFileName = Path.GetFileName(file.FileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(originalFileName))
                return BadRequest(ApiResponse<object>.Fail("Tên tệp không hợp lệ."));

            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length <= 0)
                return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp hợp lệ."));
            if (buffer.Length > MaxUploadBytes)
                return BadRequest(ApiResponse<object>.Fail("Dung lượng tệp vượt quá giới hạn 25 MB."));

            if (!AttachmentContentInspector.ValidateUploadContent(buffer, originalFileName, out var canonicalMime, out var contentError))
                return BadRequest(ApiResponse<object>.Fail(contentError ?? "Nội dung tệp không khớp với định dạng tệp đã chọn."));

            buffer.Position = 0;
            if (transcriptions is not null && claims is not null &&
                fileStorageService is not LocalDevMediaStorageService &&
                (User.IsInRole("ContentAuthor") || User.IsInRole("Admin")) &&
                ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Ok(await transcriptions.UploadAsync(buffer, originalFileName, claims.GetUserClaim().Id, cancellationToken));
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(ApiResponse<ListeningAudioUploadResponse>.Fail(ex.Message));
                }
                catch (InvalidOperationException)
                {
                    return StatusCode(503, ApiResponse<ListeningAudioUploadResponse>.Fail(
                        "Dịch vụ lưu audio chưa được cấu hình đầy đủ.", System.Net.HttpStatusCode.ServiceUnavailable,
                        "AUDIO_STORAGE_NOT_CONFIGURED"));
                }
                catch (Amazon.Runtime.AmazonServiceException ex)
                {
                    var failure = AwsAudioUploadErrors.Create<ListeningAudioUploadResponse>(ex, logger);
                    return StatusCode(failure.StatusCode, failure);
                }
            }

            try
            {
                buffer.Position = 0;
                var result = await fileStorageService.UploadAsync(buffer, originalFileName, canonicalMime, folder.Trim(), cancellationToken);
                return Ok(result with
                {
                    OriginalFileName = originalFileName,
                    ContentType = canonicalMime,
                });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException)
            {
                return StatusCode(503, new { message = "Dịch vụ lưu file chưa được cấu hình đầy đủ." });
            }
            catch (Amazon.Runtime.AmazonServiceException ex)
            {
                var failure = AwsAudioUploadErrors.Create<UploadedFileResult>(ex, logger);
                return StatusCode(failure.StatusCode, failure);
            }
        }

        /// <summary>
        /// Đọc tệp media cục bộ chỉ trong chế độ Development LocalDevMediaStorage.
        /// </summary>
        [HttpGet("local/{folder}/{fileName}")]
        [AllowAnonymous]
        public IActionResult GetLocalMedia(string folder, string fileName)
        {
            if (string.Equals(folder?.Trim(), "mentor-proposals", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            if (fileStorageService is not LocalDevMediaStorageService localDev ||
                !localDev.TryOpenPublicMedia(folder, fileName, out var stream, out var contentType) ||
                stream is null)
            {
                return NotFound();
            }

            return File(stream, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// Xoá file đã lưu trên storage (chỉ Admin hoặc ContentAuthor để ngăn IDOR/xóa file chéo người dùng).
        /// </summary>
        [HttpDelete]
        [Authorize(Roles = "Admin,ContentAuthor")]
        public async Task<IActionResult> Delete([FromQuery] string storedFileName, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            if (!IsSafeSegment(storedFileName, allowEmpty: false) || !IsSafeSegment(folder, allowEmpty: true))
                return BadRequest(ApiResponse<object>.Fail("Tên tệp hoặc thư mục không hợp lệ."));

            try
            {
                await fileStorageService.DeleteAsync(storedFileName.Trim(), folder.Trim(), cancellationToken);
                return NoContent();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        private static bool IsSafeSegment(string? value, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value)) return allowEmpty;
            var trimmed = value.Trim();
            if (trimmed.Contains("..", StringComparison.Ordinal) ||
                trimmed.Contains('\\', StringComparison.Ordinal) ||
                trimmed.Contains('\0', StringComparison.Ordinal) ||
                trimmed.StartsWith('/'))
            {
                return false;
            }
            return trimmed.Length <= 180;
        }
    }
}
