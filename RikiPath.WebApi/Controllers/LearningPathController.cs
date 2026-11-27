using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using RikiPath.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.LearningPath;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.LearningPaths;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class LearningPathController(
        ILearningPathService learningPathService,
        SubscriptionAccessService subscriptions,
        IClaimService claimService,
        ILearnerSetupStateService setupStateService) : ControllerBase
    {
        private static readonly ConcurrentDictionary<int, Task<ApiResponse<LearningPathResponse>>> InFlightGenerations = new();

        private async Task<IActionResult?> ValidateAiPrerequisiteAsync(
            int userId,
            int? requestedTargetLevelId,
            CancellationToken cancellationToken)
        {
            var setupState = await setupStateService.ResolveForUserAsync(userId, includeProgress: false, cancellationToken);
            if (setupState is null)
                return StatusCode(StatusCodes.Status404NotFound, ApiResponse<LearningPathResponse>.NotFound("Không tìm thấy người dùng."));

            // Never allow request body level IDs to bypass confirmed Learning Goal prerequisite.
            if (!setupState.HasConfirmedGoal)
            {
                var fail = ApiResponse<LearningPathResponse>.Fail(
                    "Vui lòng thiết lập mục tiêu JLPT (N5–N2) trước khi tạo lộ trình học AI.",
                    System.Net.HttpStatusCode.BadRequest, errorCode: "LEARNING_PATH_GOAL_REQUIRED");
                return StatusCode((int)fail.StatusCode, fail);
            }

            if (requestedTargetLevelId.HasValue && requestedTargetLevelId != setupState.TargetJlptLevelId)
            {
                var hasPlacementForTarget = await setupStateService.HasValidCompletedPlacementForTargetAsync(
                    userId, requestedTargetLevelId.Value, cancellationToken);
                if (!hasPlacementForTarget)
                {
                    var fail = ApiResponse<LearningPathResponse>.Fail(
                        "Bạn cần hoàn thành bài đánh giá năng lực hợp lệ và tương thích cho cấp độ mục tiêu này trước khi tạo lộ trình AI.",
                        System.Net.HttpStatusCode.Conflict, errorCode: "LEARNING_PATH_PLACEMENT_REQUIRED");
                    return StatusCode((int)fail.StatusCode, fail);
                }
                return null;
            }

            if (!setupState.AiRoadmapEligible)
            {
                var errorCode = setupState.PlacementCompatibilityStatus == "INSUFFICIENT_FOR_TARGET"
                    ? "PLACEMENT_INSUFFICIENT_FOR_TARGET"
                    : "LEARNING_PATH_PLACEMENT_REQUIRED";
                var message = setupState.PlacementCompatibilityStatus == "INSUFFICIENT_FOR_TARGET"
                    ? (setupState.PlacementCompatibilityReason ?? "Kết quả đánh giá hiện tại chưa đủ dữ liệu năng lực cho mục tiêu này. Vui lòng hoàn thành đánh giá bổ sung.")
                    : setupState.IsPlacementExplicitlySkipped
                        ? "Bạn đã chọn bỏ qua đánh giá năng lực. Hãy hoàn thành bài đánh giá năng lực để mở khóa lộ trình cá nhân hóa AI."
                        : "Bạn cần hoàn thành bài đánh giá năng lực hợp lệ trước khi tạo lộ trình cá nhân hóa AI.";

                var fail = ApiResponse<LearningPathResponse>.Fail(
                    message,
                    System.Net.HttpStatusCode.Conflict, errorCode: errorCode);
                return StatusCode((int)fail.StatusCode, fail);
            }

            return null;
        }
        /// <summary>Lấy lộ trình học gần nhất đã được tạo cho Learner hiện tại.</summary>
        /// <remarks>Không tự gọi AI. Result là null khi người học chưa từng tạo lộ trình.</remarks>
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Lấy lộ trình ACTIVE; tương đương GET /latest.</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetLatestLearningPathAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Bắt đầu hoặc tiếp tục một ngày đang được mở trong lộ trình ACTIVE.</summary>
        [HttpPost("active/days/{day:int}/start")]
        public async Task<IActionResult> StartActiveDay(int day, CancellationToken cancellationToken)
        {
            var response = await learningPathService.StartActiveDayAsync(day, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Đánh dấu ngày học hoàn thành để mở ngày tiếp theo.</summary>
        [HttpPost("active/days/{day:int}/complete")]
        public async Task<IActionResult> CompleteActiveDay(int day, CancellationToken cancellationToken)
        {
            var response = await learningPathService.CompleteActiveDayAsync(day, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Lịch sử lộ trình của Learner, gồm ACTIVE và SUPERSEDED.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
        {
            var response = await learningPathService.GetHistoryAsync(cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Xem trước thay đổi cấp độ mà không thay thế lộ trình đang ACTIVE.</summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewLearningPathRequest request, CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var prereqError = await ValidateAiPrerequisiteAsync(
                userId, request?.TargetCertificateLevelId ?? request?.TargetJlptLevelId, cancellationToken);
            if (prereqError is not null) return prereqError;

            var response = await subscriptions.UseAiAsync("learning_path",
                () => learningPathService.PreviewLearningPathAsync(request!, cancellationToken), cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Xác nhận preview và kích hoạt atomically; retry cùng ID trả lại bản ACTIVE.</summary>
        [HttpPost("previews/{previewId:int}/confirm")]
        public async Task<IActionResult> Confirm(int previewId, CancellationToken cancellationToken)
        {
            var response = await learningPathService.ConfirmPreviewAsync(previewId, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>Hủy preview; không thay đổi lộ trình hiện tại.</summary>
        [HttpDelete("previews/{previewId:int}")]
        public async Task<IActionResult> Cancel(int previewId, CancellationToken cancellationToken)
        {
            var response = await learningPathService.CancelPreviewAsync(previewId, cancellationToken);
            return StatusCode((int)response.StatusCode, response);
        }

        /// <summary>
        /// Tạo đề xuất lộ trình học cá nhân hóa bằng AI cho người dùng.
        /// </summary>
        /// <remarks>
        /// Màn hình sử dụng: Màn hình tạo lộ trình học cá nhân (Learning Path) trên app Learner.
        /// 
        /// Luồng xử lý: API gọi dịch vụ AI ngầm để sinh đề xuất dựa trên trình độ/mục tiêu của người học (theo ngày, tích hợp tùy chọn từ vựng, kanji, ngữ pháp), lưu vào CSDL và trả về kết quả.
        /// 
        /// Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner). Thời gian xử lý phụ thuộc model và số đợt 30 ngày; FE cần hiển thị trạng thái đang tạo và hỗ trợ hủy yêu cầu.
        /// </remarks>
        /// <param name="request">Loại chứng chỉ/cấp độ, ngày thi, số phút cố định hoặc min/max và horizon; trường bỏ trống dùng hồ sơ hiện tại.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về lộ trình học tập được thiết kế bởi AI.</response>
        /// <response code="400">Yêu cầu không hợp lệ: Thiếu thông tin profile hoặc định dạng JSON không đúng.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="429">Bị từ chối do vượt quá hạn ngạch gọi AI (Too Many Requests / Quota Exceeded).</response>
        /// <response code="500">Lỗi server hoặc dịch vụ AI không phản hồi.</response>
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            [FromBody] GenerateLearningPathRequest request,
            CancellationToken cancellationToken)
        {
            var userId = claimService.GetUserClaim().Id;
            var prereqError = await ValidateAiPrerequisiteAsync(
                userId, request?.TargetCertificateLevelId ?? request?.TargetJlptLevelId, cancellationToken);
            if (prereqError is not null) return prereqError;

            if (InFlightGenerations.TryGetValue(userId, out var existingTask))
            {
                var sharedResponse = await existingTask.WaitAsync(cancellationToken);
                return StatusCode((int)sharedResponse.StatusCode, sharedResponse);
            }

            var tcs = new TaskCompletionSource<ApiResponse<LearningPathResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!InFlightGenerations.TryAdd(userId, tcs.Task))
            {
                if (InFlightGenerations.TryGetValue(userId, out var racedTask))
                {
                    var sharedResponse = await racedTask.WaitAsync(cancellationToken);
                    return StatusCode((int)sharedResponse.StatusCode, sharedResponse);
                }
            }

            try
            {
                // Detach shared AI generation from caller 1's cancellation token so aborting one tab
                // never fails coalesced concurrent callers awaiting the same in-flight task.
                using var serverTimeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var response = await subscriptions.UseAiAsync("learning_path",
                    () => learningPathService.GenerateLearningPathAsync(request!, serverTimeoutCts.Token),
                    serverTimeoutCts.Token);
                tcs.TrySetResult(response);
                var awaitedResponse = await tcs.Task.WaitAsync(cancellationToken);
                return StatusCode((int)awaitedResponse.StatusCode, awaitedResponse);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                throw;
            }
            finally
            {
                InFlightGenerations.TryRemove(new KeyValuePair<int, Task<ApiResponse<LearningPathResponse>>>(userId, tcs.Task));
            }
        }
    }
}
