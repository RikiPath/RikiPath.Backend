using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorHourlyRateConfig : IEntityTypeConfiguration<MentorHourlyRate>
{
    public void Configure(EntityTypeBuilder<MentorHourlyRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AmountPerHour).HasPrecision(18, 2);
        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveUntilUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorHourlyRate_Positive", "\"AmountPerHour\" > 0");
            t.HasCheckConstraint("CK_MentorHourlyRate_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
        });
        builder.HasIndex(x => new { x.MentorUserId, x.EffectiveFromUtc }).IsUnique();
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConfiguredByAdmin).WithMany().HasForeignKey(x => x.ConfiguredByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class JlptLevelPayCoefficientConfig : IEntityTypeConfiguration<JlptLevelPayCoefficient>
{
    public void Configure(EntityTypeBuilder<JlptLevelPayCoefficient> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Coefficient).HasPrecision(10, 4);
        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveUntilUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_JlptLevelPayCoefficient_Positive", "\"Coefficient\" > 0");
            t.HasCheckConstraint("CK_JlptLevelPayCoefficient_Range", "\"EffectiveUntilUtc\" IS NULL OR \"EffectiveUntilUtc\" > \"EffectiveFromUtc\"");
        });
        builder.HasIndex(x => new { x.CertificateLevelId, x.EffectiveFromUtc }).IsUnique();
        builder.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConfiguredByAdmin).WithMany().HasForeignKey(x => x.ConfiguredByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorTeachingQualificationConfig : IEntityTypeConfiguration<MentorTeachingQualification>
{
    public void Configure(EntityTypeBuilder<MentorTeachingQualification> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VerifiedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.RevokedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t => t.HasCheckConstraint("CK_MentorTeachingQualification_Revoked", "\"RevokedAtUtc\" IS NULL OR \"RevokedAtUtc\" >= \"VerifiedAtUtc\""));
        builder.HasIndex(x => new { x.MentorUserId, x.CertificateLevelId }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VerifiedByAdmin).WithMany().HasForeignKey(x => x.VerifiedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorAttendanceApprovalConfig : IEntityTypeConfiguration<MentorAttendanceApproval>
{
    public void Configure(EntityTypeBuilder<MentorAttendanceApproval> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.EvidenceSummary).HasMaxLength(2000);
        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorAttendanceApproval_Minutes", "\"ApprovedEligibleMinutes\" IS NULL OR (\"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90)");
            t.HasCheckConstraint("CK_MentorAttendanceApproval_RawDuration", "\"RawMentorDurationSeconds\" >= 0 AND \"RawLearnerDurationSeconds\" >= 0");
        });
        builder.HasIndex(x => x.MentorBookingId).IsUnique();
        builder.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByAdmin).WithMany().HasForeignKey(x => x.ReviewedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorEarningEntryConfig : IEntityTypeConfiguration<MentorEarningEntry>
{
    public void Configure(EntityTypeBuilder<MentorEarningEntry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JlptLevelCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.FrozenHourlyRate).HasPrecision(18, 2);
        builder.Property(x => x.FrozenLevelCoefficient).HasPrecision(10, 4);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OperationKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorEarningEntry_Positive", "\"FrozenHourlyRate\" > 0 AND \"FrozenLevelCoefficient\" > 0 AND \"ApprovedEligibleMinutes\" > 0 AND \"ApprovedEligibleMinutes\" <= 90 AND \"GrossAmount\" > 0");
        });
        builder.HasIndex(x => x.MentorBookingId).IsUnique();
        builder.HasIndex(x => x.AttendanceApprovalId).IsUnique();
        builder.HasIndex(x => x.OperationKey).IsUnique();
        builder.HasIndex(x => new { x.MentorUserId, x.Status });
        builder.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AttendanceApproval).WithMany().HasForeignKey(x => x.AttendanceApprovalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PayrollPeriod).WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByAdmin).WithMany().HasForeignKey(x => x.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorPayrollPeriodConfig : IEntityTypeConfiguration<MentorPayrollPeriod>
{
    public void Configure(EntityTypeBuilder<MentorPayrollPeriod> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SupplementalReason).HasMaxLength(1000);
        builder.Property(x => x.ApprovalNotes).HasMaxLength(1000);
        builder.Property(x => x.TotalEarningsAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAdjustmentsAmount).HasPrecision(18, 2);
        builder.Property(x => x.GrossPayableAmount).HasPrecision(18, 2);
        builder.Property(x => x.PreparedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ApprovedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ClosedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorPayrollPeriod_Period", "\"Year\" >= 2020 AND \"Year\" <= 2100 AND \"Month\" >= 1 AND \"Month\" <= 12 AND \"SequenceNumber\" >= 1");
            t.HasCheckConstraint("CK_MentorPayrollPeriod_Payable", "\"TotalEarningsAmount\" >= 0 AND \"GrossPayableAmount\" >= 0");
            t.HasCheckConstraint("CK_MentorPayrollPeriod_Supplemental", "\"SequenceNumber\" = 1 OR (\"SupplementalReason\" IS NOT NULL AND length(trim(\"SupplementalReason\")) > 0)");
        });
        builder.HasIndex(x => new { x.MentorUserId, x.Year, x.Month, x.SequenceNumber, x.Currency }).IsUnique();
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreparedByAdmin).WithMany().HasForeignKey(x => x.PreparedByAdminId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedByAdmin).WithMany().HasForeignKey(x => x.ApprovedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorPayrollLineConfig : IEntityTypeConfiguration<MentorPayrollLine>
{
    public void Configure(EntityTypeBuilder<MentorPayrollLine> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FrozenHourlyRate).HasPrecision(18, 2);
        builder.Property(x => x.FrozenLevelCoefficient).HasPrecision(10, 4);
        builder.Property(x => x.LineAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorPayrollLine_Positive", "\"LineAmount\" > 0 AND \"ApprovedEligibleMinutes\" > 0");
        });
        builder.HasIndex(x => x.EarningEntryId).IsUnique();
        builder.HasIndex(x => x.MentorBookingId).IsUnique();
        builder.HasOne(x => x.PayrollPeriod).WithMany(p => p.Lines).HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EarningEntry).WithMany().HasForeignKey(x => x.EarningEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MentorBooking).WithMany().HasForeignKey(x => x.MentorBookingId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorPayrollAdjustmentConfig : IEntityTypeConfiguration<MentorPayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<MentorPayrollAdjustment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SignedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.OperationKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorPayrollAdjustment_NonZero", "\"SignedAmount\" <> 0");
        });
        builder.HasIndex(x => x.OperationKey).IsUnique();
        builder.HasOne(x => x.PayrollPeriod).WithMany(p => p.Adjustments).HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedByAdmin).WithMany().HasForeignKey(x => x.ApprovedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorBankAccountConfig : IEntityTypeConfiguration<MentorBankAccount>
{
    public void Configure(EntityTypeBuilder<MentorBankAccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.BankCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EncryptedAccountNumber).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountNumberHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.VerificationNotes).HasMaxLength(1000);
        builder.Property(x => x.VerifiedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveUntilUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorBankAccount_Version", "\"AccountVersion\" >= 1");
        });
        builder.HasIndex(x => new { x.MentorUserId, x.AccountVersion }).IsUnique();
        builder.HasIndex(x => x.MentorUserId).IsUnique().HasDatabaseName("IX_MentorBankAccounts_MentorUserId_Current").HasFilter("\"IsCurrent\" = TRUE");
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VerifiedByAdmin).WithMany().HasForeignKey(x => x.VerifiedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrganizationPayoutAccountConfig : IEntityTypeConfiguration<OrganizationPayoutAccount>
{
    public void Configure(EntityTypeBuilder<OrganizationPayoutAccount> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChannelCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BankCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderAccountReference).HasMaxLength(200);
        builder.Property(x => x.VerifiedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.ChannelCode).IsUnique();
        builder.HasOne(x => x.ConfiguredByAdmin).WithMany().HasForeignKey(x => x.ConfiguredByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorPayoutObligationConfig : IEntityTypeConfiguration<MentorPayoutObligation>
{
    public void Configure(EntityTypeBuilder<MentorPayoutObligation> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.PayableAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.SelectedMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.SettledReference).HasMaxLength(200);
        builder.Property(x => x.SettledAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorPayoutObligation_Positive", "\"PayableAmount\" > 0");
            t.HasCheckConstraint("CK_MentorPayoutObligation_Settlement", "(\"Status\" <> 'Paid') OR (\"SettledAttemptId\" IS NOT NULL AND \"SettledReference\" IS NOT NULL AND \"SettledAtUtc\" IS NOT NULL)");
        });
        builder.HasIndex(x => x.PayrollPeriodId).IsUnique();
        builder.HasOne(x => x.PayrollPeriod).WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MentorUser).WithMany().HasForeignKey(x => x.MentorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LockedBankAccount).WithMany().HasForeignKey(x => x.LockedBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OrganizationPayoutAccount).WithMany().HasForeignKey(x => x.OrganizationPayoutAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedByAdmin).WithMany().HasForeignKey(x => x.ApprovedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MentorPayoutAttemptConfig : IEntityTypeConfiguration<MentorPayoutAttempt>
{
    public void Configure(EntityTypeBuilder<MentorPayoutAttempt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExecutionMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BatchId).HasMaxLength(150);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.InternalStatus).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);
        builder.Property(x => x.ProviderPayoutId).HasMaxLength(200);
        builder.Property(x => x.TransferReference).HasMaxLength(200);
        builder.Property(x => x.EvidenceStorageKey).HasMaxLength(500);
        builder.Property(x => x.EvidenceNotes).HasMaxLength(1000);
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MentorPayoutAttempt_Positive", "\"Amount\" > 0 AND \"AttemptNumber\" >= 1");
            t.HasCheckConstraint("CK_MentorPayoutAttempt_ManualEvidence", "(\"ExecutionMethod\" <> 'ManualBankTransfer' OR \"InternalStatus\" <> 'Succeeded') OR (\"TransferReference\" IS NOT NULL AND \"EvidenceStorageKey\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.PayoutObligationId, x.AttemptNumber }).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        // Tại một thời điểm chỉ có tối đa một attempt đang xử lý/chờ đối soát cho cùng một nghĩa vụ chi trả.
        builder.HasIndex(x => x.PayoutObligationId).IsUnique()
            .HasDatabaseName("IX_MentorPayoutAttempts_OneActivePerObligation")
            .HasFilter("\"InternalStatus\" IN ('Initiated', 'Processing', 'UnknownPendingReconciliation', 'OnHoldMissingEvidence')");
        // Tuyệt đối chỉ có tối đa một attempt Succeeded cho một nghĩa vụ chi trả xuyên suốt cả hai phương thức.
        builder.HasIndex(x => x.PayoutObligationId).IsUnique()
            .HasDatabaseName("IX_MentorPayoutAttempts_OneSucceededPerObligation")
            .HasFilter("\"InternalStatus\" = 'Succeeded'");
        builder.HasIndex(x => x.TransferReference).IsUnique()
            .HasDatabaseName("IX_MentorPayoutAttempts_SucceededTransferReference")
            .HasFilter("\"TransferReference\" IS NOT NULL AND \"InternalStatus\" = 'Succeeded'");
        builder.HasOne(x => x.PayoutObligation).WithMany(o => o.Attempts).HasForeignKey(x => x.PayoutObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LockedBankAccount).WithMany().HasForeignKey(x => x.LockedBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InitiatedByAdmin).WithMany().HasForeignKey(x => x.InitiatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ConfirmedByAdmin).WithMany().HasForeignKey(x => x.ConfirmedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
