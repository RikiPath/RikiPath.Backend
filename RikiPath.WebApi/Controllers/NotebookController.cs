using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class NotebookController(INotebookService notebookService) : ControllerBase
    {
        [HttpPost("lists")]
        public async Task<IActionResult> CreateList([FromBody] CreateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.CreateListAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("lists")]
        public async Task<IActionResult> GetMyLists(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.GetMyListsAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPut("lists/{listId:int}")]
        public async Task<IActionResult> UpdateList(int listId, [FromBody] UpdateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.UpdateListAsync(userId, listId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpDelete("lists/{listId:int}")]
        public async Task<IActionResult> DeleteList(int listId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.DeleteListAsync(userId, listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("lists/{listId:int}/entries")]
        public async Task<IActionResult> GetListEntries(int listId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.GetListEntriesAsync(userId, listId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("bookmark/vocabulary")]
        public async Task<IActionResult> BookmarkVocabulary([FromBody] BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkVocabularyAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("bookmark/kanji")]
        public async Task<IActionResult> BookmarkKanji([FromBody] BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkKanjiAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("bookmark/grammar/{grammarPointId:int}")]
        public async Task<IActionResult> BookmarkGrammar(int grammarPointId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.BookmarkGrammarAsync(userId, grammarPointId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("entries")]
        public async Task<IActionResult> AddManualEntry([FromBody] AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.AddManualEntryAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpDelete("entries/{noteEntryId:int}")]
        public async Task<IActionResult> RemoveEntry(int noteEntryId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notebookService.RemoveEntryAsync(userId, noteEntryId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
