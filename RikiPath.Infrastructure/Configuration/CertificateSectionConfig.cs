using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificateSectionConfig : IEntityTypeConfiguration<CertificateSection>
    {
        public void Configure(EntityTypeBuilder<CertificateSection> builder)
        {
            builder.HasKey(x => x.Id);

            // Mapping tên cột DB theo convention của dự án
            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");

            // Cấu hình độ dài và ràng buộc bắt buộc
            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            // Mối quan hệ 1-N: CertificateLevel (1) -> CertificateSection (N)
            builder.HasOne(x => x.CertificateLevel)
                .WithMany()
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index tối ưu truy vấn theo Cấp độ và Mã phần thi
            builder.HasIndex(x => new { x.CertificateLevelId, x.Code });
        }
    }
}