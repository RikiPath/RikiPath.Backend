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

            builder.HasData(new CertificateType
            {
                Id = 1,
                Name = "JLPT",
                Description = "Japanese-Language Proficiency Test",
                IsActive = true,
                SortOrder = 1,
                CreatedDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            });
        }
    }
}
