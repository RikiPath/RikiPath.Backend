using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class GrammarPatternConfig : IEntityTypeConfiguration<GrammarPattern>
    {
        public void Configure(EntityTypeBuilder<GrammarPattern> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Structure).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ReviewedByName).HasMaxLength(200);

            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.GrammarPatterns)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ContentAuthor)
                .WithMany(x => x.AuthoredGrammarPatterns)
                .HasForeignKey(x => x.ContentAuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.CertificateLevelId, x.Status });
        }
    }
}
