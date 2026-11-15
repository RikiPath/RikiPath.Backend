using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Content;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;

namespace RikiPath.Api.Controllers
{
    [ApiController]
    [Route("api/content-import")]
    [Authorize(Roles = "ContentAuthor,Admin")]
    public class ContentImportController(IExcelContentImportService importService, IFileStorageService fileStorage) : ControllerBase
    {
        private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private static readonly JsonSerializerOptions WorkbookJson = new(JsonSerializerDefaults.Web);

        [HttpPost("listening-audio")]
        [Authorize(Roles = "ContentAuthor,Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadListeningAudio(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length < 4 || !string.Equals(Path.GetExtension(file.FileName), ".mp3", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<UploadedFileResult>.Fail("Vui lòng chọn file nghe .mp3."));

            await using var stream = file.OpenReadStream();
            if (!await HasMp3HeaderAsync(stream, cancellationToken))
                return BadRequest(ApiResponse<UploadedFileResult>.Fail("File nghe không có định dạng MP3 hợp lệ."));

            await using var audio = file.OpenReadStream();
            var uploaded = await fileStorage.UploadAsync(audio, file.FileName, "audio/mpeg", "jlpt-listening", cancellationToken);
            return Ok(ApiResponse<UploadedFileResult>.Success(uploaded));
        }

        /// <summary>
        /// Upload file Excel để xem trước (Preview) sự thay đổi và kiểm tra lỗi trước khi nhập chính thức.
        /// </summary>
        /// <param name="file">File Excel (.xlsx, .xls)</param>
        /// <param name="entityType">Tùy chọn: Chọn loại nội dung cụ thể nếu chỉ muốn đọc 1 sheet duy nhất</param>
        /// <param name="cancellationToken"></param>
        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ImportPreviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportPreviewResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Preview(
            IFormFile file,
            [FromQuery] ImportEntityType? entityType,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<ImportPreviewResponse>.Fail("Vui lòng chọn file Excel để tải lên."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".xlsx" or ".xls"))
            {
                return BadRequest(ApiResponse<ImportPreviewResponse>.Fail("Định dạng file không hỗ trợ. Vui lòng tải lên file .xlsx hoặc .xls."));
            }

            await using var stream = file.OpenReadStream();
            var result = await importService.PreviewAsync(stream, file.FileName, entityType, cancellationToken,
                (path, token) => UploadLocalMediaAsync(path, isAudio: true, token),
                (path, token) => UploadLocalMediaAsync(path, isAudio: false, token));

            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("workbook-preview")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PreviewWorkbook(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<WorkbookPreviewResponse>.Fail("Vui lòng chọn file Excel .xlsx."));

            await using var stream = file.OpenReadStream();
            var result = importService.PreviewWorkbook(stream, file.FileName, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("workbook-edit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EditWorkbook(IFormFile? file, [FromForm] string? edits, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0 || !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<byte[]>.Fail("Vui lòng chọn file Excel .xlsx."));

            List<WorkbookCellEdit>? changes;
            try
            {
                changes = string.IsNullOrWhiteSpace(edits) ? null : JsonSerializer.Deserialize<List<WorkbookCellEdit>>(edits, WorkbookJson);
            }
            catch (JsonException)
            {
                return BadRequest(ApiResponse<byte[]>.Fail("Danh sách ô Excel đã sửa không hợp lệ."));
            }
            if (changes is not { Count: > 0 })
                return BadRequest(ApiResponse<byte[]>.Fail("Chưa có ô Excel nào được sửa."));

            await using var stream = file.OpenReadStream();
            var result = importService.EditWorkbook(stream, changes, cancellationToken);
            return result.IsSuccess ? File(result.Result!, ExcelContentType, Path.GetFileName(file.FileName))
                : StatusCode(result.StatusCode, result);
        }

        private async Task<string> UploadLocalMediaAsync(string path, bool isAudio, CancellationToken cancellationToken)
        {
            if (!User.IsInRole("ContentAuthor") && !User.IsInRole("Admin"))
                throw new InvalidDataException("Chỉ Content Author hoặc Admin được nhập ảnh hoặc audio từ đường dẫn trên máy chủ.");

            var audioRoot = Environment.GetEnvironmentVariable("RIKIPATH_IMPORT_AUDIO_ROOT");
            if (string.IsNullOrWhiteSpace(audioRoot) || !Path.IsPathFullyQualified(audioRoot))
                throw new InvalidDataException("Máy chủ chưa cấu hình thư mục audio RIKIPATH_IMPORT_AUDIO_ROOT.");

            try
            {
                var fullPath = Path.GetFullPath(path);
                var relative = Path.GetRelativePath(Path.GetFullPath(audioRoot), fullPath);
                if (Path.IsPathRooted(relative) || relative == ".." ||
                    relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative.Contains(':'))
                    throw new InvalidDataException("Đường dẫn file phải nằm trong thư mục audio được phép.");
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                var contentType = isAudio
                    ? (extension == ".mp3" ? "audio/mpeg" : null)
                    : extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null };
                if (contentType is null)
                    throw new InvalidDataException(isAudio ? "Đường dẫn audio phải trỏ tới file .mp3." : "Ảnh phải là JPG, PNG hoặc WebP.");

                for (string? entry = fullPath; entry is not null; entry = Path.GetDirectoryName(entry))
                {
                    if ((System.IO.File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Đường dẫn audio không được đi qua thư mục hoặc file liên kết.");
                }

                await using var audio = System.IO.File.OpenRead(fullPath);
                if (audio.Length > 25 * 1024 * 1024)
                    throw new InvalidDataException("File tối đa 25 MB.");
                if (isAudio && !await HasMp3HeaderAsync(audio, cancellationToken))
                    throw new InvalidDataException("File nghe không có định dạng MP3 hợp lệ.");
                audio.Position = 0;

                try
                {
                    var uploaded = await fileStorage.UploadAsync(audio, Path.GetFileName(fullPath), contentType, isAudio ? "jlpt-listening" : "jlpt-images", cancellationToken);
                    return uploaded.Url;
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
                {
                    throw new InvalidDataException("Không tải được file lên kho lưu trữ. Vui lòng thử lại.");
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new InvalidDataException("Không tìm thấy file trên máy chạy backend.");
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new InvalidDataException("Không đọc được file trên máy chạy backend.");
            }
        }

        private static async Task<bool> HasMp3HeaderAsync(Stream audio, CancellationToken cancellationToken)
        {
            var header = new byte[4];
            if (await audio.ReadAtLeastAsync(header, header.Length, false, cancellationToken) < header.Length)
                return false;

            var hasId3 = header[0] == 'I' && header[1] == 'D' && header[2] == '3' && header[3] is >= 2 and <= 4;
            var hasMp3Frame = header[0] == 0xff && (header[1] & 0xe6) == 0xe2 &&
                (header[1] & 0x18) != 0x08 && (header[2] & 0xf0) != 0xf0 && (header[2] & 0x0c) != 0x0c;
            return hasId3 || hasMp3Frame;
        }

        /// <summary>
        /// Lưu các dòng hợp lệ từ bản xem trước thành bản nháp của tác giả.
        /// </summary>
        /// <param name="request">Danh sách dòng New/Modified do Frontend gửi lên</param>
        /// <param name="cancellationToken"></param>
        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ApiResponse<ImportConfirmResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ImportConfirmResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Confirm(
            [FromBody] ConfirmImportRequest request,
            CancellationToken cancellationToken)
        {
            var actorUserId = GetCurrentUserId();
            if (actorUserId is null)
            {
                return Unauthorized(ApiResponse<ImportConfirmResponse>.Fail("Không xác định được thông tin người dùng."));
            }

            var result = await importService.ConfirmAsync(request, actorUserId.Value, cancellationToken);

            if (!result.IsSuccess)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Trích xuất UserId của người dùng hiện tại từ Claims/Token JWT
        /// </summary>
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            return int.TryParse(userIdClaim?.Value, out var userId) ? userId : null;
        }
    }
}
