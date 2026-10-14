using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class GrammarPatternConfig : IEntityTypeConfiguration<GrammarPattern>
    {
        public void Configure(EntityTypeBuilder<GrammarPattern> builder)
        {
            builder.HasKey(x => x.Id);

            // Các trường bắt buộc
            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(x => x.Structure)
                .IsRequired()
                .HasMaxLength(300);

            // Các trường không bắt buộc (Nullable)
            builder.Property(x => x.UsageNotes)
                .HasMaxLength(2000);

            builder.Property(x => x.ExampleSentence)
                .HasMaxLength(1000);

            builder.Property(x => x.ExampleSentenceMeaning)
                .HasMaxLength(1000);

            // Index tối ưu tốc độ tìm kiếm theo tiêu đề mẫu ngữ pháp
            builder.HasIndex(x => x.Title);
        }
    }
}