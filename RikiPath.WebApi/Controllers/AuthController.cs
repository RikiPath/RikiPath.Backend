using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Auth;

namespace RikiPath.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.VerifyEmailAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPut("update-email/{userId:int}")]
        public async Task<IActionResult> UpdateEmail(int userId, [FromBody] UpdateEmailRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.UpdateEmailAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("change-password/{userId:int}")]
        public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            var result = await authService.ChangePasswordAsync(userId, request, cancellationToken);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
