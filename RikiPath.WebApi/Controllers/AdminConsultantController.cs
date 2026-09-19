using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminConsultant;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConsultant;

namespace RikiPath.WebApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/consultants")]
    public class AdminConsultantController(IAdminConsultantService adminConsultantService) : ControllerBase
    {
        // ==========================================
        // 1. Quản lý Chuyên gia (Consultant)
        // ==========================================

        /// <summary>
        /// Tạo mới tài khoản Chuyên gia tư vấn (Consultant).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dashboard Admin - Quản lý nhân sự/Consultant (Task Package 2).
        /// - Luồng xử lý: Tạo tài khoản UserAccount với Role = Consultant, tự động đặt IsEmailVerified = true (do Admin khởi tạo).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Request body bao gồm Email, Mật khẩu (tối thiểu 6 ký tự), Họ tên và SĐT.
        /// </remarks>
        /// <param name="request">Thông tin khởi tạo tài khoản Consultant.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin Consultant vừa tạo.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Email đã tồn tại, mật khẩu quá ngắn hoặc dữ liệu thiếu.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi tạo tài khoản.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ConsultantResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ConsultantResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateConsultant(
            [FromBody] CreateConsultantRequest request,
            CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.CreateConsultantAsync(request, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Lấy danh sách tất cả Chuyên gia tư vấn (Consultant) trong hệ thống.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình danh sách Consultant trong Admin Portal.
        /// - Luồng xử lý: Truys vấn các UserAccount có Role = Consultant và chưa bị xóa mềm (!IsDeleted), sắp xếp theo Tên.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Kết quả trả về danh sách DTO gọn gàng phục vụ hiển thị dạng Table/Grid.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách Consultant.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi truy vấn CSDL.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ConsultantResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<ConsultantResponse>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetConsultants(CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.GetConsultantsAsync(cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Kích hoạt hoặc Khóa/Vô hiệu hóa tài khoản Consultant.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Toggle switch kích hoạt/khóa tài khoản trên dòng tương ứng trong Bảng Consultant.
        /// - Luồng xử lý: Cập nhật cờ IsDeleted của UserAccount (IsDeleted = !isActive).
        /// - Lưu ý cho FE: Truyền consultantId trên URL Route và trạng thái isActive (true/false) dưới dạng Query Parameter.
        /// </remarks>
        /// <param name="consultantId">ID định danh tài khoản Consultant.</param>
        /// <param name="isActive">Trạng thái mong muốn: true = Kích hoạt, false = Khóa.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin Consultant cùng thông báo cập nhật trạng thái thành công.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Không tìm thấy Consultant với ID chỉ định.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái.</response>
        [HttpPut("{consultantId:int}/active")]
        [ProducesResponseType(typeof(ApiResponse<ConsultantResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ConsultantResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetConsultantActive(
            [FromRoute] int consultantId,
            [FromQuery] bool isActive,
            CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.SetConsultantActiveAsync(consultantId, isActive, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // ==========================================
        // 2. Quản lý Gói tư vấn (Consultation Package)
        // ==========================================

        /// <summary>
        /// Tạo mới gói dịch vụ tư vấn (Consultation Package).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Quản lý Gói dịch vụ PayOS dành cho Admin (Task Package 1 &amp; 5).
        /// - Luồng xử lý: Lưu gói dịch vụ tư vấn (1-1 Meeting hoặc Ticket câu hỏi) vào CSDL với trạng thái IsActive = true.
        /// - Lưu ý cho FE: Price không được âm, DurationMinutes &gt; 0. Type xác định hình thức tư vấn (Meeting/WrittenAnswer).
        /// </remarks>
        /// <param name="request">Thông tin gói tư vấn cần tạo mới.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết gói tư vấn vừa tạo thành công.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Tên gói trống, giá âm hoặc thời lượng không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi lưu gói dịch vụ.</response>
        [HttpPost("packages")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateConsultationPackage(
            [FromBody] CreateConsultationPackageRequest request,
            CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.CreateConsultationPackageAsync(request, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Cập nhật thông tin gói dịch vụ tư vấn.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Modal/Form chỉnh sửa thông tin gói tư vấn trong Admin Portal.
        /// - Luồng xử lý: Cập nhật Name, Description, Price, DurationMinutes. Giữ nguyên Type cũ để bảo toàn tính toàn vẹn dữ liệu giao dịch PayOS.
        /// - Lưu ý cho FE: Yêu cầu truyền packageId trên URL và thông tin cập nhật trong Request Body.
        /// </remarks>
        /// <param name="packageId">ID định danh gói tư vấn.</param>
        /// <param name="request">Thông tin chỉnh sửa gói tư vấn.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin gói tư vấn đã cập nhật.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Không tìm thấy gói hoặc dữ liệu đầu vào vi phạm ràng buộc.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi cập nhật CSDL.</response>
        [HttpPut("packages/{packageId:int}")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateConsultationPackage(
            [FromRoute] int packageId,
            [FromBody] UpdateConsultationPackageRequest request,
            CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.UpdateConsultationPackageAsync(packageId, request, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Lấy danh sách tất cả các gói dịch vụ tư vấn trong hệ thống.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình danh sách Gói tư vấn của Admin.
        /// - Luồng xử lý: Truy vấn tất cả gói tư vấn bao gồm cả gói đang mở bán (Active) và ngừng bán (Inactive).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Danh sách sắp xếp theo tên gói.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách tất cả các gói tư vấn.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi truy vấn CSDL.</response>
        [HttpGet("packages")]
        [ProducesResponseType(typeof(ApiResponse<List<ConsultationPackageResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<ConsultationPackageResponse>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetConsultationPackages(CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.GetConsultationPackagesAsync(cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Mở bán hoặc Ngừng bán gói dịch vụ tư vấn.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Toggle switch Bật/Tắt mở bán trên danh sách Gói tư vấn.
        /// - Luồng xử lý: Cập nhật cờ IsActive của ConsultationPackage. Gói bị ngưng bán (IsActive = false) sẽ ẩn khỏi giao diện mua của Learner.
        /// - Lưu ý cho FE: Truyền packageId trên URL Route và isActive dưới dạng Query Parameter.
        /// </remarks>
        /// <param name="packageId">ID định danh gói tư vấn.</param>
        /// <param name="isActive">Trạng thái mở bán: true = Mở bán, false = Ngừng bán.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về gói tư vấn kèm thông báo cập nhật trạng thái.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Không tìm thấy gói tư vấn với ID tương ứng.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái gói.</response>
        [HttpPut("packages/{packageId:int}/active")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<ConsultationPackageResponse>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetConsultationPackageActive(
            [FromRoute] int packageId,
            [FromQuery] bool isActive,
            CancellationToken cancellationToken)
        {
            var result = await adminConsultantService.SetConsultationPackageActiveAsync(packageId, isActive, cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }
    }
}