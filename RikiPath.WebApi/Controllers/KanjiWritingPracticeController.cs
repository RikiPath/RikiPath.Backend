using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.KanjiWriting;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class KanjiWritingPracticeController(IKanjiWritingPracticeService kanjiWritingPracticeService) : ControllerBase
    {
        /// <summary>
        /// Danh sách Kanji cần luyện viết hôm nay (ưu tiên chữ đến hạn ôn, bổ sung chữ mới nếu thiếu).
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Trang "Luyện viết Kanji".
        /// - Luồng xử lý: FE dùng field Character để khởi tạo HanziWriter cho từng chữ; nếu IsNew =
        ///   true, nên phát animation stroke-order mẫu (HanziWriter.animateCharacter()) trước khi cho
        ///   learner tự vẽ (quiz()), thay vì bắt vẽ ngay.
        /// </remarks>
        [HttpGet("due")]
        public async Task<IActionResult> GetDue([FromQuery] int count = 10, CancellationToken cancellationToken = default)
        {
            var result = await kanjiWritingPracticeService.GetDueForPracticeAsync(count, cancellationToken);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll([FromQuery] int count = 100, CancellationToken cancellationToken = default)
        {
            var result = await kanjiWritingPracticeService.GetAllForPracticeAsync(count, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Ghi nhận kết quả 1 lượt luyện viết 1 chữ Kanji, tính lại lịch ôn theo SM-2.
        /// </summary>
        /// <remarks>
        /// - Luồng xử lý: Gọi ngay sau khi HanziWriter.quiz()'s onComplete callback trả về, truyền
        ///   thẳng summaryData.totalMistakes vào TotalMistakes.
        /// </remarks>
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitResult(
            [FromBody] SubmitKanjiWritingResultRequest request, CancellationToken cancellationToken)
        {
            var result = await kanjiWritingPracticeService.SubmitResultAsync(request, cancellationToken);
            return Ok(result);
        }

        [HttpGet("scores")]
        public async Task<IActionResult> GetScores(CancellationToken cancellationToken)
        {
            var result = await kanjiWritingPracticeService.GetScoresAsync(cancellationToken);
            return Ok(result);
        }
    }
}