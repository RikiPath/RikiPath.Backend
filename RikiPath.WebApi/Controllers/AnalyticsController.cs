using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
    {
        /// <summary>
        /// Lấy chuỗi ngày học liên tiếp (study streak) của người dùng hiện tại.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Hiển thị trên dashboard / Progress của Learner để khuyến khích thói quen học.
        /// - Luồng xử lý: API chỉ đọc dữ liệu phân tích từ CSDL/Cache (không gọi AI). Có thể có cache để trả nhanh.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Thường trả nhanh (<200ms); FE nên hiển thị loading placeholder khi chờ.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về số ngày liên tiếp hiện tại và metadata (JSON).</response>
        /// <response code="400">Yêu cầu không hợp lệ: tham số không hợp lệ (hiếm).</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu hoặc cache.</response>
        [HttpGet("study-streak")]
        public async Task<IActionResult> GetStudyStreak(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetStudyStreakAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy thống kê hoàn thành khóa học / bài tập của người dùng (completion stats).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Dashboard hoặc trang Progress/Statistics của Learner.
        /// - Luồng xử lý: Đọc từ CSDL/OLAP; có thể tính toán tổng hợp (aggregate) tại server, không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Response có thể gồm nhiều trường (percent, counts); FE nên xử lý null/empty.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về các chỉ số hoàn thành (JSON) như percentComplete, lessonsCompleted.</response>
        /// <response code="400">Yêu cầu không hợp lệ: tham số truy vấn sai định dạng (nếu có).</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi tổng hợp dữ liệu.</response>
        [HttpGet("completion-stats")]
        public async Task<IActionResult> GetCompletionStats(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetCompletionStatsAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy phân tích theo kỹ năng (skill breakdown) của người học.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang chi tiết năng lực / Skill Insights trong ứng dụng Learner.
        /// - Luồng xử lý: Server tổng hợp điểm/tiến độ theo từng kỹ năng từ CSDL; không gọi AI. Có thể trả theo dạng danh sách {skill, score, progress}.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Kết quả có thể được cache; FE nên support cập nhật khi user làm mới dữ liệu.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách kỹ năng cùng điểm/tiến độ.</response>
        /// <response code="400">Yêu cầu không hợp lệ: tham số truyền vào sai (nếu có).</response>
        /// <response code="401">Chưa xác thực: Thiếu hoặc sai Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi tổng hợp hoặc truy vấn dữ liệu.</response>
        [HttpGet("skill-breakdown")]
        public async Task<IActionResult> GetSkillBreakdown(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await analyticsService.GetSkillBreakdownAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
