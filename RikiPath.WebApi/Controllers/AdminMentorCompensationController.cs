using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Route("api/admin/mentor-compensation")]
[Authorize(Roles = "Admin")]
public sealed class AdminMentorCompensationController(IMentorCompensationService compensationService) : ControllerBase
{
    [HttpGet("hourly-rates")]
    public async Task<IActionResult> GetHourlyRates([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorHourlyRatesAsync(mentorUserId, ct));

    [HttpPost("hourly-rates")]
    public async Task<IActionResult> ConfigureHourlyRate([FromBody] ConfigureMentorHourlyRateRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureMentorHourlyRateAsync(request, ct));

    [HttpGet("jlpt-coefficients")]
    public async Task<IActionResult> GetJlptCoefficients(CancellationToken ct)
        => ToActionResult(await compensationService.GetJlptCoefficientsAsync(ct));

    [HttpPost("jlpt-coefficients")]
    public async Task<IActionResult> ConfigureJlptCoefficient([FromBody] ConfigureJlptCoefficientRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureJlptCoefficientAsync(request, ct));

    [HttpGet("qualifications")]
    public async Task<IActionResult> GetQualifications([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorQualificationsAsync(mentorUserId, ct));

    [HttpPost("qualifications/grant")]
    public async Task<IActionResult> GrantQualification([FromBody] GrantMentorTeachingQualificationRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.GrantTeachingQualificationAsync(request, ct));

    [HttpPost("qualifications/revoke")]
    public async Task<IActionResult> RevokeQualification([FromBody] RevokeMentorTeachingQualificationRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.RevokeTeachingQualificationAsync(request, ct));

    [HttpPost("bookings/{bookingId:int}/attendance/evaluate")]
    public async Task<IActionResult> EvaluateBookingAttendance(int bookingId, CancellationToken ct)
        => ToActionResult(await compensationService.EvaluateBookingAttendanceAsync(bookingId, ct));

    [HttpPost("bookings/{bookingId:int}/attendance/review")]
    public async Task<IActionResult> ReviewBookingAttendance(
        int bookingId,
        [FromBody] ReviewBookingAttendanceRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ReviewBookingAttendanceAsync(bookingId, request, ct));

    [HttpGet("bookings/{bookingId:int}/attendance")]
    public async Task<IActionResult> GetBookingAttendanceApproval(int bookingId, CancellationToken ct)
        => ToActionResult(await compensationService.GetBookingAttendanceApprovalAsync(bookingId, ct));

    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorEarningsAsync(mentorUserId, ct));

    [HttpPost("payrolls/generate")]
    public async Task<IActionResult> GenerateMonthlyPayroll([FromBody] GenerateMonthlyPayrollRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.GenerateMonthlyPayrollAsync(request, ct));

    [HttpGet("payrolls")]
    public async Task<IActionResult> GetPayrollPeriods(
        [FromQuery] int? mentorUserId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodsAsync(mentorUserId, year, month, ct));

    [HttpGet("payrolls/{payrollPeriodId:long}")]
    public async Task<IActionResult> GetPayrollPeriod(long payrollPeriodId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodAsync(payrollPeriodId, ct));

    [HttpPost("payrolls/{payrollPeriodId:long}/adjustments")]
    public async Task<IActionResult> AddPayrollAdjustment(
        long payrollPeriodId,
        [FromBody] CreatePayrollAdjustmentRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.AddPayrollAdjustmentAsync(payrollPeriodId, request, ct));

    [HttpPost("payrolls/{payrollPeriodId:long}/approve")]
    public async Task<IActionResult> ApproveMonthlyPayroll(
        long payrollPeriodId,
        [FromBody] ApproveMonthlyPayrollRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ApproveMonthlyPayrollAsync(payrollPeriodId, request, ct));

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetMentorBankAccounts([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorBankAccountsForAdminAsync(mentorUserId, ct));

    [HttpPost("bank-accounts/{bankAccountId:long}/verify")]
    public async Task<IActionResult> VerifyMentorBankAccount(
        long bankAccountId,
        [FromBody] VerifyMentorBankAccountRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.VerifyMentorBankAccountAsync(bankAccountId, request, ct));

    [HttpGet("organization-payout-accounts")]
    public async Task<IActionResult> GetOrganizationPayoutAccounts(CancellationToken ct)
        => ToActionResult(await compensationService.GetOrganizationPayoutAccountsAsync(ct));

    [HttpPost("organization-payout-accounts")]
    public async Task<IActionResult> ConfigureOrganizationPayoutAccount(
        [FromBody] ConfigureOrganizationPayoutAccountRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureOrganizationPayoutAccountAsync(request, ct));

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayoutObligations([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationsAsync(mentorUserId, ct));

    [HttpGet("payouts/{obligationId:long}")]
    public async Task<IActionResult> GetPayoutObligation(long obligationId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationAsync(obligationId, ct));

    [HttpPost("payouts/{obligationId:long}/select-method")]
    public async Task<IActionResult> SelectPayoutMethod(
        long obligationId,
        [FromBody] SelectPayoutMethodRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.SelectPayoutMethodAsync(obligationId, request, ct));

    [HttpPost("payouts/{obligationId:long}/manual/initiate")]
    public async Task<IActionResult> InitiateManualPayout(
        long obligationId,
        [FromBody] InitiateManualPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.InitiateManualPayoutAsync(obligationId, request, ct));

    [HttpPost("payouts/{obligationId:long}/manual/confirm")]
    public async Task<IActionResult> ConfirmManualPayout(
        long obligationId,
        [FromBody] ConfirmManualPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ConfirmManualPayoutAsync(obligationId, request, ct));

    [HttpPost("payouts/{obligationId:long}/payos/execute")]
    public async Task<IActionResult> ExecutePayOsPayout(
        long obligationId,
        [FromBody] ExecutePayOsPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ExecutePayOsPayoutAsync(obligationId, request, ct));

    [HttpPost("payouts/payos/batch")]
    public async Task<IActionResult> ExecutePayOsBatchPayout(
        [FromBody] ExecutePayOsBatchPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ExecutePayOsBatchPayoutAsync(request, ct));

    [HttpPost("payouts/{obligationId:long}/reconcile")]
    public async Task<IActionResult> ReconcilePayoutObligation(
        long obligationId,
        [FromBody] ReconcilePayoutObligationRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ReconcilePayoutObligationAsync(obligationId, request, ct));

    [HttpGet("payouts/payos/balance")]
    public async Task<IActionResult> GetPayOsPayoutBalance(CancellationToken ct)
        => ToActionResult(await compensationService.GetPayOsPayoutBalanceAsync(ct));

    [HttpPost("payouts/payos/estimate-credit")]
    public async Task<IActionResult> EstimatePayOsBatchCredit([FromBody] List<long> obligationIds, CancellationToken ct)
        => ToActionResult(await compensationService.EstimatePayOsBatchCreditAsync(obligationIds, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
