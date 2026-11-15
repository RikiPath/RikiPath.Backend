using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

/// <summary>
/// Quản lý thù lao, sổ cái thu nhập, kỳ đối soát và thanh toán của Cố vấn.
/// </summary>
[ApiController]
[Route("api/mentor/compensation")]
[Authorize(Roles = "Mentor")]
public sealed class MentorCompensationController(IMentorCompensationService compensationService) : ControllerBase
{
    /// <summary>
    /// Xem tổng quan thù lao và trạng thái tài chính của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Trả về tổng quan thu nhập tích lũy, các kỳ đối soát gần nhất, trạng thái tài khoản ngân hàng thụ hưởng và các nghĩa vụ chi trả đang chờ của Cố vấn hiện tại.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất tổng quan thù lao thành công.</response>
    /// <response code="401">Chưa xác thực hoặc phiên đăng nhập hết hạn.</response>
    /// <response code="403">Tài khoản không có vai trò Cố vấn.</response>
    [HttpGet("overview")]
    public async Task<IActionResult> GetMyOverview(CancellationToken ct)
        => ToActionResult(await compensationService.GetMyCompensationOverviewAsync(ct));

    /// <summary>
    /// Xem đơn giá thù lao theo giờ hiện áp dụng của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Lấy danh sách các mức thù lao theo giờ hiện áp dụng cho Cố vấn hiện tại theo từng loại hình buổi tư vấn hoặc chính sách hệ thống.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách đơn giá thù lao thành công.</response>
    /// <response code="401">Chưa xác thực.</response>
    [HttpGet("hourly-rates")]
    public async Task<IActionResult> GetMyHourlyRates(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorHourlyRatesAsync(null, ct));

    /// <summary>
    /// Xem danh sách chứng chỉ và quyền giảng dạy JLPT của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Hiển thị các trình độ JLPT (N5 - N1) mà Cố vấn đã được Quản trị viên cấp quyền giảng dạy, cùng hệ số thù lao tương ứng.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách quyền giảng dạy thành công.</response>
    /// <response code="401">Chưa xác thực.</response>
    [HttpGet("qualifications")]
    public async Task<IActionResult> GetMyQualifications(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorQualificationsAsync(null, ct));

    /// <summary>
    /// Xem sổ cái thu nhập thù lao buổi tư vấn của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Truy xuất danh sách các khoản thù lao phát sinh từ các buổi tư vấn đã hoàn thành, bao gồm số tiền cơ sở, hệ số JLPT, trạng thái ghi nhận vào kỳ đối soát và số tiền thực nhận.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất sổ cái thu nhập thành công.</response>
    /// <response code="401">Chưa xác thực.</response>
    [HttpGet("earnings")]
    public async Task<IActionResult> GetMyEarnings(CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorEarningsAsync(null, ct));

    /// <summary>
    /// Xem danh sách các kỳ đối soát thù lao của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Lấy danh sách các kỳ đối soát thù lao (theo tháng hoặc chu kỳ đối soát) của Cố vấn đăng nhập. Cho phép lọc theo năm và tháng.
    /// </remarks>
    /// <param name="year">Năm cần lọc kỳ đối soát thù lao (ví dụ: 2026).</param>
    /// <param name="month">Tháng cần lọc kỳ đối soát thù lao (từ 1 đến 12).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách kỳ đối soát thù lao thành công.</response>
    /// <response code="401">Chưa xác thực.</response>
    [HttpGet("payrolls")]
    public async Task<IActionResult> GetMyPayrollPeriods([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodsAsync(null, year, month, ct));

    /// <summary>
    /// Xem chi tiết thù lao trong một kỳ đối soát của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Trả về thông tin chi tiết của một kỳ đối soát thù lao cụ thể: tổng số buổi dạy, tổng tiền cơ sở, các bút toán điều chỉnh (thưởng/phạt), thuế/khấu trừ và trạng thái phê duyệt.
    /// </remarks>
    /// <param name="payrollPeriodId">Mã định danh kỳ đối soát thù lao (ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất chi tiết kỳ đối soát thù lao thành công.</response>
    /// <response code="404">Không tìm thấy kỳ đối soát theo mã định danh.</response>
    [HttpGet("payrolls/{payrollPeriodId:long}")]
    public async Task<IActionResult> GetMyPayrollPeriod(long payrollPeriodId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodAsync(payrollPeriodId, ct));

    /// <summary>
    /// Xem danh sách tài khoản ngân hàng thụ hưởng của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Trả về danh sách tài khoản ngân hàng nhận thù lao của Cố vấn. Số tài khoản được che mờ (mask) để bảo mật thông tin tài chính.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách tài khoản ngân hàng thành công.</response>
    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetMyBankAccounts(CancellationToken ct)
        => ToActionResult(await compensationService.GetMyBankAccountsAsync(ct));

    /// <summary>
    /// Thêm mới hoặc cập nhật tài khoản ngân hàng thụ hưởng của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Đăng ký hoặc cập nhật thông tin tài khoản ngân hàng (mã ngân hàng, tên chủ tài khoản, số tài khoản). Tài khoản mới cập nhật sẽ cần Quản trị viên xác minh trước khi được dùng để chi trả.
    /// </remarks>
    /// <param name="request">Thông tin tài khoản ngân hàng thụ hưởng mới của Cố vấn.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Cập nhật tài khoản ngân hàng thành công.</response>
    /// <response code="400">Thông tin tài khoản ngân hàng không hợp lệ.</response>
    [HttpPost("bank-accounts")]
    public async Task<IActionResult> UpsertMyBankAccount([FromBody] UpsertMentorBankAccountRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.UpsertMyBankAccountAsync(request, ct));

    /// <summary>
    /// Xem danh sách các đợt chi trả thù lao của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Lấy danh sách các nghĩa vụ chi trả thù lao phát sinh từ các kỳ đối soát đã được duyệt, bao gồm phương thức chi trả, trạng thái chuyển tiền và mã tham chiếu giao dịch.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách đợt chi trả thù lao thành công.</response>
    [HttpGet("payouts")]
    public async Task<IActionResult> GetMyPayouts(CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationsAsync(null, ct));

    /// <summary>
    /// Xem chi tiết một đợt chi trả thù lao của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Xem thông tin chi tiết của một lệnh chi trả thù lao cụ thể: số tiền chuyển, tài khoản thụ hưởng, thời gian khởi tạo và bằng chứng đối soát giao dịch.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất chi tiết đợt chi trả thù lao thành công.</response>
    /// <response code="404">Không tìm thấy đợt chi trả theo mã định danh.</response>
    [HttpGet("payouts/{obligationId:long}")]
    public async Task<IActionResult> GetMyPayoutObligation(long obligationId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationAsync(obligationId, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
