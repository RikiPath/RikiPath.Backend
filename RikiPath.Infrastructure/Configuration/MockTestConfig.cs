using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestConfig : IEntityTypeConfiguration<MockTest>
    {
        public void Configure(EntityTypeBuilder<MockTest> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.IsPlacementAssessment).HasDefaultValue(false);

            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.MockTests)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateLevelId });
        }
    }
}
