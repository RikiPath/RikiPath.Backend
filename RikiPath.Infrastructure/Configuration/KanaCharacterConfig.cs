using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanaCharacterConfig : IEntityTypeConfiguration<KanaCharacter>
    {
        public void Configure(EntityTypeBuilder<KanaCharacter> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Character).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Romaji).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ReviewedByName).HasMaxLength(200);
            builder.HasIndex(x => new { x.Character, x.Type }).IsUnique();
            builder.HasOne(x => x.ContentAuthor).WithMany()
                .HasForeignKey(x => x.ContentAuthorId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
