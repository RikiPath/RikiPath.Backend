using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestAttemptConfig : IEntityTypeConfiguration<MockTestAttempt>
    {
        public void Configure(EntityTypeBuilder<MockTestAttempt> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.StartedAt).IsRequired();

            // Quan hệ với UserAccount (1 - N)
            builder.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index tối ưu truy vấn lịch sử làm bài của người dùng
            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.StartedAt);
        }
    }
}