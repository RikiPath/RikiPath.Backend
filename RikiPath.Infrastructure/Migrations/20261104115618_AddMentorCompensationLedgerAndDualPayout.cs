using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorCompensationLedgerAndDualPayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "UserSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CertificateLevelId",
                table: "MentorCreditGrants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompensationCurrency",
                table: "MentorBookings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreditGrantId",
                table: "MentorBookings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MentorAttendanceApprovals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RawMentorDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RawLearnerDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: true),
                    EvidenceSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAttendanceApprovals", x => x.Id);
                    table.CheckConstraint("CK_MentorAttendanceApproval_Minutes", "\"ApprovedEligibleMinutes\" IS NULL OR (\"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90)");
                    table.CheckConstraint("CK_MentorAttendanceApproval_RawDuration", "\"RawMentorDurationSeconds\" >= 0 AND \"RawLearnerDurationSeconds\" >= 0");
                    table.ForeignKey(
                        name: "FK_MentorAttendanceApprovals_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorAttendanceApprovals_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorBankAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    AccountVersion = table.Column<int>(type: "integer", nullable: false),
                    BankCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EncryptedAccountNumber = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccountNumberHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorBankAccounts", x => x.Id);
                    table.CheckConstraint("CK_MentorBankAccount_Version", "\"AccountVersion\" >= 1");
                    table.ForeignKey(
                        name: "FK_MentorBankAccounts_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorBankAccounts_Users_VerifiedByAdminId",
                        column: x => x.VerifiedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    SupplementalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalEarningsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAdjustmentsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreparedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollPeriods", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollPeriod_Payable", "\"TotalEarningsAmount\" >= 0 AND \"GrossPayableAmount\" >= 0");
                    table.CheckConstraint("CK_MentorPayrollPeriod_Period", "\"Year\" >= 2020 AND \"Year\" <= 2100 AND \"Month\" >= 1 AND \"Month\" <= 12 AND \"SequenceNumber\" >= 1");
                    table.CheckConstraint("CK_MentorPayrollPeriod_Supplemental", "\"SequenceNumber\" = 1 OR (\"SupplementalReason\" IS NOT NULL AND length(trim(\"SupplementalReason\")) > 0)");
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollPeriods_Users_PreparedByAdminId",
                        column: x => x.PreparedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationPayoutAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaskedAccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderAccountReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ConfiguredByAdminId = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationPayoutAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationPayoutAccounts_Users_ConfiguredByAdminId",
                        column: x => x.ConfiguredByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorEarningEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    AttendanceApprovalId = table.Column<long>(type: "bigint", nullable: false),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    CertificateLevelId = table.Column<int>(type: "integer", nullable: false),
                    JlptLevelCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FrozenHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FrozenLevelCoefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FormulaVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    OperationKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorEarningEntries", x => x.Id);
                    table.CheckConstraint("CK_MentorEarningEntry_Positive", "\"FrozenHourlyRate\" > 0 AND \"FrozenLevelCoefficient\" > 0 AND \"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90 AND \"GrossAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_CertificateLevels_CertificateLevelId",
                        column: x => x.CertificateLevelId,
                        principalTable: "CertificateLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorAttendanceApprovals_AttendanceAp~",
                        column: x => x.AttendanceApprovalId,
                        principalTable: "MentorAttendanceApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorEarningEntries_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayrollAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    RelatedEarningEntryId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedBookingId = table.Column<int>(type: "integer", nullable: true),
                    SignedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollAdjustments", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollAdjustment_NonZero", "\"SignedAmount\" <> 0");
                    table.ForeignKey(
                        name: "FK_MentorPayrollAdjustments_MentorPayrollPeriods_PayrollPeriod~",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollAdjustments_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayoutObligations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    MentorUserId = table.Column<int>(type: "integer", nullable: false),
                    LockedBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    OrganizationPayoutAccountId = table.Column<long>(type: "bigint", nullable: true),
                    PayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SelectedMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActiveAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    SettledAttemptId = table.Column<long>(type: "bigint", nullable: true),
                    SettledReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayoutObligations", x => x.Id);
                    table.CheckConstraint("CK_MentorPayoutObligation_Positive", "\"PayableAmount\" > 0");
                    table.CheckConstraint("CK_MentorPayoutObligation_Settlement", "(\"Status\" <> 'Paid') OR (\"SettledAttemptId\" IS NOT NULL AND \"SettledReference\" IS NOT NULL AND \"SettledAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_MentorBankAccounts_LockedBankAccoun~",
                        column: x => x.LockedBankAccountId,
                        principalTable: "MentorBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_OrganizationPayoutAccounts_Organiza~",
                        column: x => x.OrganizationPayoutAccountId,
                        principalTable: "OrganizationPayoutAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_Users_ApprovedByAdminId",
                        column: x => x.ApprovedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutObligations_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayrollLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollPeriodId = table.Column<long>(type: "bigint", nullable: false),
                    EarningEntryId = table.Column<long>(type: "bigint", nullable: false),
                    MentorBookingId = table.Column<int>(type: "integer", nullable: false),
                    FrozenHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FrozenLevelCoefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ApprovedEligibleMinutes = table.Column<int>(type: "integer", nullable: false),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayrollLines", x => x.Id);
                    table.CheckConstraint("CK_MentorPayrollLine_Positive", "\"LineAmount\" > 0 AND \"ApprovedEligibleMinutes\" > 0");
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorBookings_MentorBookingId",
                        column: x => x.MentorBookingId,
                        principalTable: "MentorBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorEarningEntries_EarningEntryId",
                        column: x => x.EarningEntryId,
                        principalTable: "MentorEarningEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayrollLines_MentorPayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "MentorPayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPayoutAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayoutObligationId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ExecutionMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BatchId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LockedBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    InternalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderPayoutId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TransferReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvidenceStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EvidenceNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InitiatedByAdminId = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPayoutAttempts", x => x.Id);
                    table.CheckConstraint("CK_MentorPayoutAttempt_ManualEvidence", "(\"ExecutionMethod\" <> 'ManualBankTransfer' OR \"InternalStatus\" <> 'Succeeded') OR (\"TransferReference\" IS NOT NULL AND \"EvidenceStorageKey\" IS NOT NULL)");
                    table.CheckConstraint("CK_MentorPayoutAttempt_Positive", "\"Amount\" > 0 AND \"AttemptNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_MentorBankAccounts_LockedBankAccountId",
                        column: x => x.LockedBankAccountId,
                        principalTable: "MentorBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_MentorPayoutObligations_PayoutObligati~",
                        column: x => x.PayoutObligationId,
                        principalTable: "MentorPayoutObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_Users_ConfirmedByAdminId",
                        column: x => x.ConfirmedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MentorPayoutAttempts_Users_InitiatedByAdminId",
                        column: x => x.InitiatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_CertificateLevelId",
                table: "UserSubscriptions",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_CertificateLevelId",
                table: "SubscriptionPlans",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_CertificateLevelId",
                table: "MentorCreditGrants",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorCreditGrants_LearnerId_CertificateLevelId_ExpiresAtUtc",
                table: "MentorCreditGrants",
                columns: new[] { "LearnerId", "CertificateLevelId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorBookings_CreditGrantId",
                table: "MentorBookings",
                column: "CreditGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceApprovals_MentorBookingId",
                table: "MentorAttendanceApprovals",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorAttendanceApprovals_ReviewedByAdminId",
                table: "MentorAttendanceApprovals",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_MentorUserId_AccountVersion",
                table: "MentorBankAccounts",
                columns: new[] { "MentorUserId", "AccountVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_MentorUserId_Current",
                table: "MentorBankAccounts",
                column: "MentorUserId",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_MentorBankAccounts_VerifiedByAdminId",
                table: "MentorBankAccounts",
                column: "VerifiedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_AttendanceApprovalId",
                table: "MentorEarningEntries",
                column: "AttendanceApprovalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_CertificateLevelId",
                table: "MentorEarningEntries",
                column: "CertificateLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_CreatedByAdminId",
                table: "MentorEarningEntries",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_MentorBookingId",
                table: "MentorEarningEntries",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_MentorUserId_Status",
                table: "MentorEarningEntries",
                columns: new[] { "MentorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_OperationKey",
                table: "MentorEarningEntries",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorEarningEntries_PayrollPeriodId",
                table: "MentorEarningEntries",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_ConfirmedByAdminId",
                table: "MentorPayoutAttempts",
                column: "ConfirmedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_IdempotencyKey",
                table: "MentorPayoutAttempts",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_InitiatedByAdminId",
                table: "MentorPayoutAttempts",
                column: "InitiatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_LockedBankAccountId",
                table: "MentorPayoutAttempts",
                column: "LockedBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_OneSucceededPerObligation",
                table: "MentorPayoutAttempts",
                column: "PayoutObligationId",
                unique: true,
                filter: "\"InternalStatus\" = 'Succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_PayoutObligationId_AttemptNumber",
                table: "MentorPayoutAttempts",
                columns: new[] { "PayoutObligationId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutAttempts_SucceededTransferReference",
                table: "MentorPayoutAttempts",
                column: "TransferReference",
                unique: true,
                filter: "\"TransferReference\" IS NOT NULL AND \"InternalStatus\" = 'Succeeded'");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_ApprovedByAdminId",
                table: "MentorPayoutObligations",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_LockedBankAccountId",
                table: "MentorPayoutObligations",
                column: "LockedBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_MentorUserId",
                table: "MentorPayoutObligations",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_OrganizationPayoutAccountId",
                table: "MentorPayoutObligations",
                column: "OrganizationPayoutAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayoutObligations_PayrollPeriodId",
                table: "MentorPayoutObligations",
                column: "PayrollPeriodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_ApprovedByAdminId",
                table: "MentorPayrollAdjustments",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_OperationKey",
                table: "MentorPayrollAdjustments",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollAdjustments_PayrollPeriodId",
                table: "MentorPayrollAdjustments",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_EarningEntryId",
                table: "MentorPayrollLines",
                column: "EarningEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_MentorBookingId",
                table: "MentorPayrollLines",
                column: "MentorBookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollLines_PayrollPeriodId",
                table: "MentorPayrollLines",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_ApprovedByAdminId",
                table: "MentorPayrollPeriods",
                column: "ApprovedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_MentorUserId_Year_Month_SequenceNumber~",
                table: "MentorPayrollPeriods",
                columns: new[] { "MentorUserId", "Year", "Month", "SequenceNumber", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorPayrollPeriods_PreparedByAdminId",
                table: "MentorPayrollPeriods",
                column: "PreparedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPayoutAccounts_ChannelCode",
                table: "OrganizationPayoutAccounts",
                column: "ChannelCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationPayoutAccounts_ConfiguredByAdminId",
                table: "OrganizationPayoutAccounts",
                column: "ConfiguredByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorBookings_MentorCreditGrants_CreditGrantId",
                table: "MentorBookings",
                column: "CreditGrantId",
                principalTable: "MentorCreditGrants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MentorCreditGrants_CertificateLevels_CertificateLevelId",
                table: "MentorCreditGrants",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionPlans_CertificateLevels_CertificateLevelId",
                table: "SubscriptionPlans",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_CertificateLevels_CertificateLevelId",
                table: "UserSubscriptions",
                column: "CertificateLevelId",
                principalTable: "CertificateLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorBookings_MentorCreditGrants_CreditGrantId",
                table: "MentorBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_MentorCreditGrants_CertificateLevels_CertificateLevelId",
                table: "MentorCreditGrants");

            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionPlans_CertificateLevels_CertificateLevelId",
                table: "SubscriptionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_CertificateLevels_CertificateLevelId",
                table: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "MentorPayoutAttempts");

            migrationBuilder.DropTable(
                name: "MentorPayrollAdjustments");

            migrationBuilder.DropTable(
                name: "MentorPayrollLines");

            migrationBuilder.DropTable(
                name: "MentorPayoutObligations");

            migrationBuilder.DropTable(
                name: "MentorEarningEntries");

            migrationBuilder.DropTable(
                name: "MentorBankAccounts");

            migrationBuilder.DropTable(
                name: "OrganizationPayoutAccounts");

            migrationBuilder.DropTable(
                name: "MentorAttendanceApprovals");

            migrationBuilder.DropTable(
                name: "MentorPayrollPeriods");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_CertificateLevelId",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPlans_CertificateLevelId",
                table: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_MentorCreditGrants_CertificateLevelId",
                table: "MentorCreditGrants");

            migrationBuilder.DropIndex(
                name: "IX_MentorCreditGrants_LearnerId_CertificateLevelId_ExpiresAtUtc",
                table: "MentorCreditGrants");

            migrationBuilder.DropIndex(
                name: "IX_MentorBookings_CreditGrantId",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "CertificateLevelId",
                table: "MentorCreditGrants");

            migrationBuilder.DropColumn(
                name: "CompensationCurrency",
                table: "MentorBookings");

            migrationBuilder.DropColumn(
                name: "CreditGrantId",
                table: "MentorBookings");
        }
    }
}
