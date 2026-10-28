using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.CertificateType;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/certificate-types")]
    [ApiController]
    public class CertificateTypeController(ICertificateTypeService certificateTypeService) : ControllerBase
    {
        /// <summary>
        /// Lấy danh sách các chứng chỉ đang hoạt động (dùng cho learner chọn chứng chỉ).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Chọn chứng chỉ muốn thi/học (JLPT, TOEIC...) trước khi chọn cấp độ.
        /// - Luồng xử lý: Trả về các chứng chỉ có IsActive = true, sắp xếp theo SortOrder.
        /// - Lưu ý cho FE: Dùng Id của chứng chỉ để gọi API lấy danh sách cấp độ theo chứng chỉ.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công.</response>
        /// <response code="500">Lỗi hệ thống.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateTypeResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<CertificateTypeResponse>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllCertificateTypes(CancellationToken cancellationToken)
        {
            var result = await certificateTypeService.GetAllAsync(cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}