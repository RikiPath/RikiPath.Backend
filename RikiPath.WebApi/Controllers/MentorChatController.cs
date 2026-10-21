using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/mentor-chat")]
[Authorize(Roles = "Learner,Mentor")]
public sealed class MentorChatController(IMentorChatService chatService) : ControllerBase
{
    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads(CancellationToken cancellationToken)
        => ToActionResult(await chatService.GetThreadsAsync(cancellationToken));

    [HttpGet("threads/{bookingId:int}")]
    public async Task<IActionResult> GetThread(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await chatService.GetThreadAsync(bookingId, cancellationToken));

    [HttpGet("threads/{bookingId:int}/messages")]
    public async Task<IActionResult> GetMessages(
        int bookingId,
        [FromQuery] int? beforeMessageId,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => ToActionResult(await chatService.GetMessagesAsync(
            bookingId, beforeMessageId, pageSize, cancellationToken));

    [HttpPost("threads/{bookingId:int}/read")]
    public async Task<IActionResult> MarkRead(int bookingId, CancellationToken cancellationToken)
        => ToActionResult(await chatService.MarkReadAsync(bookingId, cancellationToken));

    private IActionResult ToActionResult<T>(RikiPath.Application.Responses.ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
