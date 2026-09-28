using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificateTypeConfig : IEntityTypeConfiguration<CertificateType>
    {
        public void Configure(EntityTypeBuilder<CertificateType> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(x => x.Name).IsUnique();
        }
    }
}
