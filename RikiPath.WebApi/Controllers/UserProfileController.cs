using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class UserProfileController(IUserProfileService userProfileService) : ControllerBase
    {
        /// <summary>
        /// Lấy thông tin hồ sơ (profile) của người dùng hiện tại.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang Profile / Settings của Learner.
        /// - Luồng xử lý: Truy vấn hồ sơ từ CSDL, bao gồm tên, email, avatar, progress, JLPT goal.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Response nhanh; FE nên hiển thị skeleton khi chờ.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về thông tin profile của user.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="404">Không tìm thấy profile (hiếm).</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.GetProfileAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật thông tin hồ sơ của user (name, bio, locale,...).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Profile edit form.
        /// - Luồng xử lý: Validate và cập nhật các trường trong CSDL.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Trường email nếu thay đổi có thể cần xác thực lại.
        /// </remarks>
        /// <param name="request">UpdateProfileRequest: FullName (Optional), Bio (Optional), Locale (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Profile đã được cập nhật.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi cập nhật profile.</response>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] RikiPath.Application.Requests.Profile.UpdateProfileRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateProfileAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật avatar của người dùng (upload file hình ảnh).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Profile -> Change Avatar.
        /// - Luồng xử lý: Upload file (FileStorage) và cập nhật URL avatar trong profile.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Gửi multipart/form-data với key `avatarFile`. File size/format nên tuân theo giới hạn server.
        /// </remarks>
        /// <param name="avatarFile">File ảnh avatar (Required) — multipart/form-data.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về URL avatar mới.</response>
        /// <response code="400">Dữ liệu không hợp lệ: file không phải ảnh hoặc quá lớn.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi upload hoặc cập nhật profile.</response>
        [HttpPost("me/avatar")]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateAvatarAsync(userId, avatarFile, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Thiết lập mục tiêu JLPT cho người dùng.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Profile -> JLPT Goal setting.
        /// - Luồng xử lý: Lưu mục tiêu JLPT vào profile và có thể kích hoạt tính năng tạo learning path.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Field JlptLevelId là Required.
        /// </remarks>
        /// <param name="request">SetJlptGoalRequest: JlptLevelId (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Mục tiêu JLPT được lưu.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi cập nhật mục tiêu.</response>
        [HttpPost("me/jlpt-goal")]
        public async Task<IActionResult> SetJlptGoal([FromBody] RikiPath.Application.Requests.Profile.SetJlptGoalRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.SetJlptGoalAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Cập nhật cài đặt thông báo (notification settings) cho user.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Profile -> Notification Settings.
        /// - Luồng xử lý: Lưu các tuỳ chọn thông báo (email/push/in-app) vào CSDL.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Thao tác nhanh; FE nên cập nhật UI ngay sau khi nhận 200.
        /// </remarks>
        /// <param name="request">UpdateNotificationSettingsRequest: EmailEnabled (Optional), PushEnabled (Optional), InAppEnabled (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Cài đặt đã được cập nhật.</response>
        /// <response code="400">Dữ liệu không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="500">Lỗi server khi lưu cài đặt.</response>
        [HttpPut("me/notification-settings")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] RikiPath.Application.Requests.Profile.UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateNotificationSettingsAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
