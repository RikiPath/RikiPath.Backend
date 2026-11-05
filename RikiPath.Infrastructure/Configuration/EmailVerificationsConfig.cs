using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class EmailVerificationsConfig : IEntityTypeConfiguration<EmailVerification>
    {
        public void Configure(EntityTypeBuilder<EmailVerification> builder)
        {
            builder.HasOne(x => x.User)
                   .WithMany(x => x.EmailVerifications)
                   .HasForeignKey(x => x.UserId);
        }
    }
}
