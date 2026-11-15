using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController(INotificationService notificationService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool onlyUnread = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var result = await notificationService.GetMyNotificationsAsync(onlyUnread, page, pageSize, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
        {
            var result = await notificationService.GetUnreadCountAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPatch("{notificationId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken cancellationToken)
        {
            var result = await notificationService.MarkAsReadAsync(notificationId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPatch("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
        {
            var result = await notificationService.MarkAllAsReadAsync(cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
