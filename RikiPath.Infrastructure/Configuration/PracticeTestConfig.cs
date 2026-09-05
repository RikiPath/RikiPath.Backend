using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeTestConfig : IEntityTypeConfiguration<PracticeTest>
    {
        public void Configure(EntityTypeBuilder<PracticeTest> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.PracticeTests)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContentAuthor / ReviewedBy relationships configured from UserConfig.
        }
    }
}
