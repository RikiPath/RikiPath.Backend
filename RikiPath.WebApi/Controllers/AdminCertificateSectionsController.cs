using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.CertificateSections;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateSections;

namespace RikiPath.WebApi.Controllers
{
    /// <summary>API Admin quản lý CertificateSection theo từng cấp độ JLPT.</summary>
    [ApiController]
    [Route("api/admin/certificate-sections")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminCertificateSectionsController(
        IAdminCertificateSectionService service) : ControllerBase
    {
        /// <summary>Lấy danh sách CertificateSection phục vụ màn hình quản trị.</summary>
        /// <param name="certificateLevelId">Lọc theo ID CertificateLevel; bỏ trống để lấy mọi cấp độ.</param>
        /// <param name="includeArchived">Bao gồm section đã lưu trữ mềm.</param>
        /// <param name="cancellationToken">Token hủy request.</param>
        /// <response code="200">Trả về danh sách section theo bộ lọc.</response>
        /// <response code="400">Bộ lọc CertificateLevelId không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role Admin.</response>
        /// <response code="500">Không thể tải dữ liệu section.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateSectionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateSectionResponse>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateSectionResponse>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? certificateLevelId,
            [FromQuery] bool includeArchived = false,
            CancellationToken cancellationToken = default)
        {
            var result = await service.GetAllAsync(
                certificateLevelId,
                includeArchived,
                cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Lấy chi tiết một CertificateSection, kể cả section đã lưu trữ.</summary>
        /// <response code="200">Trả về chi tiết section.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role Admin.</response>
        /// <response code="404">Không tìm thấy section.</response>
        /// <response code="500">Không thể tải dữ liệu section.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var result = await service.GetByIdAsync(id, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Tạo CertificateSection mới cho một CertificateLevel JLPT đang khả dụng.</summary>
        /// <remarks>Code không có unique constraint trong schema hiện tại; API không tự đặt thêm quy tắc trùng mã.</remarks>
        /// <response code="201">Tạo section thành công.</response>
        /// <response code="400">Dữ liệu hoặc CertificateLevel không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role Admin.</response>
        /// <response code="500">Không thể tạo section.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create(
            [FromBody] CreateCertificateSectionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Cập nhật các trường quản trị của một CertificateSection đang hoạt động.</summary>
        /// <response code="200">Cập nhật section thành công.</response>
        /// <response code="400">Dữ liệu hoặc CertificateLevel không hợp lệ.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role Admin.</response>
        /// <response code="404">Không tìm thấy section đang hoạt động.</response>
        /// <response code="500">Không thể cập nhật section.</response>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateCertificateSectionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>Lưu trữ mềm CertificateSection để không phá vỡ nội dung Mock Test đang tham chiếu.</summary>
        /// <response code="200">Lưu trữ section thành công hoặc section đã được lưu trữ trước đó.</response>
        /// <response code="401">Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Token không có role Admin.</response>
        /// <response code="404">Không tìm thấy section.</response>
        /// <response code="500">Không thể lưu trữ section.</response>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CertificateSectionResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Archive(
            int id,
            CancellationToken cancellationToken)
        {
            var result = await service.ArchiveAsync(id, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
