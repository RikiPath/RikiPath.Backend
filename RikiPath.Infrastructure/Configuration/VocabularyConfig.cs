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
        }
    }
}