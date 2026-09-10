using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ConsultationAnswerConfig : IEntityTypeConfiguration<ConsultationAnswer>
    {
        public void Configure(EntityTypeBuilder<ConsultationAnswer> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.ConsultationRequest)
                .WithOne(x => x.ConsultationAnswer)
                .HasForeignKey<ConsultationAnswer>(x => x.ConsultationRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Consultant side (Restrict) configured in UserConfig.
        }
    }
}
