using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class TopicConfig : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.StageCode).HasMaxLength(60);
        builder.Property(x => x.LearningObjectives).HasMaxLength(1000);
        builder.HasOne(x => x.CertificateLevel).WithMany()
            .HasForeignKey(x => x.CertificateLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CertificateLevelId, x.SortOrder });
        builder.HasMany(x => x.Lessons).WithOne(x => x.Topic)
            .HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
    }
}
