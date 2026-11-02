using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorScheduling;

namespace RikiPath.WebApi.Controllers;

[ApiController]
[Authorize]
public class MentorCreditLedgerController : ControllerBase
{
    private readonly IMentorCreditLedgerService _creditLedgerService;

    public MentorCreditLedgerController(IMentorCreditLedgerService creditLedgerService)
    {
        _creditLedgerService = creditLedgerService;
    }

    // Học viên kiểm tra số dư lượt tư vấn, thời hạn các gói và biến động gần nhất.
    [HttpGet("api/v1/learner/credits/balance")]
    [HttpGet("api/learner/credits/balance")]
    [HttpGet("api/v1/mentor-credits/balance")]
    [HttpGet("api/mentor-credits/balance")]
    [Authorize(Roles = "Learner")]
    public async Task<ActionResult<ApiResponse<LearnerCreditBalanceResponse>>> GetMyBalance(
        CancellationToken cancellationToken)
    {
        var result = await _creditLedgerService.GetMyCreditBalanceAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Học viên xem lịch sử chi tiết các bút toán tín dụng (Grant, Spend, Refund).
    [HttpGet("api/v1/learner/credits/history")]
    [HttpGet("api/learner/credits/history")]
    [HttpGet("api/v1/mentor-credits/statement")]
    [HttpGet("api/mentor-credits/statement")]
    [Authorize(Roles = "Learner")]
    public async Task<ActionResult<ApiResponse<List<CreditEntryItemResponse>>>> GetMyHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _creditLedgerService.GetMyCreditHistoryAsync(page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    // Quản trị viên đối soát tính toàn vẹn và khớp đúng của sổ cái tín dụng học viên.
    [HttpPost("api/v1/admin/credits/reconcile/{learnerId:int}")]
    [HttpPost("api/admin/credits/reconcile/{learnerId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<CreditReconciliationResponse>>> ReconcileLearnerCredits(
        int learnerId,
        CancellationToken cancellationToken)
    {
        var result = await _creditLedgerService.ReconcileLearnerCreditsForAdminAsync(learnerId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
