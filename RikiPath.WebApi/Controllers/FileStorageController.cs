using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using Microsoft.AspNetCore.Http;
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
        /// <summary>
        /// Upload một file lên storage (S3 hoặc provider cấu hình).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Các form upload file trong ứng dụng (ví dụ upload ảnh avatar, tài liệu bài tập).
        /// - Luồng xử lý: API nhận multipart/form-data, stream file lên storage provider (S3/GCS/Local), trả về URL hoặc tên file lưu trữ.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Authorize). Gửi request dưới dạng multipart/form-data với key `file`. File có thể lớn; FE cần hiển thị tiến trình upload và có timeout tuỳ kích thước.
        /// </remarks>
        /// <param name="file">File upload (Required) — gửi multipart/form-data, key = file.</param>
        /// <param name="folder">Thư mục lưu trên storage (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin file lưu trữ (URL hoặc storedFileName).</response>
        /// <response code="400">Dữ liệu không hợp lệ: không có file hoặc kích thước/format không được chấp nhận.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có quyền upload (nếu có hạn chế).</response>
        /// <response code="500">Lỗi server hoặc lỗi provider khi upload.</response>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            if (file == null)
                return BadRequest();

            await using var stream = file.OpenReadStream();
            if (transcriptions is not null && claims is not null &&
                (User.IsInRole("ContentAuthor") || User.IsInRole("Admin")) &&
                Path.GetExtension(file.FileName).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Ok(await transcriptions.UploadAsync(stream, file.FileName, claims.GetUserClaim().Id, cancellationToken));
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
                var result = await fileStorageService.UploadAsync(stream, file.FileName, file.ContentType, folder, cancellationToken);
                return Ok(result);
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
        /// Xoá file đã lưu trên storage theo tên lưu trữ.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Quản lý file hoặc chức năng xóa tài liệu/ảnh.
        /// - Luồng xử lý: Xoá file trên storage provider; trả NoContent khi thành công.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. FE phải truyền chính xác storedFileName trả về từ API upload.
        /// </remarks>
        /// <param name="storedFileName">Tên file đã lưu trên storage (Required).</param>
        /// <param name="folder">Thư mục lưu trên storage (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="204">Thành công: File đã bị xóa.</response>
        /// <response code="400">Dữ liệu không hợp lệ: storedFileName rỗng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có quyền xóa file.</response>
        /// <response code="404">Không tìm thấy file trên storage.</response>
        /// <response code="500">Lỗi server hoặc provider khi xóa file.</response>
        [HttpDelete]
        public async Task<IActionResult> Delete([FromQuery] string storedFileName, [FromQuery] string folder = "", CancellationToken cancellationToken = default)
        {
            try
            {
                await fileStorageService.DeleteAsync(storedFileName, folder, cancellationToken);
                return NoContent();
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }
    }
}
