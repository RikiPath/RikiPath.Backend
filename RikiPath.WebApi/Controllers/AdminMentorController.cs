using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminMentor;

namespace RikiPath.WebApi.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin/mentors")]
    public class AdminMentorController(IAdminMentorService adminMentorService) : ControllerBase
    {
        // ==========================================
        // 1. Quản lý Chuyên gia (Mentor)
        // ==========================================

        /// <summary>
        /// Tạo mới tài khoản Chuyên gia tư vấn (Mentor).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dashboard Admin - Quản lý nhân sự/Mentor (Task Package 2).
        /// - Luồng xử lý: Tạo tài khoản UserAccount với Role = Mentor, tự động đặt IsEmailVerified = true (do Admin khởi tạo).
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Request body bao gồm Email, Mật khẩu (tối thiểu 6 ký tự), Họ tên và SĐT.
        /// </remarks>
        /// <param name="request">Thông tin khởi tạo tài khoản Mentor.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin Mentor vừa tạo.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Email đã tồn tại, mật khẩu quá ngắn hoặc dữ liệu thiếu.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi tạo tài khoản.</response>
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

        /// <summary>
        /// Lấy danh sách tất cả Chuyên gia tư vấn (Mentor) trong hệ thống.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình danh sách Mentor trong Admin Portal.
        /// - Luồng xử lý: Truys vấn các UserAccount có Role = Mentor và chưa bị xóa mềm (!IsDeleted), sắp xếp theo Tên.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Admin). Kết quả trả về danh sách DTO gọn gàng phục vụ hiển thị dạng Table/Grid.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách Mentor.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi truy vấn CSDL.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<MentorResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<MentorResponse>>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMentors(CancellationToken cancellationToken)
        {
            var result = await adminMentorService.GetMentorsAsync(cancellationToken);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Kích hoạt hoặc Khóa/Vô hiệu hóa tài khoản Mentor.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Toggle switch kích hoạt/khóa tài khoản trên dòng tương ứng trong Bảng Mentor.
        /// - Luồng xử lý: Cập nhật cờ IsDeleted của UserAccount (IsDeleted = !isActive).
        /// - Lưu ý cho FE: Truyền mentorId trên URL Route và trạng thái isActive (true/false) dưới dạng Query Parameter.
        /// </remarks>
        /// <param name="mentorId">ID định danh tài khoản Mentor.</param>
        /// <param name="isActive">Trạng thái mong muốn: true = Kích hoạt, false = Khóa.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin Mentor cùng thông báo cập nhật trạng thái thành công.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Không tìm thấy Mentor với ID chỉ định.</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không phải quyền Admin.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái.</response>
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
    }
}