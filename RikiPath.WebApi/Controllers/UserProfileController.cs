using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(Roles = "Learner")]
    public class UserProfileController(IUserProfileService userProfileService) : ControllerBase
    {
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.GetProfileAsync(userId, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] RikiPath.Application.Requests.Profile.UpdateProfileRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateProfileAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("me/avatar")]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateAvatarAsync(userId, avatarFile, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("me/jlpt-goal")]
        public async Task<IActionResult> SetJlptGoal([FromBody] RikiPath.Application.Requests.Profile.SetJlptGoalRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.SetJlptGoalAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPut("me/notification-settings")]
        public async Task<IActionResult> UpdateNotificationSettings([FromBody] RikiPath.Application.Requests.Profile.UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await userProfileService.UpdateNotificationSettingsAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
