using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MockTests;
using RikiPath.Application.Requests.PlacementTests;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MockTests;
using RikiPath.Application.Responses.PlacementTests;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class MockTestController(IMockTestService mockTestService) : ControllerBase
    {
        /// <summary>
        /// Lấy trạng thái bài đánh giá năng lực (placement) của học viên.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình chính / onboarding, để quyết định có hiển thị bài placement hay không.
        /// - Luồng xử lý: Truy vấn trạng thái placement hiện tại của user; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token (Role = Learner).
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về trạng thái placement.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("placement/status")]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestStatusResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestStatusResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetStatusAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Bỏ qua bài đánh giá năng lực (skip placement).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Nút "Bỏ qua" ở màn hình giới thiệu bài placement.
        /// - Luồng xử lý: Cập nhật trạng thái placement của user thành đã bỏ qua.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Response trả về trạng thái placement mới.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về trạng thái placement sau khi bỏ qua.</response>
        /// <response code="400">Không thể bỏ qua (ví dụ đã hoàn thành placement).</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi cập nhật trạng thái.</response>
        [HttpPost("placement/skip")]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestStatusResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestStatusResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestStatusResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SkipPlacement(CancellationToken cancellationToken)
        {
            var result = await mockTestService.SkipPlacementAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy bài đánh giá năng lực (mock test) theo cấp độ chứng chỉ, kèm toàn bộ thông tin: sections, câu hỏi và các option.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Sau khi học viên chọn chứng chỉ và cấp độ, hiển thị bài đánh giá năng lực của cấp độ đó.
        /// - Luồng xử lý: Nhận certificateLevelId, trả về 1 đề thuộc cấp độ đó cùng các section, câu hỏi và option; không trả đáp án đúng; không gọi AI.
        /// - Lưu ý cho FE: Gọi API chứng chỉ và API cấp độ theo chứng chỉ trước để có certificateLevelId. Để làm bài và lưu đáp án, gọi placement/start rồi dùng attemptId trả về.
        /// </remarks>
        /// <param name="certificateLevelId">Id của cấp độ chứng chỉ (CertificateLevel) (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết bài đánh giá năng lực.</response>
        /// <response code="400">certificateLevelId không hợp lệ.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="404">Không tìm thấy cấp độ chứng chỉ hoặc chưa có bài đánh giá cho cấp độ này.</response>
        /// <response code="500">Lỗi hệ thống.</response>
        [HttpGet("placement/test")]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlacementTest(
            [FromQuery] int certificateLevelId,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetPlacementTestsByCertificateLevelAsync(certificateLevelId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy attempt placement đang làm dở (nếu có) để học viên làm tiếp.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Khi vào lại trang làm bài sau khi tắt web / mất điện giữa chừng.
        /// - Luồng xử lý: Truy vấn attempt chưa nộp của user, kèm snapshot câu hỏi và các đáp án đã autosave.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Data = null nghĩa là không có attempt đang làm dở.
        /// </remarks>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về snapshot attempt đang làm hoặc null.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("placement/active")]
        [ProducesResponseType(typeof(ApiResponse<PlacementAttemptSnapshotResponse?>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAttemptSnapshotResponse?>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetActiveAttempt(CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetActiveAttemptAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Bắt đầu một lần làm bài đánh giá năng lực (placement).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Màn hình Start Placement Test.
        /// - Luồng xử lý: Tạo attempt và snapshot bộ câu hỏi; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Body là tùy chọn (có thể bỏ trống). QuestionId/OptionId trong response là ID Backend phải dùng khi lưu đáp án và submit.
        /// </remarks>
        /// <param name="request">StartPlacementTestRequest (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về attemptId, thời gian và bộ câu hỏi/options.</response>
        /// <response code="400">Dữ liệu không hợp lệ hoặc không thể bắt đầu bài placement.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: Token không có role Learner.</response>
        /// <response code="500">Lỗi server khi tạo attempt.</response>
        [HttpPost("placement/start")]
        [ProducesResponseType(typeof(ApiResponse<PlacementAttemptSnapshotResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAttemptSnapshotResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAttemptSnapshotResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartPlacementTest(
            [FromBody] StartPlacementTestRequest? request,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.StartPlacementTestAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Autosave một câu trả lời ngay khi học viên chọn đáp án.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang làm bài placement, gọi mỗi khi học viên chọn/đổi đáp án.
        /// - Luồng xử lý: Lưu đáp án của một câu để nếu mất điện/tắt web thì quay lại vẫn còn tiến độ.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nên debounce và không chặn UI khi gọi.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="request">SavePlacementAnswerRequest: câu hỏi và đáp án đã chọn (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về xác nhận đã lưu.</response>
        /// <response code="400">Dữ liệu không hợp lệ hoặc attempt đã kết thúc.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="500">Lỗi server khi lưu đáp án.</response>
        [HttpPut("placement/attempts/{attemptId:int}/answer")]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswerAckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswerAckResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswerAckResponse>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SavePlacementAnswer(
            int attemptId,
            [FromBody] SavePlacementAnswerRequest request,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.SavePlacementAnswerAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lưu hàng loạt đáp án của attempt (không nộp bài).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang làm bài placement, dùng khi cần đồng bộ nhiều đáp án một lần (ví dụ khi có mạng trở lại).
        /// - Luồng xử lý: Lưu danh sách đáp án; chưa tính điểm và không kết thúc attempt.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Để kết thúc bài, gọi API submit.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="request">SubmitPlacementTestRequest: danh sách đáp án (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về xác nhận đã lưu.</response>
        /// <response code="400">Dữ liệu không hợp lệ hoặc attempt đã kết thúc.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="500">Lỗi server khi lưu đáp án.</response>
        [HttpPut("placement/attempts/{attemptId:int}/answers")]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswersSavedResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswersSavedResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PlacementAnswersSavedResponse>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SavePlacementAnswers(
            int attemptId,
            [FromBody] SubmitPlacementTestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.SavePlacementAnswersAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nộp bài đánh giá năng lực (submit placement).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Nút Submit trên trang làm bài placement.
        /// - Luồng xử lý: Tính điểm, xác định trình độ và lưu kết quả; có thể mất thêm thời gian nếu có bước chấm tự động.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token. Nên hiển thị loading khi chờ response.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="request">SubmitPlacementTestRequest: danh sách đáp án (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về kết quả placement.</response>
        /// <response code="400">Dữ liệu không hợp lệ: answers thiếu hoặc sai format.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="409">Attempt đã được nộp trước đó hoặc không hợp lệ.</response>
        /// <response code="500">Lỗi server khi tính điểm.</response>
        [HttpPost("placement/attempts/{attemptId:int}/submit")]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SubmitPlacementTest(
            int attemptId,
            [FromBody] SubmitPlacementTestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.SubmitPlacementTestAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy kết quả bài đánh giá năng lực của một attempt.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang kết quả placement.
        /// - Luồng xử lý: Truy vấn kết quả đã lưu; không gọi AI.
        /// - Lưu ý cho FE: Yêu cầu Bearer Token.
        /// </remarks>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về kết quả placement.</response>
        /// <response code="401">Chưa xác thực: Thiếu Bearer Token.</response>
        /// <response code="403">Không có quyền: attempt không thuộc user.</response>
        /// <response code="404">Không tìm thấy attempt.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("placement/attempts/{attemptId:int}/result")]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<PlacementTestResultResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlacementTestResult(int attemptId, CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetPlacementTestResultAsync(attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Bắt đầu một lần làm bài kiểm tra (practice test / mock test) cho user.
        /// </summary>
        /// <param name="practiceTestId">ID practice test (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpPost("{practiceTestId:int}/start")]
        [ProducesResponseType(typeof(ApiResponse<StartAttemptResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<StartAttemptResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<StartAttemptResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<StartAttemptResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartAttempt(int practiceTestId, CancellationToken cancellationToken)
        {
            var result = await mockTestService.StartAttemptAsync(practiceTestId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Nộp kết quả một lần làm bài (submit attempt).
        /// </summary>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="request">SubmitAttemptRequest: Answers (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpPost("attempts/{attemptId:int}/submit")]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SubmitAttempt(
            int attemptId,
            [FromBody] SubmitAttemptRequest request,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.SubmitAttemptAsync(attemptId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy kết quả tóm tắt của một attempt.
        /// </summary>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpGet("attempts/{attemptId:int}")]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<AttemptResultResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAttemptResult(int attemptId, CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetAttemptResultAsync(attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách practice tests theo JLPT level.
        /// </summary>
        /// <param name="jlptLevelId">JLPT level ID (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpGet("level/{jlptLevelId:int}/tests")]
        [ProducesResponseType(typeof(ApiResponse<List<TestSummaryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<TestSummaryResponse>>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<List<TestSummaryResponse>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<TestSummaryResponse>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTestsByLevel(int jlptLevelId, CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetTestsByLevelAsync(jlptLevelId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy bài đánh giá năng lực theo cấp độ chứng chỉ.
        /// </summary>
        /// <param name="certificateLevelId">Id của cấp độ chứng chỉ (CertificateLevel).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpGet("placement")]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<MockTestDetailResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlacementTests(
            [FromQuery] int certificateLevelId,
            CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetPlacementTestsByCertificateLevelAsync(certificateLevelId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy kết quả chi tiết của một attempt (per-question breakdown, feedback).
        /// </summary>
        /// <param name="attemptId">ID attempt (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpGet("attempts/{attemptId:int}/detailed")]
        [ProducesResponseType(typeof(ApiResponse<DetailedAttemptResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DetailedAttemptResultResponse>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DetailedAttemptResultResponse>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<DetailedAttemptResultResponse>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<DetailedAttemptResultResponse>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDetailedResult(int attemptId, CancellationToken cancellationToken)
        {
            var result = await mockTestService.GetDetailedResultAsync(attemptId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách câu hỏi sai để ôn tập lại (Quizlet Review Mode).
        /// </summary>
        /// <param name="practiceTestId">ID đề thi tùy chọn để lọc câu hỏi sai.</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        [HttpGet("wrong-questions")]
        [ProducesResponseType(typeof(ApiResponse<List<QuestionReviewCard>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<QuestionReviewCard>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<QuestionReviewCard>>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetWrongQuestionsReviewSession(
            [FromQuery] int? practiceTestId = null,
            CancellationToken cancellationToken = default)
        {
            var result = await mockTestService.GetWrongQuestionsReviewSessionAsync(practiceTestId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
