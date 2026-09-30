using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestSectionResultConfig : IEntityTypeConfiguration<MockTestSectionResult>
    {
        public void Configure(EntityTypeBuilder<MockTestSectionResult> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockTestAttemptId).HasColumnName("PracticeTestAttemptId");
            builder.Property(x => x.MockTestSectionId).HasColumnName("PracticeTestSectionId");

            builder.HasOne(x => x.MockTestAttempt)
                .WithMany(x => x.SectionResults)
                .HasForeignKey(x => x.MockTestAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.MockTestSection)
                .WithMany(x => x.SectionResults)
                .HasForeignKey(x => x.MockTestSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.MockTestAttemptId, x.MockTestSectionId }).IsUnique();
        }
    }
}
