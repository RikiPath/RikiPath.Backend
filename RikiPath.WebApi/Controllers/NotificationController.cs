using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class NotificationController(INotificationService notificationService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool onlyUnread = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.GetMyNotificationsAsync(userId, onlyUnread, page, pageSize, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPatch("{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPatch("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
