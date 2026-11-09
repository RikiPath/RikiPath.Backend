using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;

namespace RikiPath.WebApi.Controllers;

/// <summary>
/// Quản trị thù lao Cố vấn, hệ số JLPT, đối soát buổi tư vấn, bảng tính thù lao và quy trình chi trả.
/// </summary>
[ApiController]
[Route("api/admin/mentor-compensation")]
[Authorize(Roles = "Admin")]
public sealed class AdminMentorCompensationController(IMentorCompensationService compensationService) : ControllerBase
{
    /// <summary>
    /// Xem danh sách đơn giá thù lao theo giờ của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem các mức đơn giá thù lao theo giờ đang áp dụng chung hoặc cho từng Cố vấn cụ thể.
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (User ID, tùy chọn).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách đơn giá thù lao thành công.</response>
    [HttpGet("hourly-rates")]
    public async Task<IActionResult> GetHourlyRates([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorHourlyRatesAsync(mentorUserId, ct));

    /// <summary>
    /// Cấu hình đơn giá thù lao theo giờ cho Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên thiết lập hoặc cập nhật mức thù lao theo giờ cho toàn hệ thống hoặc cho một Cố vấn đặc thù.
    /// </remarks>
    /// <param name="request">Thông tin cấu hình đơn giá thù lao mới bao gồm số tiền, loại hình áp dụng và ngày hiệu lực.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Cấu hình đơn giá thù lao thành công.</response>
    [HttpPost("hourly-rates")]
    public async Task<IActionResult> ConfigureHourlyRate([FromBody] ConfigureMentorHourlyRateRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureMentorHourlyRateAsync(request, ct));

    /// <summary>
    /// Xem danh sách hệ số thù lao theo trình độ JLPT.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem bảng hệ số nhân thù lao tương ứng với từng cấp độ JLPT giảng dạy (N5 đến N1).
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách hệ số thù lao thành công.</response>
    [HttpGet("jlpt-coefficients")]
    public async Task<IActionResult> GetJlptCoefficients(CancellationToken ct)
        => ToActionResult(await compensationService.GetJlptCoefficientsAsync(ct));

    /// <summary>
    /// Cấu hình hệ số thù lao theo trình độ JLPT.
    /// </summary>
    /// <remarks>
    /// Quản trị viên cập nhật hệ số nhân thù lao cho từng trình độ chứng chỉ JLPT.
    /// </remarks>
    /// <param name="request">Cấu hình hệ số thù lao theo cấp độ JLPT.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Cấu hình hệ số thù lao thành công.</response>
    [HttpPost("jlpt-coefficients")]
    public async Task<IActionResult> ConfigureJlptCoefficient([FromBody] ConfigureJlptCoefficientRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureJlptCoefficientAsync(request, ct));

    /// <summary>
    /// Xem danh sách quyền giảng dạy JLPT của các Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên tra cứu danh sách các cấp độ JLPT mà Cố vấn đã được cấp phép giảng dạy trên nền tảng.
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (User ID, tùy chọn).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách quyền giảng dạy thành công.</response>
    [HttpGet("qualifications")]
    public async Task<IActionResult> GetQualifications([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorQualificationsAsync(mentorUserId, ct));

    /// <summary>
    /// Cấp quyền giảng dạy trình độ JLPT cho Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên cấp phép cho Cố vấn được nhận các buổi tư vấn ở một trình độ JLPT xác định kèm hệ số thù lao.
    /// </remarks>
    /// <param name="request">Thông tin cấp quyền giảng dạy gồm mã Cố vấn, cấp độ JLPT và tài liệu chứng minh.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Cấp quyền giảng dạy thành công.</response>
    [HttpPost("qualifications/grant")]
    public async Task<IActionResult> GrantQualification([FromBody] GrantMentorTeachingQualificationRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.GrantTeachingQualificationAsync(request, ct));

    /// <summary>
    /// Thu hồi quyền giảng dạy trình độ JLPT của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên hủy bỏ quyền nhận lịch tư vấn ở một trình độ JLPT nhất định nếu Cố vấn không còn đáp ứng tiêu chuẩn.
    /// </remarks>
    /// <param name="request">Thông tin thu hồi quyền giảng dạy và lý do thu hồi.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Thu hồi quyền giảng dạy thành công.</response>
    [HttpPost("qualifications/revoke")]
    public async Task<IActionResult> RevokeQualification([FromBody] RevokeMentorTeachingQualificationRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.RevokeTeachingQualificationAsync(request, ct));

    /// <summary>
    /// Tự động đánh giá thời lượng và tính hợp lệ của buổi tư vấn.
    /// </summary>
    /// <remarks>
    /// Hệ thống tính toán thời lượng có mặt thực tế từ nhật ký phòng họp và đề xuất trạng thái hoàn thành hợp lệ.
    /// </remarks>
    /// <param name="bookingId">Mã định danh buổi tư vấn (Booking ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Đánh giá thời lượng buổi tư vấn thành công.</response>
    [HttpPost("bookings/{bookingId:int}/attendance/evaluate")]
    public async Task<IActionResult> EvaluateBookingAttendance(int bookingId, CancellationToken ct)
        => ToActionResult(await compensationService.EvaluateBookingAttendanceAsync(bookingId, ct));

    /// <summary>
    /// Quản trị viên xét duyệt thời lượng tham gia buổi tư vấn thủ công.
    /// </summary>
    /// <remarks>
    /// Quản trị viên can thiệp ghi nhận thời lượng thực tế của buổi tư vấn trong trường hợp có tranh chấp hoặc sự cố kỹ thuật.
    /// </remarks>
    /// <param name="bookingId">Mã định danh buổi tư vấn (Booking ID).</param>
    /// <param name="request">Thời lượng buổi tư vấn được công nhận và ghi chú xét duyệt.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Xét duyệt thời lượng buổi tư vấn thành công.</response>
    [HttpPost("bookings/{bookingId:int}/attendance/review")]
    public async Task<IActionResult> ReviewBookingAttendance(
        int bookingId,
        [FromBody] ReviewBookingAttendanceRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ReviewBookingAttendanceAsync(bookingId, request, ct));

    /// <summary>
    /// Xem kết quả xác nhận thời lượng và điểm danh buổi tư vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem dữ liệu điểm danh, thời lượng tham gia thực tế của Cố vấn và Học viên trong buổi tư vấn.
    /// </remarks>
    /// <param name="bookingId">Mã định danh buổi tư vấn (Booking ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất kết quả xác nhận thời lượng thành công.</response>
    [HttpGet("bookings/{bookingId:int}/attendance")]
    public async Task<IActionResult> GetBookingAttendanceApproval(int bookingId, CancellationToken ct)
        => ToActionResult(await compensationService.GetBookingAttendanceApprovalAsync(bookingId, ct));

    /// <summary>
    /// Xem sổ cái thu nhập thù lao buổi tư vấn của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên tra cứu các bản ghi thu nhập phát sinh từ các buổi tư vấn hoàn thành, lọc theo Cố vấn hoặc thời gian.
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (User ID, tùy chọn).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất sổ cái thu nhập thù lao thành công.</response>
    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorEarningsAsync(mentorUserId, ct));

    /// <summary>
    /// Khởi tạo kỳ đối soát thù lao tháng cho Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên tổng hợp các buổi tư vấn đã hoàn thành trong tháng để phát sinh bảng tính thù lao kỳ đối soát.
    /// </remarks>
    /// <param name="request">Thông tin kỳ đối soát cần tạo bao gồm năm, tháng và phạm vi Cố vấn áp dụng.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Khởi tạo kỳ đối soát thù lao thành công.</response>
    [HttpPost("payrolls/generate")]
    public async Task<IActionResult> GenerateMonthlyPayroll([FromBody] GenerateMonthlyPayrollRequest request, CancellationToken ct)
        => ToActionResult(await compensationService.GenerateMonthlyPayrollAsync(request, ct));

    /// <summary>
    /// Xem danh sách các kỳ đối soát thù lao của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên truy xuất danh sách các kỳ đối soát thù lao theo tháng của các Cố vấn, cho phép lọc theo năm, tháng và Cố vấn.
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (tùy chọn).</param>
    /// <param name="year">Năm cần lọc kỳ đối soát thù lao (ví dụ: 2026).</param>
    /// <param name="month">Tháng cần lọc kỳ đối soát thù lao (từ 1 đến 12).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách kỳ đối soát thù lao thành công.</response>
    [HttpGet("payrolls")]
    public async Task<IActionResult> GetPayrollPeriods(
        [FromQuery] int? mentorUserId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodsAsync(mentorUserId, year, month, ct));

    /// <summary>
    /// Xem chi tiết một kỳ đối soát thù lao của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem chi tiết bảng tính thù lao: số buổi dạy, đơn giá, hệ số, phụ cấp, các khoản thưởng/phạt và số tiền thực nhận.
    /// </remarks>
    /// <param name="payrollPeriodId">Mã định danh kỳ đối soát thù lao (ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất chi tiết kỳ đối soát thù lao thành công.</response>
    /// <response code="404">Không tìm thấy kỳ đối soát theo mã định danh.</response>
    [HttpGet("payrolls/{payrollPeriodId:long}")]
    public async Task<IActionResult> GetPayrollPeriod(long payrollPeriodId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayrollPeriodAsync(payrollPeriodId, ct));

    /// <summary>
    /// Thêm bút toán điều chỉnh vào kỳ đối soát thù lao.
    /// </summary>
    /// <remarks>
    /// Quản trị viên ghi nhận các khoản cộng thưởng, trừ phạt hoặc điều chỉnh sai sót vào kỳ đối soát trước khi duyệt.
    /// </remarks>
    /// <param name="payrollPeriodId">Mã định danh kỳ đối soát thù lao (ID).</param>
    /// <param name="request">Thông tin bút toán điều chỉnh: loại điều chỉnh (Thưởng/Phạt), số tiền và lý do cụ thể.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Thêm bút toán điều chỉnh thành công.</response>
    [HttpPost("payrolls/{payrollPeriodId:long}/adjustments")]
    public async Task<IActionResult> AddPayrollAdjustment(
        long payrollPeriodId,
        [FromBody] CreatePayrollAdjustmentRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.AddPayrollAdjustmentAsync(payrollPeriodId, request, ct));

    /// <summary>
    /// Phê duyệt kỳ đối soát thù lao và phát hành nghĩa vụ chi trả.
    /// </summary>
    /// <remarks>
    /// Quản trị viên chốt bảng đối soát thù lao, chuyển trạng thái sang đã phê duyệt và tự động tạo nghĩa vụ chi trả cho Cố vấn.
    /// </remarks>
    /// <param name="payrollPeriodId">Mã định danh kỳ đối soát thù lao (ID).</param>
    /// <param name="request">Quyết định phê duyệt kỳ đối soát và ghi chú của Quản trị viên.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Phê duyệt kỳ đối soát thù lao thành công.</response>
    [HttpPost("payrolls/{payrollPeriodId:long}/approve")]
    public async Task<IActionResult> ApproveMonthlyPayroll(
        long payrollPeriodId,
        [FromBody] ApproveMonthlyPayrollRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ApproveMonthlyPayrollAsync(payrollPeriodId, request, ct));

    /// <summary>
    /// Xem danh sách tài khoản ngân hàng thụ hưởng của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem danh sách các tài khoản ngân hàng nhận thù lao của Cố vấn (toàn hệ thống hoặc lọc theo mentorUserId).
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (User ID, tùy chọn).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách tài khoản ngân hàng thành công.</response>
    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetMentorBankAccounts([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetMentorBankAccountsForAdminAsync(mentorUserId, ct));

    /// <summary>
    /// Xác thực và phê duyệt tài khoản ngân hàng thụ hưởng của Cố vấn.
    /// </summary>
    /// <remarks>
    /// Quản trị viên duyệt hoặc từ chối thông tin tài khoản ngân hàng của Cố vấn sau khi đối chiếu thông tin ngân hàng.
    /// </remarks>
    /// <param name="bankAccountId">Mã định danh tài khoản ngân hàng thụ hưởng (ID).</param>
    /// <param name="request">Trạng thái xác thực (Duyệt/Từ chối) và ghi chú đối soát của Quản trị viên.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Xác thực tài khoản ngân hàng thành công.</response>
    [HttpPost("bank-accounts/{bankAccountId:long}/verify")]
    public async Task<IActionResult> VerifyMentorBankAccount(
        long bankAccountId,
        [FromBody] VerifyMentorBankAccountRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.VerifyMentorBankAccountAsync(bankAccountId, request, ct));

    /// <summary>
    /// Xem danh sách tài khoản nguồn chi trả của tổ chức.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem các tài khoản ngân hàng và ví nguồn của hệ thống dùng để thực hiện chi trả thù lao cho Cố vấn.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách tài khoản nguồn chi trả thành công.</response>
    [HttpGet("organization-payout-accounts")]
    public async Task<IActionResult> GetOrganizationPayoutAccounts(CancellationToken ct)
        => ToActionResult(await compensationService.GetOrganizationPayoutAccountsAsync(ct));

    /// <summary>
    /// Cấu hình tài khoản nguồn chi trả của tổ chức.
    /// </summary>
    /// <remarks>
    /// Quản trị viên thiết lập hoặc cập nhật thông tin tài khoản ngân hàng nguồn chi trả thù lao.
    /// </remarks>
    /// <param name="request">Thông tin tài khoản nguồn chi trả của tổ chức.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Cấu hình tài khoản nguồn chi trả thành công.</response>
    [HttpPost("organization-payout-accounts")]
    public async Task<IActionResult> ConfigureOrganizationPayoutAccount(
        [FromBody] ConfigureOrganizationPayoutAccountRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ConfigureOrganizationPayoutAccountAsync(request, ct));

    /// <summary>
    /// Xem danh sách các nghĩa vụ chi trả thù lao.
    /// </summary>
    /// <remarks>
    /// Quản trị viên theo dõi tất cả các lệnh chi trả thù lao phát sinh từ các kỳ đối soát đã được phê duyệt.
    /// </remarks>
    /// <param name="mentorUserId">Mã tài khoản người dùng của Cố vấn (tùy chọn).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất danh sách nghĩa vụ chi trả thành công.</response>
    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayoutObligations([FromQuery] int? mentorUserId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationsAsync(mentorUserId, ct));

    /// <summary>
    /// Xem chi tiết một nghĩa vụ chi trả thù lao.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xem thông tin chi tiết lệnh chi trả: số tiền, Cố vấn thụ hưởng, trạng thái chuyển tiền và nhật ký đối soát.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất chi tiết nghĩa vụ chi trả thành công.</response>
    /// <response code="404">Không tìm thấy nghĩa vụ chi trả theo mã định danh.</response>
    [HttpGet("payouts/{obligationId:long}")]
    public async Task<IActionResult> GetPayoutObligation(long obligationId, CancellationToken ct)
        => ToActionResult(await compensationService.GetPayoutObligationAsync(obligationId, ct));

    /// <summary>
    /// Lựa chọn phương thức chi trả thù lao.
    /// </summary>
    /// <remarks>
    /// Quản trị viên chọn kênh thanh toán cho nghĩa vụ chi trả (Chuyển khoản thủ công hoặc Cổng tự động payOS Payout).
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="request">Phương thức chi trả được chọn (Manual hoặc PayOS).</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Chọn phương thức chi trả thành công.</response>
    [HttpPost("payouts/{obligationId:long}/select-method")]
    public async Task<IActionResult> SelectPayoutMethod(
        long obligationId,
        [FromBody] SelectPayoutMethodRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.SelectPayoutMethodAsync(obligationId, request, ct));

    /// <summary>
    /// Khởi tạo lệnh chi trả chuyển khoản ngân hàng thủ công.
    /// </summary>
    /// <remarks>
    /// Quản trị viên chuyển trạng thái nghĩa vụ chi trả sang đang xử lý thủ công để chuẩn bị ủy nhiệm chi ngân hàng.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="request">Ghi chú khởi tạo quy trình chi trả ngân hàng thủ công.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Khởi tạo chi trả thủ công thành công.</response>
    [HttpPost("payouts/{obligationId:long}/manual/initiate")]
    public async Task<IActionResult> InitiateManualPayout(
        long obligationId,
        [FromBody] InitiateManualPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.InitiateManualPayoutAsync(obligationId, request, ct));

    /// <summary>
    /// Xác nhận đã hoàn tất chi trả chuyển khoản ngân hàng thủ công.
    /// </summary>
    /// <remarks>
    /// Quản trị viên xác nhận đã chuyển tiền thành công ngoài hệ thống ngân hàng, đính kèm mã tham chiếu giao dịch và ngày chuyển.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="request">Mã tham chiếu ngân hàng, ngày giao dịch và ghi chú hoàn tất chuyển khoản thủ công.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Xác nhận hoàn tất chi trả thủ công thành công.</response>
    [HttpPost("payouts/{obligationId:long}/manual/confirm")]
    public async Task<IActionResult> ConfirmManualPayout(
        long obligationId,
        [FromBody] ConfirmManualPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ConfirmManualPayoutAsync(obligationId, request, ct));

    /// <summary>
    /// Thực hiện chi trả tức thì qua cổng payOS Payout.
    /// </summary>
    /// <remarks>
    /// Quản trị viên gửi lệnh chuyển tiền tức thì cho một nghĩa vụ chi trả cụ thể qua cổng tích hợp payOS.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="request">Yêu cầu thực hiện chi trả qua cổng payOS.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Thực hiện chi trả qua cổng payOS thành công.</response>
    [HttpPost("payouts/{obligationId:long}/payos/execute")]
    public async Task<IActionResult> ExecutePayOsPayout(
        long obligationId,
        [FromBody] ExecutePayOsPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ExecutePayOsPayoutAsync(obligationId, request, ct));

    /// <summary>
    /// Thực hiện chi trả thù lao hàng loạt qua cổng payOS Payout.
    /// </summary>
    /// <remarks>
    /// Quản trị viên kích hoạt lệnh chi trả tự động hàng loạt cho các nghĩa vụ chi trả đã chọn qua cổng thanh toán payOS.
    /// </remarks>
    /// <param name="request">Danh sách mã nghĩa vụ chi trả cần thực hiện tự động qua payOS.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Thực hiện chi trả hàng loạt qua payOS thành công.</response>
    [HttpPost("payouts/payos/batch")]
    public async Task<IActionResult> ExecutePayOsBatchPayout(
        [FromBody] ExecutePayOsBatchPayoutRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ExecutePayOsBatchPayoutAsync(request, ct));

    /// <summary>
    /// Đối soát trạng thái giao dịch chi trả thù lao.
    /// </summary>
    /// <remarks>
    /// Quản trị viên đối soát trạng thái giao dịch với ngân hàng hoặc cổng payOS để cập nhật trạng thái chi trả cuối cùng.
    /// </remarks>
    /// <param name="obligationId">Mã định danh nghĩa vụ chi trả thù lao (ID).</param>
    /// <param name="request">Thông tin đối soát trạng thái giao dịch chi trả.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Đối soát trạng thái chi trả thành công.</response>
    [HttpPost("payouts/{obligationId:long}/reconcile")]
    public async Task<IActionResult> ReconcilePayoutObligation(
        long obligationId,
        [FromBody] ReconcilePayoutObligationRequest request,
        CancellationToken ct)
        => ToActionResult(await compensationService.ReconcilePayoutObligationAsync(obligationId, request, ct));

    /// <summary>
    /// Xem số dư khả dụng trên cổng chi trả payOS Payout.
    /// </summary>
    /// <remarks>
    /// Quản trị viên kiểm tra số dư ví chi trả hiện tại trên cổng dịch vụ payOS để đảm bảo đủ tiền chi trả hàng loạt.
    /// </remarks>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Truy xuất số dư ví payOS thành công.</response>
    [HttpGet("payouts/payos/balance")]
    public async Task<IActionResult> GetPayOsPayoutBalance(CancellationToken ct)
        => ToActionResult(await compensationService.GetPayOsPayoutBalanceAsync(ct));

    /// <summary>
    /// Ước tính tổng tiền và phí cần nạp vào ví payOS trước khi chi trả.
    /// </summary>
    /// <remarks>
    /// Tính toán tổng số tiền thực nhận và phí giao dịch dự kiến cho danh sách các đợt chi trả để chuẩn bị nguồn vốn.
    /// </remarks>
    /// <param name="obligationIds">Danh sách các mã nghĩa vụ chi trả cần ước tính số tiền.</param>
    /// <param name="ct">Token hủy yêu cầu.</param>
    /// <response code="200">Ước tính số tiền chi trả thành công.</response>
    [HttpPost("payouts/payos/estimate-credit")]
    public async Task<IActionResult> EstimatePayOsBatchCredit([FromBody] List<long> obligationIds, CancellationToken ct)
        => ToActionResult(await compensationService.EstimatePayOsBatchCreditAsync(obligationIds, ct));

    private IActionResult ToActionResult<T>(ApiResponse<T> response)
        => StatusCode(response.StatusCode, response);
}
