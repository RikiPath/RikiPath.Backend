using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanaWritingPracticeCardConfig : IEntityTypeConfiguration<KanaWritingPracticeCard>
    {
        public void Configure(EntityTypeBuilder<KanaWritingPracticeCard> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.KanaCharacter).WithMany(x => x.PracticeCards).HasForeignKey(x => x.KanaCharacterId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.UserId, x.KanaCharacterId }).IsUnique();
            builder.HasIndex(x => new { x.UserId, x.NextReviewDate });
        }
    }
}
