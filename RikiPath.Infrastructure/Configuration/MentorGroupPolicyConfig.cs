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
            t.HasCheckConstraint("CK_MentorGroupPolicy_Capacity", "\"MaxLearners\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Positive", "\"Version\" > 0 AND \"SessionMinutes\" > 0 AND \"RestMinutes\" >= 0 AND \"DailySessionCap\" > 0 AND \"PublicationHorizonDays\" > 0 AND \"EnrollmentCutoffMinutesBeforeFirstSession\" >= 0 AND \"CancellationLeadHours\" >= 0 AND \"ReschedulingLeadHours\" >= 0 AND \"HomeworkDeadlineDays\" > 0 AND \"PlacementAttemptLimit\" > 0 AND \"KpiReportingPeriodDays\" > 0");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Remediation", "\"RemediationCompletionThresholdPercent\" > 0 AND \"RemediationCompletionThresholdPercent\" <= 100");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Evidence", "\"RequireOfficialExamEvidence\" AND \"RequireAdminRemediationReview\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_Comparison", "\"RemediationThresholdComparison\" IN ('>', '>=')");
            t.HasCheckConstraint("CK_MentorGroupPolicy_ReplacementLead", "\"MentorReplacementLeadHours\" BETWEEN 0 AND 720");
            t.HasCheckConstraint("CK_MentorGroupPolicy_WeeklySessionCap", "\"WeeklySessionCap\" BETWEEN 0 AND 84");
            t.HasCheckConstraint("CK_MentorGroupPolicy_TransferAfterStart", "\"LearnerTransferEnabled\" OR NOT \"TransferAllowedAfterSourceStart\"");
            t.HasCheckConstraint("CK_MentorGroupPolicy_CourseDurationWeeks", "\"CourseDurationWeeks\" BETWEEN 1 AND 52");
            t.HasCheckConstraint("CK_MentorGroupPolicy_AllowedSessionsPerWeek", "\"AllowedSessionsPerWeekCsv\" ~ '^[1-7](,[1-7]){0,6}$'");
            t.HasCheckConstraint("CK_MentorGroupPolicy_DefaultSessionsPerWeek", "\"DefaultSessionsPerWeek\" BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_MentorGroupPolicy_CheckoutHold", "\"CheckoutHoldMinutes\" BETWEEN 1 AND 60");
            t.HasCheckConstraint("CK_MentorGroupPolicy_MinLearners", "\"MinLearnersToStart\" BETWEEN 1 AND \"MaxLearners\"");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Version).IsUnique();
        b.HasIndex(x => x.EffectiveFromUtc).IsUnique();
        b.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        b.Property(x => x.RecurringWeekdayPatternsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.RemediationThresholdComparison).HasMaxLength(2).IsRequired();
        b.Property(x => x.RemediationCompletionThresholdPercent).HasPrecision(5, 2);
        b.Property(x => x.AllowedSessionsPerWeekCsv).HasMaxLength(32).IsRequired();
        // Không dùng HasDefaultValue cho bool/int (tránh sentinel false/0 bị thay bằng default DB); migration tự đặt giá trị cho dòng cũ.
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(new MentorGroupPolicy
        {
            Id = 1,
            Version = 1,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
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
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
