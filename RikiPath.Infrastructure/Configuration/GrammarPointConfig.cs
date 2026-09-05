using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class GrammarPointConfig : IEntityTypeConfiguration<GrammarPoint>
    {
        public void Configure(EntityTypeBuilder<GrammarPoint> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Structure).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.GrammarPoints)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContentAuthor / ReviewedBy relationships configured from UserConfig.
            builder.HasIndex(x => new { x.Title, x.JlptLevelId });
        }
    }
}
