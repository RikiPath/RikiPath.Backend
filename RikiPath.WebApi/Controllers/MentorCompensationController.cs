using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/mentor/compensation")]
[Authorize(Roles = "Mentor")]
public sealed class MentorCompensationController(IMentorCompensationService compensationService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetMyOverview(CancellationToken ct)
        => ToActionResult(await compensationService.GetMyCompensationOverviewAsync(ct));

    [HttpGet("hourly-rates")]
    public async Task<IActionResult> GetMyHourlyRates(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorHourlyRatesAsync(null, ct));

    [HttpGet("qualifications")]
    public async Task<IActionResult> GetMyQualifications(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorQualificationsAsync(null, ct));

    [HttpGet("earnings")]
    public async Task<IActionResult> GetMyEarnings(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorEarningsAsync(null, ct));

    [HttpGet("payrolls")]
    public async Task<IActionResult> GetMyPayrollPeriods([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodsAsync(null, year, month, ct));

    [HttpGet("payrolls/{payrollPeriodId:long}")]
    public async Task<IActionResult> GetMyPayrollPeriod(long payrollPeriodId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodAsync(payrollPeriodId, ct));

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetMyBankAccounts(CancellationToken ct)
        => ToActionResult(await compensationService.GetMyBankAccountsAsync(ct));

    [HttpPost("bank-accounts")]
    public async Task<IActionResult> UpsertMyBankAccount([FromBody] UpsertMentorBankAccountRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.UpsertMyBankAccountAsync(request, ct));

    [HttpGet("payouts")]
    public async Task<IActionResult> GetMyPayouts(CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationsAsync(null, ct));

    [HttpGet("payouts/{obligationId:long}")]
    public async Task<IActionResult> GetMyPayoutObligation(long obligationId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationAsync(obligationId, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
