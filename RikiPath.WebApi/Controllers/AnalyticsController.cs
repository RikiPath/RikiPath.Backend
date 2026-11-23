using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Analytics;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
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
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Thường trả nhanh (&lt;200ms); FE nên hiển thị loading placeholder khi chờ.
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
            var result = await analyticsService.GetStudyStreakAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Kiểm tra sự kiện chúc mừng chuỗi ngày học của hôm nay (giờ Việt Nam) cho người học hiện tại.
        /// </summary>
        /// <remarks>
        /// - Chỉ đọc: không bao giờ tạo hoạt động học hay sự kiện mới khi mở trang.
        /// - `hasPendingEvent = true` chỉ khi hôm nay đã có hoạt động học hợp lệ và sự kiện chưa được nhận trên bất kỳ thiết bị nào.
        /// </remarks>
        /// <response code="200">Trạng thái sự kiện, chuỗi hiện tại, mốc và các ngày học trong tuần.</response>
        [HttpGet("streak-celebration")]
        public async Task<IActionResult> GetStreakCelebration(CancellationToken cancellationToken)
        {
            var result = await analyticsService.GetStreakCelebrationAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nhận (claim) sự kiện chúc mừng chuỗi ngày học của hôm nay ngay trước khi hiển thị popup.
        /// </summary>
        /// <remarks>
        /// - Idempotent theo learner + ngày học: chỉ một request thắng (`claimed = true`); tab/thiết bị khác nhận `claimed = false`.
        /// - Máy chủ tự tính lại sự kiện; `eventId` cũ (ví dụ qua nửa đêm) không được nhận.
        /// </remarks>
        /// <response code="200">`claimed = true` nếu request này được phép hiển thị popup.</response>
        [HttpPost("streak-celebration/claim")]
        public async Task<IActionResult> ClaimStreakCelebration(
            [FromBody] ClaimStreakCelebrationRequest request,
            CancellationToken cancellationToken)
        {
            var result = await analyticsService.ClaimStreakCelebrationAsync(request, cancellationToken);
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
            var result = await analyticsService.GetCompletionStatsAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy phân tích theo kỹ năng (skill breakdown) của người học.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang chi tiết năng lực / LanguageSkill Insights trong ứng dụng Learner.
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
        public async Task<IActionResult> GetLanguageSkillBreakdown(CancellationToken cancellationToken)
        {
            var result = await analyticsService.GetLanguageSkillBreakdownAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy năng lực cá nhân theo từng kỹ năng theo tháng, từ Placement Test và Mock Test đã nộp.
        /// </summary>
        /// <param name="months">Số tháng cần xem, từ 1 đến 24; mặc định 12 tháng.</param>
        /// <param name="cancellationToken">Token hủy request.</param>
        [HttpGet("skill-monthly")]
        public async Task<IActionResult> GetMonthlySkillAnalytics(
            [FromQuery] int months = 12,
            CancellationToken cancellationToken = default)
        {
            var result = await analyticsService.GetMonthlySkillAnalyticsAsync(months, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
