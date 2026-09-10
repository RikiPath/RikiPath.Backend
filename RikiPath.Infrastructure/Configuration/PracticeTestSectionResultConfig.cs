using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeTestSectionResultConfig : IEntityTypeConfiguration<PracticeTestSectionResult>
    {
        public void Configure(EntityTypeBuilder<PracticeTestSectionResult> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.PracticeTestAttempt)
                .WithMany(x => x.SectionResults)
                .HasForeignKey(x => x.PracticeTestAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.PracticeTestSection)
                .WithMany(x => x.SectionResults)
                .HasForeignKey(x => x.PracticeTestSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.PracticeTestAttemptId, x.PracticeTestSectionId }).IsUnique();
        }
    }
}
