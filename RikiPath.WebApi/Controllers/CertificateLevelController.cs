using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Catalog;
using RikiPath.Application.Responses.CertificateLevel;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public sealed class CertificateLevelController(ICertificateLevelService certificateLevelService, IJlptCatalogService catalogService) : ControllerBase
    {
        /// <summary>
        /// Lấy danh mục cấp độ JLPT khả dụng cho Learner.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Onboarding, chọn mục tiêu JLPT và các bộ lọc cần ID cấp độ.
        /// - Luồng xử lý: Chỉ trả cấp độ thuộc loại JLPT đang hoạt động, không bị xóa mềm.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Dùng Id từ response cho các API cần level ID.
        /// - Thứ tự: SortOrder tăng dần, sau đó Id tăng dần để kết quả luôn ổn định.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công, kể cả khi catalog rỗng.</response>
        /// <response code="401">Chưa xác thực hoặc thiếu Bearer Token.</response>
        /// <response code="403">Tài khoản không có role Learner.</response>
        /// <response code="500">Không thể truy vấn catalog.</response>
        [HttpGet("jlpt")]
        [ProducesResponseType(typeof(ApiResponse<List<JlptLevelCatalogResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<List<JlptLevelCatalogResponse>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetJlptLevels(CancellationToken cancellationToken)
        {
            var result = await catalogService.GetLevelsAsync(cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách cấp độ theo chứng chỉ (dùng cho learner chọn cấp độ sau khi chọn chứng chỉ).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chọn cấp độ muốn thi/học sau khi đã chọn loại chứng chỉ.
        /// - Luồng xử lý: Nhận certificateTypeId, trả về các cấp độ thuộc chứng chỉ đó, sắp xếp theo SortOrder.
        /// - Lưu ý cho FE: Gọi API lấy danh sách chứng chỉ trước để có certificateTypeId.
        /// </remarks>
        /// <param name="certificateTypeId">Id của chứng chỉ (CertificateType).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công.</response>
        /// <response code="400">certificateTypeId không hợp lệ.</response>
        /// <response code="404">Không tìm thấy chứng chỉ.</response>
        /// <response code="500">Lỗi hệ thống.</response>
        [HttpGet("by-certificate/{certificateTypeId:int}")]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateLevelResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateLevelResponse>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateLevelResponse>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateLevelResponse>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCertificateLevelsByCertificate(
            int certificateTypeId, CancellationToken cancellationToken)
        {
            var result = await certificateLevelService.GetByCertificateTypeIdAsync(certificateTypeId, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
