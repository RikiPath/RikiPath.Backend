using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class CertificateSectionConfig : IEntityTypeConfiguration<CertificateSection>
    {
        // Giữ ngày tạo của dữ liệu mẫu khớp snapshot hiện tại để EF không sinh UpdateData ở mỗi lần build.
        private static readonly DateTime SeedCreatedDate = new(2026, 10, 31, 11, 13, 24, 991, DateTimeKind.Utc);

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

            // Seed data mẫu cho các phần thi JLPT (N5 tới N2) với Id là int.
            // N5 dùng một section gộp Ngữ pháp & Đọc hiểu theo cấu trúc JLPT N5;
            // N4-N2 tách riêng hai kỹ năng này để khớp file import mock test.
            builder.HasData(
                // ==================== CẤP ĐỘ N5 (Giả sử CertificateLevel Id của N5 là 1) ====================
                new CertificateSection
                {
                    Id = 1,
                    CreatedDate = SeedCreatedDate.AddTicks(5285),
                    CertificateLevelId = 1, // Khớp với Id của N5 trong bảng CertificateLevel
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N5_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N5"
                },
                new CertificateSection
                {
                    Id = 2,
                    CreatedDate = SeedCreatedDate.AddTicks(5292),
                    CertificateLevelId = 1,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp) & Đọc hiểu",
                    Code = "N5_GRAMMAR_READING",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và đọc hiểu đoạn văn ngắn cấp độ N5"
                },
                new CertificateSection
                {
                    Id = 3,
                    CreatedDate = SeedCreatedDate.AddTicks(5294),
                    CertificateLevelId = 1,
                    Name = "Nghe hiểu",
                    Code = "N5_LISTENING",
                    Description = "Phần thi nghe hiểu tình huống giao tiếp hàng ngày cấp độ N5"
                },

                // ==================== CẤP ĐỘ N4 (Giả sử CertificateLevel Id của N4 là 2) ====================
                new CertificateSection
                {
                    Id = 4,
                    CreatedDate = SeedCreatedDate.AddTicks(5295),
                    CertificateLevelId = 2, // Khớp với Id của N4 trong bảng CertificateLevel
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N4_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 5,
                    CreatedDate = SeedCreatedDate.AddTicks(5296),
                    CertificateLevelId = 2,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp)",
                    Code = "N4_GRAMMAR",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 6,
                    CreatedDate = SeedCreatedDate.AddTicks(5297),
                    CertificateLevelId = 2,
                    Name = "Đọc hiểu",
                    Code = "N4_READING",
                    Description = "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N4"
                },
                new CertificateSection
                {
                    Id = 7,
                    CreatedDate = SeedCreatedDate.AddTicks(5298),
                    CertificateLevelId = 2,
                    Name = "Nghe hiểu",
                    Code = "N4_LISTENING",
                    Description = "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N4"
                },

                // ==================== CẤP ĐỘ N3 (CertificateLevel Id = 3) ====================
                new CertificateSection
                {
                    Id = 8,
                    CreatedDate = SeedCreatedDate.AddTicks(5300),
                    CertificateLevelId = 3,
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N3_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N3"
                },
                new CertificateSection
                {
                    Id = 9,
                    CreatedDate = SeedCreatedDate.AddTicks(5301),
                    CertificateLevelId = 3,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp)",
                    Code = "N3_GRAMMAR",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N3"
                },
                new CertificateSection
                {
                    Id = 10,
                    CreatedDate = SeedCreatedDate.AddTicks(5302),
                    CertificateLevelId = 3,
                    Name = "Đọc hiểu",
                    Code = "N3_READING",
                    Description = "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N3"
                },
                new CertificateSection
                {
                    Id = 11,
                    CreatedDate = SeedCreatedDate.AddTicks(5303),
                    CertificateLevelId = 3,
                    Name = "Nghe hiểu",
                    Code = "N3_LISTENING",
                    Description = "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N3"
                },

                // ==================== CẤP ĐỘ N2 (CertificateLevel Id = 4) ====================
                new CertificateSection
                {
                    Id = 12,
                    CreatedDate = SeedCreatedDate.AddTicks(5304),
                    CertificateLevelId = 4,
                    Name = "Kiến thức ngôn ngữ (Từ vựng)",
                    Code = "N2_VOCAB",
                    Description = "Phần thi kiểm tra chữ Hán, cách đọc và từ vựng cấp độ N2"
                },
                new CertificateSection
                {
                    Id = 13,
                    CreatedDate = SeedCreatedDate.AddTicks(5305),
                    CertificateLevelId = 4,
                    Name = "Kiến thức ngôn ngữ (Ngữ pháp)",
                    Code = "N2_GRAMMAR",
                    Description = "Phần thi kiểm tra cấu trúc ngữ pháp và sắp xếp câu cấp độ N2"
                },
                new CertificateSection
                {
                    Id = 14,
                    CreatedDate = SeedCreatedDate.AddTicks(5306),
                    CertificateLevelId = 4,
                    Name = "Đọc hiểu",
                    Code = "N2_READING",
                    Description = "Phần thi đọc hiểu văn bản, thông báo và biểu mẫu cấp độ N2"
                },
                new CertificateSection
                {
                    Id = 15,
                    CreatedDate = SeedCreatedDate.AddTicks(5307),
                    CertificateLevelId = 4,
                    Name = "Nghe hiểu",
                    Code = "N2_LISTENING",
                    Description = "Phần thi nghe hiểu hội thoại và tình huống thực tế cấp độ N2"
                }
            );
        }
    }
}
