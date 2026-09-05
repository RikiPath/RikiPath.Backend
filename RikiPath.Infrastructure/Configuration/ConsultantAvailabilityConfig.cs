using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ConsultantAvailabilityConfig : IEntityTypeConfiguration<ConsultantAvailability>
    {
        public void Configure(EntityTypeBuilder<ConsultantAvailability> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.ConsultantId, x.StartTime, x.EndTime });

            // Consultant side (Cascade) and the 1-1 link to ConsultationRequest
            // (via ConsultationRequest.ConsultantAvailabilityId) are configured
            // in UserConfig / ConsultationRequestConfig respectively.
        }
    }
}
