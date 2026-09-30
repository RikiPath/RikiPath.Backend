using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanaWritingPracticeHistoryConfig : IEntityTypeConfiguration<KanaWritingPracticeHistory>
    {
        public void Configure(EntityTypeBuilder<KanaWritingPracticeHistory> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.Card).WithMany(x => x.Histories).HasForeignKey(x => x.KanaWritingPracticeCardId).OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20);
        }
    }
}
