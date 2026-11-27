using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorGroupPolicyConfig : IEntityTypeConfiguration<MentorGroupPolicy>
{
    public void Configure(EntityTypeBuilder<MentorGroupPolicy> b)
    {
        b.ToTable("MentorGroupPolicies", t =>
        {
            t.HasCheckConstraint("CK_MentorGroupPolicy_Capacity", "\"MaxLearners\" BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Positive", "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Remediation", "\"RemediationCompletionThresholdPercent\" > 0 AND \"RemediationCompletionThresholdPercent\" <= 100");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Evidence", "\"RequireOfficialExamEvidence\" AND \"RequireAdminRemediationReview\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Comparison", "\"RemediationThresholdComparison\" IN ('>', '>=')");
            t.HasCheckConstraint("CK_MentorGroupPolicy_ReplacementLead", "\"MentorReplacementLeadHours\" BETWEEN 0 AND 720");
            t.HasCheckConstraint("CK_MentorGroupPolicy_WeeklySessionCap", "\"WeeklySessionCap\" BETWEEN 0 AND 84");
            t.HasCheckConstraint("CK_MentorGroupPolicy_TransferAfterStart", "\"LearnerTransferEnabled\" OR NOT \"TransferAllowedAfterSourceStart\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_CourseDurationWeeks", "\"CourseDurationWeeks\" BETWEEN 1 AND 52");
            t.HasCheckConstraint("CK_MentorGroupPolicy_AllowedSessionsPerWeek", "\"AllowedSessionsPerWeekCsv\" ~ '^[1-9][0-9]*(,[1-9][0-9]*)*$'");
            t.HasCheckConstraint("CK_MentorGroupPolicy_DefaultSessionsPerWeek", "\"DefaultSessionsPerWeek\" >= 1");
            t.HasCheckConstraint("CK_MentorGroupPolicy_CheckoutHold", "\"CheckoutHoldMinutes\" BETWEEN 1 AND 60");
            t.HasCheckConstraint("CK_MentorGroupPolicy_MinLearners", "\"MinLearnersToStart\" BETWEEN 1 AND \"MaxLearners\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_LeadDays", "\"MinimumProposalLeadDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" BETWEEN 1 AND 180 AND \"MinimumPublishedEnrollmentDays\" <= \"MinimumProposalLeadDays\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Tuition", "\"TuitionPerSessionN5\" >= 0 AND \"TuitionPerSessionN4\" >= 0 AND \"TuitionPerSessionN3\" >= 0 AND \"TuitionPerSessionN2\" >= 0");
            t.HasCheckConstraint("CK_MentorGroupPolicy_MaxTotalActiveClasses", "\"MaxTotalActiveClassesPerMentor\" BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_MentorGroupPolicy_State", "\"Revision\" > 0 AND NOT (\"IsActive\" AND (\"IsArchivedRevision\" OR \"IsDeleted\"))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.Version)
            .IsUnique()
            .HasDatabaseName("IX_MentorGroupPolicies_LiveVersion")
            .HasFilter("\"IsArchivedRevision\" = FALSE");
        b.HasIndex(x => x.EffectiveFromUtc);
        b.HasIndex(x => x.IsActive)
            .IsUnique()
            .HasDatabaseName("IX_MentorGroupPolicies_SingleActive")
            .HasFilter("\"IsActive\" = TRUE");
        b.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.LastAppliedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.RecurringWeekdayPatternsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RemediationThresholdComparison).HasMaxLength(2).IsRequired();
        b.Property(x => x.RemediationCompletionThresholdPercent).HasPrecision(5, 2);
        b.Property(x => x.AllowedSessionsPerWeekCsv).HasMaxLength(512).IsRequired();
        b.Property(x => x.TuitionPerSessionN5).HasPrecision(18, 0);
        b.Property(x => x.TuitionPerSessionN4).HasPrecision(18, 0);
        b.Property(x => x.TuitionPerSessionN3).HasPrecision(18, 0);
        b.Property(x => x.TuitionPerSessionN2).HasPrecision(18, 0);
        // Không dùng HasDefaultValue cho bool/int (tránh sentinel false/0 bị thay bằng default DB); migration tự đặt giá trị cho dòng cũ.
        b.HasOne(x => x.CreatedByAdmin).WithMany().HasForeignKey(x => x.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LastAppliedByAdmin).WithMany().HasForeignKey(x => x.LastAppliedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UpdatedByAdmin).WithMany().HasForeignKey(x => x.UpdatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DeletedByAdmin).WithMany().HasForeignKey(x => x.DeletedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.LevelLimits).WithOne().HasForeignKey(x => x.MentorGroupPolicyId).OnDelete(DeleteBehavior.Cascade);
        b.HasData(new MentorGroupPolicy
        {
            Id = 1,
            Version = 1,
            Revision = 1,
            IsActive = true,
            IsArchivedRevision = false,
            IsDeleted = false,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastAppliedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            MaxLearners = 4,
            EnrollmentCutoffMinutesBeforeFirstSession = 0,
            SessionMinutes = 90,
            RestMinutes = 15,
            DailySessionCap = 4,
            WeeklySessionCap = 0,
            PublicationHorizonDays = 30,
            CancellationLeadHours = 24,
            ReschedulingLeadHours = 24,
            HomeworkDeadlineDays = 7,
            PlacementAttemptLimit = 2,
            RemediationCompletionThresholdPercent = 90,
            RemediationThresholdComparison = ">",
            RequireOfficialExamEvidence = true,
            RequireAdminRemediationReview = true,
            KpiReportingPeriodDays = 30,
            LearnerTransferEnabled = true,
            TransferAllowedAfterSourceStart = false,
            MentorReplacementEnabled = true,
            MentorReplacementLeadHours = 24,
            RecurringWeekdayPatternsJson = "[[1,3,5],[2,4,6]]",
            CourseDurationWeeks = 10,
            AllowedSessionsPerWeekCsv = "2,3,4",
            DefaultSessionsPerWeek = 3,
            CheckoutHoldMinutes = 10,
            MinLearnersToStart = 1,
            MinimumProposalLeadDays = 14,
            MinimumPublishedEnrollmentDays = 7,
            TuitionPerSessionN5 = 60_000m,
            TuitionPerSessionN4 = 75_000m,
            TuitionPerSessionN3 = 90_000m,
            TuitionPerSessionN2 = 110_000m,
            MaxTotalActiveClassesPerMentor = 4,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

public sealed class MentorGroupPolicyLevelLimitConfig : IEntityTypeConfiguration<MentorGroupPolicyLevelLimit>
{
    public void Configure(EntityTypeBuilder<MentorGroupPolicyLevelLimit> b)
    {
        b.ToTable("MentorGroupPolicyLevelLimits", t =>
        {
            t.HasCheckConstraint("CK_MentorGroupPolicyLevelLimit_MaxActive", "\"MaxActiveClasses\" BETWEEN 0 AND 50");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.MentorGroupPolicyId, x.CertificateLevelId }).IsUnique();
        b.HasOne(x => x.CertificateLevel).WithMany().HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
    }
}
