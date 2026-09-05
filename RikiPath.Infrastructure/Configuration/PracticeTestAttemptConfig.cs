using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeTestAttemptConfig : IEntityTypeConfiguration<PracticeTestAttempt>
    {
        public void Configure(EntityTypeBuilder<PracticeTestAttempt> builder)
        {
            builder.HasKey(x => x.Id);

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasOne(x => x.PracticeTest)
                .WithMany(x => x.Attempts)
                .HasForeignKey(x => x.PracticeTestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.PracticeTestId });
        }
    }
}
