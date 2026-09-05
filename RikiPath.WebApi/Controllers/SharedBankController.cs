using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.SharedBanks;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class SharedBankController(ISharedBankSearchService sharedBankSearchService) : ControllerBase
    {
        [HttpPost("vocabulary/search")]
        public async Task<IActionResult> SearchVocabulary([FromBody] VocabularySearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchVocabularyAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("vocabulary/{id:int}")]
        public async Task<IActionResult> GetVocabularyDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetVocabularyDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("kanji/search")]
        public async Task<IActionResult> SearchKanji([FromBody] KanjiSearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchKanjiAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("kanji/{id:int}")]
        public async Task<IActionResult> GetKanjiDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetKanjiDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("grammar/search")]
        public async Task<IActionResult> SearchGrammar([FromBody] GrammarSearchFilter filter, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.SearchGrammarAsync(filter, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("grammar/{id:int}")]
        public async Task<IActionResult> GetGrammarDetail(int id, CancellationToken cancellationToken)
        {
            var result = await sharedBankSearchService.GetGrammarDetailAsync(id, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
