using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application;
using RikiPath.Application.Common;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Devices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Devices;
using RikiPath.Domain.Entities;

namespace RikiPath.WebApi.Controllers
{
    [ApiController]
    [Route("api/devices")]
    [Authorize]
    public class DeviceTokenController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClaimService _claimService;

        public DeviceTokenController(IUnitOfWork unitOfWork, IClaimService claimService)
        {
            _unitOfWork = unitOfWork;
            _claimService = claimService;
        }

        /// <summary>
        /// Registers or updates an FCM device token for the currently authenticated user.
        /// Handles token rotation, multi-device fan-out, and cross-user isolation.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.DeviceIdentifier))
                return BadRequest(ApiResponse.Fail("DeviceIdentifier is required.", HttpStatusCode.BadRequest));

            if (string.IsNullOrWhiteSpace(request.FcmToken))
                return BadRequest(ApiResponse.Fail("FcmToken is required.", HttpStatusCode.BadRequest));

            var userId = _claimService.GetUserClaim().Id;
            var token = await _unitOfWork.UserDeviceTokens.RegisterOrUpdateTokenAsync(
                userId,
                request.DeviceIdentifier.Trim(),
                string.IsNullOrWhiteSpace(request.Platform) ? "Android" : request.Platform.Trim(),
                request.FcmToken.Trim(),
                cancellationToken);

            var response = MapToResponse(token);
            return Ok(ApiResponse<DeviceTokenResponse>.Success(response));
        }

        /// <summary>
        /// Refreshes an existing device token for the currently authenticated user.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
        {
            return await RegisterToken(request, cancellationToken);
        }

        /// <summary>
        /// Revokes/deactivates a device token upon logout.
        /// </summary>
        [HttpPost("unregister")]
        public async Task<IActionResult> UnregisterTokenPost([FromBody] UnregisterDeviceTokenRequest request, CancellationToken cancellationToken)
        {
            return await ExecuteUnregister(request, cancellationToken);
        }

        /// <summary>
        /// Revokes/deactivates a device token upon logout (DELETE verb).
        /// </summary>
        [HttpDelete("unregister")]
        public async Task<IActionResult> UnregisterTokenDelete([FromBody] UnregisterDeviceTokenRequest request, CancellationToken cancellationToken)
        {
            return await ExecuteUnregister(request, cancellationToken);
        }

        /// <summary>
        /// Returns all active registered devices for the current user.
        /// </summary>
        [HttpGet("my-tokens")]
        public async Task<IActionResult> GetMyTokens(CancellationToken cancellationToken)
        {
            var userId = _claimService.GetUserClaim().Id;
            var tokens = await _unitOfWork.UserDeviceTokens.GetActiveTokensByUserIdAsync(userId, cancellationToken);
            var response = tokens.Select(MapToResponse).ToList();
            return Ok(ApiResponse<System.Collections.Generic.List<DeviceTokenResponse>>.Success(response));
        }

        private async Task<IActionResult> ExecuteUnregister(UnregisterDeviceTokenRequest request, CancellationToken cancellationToken)
        {
            var userId = _claimService.GetUserClaim().Id;

            if (!string.IsNullOrWhiteSpace(request.FcmToken))
            {
                await _unitOfWork.UserDeviceTokens.DeactivateTokenAsync(request.FcmToken.Trim(), cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(request.DeviceIdentifier))
            {
                await _unitOfWork.UserDeviceTokens.DeactivateDeviceAsync(userId, request.DeviceIdentifier.Trim(), cancellationToken);
            }

            return Ok(ApiResponse.Success("Device token successfully unregistered."));
        }

        private static DeviceTokenResponse MapToResponse(UserDeviceToken t) => new()
        {
            Id = t.Id,
            UserId = t.UserId,
            DeviceIdentifier = t.DeviceIdentifier,
            Platform = t.Platform,
            FcmToken = t.FcmToken,
            IsActive = t.IsActive,
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.UpdatedAtUtc
        };
    }
}
