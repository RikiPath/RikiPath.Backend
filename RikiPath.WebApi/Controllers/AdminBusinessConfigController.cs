using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminConfig;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.AdminConfig;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminBusinessConfigController(
    IAdminBusinessConfigService service,
    IClaimService claims) : ControllerBase
{
    [HttpGet("subscription-plans")]
    [ProducesResponseType(typeof(ApiResponse<AdminSubscriptionPlansWorkspaceResponse>), 200)]
    public async Task<IActionResult> GetSubscriptionPlans(CancellationToken ct)
    {
        var result = await service.GetSubscriptionPlansAsync(ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("subscription-plans")]
    [ProducesResponseType(typeof(ApiResponse<AdminSubscriptionPlanDto>), 200)]
    public async Task<IActionResult> CreateSubscriptionPlan([FromBody] SaveSubscriptionPlanRequest request, CancellationToken ct)
    {
        var result = await service.SaveSubscriptionPlanAsync(null, request, claims.GetUserClaim().Id, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPut("subscription-plans/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminSubscriptionPlanDto>), 200)]
    public async Task<IActionResult> UpdateSubscriptionPlan(int id, [FromBody] SaveSubscriptionPlanRequest request, CancellationToken ct)
    {
        var result = await service.SaveSubscriptionPlanAsync(id, request, claims.GetUserClaim().Id, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<AdminPaymentListResponse>), 200)]
    public async Task<IActionResult> GetPayments([FromQuery] AdminPaymentQuery query, CancellationToken ct)
    {
        var result = await service.GetPaymentsAsync(query, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("payments/{stream}/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminPaymentDetailDto>), 200)]
    public async Task<IActionResult> GetPaymentDetail(string stream, int id, CancellationToken ct)
    {
        var result = await service.GetPaymentDetailAsync(stream, id, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("payments/gateway-status")]
    [ProducesResponseType(typeof(ApiResponse<PayOsGatewayStatusDto>), 200)]
    public async Task<IActionResult> GetPayOsStatus(CancellationToken ct)
    {
        var result = await service.GetPayOsStatusAsync(ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminAuditLogDto>>), 200)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await service.GetAuditLogsAsync(entityType, entityId, take, ct);
        return StatusCode((int)result.StatusCode, result);
    }
}
