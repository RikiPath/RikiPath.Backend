using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LearningPathSuggestionConfig : IEntityTypeConfiguration<LearningPathSuggestion>
    {
        public void Configure(EntityTypeBuilder<LearningPathSuggestion> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SuggestionJson).IsRequired();

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasIndex(x => new { x.UserId, x.GeneratedAt });
        }
    }
}
