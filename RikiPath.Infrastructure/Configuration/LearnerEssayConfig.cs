using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class LearnerEssayConfig : IEntityTypeConfiguration<LearnerEssay>
{
    public void Configure(EntityTypeBuilder<LearnerEssay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ImageStoragePath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.OriginalOcrText).IsRequired();
        builder.Property(x => x.ContentText).IsRequired();
        builder.Property(x => x.OcrLanguage).IsRequired().HasMaxLength(10);

        builder.HasOne(x => x.UserAccount)
            .WithMany(x => x.LearnerEssays)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.CreatedDate });
    }
}
