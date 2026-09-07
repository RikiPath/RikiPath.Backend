using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Auth;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        /// <summary>
        /// Đăng ký tài khoản mới.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình đăng ký (Sign Up) của ứng dụng.
        /// - Luồng xử lý: Tạo user mới trong CSDL, gửi email xác thực (gọi Mail service) với token; không đăng nhập tự động.
        /// - Lưu ý cho FE: Gửi body JSON theo RegisterRequest. Response có thể mất ~0.5-2s do gửi email. Không cần Bearer Token.
        /// </remarks>
        /// <param name="request">Thông tin đăng ký: Email (Required), Password (Required), FullName (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Tạo tài khoản và gửi email xác thực.</response>
        /// <response code="400">Dữ liệu không hợp lệ: email đã tồn tại hoặc password không đủ mạnh.</response>
        /// <response code="500">Lỗi server khi tạo user hoặc gửi email.</response>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Xác thực email bằng token gửi qua email.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình xác thực email (Verify Email) hoặc link từ email.
        /// - Luồng xử lý: Kiểm tra token, cập nhật trạng thái emailVerified trong CSDL; có thể ghi log xác thực.
        /// - Lưu ý cho FE: Gửi token và email trong body; không cần Bearer Token. Response nhanh (<500ms).
        /// </remarks>
        /// <param name="request">VerifyEmailRequest: Email (Required), Token (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Email được xác thực.</response>
        /// <response code="400">Token không hợp lệ hoặc đã hết hạn.</response>
        /// <response code="404">Không tìm thấy user với email này.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái.</response>
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.VerifyEmailAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đăng nhập và lấy JWT token.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Login của ứng dụng.
        /// - Luồng xử lý: Kiểm tra credentials, trả về JWT + thông tin user; có thể ghi log đăng nhập.
        /// - Lưu ý cho FE: Gửi Email và Password trong body. Trả về Bearer token dùng cho các request yêu cầu xác thực. Lưu token an toàn (Secure storage).
        /// </remarks>
        /// <param name="request">LoginRequest: Email (Required), Password (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về JWT token và thông tin user.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Email hoặc mật khẩu sai.</response>
        /// <response code="500">Lỗi server khi xác thực.</response>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật địa chỉ email cho user (Admin hoặc chính chủ).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang quản lý tài khoản hoặc profile settings.
        /// - Luồng xử lý: Cập nhật email trong CSDL, gửi email xác thực mới; có thể yêu cầu xác thực lại.
        /// - Lưu ý cho FE: Yêu cầu quyền phù hợp (Bearer Token). Nếu update cho user khác, cần role admin. Thao tác có thể mất thời gian do gửi mail.
        /// </remarks>
        /// <param name="userId">ID user cần cập nhật (Required).</param>
        /// <param name="request">UpdateEmailRequest: NewEmail (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Email được cập nhật và email xác thực gửi đến địa chỉ mới.</response>
        /// <response code="400">Dữ liệu không hợp lệ: email format sai hoặc đã tồn tại.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: User không được phép cập nhật email của tài khoản này.</response>
        /// <response code="500">Lỗi server khi cập nhật hoặc gửi mail.</response>
        [HttpPut("update-email/{userId:int}")]
        public async Task<IActionResult> UpdateEmail(int userId, [FromBody] UpdateEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.UpdateEmailAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Đổi mật khẩu cho user (chính chủ hoặc admin reset).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang đổi mật khẩu hoặc quản trị viên reset mật khẩu.
        /// - Luồng xử lý: Kiểm tra mật khẩu hiện tại (nếu cần), cập nhật mật khẩu mới, invalid token phiên đăng nhập cũ nếu cần.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nếu là reset bởi admin có thể không cần current password. Thời gian xử lý nhanh (<1s).
        /// </remarks>
        /// <param name="userId">ID user cần đổi mật khẩu (Required).</param>
        /// <param name="request">ChangePasswordRequest: CurrentPassword (Required if user tự đổi), NewPassword (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Mật khẩu được đổi.</response>
        /// <response code="400">Dữ liệu không hợp lệ: mật khẩu yếu hoặc current password sai.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Người gọi không được phép đổi mật khẩu cho user này.</response>
        /// <response code="500">Lỗi server khi cập nhật mật khẩu.</response>
        [HttpPost("change-password/{userId:int}")]
        public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.ChangePasswordAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
