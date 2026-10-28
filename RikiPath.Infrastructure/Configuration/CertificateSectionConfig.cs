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

            // Seed data mẫu cho các phần thi JLPT (N5 và N4) với Id là int
            builder.HasData(
                // ==================== CẤP ĐỘ N5 (Giả sử CertificateLevel Id của N5 là 1) ====================
                new CertificateSection
                {
                    Id = 1,
                    CertificateLevelId = 1, // Khớp với Id của N5 trong bảng CertificateLevel
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N5_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N5"
                },
                new CertificateSection
                {
                    Id = 2,
                    CertificateLevelId = 1,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu",
                    Code = "N5_GRAMMAR_READING",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và đọc hiểu đoạn văn ngắn cấp độ N5"
                },
                new CertificateSection
                {
                    Id = 3,
                    CertificateLevelId = 1,
                    Name = "Nghe hiểu",
                    Code = "N5_LISTENING",
                    Description = "Phần thi nghe hiểu tình huống giao tiếp hàng ngày cấp độ N5"
                },

                // ==================== CẤP ĐỘ N4 (Giả sử CertificateLevel Id của N4 là 2) ====================
                new CertificateSection
                {
                    Id = 4,
                    CertificateLevelId = 2, // Khớp với Id của N4 trong bảng CertificateLevel
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N4_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 5,
                    CertificateLevelId = 2,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp)",
                    Code = "N4_GRAMMAR",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 6,
                    CertificateLevelId = 2,
                    Name = "Đọc hiểu",
                    Code = "N4_READING",
                    Description = "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 7,
                    CertificateLevelId = 2,
                    Name = "Nghe hiểu",
                    Code = "N4_LISTENING",
                    Description = "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N4"
                }
            );
        }
    }
}