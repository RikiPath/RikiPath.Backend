using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class GradingResultConfig : IEntityTypeConfiguration<GradingResult>
    {
        public void Configure(EntityTypeBuilder<GradingResult> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FeedbackJson).IsRequired();

            builder.HasOne(x => x.PracticeSubmission)
                .WithOne(x => x.GradingResult)
                .HasForeignKey<GradingResult>(x => x.PracticeSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.HomeworkSubmission)
                .WithOne(x => x.GradingResult)
                .HasForeignKey<GradingResult>(x => x.HomeworkSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => x.PracticeSubmissionId).IsUnique();
            builder.HasIndex(x => x.HomeworkSubmissionId).IsUnique();
        }
    }
}
