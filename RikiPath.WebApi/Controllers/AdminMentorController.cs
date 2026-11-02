using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Requests.MentorProfile;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Domain.Enums;

namespace RikiPath.WebApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/mentors")]
    public class AdminMentorController(IAdminMentorService adminMentorService, IUnitOfWork unitOfWork) : ControllerBase
    {
        // ==========================================
        // 1. Quản lý Chuyên gia (Mentor)
        // ==========================================

        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<MentorResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MentorResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMentor(
            [FromBody] CreateMentorRequest request,
            CancellationToken cancellationToken)
        {
            var result = await adminMentorService.CreateMentorAsync(request, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<MentorResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<MentorResponse>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMentors(CancellationToken cancellationToken)
        {
            var result = await adminMentorService.GetMentorsAsync(cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{mentorId:int}/active")]
        [ProducesResponseType(typeof(ApiResponse<MentorResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MentorResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetMentorActive(
            [FromRoute] int mentorId,
            [FromQuery] bool isActive,
            CancellationToken cancellationToken)
        {
            var result = await adminMentorService.SetMentorActiveAsync(mentorId, isActive, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 2. Xét duyệt Hồ sơ Xác minh (Verification)
        // ==========================================

        [HttpGet("profiles")]
        [HttpGet("verifications")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminMentorProfileResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMentorProfiles(
            [FromQuery] MentorVerificationStatus? status,
            CancellationToken cancellationToken)
        {
            var result = await adminMentorService.GetMentorProfilesAsync(status, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("{mentorId:int}/profile")]
        [ProducesResponseType(typeof(ApiResponse<AdminMentorProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AdminMentorProfileResponse>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMentorProfileById(
            [FromRoute] int mentorId,
            CancellationToken cancellationToken)
        {
            var result = await adminMentorService.GetMentorProfileByIdAsync(mentorId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("{mentorId:int}/verification-document")]
        public async Task<IActionResult> GetVerificationDocument(
            [FromRoute] int mentorId,
            [FromServices] IFileStorageService fileStorageService,
            CancellationToken cancellationToken)
        {
            var url = await unitOfWork.MentorProfiles.Query()
                .Where(profile => profile.MentorId == mentorId)
                .Select(profile => profile.VerificationDocumentUrl)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(url)) return NotFound();
            try
            {
                var signedUrl = await fileStorageService.CreateSignedReadUrlAsync(url, cancellationToken);
                Response.Headers.CacheControl = "no-store";
                return Ok(new { Url = signedUrl });
            }
            catch (ArgumentException)
            {
                return UnprocessableEntity(new { Message = "Tài liệu cũ chưa thuộc kho lưu trữ bảo vệ. Vui lòng yêu cầu cố vấn gửi lại." });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Message = "Không thể mở tài liệu xác minh lúc này. Vui lòng thử lại." });
            }
            catch (NotSupportedException)
            {
                return StatusCode(StatusCodes.Status409Conflict,
                    new { Message = "Kho QA chỉ hỗ trợ xem chứng chỉ qua phiên đăng nhập hiện tại." });
            }
        }

        /// <summary>Đọc chứng chỉ ứng tuyển qua quyền quản trị hiện tại.</summary>
        [HttpGet("{mentorId:int}/verification-document/content")]
        public async Task<IActionResult> ReadVerificationDocument(
            [FromRoute] int mentorId,
            [FromServices] IFileStorageService fileStorageService,
            CancellationToken cancellationToken)
        {
            var reference = await unitOfWork.MentorProfiles.Query()
                .Where(profile => profile.MentorId == mentorId)
                .Select(profile => profile.VerificationDocumentUrl)
                .FirstOrDefaultAsync(cancellationToken);
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

        [HttpPost("{mentorId:int}/verify")]
        [ProducesResponseType(typeof(ApiResponse<AdminMentorProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AdminMentorProfileResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AdminMentorProfileResponse>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReviewVerification(
            [FromRoute] int mentorId,
            [FromBody] ReviewMentorVerificationRequest request,
            CancellationToken cancellationToken)
        {
            var result = await adminMentorService.ReviewVerificationAsync(mentorId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
