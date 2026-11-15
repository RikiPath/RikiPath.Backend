using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorProfile;

namespace RikiPath.WebApi.Controllers
{
    [ApiController]
    public class MentorProfileController(IMentorProfileService profileService) : ControllerBase
    {
        // // Chuyên gia lấy thông tin hồ sơ của chính mình.
        // // - Màn hình sử dụng: Màn hình Hồ sơ chuyên gia (Mentor App/Portal).
        // - Quyền truy cập: Yêu cầu Bearer Token với Role = Mentor.
        [HttpGet("api/mentor/profile")]
        [Authorize(Roles = "Mentor,Learner")]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
        {
            var result = await profileService.GetMyProfileAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        // // Chuyên gia cập nhật thông tin hồ sơ chuyên môn của mình.
        // // - Màn hình sử dụng: Chỉnh sửa hồ sơ chuyên gia.
        // - Quyền truy cập: Yêu cầu Bearer Token với Role = Mentor.
        [HttpPut("api/mentor/profile")]
        [Authorize(Roles = "Mentor,Learner")]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateMyProfile(
            [FromBody] UpdateMentorProfileRequest request, CancellationToken cancellationToken)
        {
            var result = await profileService.UpdateMyProfileAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        // // Chuyên gia nộp hồ sơ xin Quản trị viên xét duyệt xác minh.
        // // - Màn hình sử dụng: Nút "Gửi duyệt hồ sơ" trên giao diện Mentor.
        // - Điều kiện: Hồ sơ phải có đầy đủ Bio, Expertise, JLPT level và link tài liệu chứng chỉ.
        // - Chuyển trạng thái từ Unverified/Rejected/ChangesRequested sang Pending.
        [HttpPost("api/mentor/profile/submit-verification")]
        [Authorize(Roles = "Mentor,Learner")]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<MentorProfileResponse>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SubmitVerification(
            [FromBody] SubmitMentorVerificationRequest request, CancellationToken cancellationToken)
        {
            var result = await profileService.SubmitVerificationAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Tải chứng chỉ vào kho riêng tư cho hồ sơ ứng tuyển của chính mình.</summary>
        [HttpPost("api/mentor/profile/verification-document")]
        [Authorize(Roles = "Learner,Mentor")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadVerificationDocument(
            IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length < 4 || file.Length > 5 * 1024 * 1024)
                return BadRequest(new { Message = "Tài liệu phải có dung lượng từ 4 byte đến 5 MB." });

            await using var document = new MemoryStream();
            await file.CopyToAsync(document, cancellationToken);
            var bytes = document.GetBuffer();
            var type = bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46
                ? "application/pdf"
                : bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF
                    ? "image/jpeg"
                    : document.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                      bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A
                        ? "image/png" : null;
            if (type is null || !string.Equals(file.ContentType, type, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = "Loại MIME và nội dung chứng chỉ phải khớp PDF, JPEG hoặc PNG hợp lệ." });
            document.Position = 0;
            var result = await profileService.UploadVerificationDocumentAsync(document, type, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lấy liên kết ngắn hạn để xem chứng chỉ thuộc tài khoản hiện tại.</summary>
        [HttpGet("api/mentor/profile/verification-document")]
        [Authorize(Roles = "Learner,Mentor")]
        public async Task<IActionResult> GetVerificationDocument(
            [FromServices] IUnitOfWork unitOfWork,
            [FromServices] IClaimService claimService,
            [FromServices] IFileStorageService fileStorageService,
            CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var url = await unitOfWork.MentorProfiles.Query()
                .Where(profile => profile.MentorId == userId)
                .Select(profile => profile.VerificationDocumentUrl)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(url)) return NotFound();
            try
            {
                var signedUrl = await fileStorageService.CreateSignedReadUrlAsync(url, cancellationToken);
                Response.Headers.CacheControl = "no-store";
                return Ok(new { Url = signedUrl });
            }
            catch (ArgumentException)
            {
                return UnprocessableEntity(new { Message = "Tài liệu cũ cần được tải lại vào kho bảo vệ." });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Message = "Kho tài liệu bảo vệ chưa sẵn sàng." });
            }
            catch (NotSupportedException)
            {
                return StatusCode(StatusCodes.Status409Conflict,
                    new { Message = "Kho QA chỉ hỗ trợ xem chứng chỉ qua phiên đăng nhập hiện tại." });
            }
        }

        /// <summary>Đọc chứng chỉ của chính mình qua phiên xác thực hiện tại.</summary>
        [HttpGet("api/mentor/profile/verification-document/content")]
        [Authorize(Roles = "Learner,Mentor")]
        public async Task<IActionResult> ReadVerificationDocument(
            [FromServices] IUnitOfWork unitOfWork,
            [FromServices] IClaimService claimService,
            [FromServices] IFileStorageService fileStorageService,
            CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var reference = await unitOfWork.MentorProfiles.Query()
                .Where(profile => profile.MentorId == userId)
                .Select(profile => profile.VerificationDocumentUrl)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(reference)) return NotFound();
            try
            {
                var document = await fileStorageService.OpenPrivateVerificationAsync(reference, cancellationToken);
                Response.Headers.CacheControl = "no-store";
                Response.Headers.XContentTypeOptions = "nosniff";
                return File(document.Content, document.ContentType);
            }
            catch (FileNotFoundException) { return NotFound(); }
            catch (ArgumentException) { return UnprocessableEntity(new { Message = "Chứng chỉ cũ cần được tải lại." }); }
            catch (InvalidOperationException) { return StatusCode(503, new { Message = "Kho chứng chỉ chưa sẵn sàng." }); }
        }

    }
}
