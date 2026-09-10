using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class CourseConfig : IEntityTypeConfiguration<Course>
    {
        public void Configure(EntityTypeBuilder<Course> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.Courses)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.CourseCategory)
                .WithMany(x => x.Courses)
                .HasForeignKey(x => x.CourseCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ContentAuthor / ReviewedBy relationships configured from UserConfig.
            builder.HasIndex(x => new { x.JlptLevelId, x.CourseCategoryId, x.Status });
        }
    }
}
