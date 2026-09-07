using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.SharedBanks;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class SharedBankController(ISharedBankSearchService sharedBankSearchService) : ControllerBase
    {
        /// <summary>
        /// Tìm kiếm từ vựng trong shared bank theo bộ lọc.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Search / Lookup từ vựng trong app (FE có thể gọi khi user tra cứu từ).
        /// - Luồng xử lý: Truy vấn chỉ mục/DB theo filter (text, jlpt level, tags); không gọi AI.
        /// - Lưu ý cho FE: Thường không cần Bearer Token (tùy policy). Gửi filter trong body; FE nên debounce input để tránh spam API.
        /// </remarks>
        /// <param name="filter">VocabularySearchFilter: Query (Optional), JlptLevel (Optional), Page (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách từ vựng phù hợp với filter.</response>
        /// <response code="400">Dữ liệu không hợp lệ: filter sai định dạng.</response>
        /// <response code="500">Lỗi server khi truy vấn chỉ mục/DB.</response>
        [HttpPost("vocabulary/search")]
        public async Task<IActionResult> SearchVocabulary([FromBody] VocabularySearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchVocabularyAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy chi tiết một từ vựng theo ID.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Detail panel hoặc modal khi user click vào từ.
        /// - Luồng xử lý: Truy vấn DB/ELK để lấy định nghĩa, ví dụ, JLPT level.
        /// - Lưu ý cho FE: Thường không cần Bearer Token. Xử lý trường hợp 404 nếu không tìm thấy.
        /// </remarks>
        /// <param name="id">ID của từ vựng (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết từ vựng.</response>
        /// <response code="404">Không tìm thấy từ vựng với ID này.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("vocabulary/{id:int}")]
        public async Task<IActionResult> GetVocabularyDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetVocabularyDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tìm kiếm Kanji theo bộ lọc.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Tra cứu Kanji / Search Kanji.
        /// - Luồng xử lý: Truy vấn DB theo filter (character, strokes, jlpt level).
        /// - Lưu ý cho FE: Thường không cần Bearer Token. FE nên debounce input và hỗ trợ pagination.
        /// </remarks>
        /// <param name="filter">KanjiSearchFilter: Query (Optional), Strokes (Optional), Page (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách Kanji khớp filter.</response>
        /// <response code="400">Filter sai định dạng.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpPost("kanji/search")]
        public async Task<IActionResult> SearchKanji([FromBody] KanjiSearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchKanjiAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy chi tiết Kanji theo ID.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Detail Kanji view.
        /// - Luồng xử lý: Truy vấn DB/ELK để lấy onyomi, kunyomi, meanings, examples.
        /// - Lưu ý cho FE: Thường không cần Bearer Token. Xử lý trường hợp 404.
        /// </remarks>
        /// <param name="id">ID Kanji (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết Kanji.</response>
        /// <response code="404">Không tìm thấy Kanji.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("kanji/{id:int}")]
        public async Task<IActionResult> GetKanjiDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetKanjiDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Tìm kiếm điểm ngữ pháp theo bộ lọc.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Tra cứu grammar trong app.
        /// - Luồng xử lý: Truy vấn DB theo keyword, JLPT level hoặc tags.
        /// - Lưu ý cho FE: Thường không cần Bearer Token. FE nên debounce input và xử lý pagination.
        /// </remarks>
        /// <param name="filter">GrammarSearchFilter: Query (Optional), JlptLevel (Optional), Page (Optional).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về danh sách grammar khớp filter.</response>
        /// <response code="400">Filter sai định dạng.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpPost("grammar/search")]
        public async Task<IActionResult> SearchGrammar([FromBody] GrammarSearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchGrammarAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Lấy chi tiết điểm ngữ pháp theo ID.
        /// </summary>
        /// <remarks>
        /// - Màn hình sử dụng: Detail grammar view.
        /// - Luồng xử lý: Truy vấn DB để trả về định nghĩa, ví dụ, tầm dùng.
        /// - Lưu ý cho FE: Thường không cần Bearer Token. Xử lý 404 nếu không tìm thấy.
        /// </remarks>
        /// <param name="id">ID grammar (Required).</param>
        /// <param name="cancellationToken">Token hủy request (Optional).</param>
        /// <response code="200">Thành công: Trả về chi tiết grammar.</response>
        /// <response code="404">Không tìm thấy grammar.</response>
        /// <response code="500">Lỗi server khi truy vấn dữ liệu.</response>
        [HttpGet("grammar/{id:int}")]
        public async Task<IActionResult> GetGrammarDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetGrammarDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
