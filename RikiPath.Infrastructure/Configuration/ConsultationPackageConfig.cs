using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class ConsultationPackageConfig : IEntityTypeConfiguration<ConsultationPackage>
    {
        public void Configure(EntityTypeBuilder<ConsultationPackage> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Price).HasColumnType("decimal(18,2)");
        }
    }
}
