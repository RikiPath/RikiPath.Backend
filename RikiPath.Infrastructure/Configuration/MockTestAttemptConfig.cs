using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestAttemptConfig : IEntityTypeConfiguration<MockTestAttempt>
    {
        public void Configure(EntityTypeBuilder<MockTestAttempt> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockTestId).HasColumnName("PracticeTestId");

            // UserAccount side (Cascade) configured in UserConfig.
            builder.HasOne(x => x.MockTest)
                .WithMany(x => x.Attempts)
                .HasForeignKey(x => x.MockTestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.MockTestId });
        }
    }
}
