using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class VocabularyListConfig : IEntityTypeConfiguration<VocabularyList>
    {
        public void Configure(EntityTypeBuilder<VocabularyList> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);

            // UserId FK/relationship (Cascade) configured from UserConfig.
            builder.HasIndex(x => new { x.UserId, x.Name });
        }
    }
}
