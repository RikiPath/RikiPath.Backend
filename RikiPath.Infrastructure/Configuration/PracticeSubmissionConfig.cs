using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeSubmissionConfig : IEntityTypeConfiguration<PracticeSubmission>
    {
        public void Configure(EntityTypeBuilder<PracticeSubmission> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(x => x.JlptLevel)
                .WithMany(x => x.PracticeSubmissions)
                .HasForeignKey(x => x.JlptLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasIndex(x => new { x.UserId, x.Type, x.SubmittedAt });
        }
    }
}
