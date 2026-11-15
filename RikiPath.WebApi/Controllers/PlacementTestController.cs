using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.PlacementTests;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class PlacementTestController(IPracticeTestService placementTestService) : ControllerBase
    {
        /// <summary>
        /// Kiểm tra trạng thái hoàn thành bài kiểm tra đánh giá năng lực đầu vào (Placement Test) và mức độ sẵn sàng tạo lộ trình.
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        {
            var result = await placementTestService.GetStatusAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Ghi nhận Learner bỏ qua Placement lúc này; không tạo lượt thi hoặc điểm giả.</summary>
        /// <remarks>Có thể làm Placement sau khi bỏ qua. Kết quả thật mới chuyển quyết định thành COMPLETED.</remarks>
        [HttpPost("skip")]
        public async Task<IActionResult> Skip(CancellationToken cancellationToken)
        {
            var result = await placementTestService.SkipPlacementAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Khôi phục lượt làm bài kiểm tra đánh giá năng lực đầu vào đang diễn ra (chưa hết hạn).
        /// </summary>
        [HttpGet("attempts/active")]
        public async Task<IActionResult> GetActiveAttempt(CancellationToken cancellationToken)
        {
            var result = await placementTestService.GetActiveAttemptAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>Lưu tự động các lựa chọn mới nhất trong lượt Placement đang làm.</summary>
        [HttpPut("attempts/{attemptId:int}/answers")]
        public async Task<IActionResult> SavePlacementAnswers(
            int attemptId,
            [FromBody] SubmitPlacementTestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await placementTestService.SavePlacementAnswersAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Bắt đầu hoặc khôi phục bài đánh giá theo mục tiêu JLPT và ngày thi đã lưu.
        /// </summary>
        /// <remarks>Tối đa hai lượt bắt đầu cho mỗi cấp. Gửi RetakeTargetAssessment = true để làm lượt thứ hai sau khi đã có kết quả.</remarks>
        [HttpPost("start")]
        public async Task<IActionResult> StartPlacementTest(
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StartPlacementTestRequest? request,
            CancellationToken cancellationToken = default)
        {
            var result = await placementTestService.StartPlacementTestAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nộp bài kiểm tra đánh giá năng lực đầu vào, tính điểm theo cấp độ JLPT từ snapshot bất biến và đề xuất cấp độ mục tiêu.
        /// </summary>
        [HttpPost("attempts/{attemptId:int}/submit")]
        public async Task<IActionResult> SubmitPlacementTest(
            int attemptId,
            [FromBody] SubmitPlacementTestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await placementTestService.SubmitPlacementTestAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Xem kết quả bài kiểm tra đánh giá năng lực đầu vào đã hoàn thành.
        /// </summary>
        [HttpGet("attempts/{attemptId:int}")]
        public async Task<IActionResult> GetPlacementTestResult(
            int attemptId,
            CancellationToken cancellationToken)
        {
            var result = await placementTestService.GetPlacementTestResultAsync(attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
