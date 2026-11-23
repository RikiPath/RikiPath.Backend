using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class VocabularyConfig : IEntityTypeConfiguration<Vocabulary>
    {
        public void Configure(EntityTypeBuilder<Vocabulary> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Word).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Reading).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(300);

            // Index trên Word và Reading để tìm kiếm từ vựng nhanh hơn
            builder.HasIndex(x => new { x.Word, x.Reading });
            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(RikiPath.Domain.Enums.ContentStatus.Published)
                .HasSentinel((RikiPath.Domain.Enums.ContentStatus)(-1));
            builder.Property(x => x.Version).HasDefaultValue(1).HasSentinel(-1);
            builder.Property(x => x.UnpublishedReason).HasMaxLength(500);
            builder.HasIndex(x => x.Status);
        }
    }
}