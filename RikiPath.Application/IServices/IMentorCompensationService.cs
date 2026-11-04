using RikiPath.Application.Requests.MentorCompensation;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorCompensation;

namespace RikiPath.Application.IServices;

public interface IMentorCompensationService
{
    // 1. Hourly Rate & JLPT Level Coefficient Configuration (Admin write; Admin / owning Mentor read)
    Task<ApiResponse<MentorHourlyRateResponse>> ConfigureMentorHourlyRateAsync(
        ConfigureMentorHourlyRateRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorHourlyRateResponse>>> GetMentorHourlyRatesAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<JlptLevelPayCoefficientResponse>> ConfigureJlptCoefficientAsync(
        ConfigureJlptCoefficientRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<JlptLevelPayCoefficientResponse>>> GetJlptCoefficientsAsync(
        CancellationToken cancellationToken = default);

    // 2. Mentor Teaching Qualifications N5–N2 (Admin write; Admin / owning Mentor read)
    Task<ApiResponse<MentorTeachingQualificationResponse>> GrantTeachingQualificationAsync(
        GrantMentorTeachingQualificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorTeachingQualificationResponse>> RevokeTeachingQualificationAsync(
        RevokeMentorTeachingQualificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorTeachingQualificationResponse>>> GetMentorQualificationsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default);

    // 3. Attendance Approval & Immutable Earning Ledger
    Task<ApiResponse<MentorAttendanceApprovalResponse>> EvaluateBookingAttendanceAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorAttendanceApprovalResponse>> ReviewBookingAttendanceAsync(
        int bookingId,
        ReviewBookingAttendanceRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorAttendanceApprovalResponse>> GetBookingAttendanceApprovalAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorEarningEntryResponse>>> GetMentorEarningsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default);

    // 4. Monthly Payroll & Signed Adjustments
    Task<ApiResponse<List<MentorPayrollPeriodResponse>>> GenerateMonthlyPayrollAsync(
        GenerateMonthlyPayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayrollAdjustmentResponse>> AddPayrollAdjustmentAsync(
        long payrollPeriodId,
        CreatePayrollAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayrollPeriodResponse>> ApproveMonthlyPayrollAsync(
        long payrollPeriodId,
        ApproveMonthlyPayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayrollPeriodResponse>> GetPayrollPeriodAsync(
        long payrollPeriodId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorPayrollPeriodResponse>>> GetPayrollPeriodsAsync(
        int? mentorUserId = null,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default);

    // 5. Mentor Bank Account Protection & Organization Payout Accounts
    Task<ApiResponse<MentorBankAccountResponse>> UpsertMyBankAccountAsync(
        UpsertMentorBankAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorBankAccountResponse>>> GetMyBankAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorBankAccountResponse>> VerifyMentorBankAccountAsync(
        long bankAccountId,
        VerifyMentorBankAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorBankAccountResponse>>> GetMentorBankAccountsForAdminAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrganizationPayoutAccountResponse>> ConfigureOrganizationPayoutAccountAsync(
        ConfigureOrganizationPayoutAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<OrganizationPayoutAccountResponse>>> GetOrganizationPayoutAccountsAsync(
        CancellationToken cancellationToken = default);

    // 6. Dual Payout (Manual Bank Transfer & payOS Payout) & Reconciliation
    Task<ApiResponse<MentorPayoutObligationResponse>> SelectPayoutMethodAsync(
        long obligationId,
        SelectPayoutMethodRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayoutObligationResponse>> InitiateManualPayoutAsync(
        long obligationId,
        InitiateManualPayoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayoutObligationResponse>> ConfirmManualPayoutAsync(
        long obligationId,
        ConfirmManualPayoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayoutObligationResponse>> ExecutePayOsPayoutAsync(
        long obligationId,
        ExecutePayOsPayoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PayOsBatchPayoutExecutionResponse>> ExecutePayOsBatchPayoutAsync(
        ExecutePayOsBatchPayoutRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayoutObligationResponse>> ReconcilePayoutObligationAsync(
        long obligationId,
        ReconcilePayoutObligationRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<MentorPayoutObligationResponse>> GetPayoutObligationAsync(
        long obligationId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<MentorPayoutObligationResponse>>> GetPayoutObligationsAsync(
        int? mentorUserId = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PayOsPayoutBalanceResponse>> GetPayOsPayoutBalanceAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<PayOsEstimateCreditResponse>> EstimatePayOsBatchCreditAsync(
        List<long> obligationIds,
        CancellationToken cancellationToken = default);

    // 7. Mentor Self-Service Overview
    Task<ApiResponse<MentorCompensationOverviewResponse>> GetMyCompensationOverviewAsync(
        CancellationToken cancellationToken = default);
}
