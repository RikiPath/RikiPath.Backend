using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class FeatureConfig : IEntityTypeConfiguration<Feature>
    {
        public void Configure(EntityTypeBuilder<Feature> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Description).HasMaxLength(500);

            builder.HasData(
                new Feature
                {
                    Id = 1,
                    Name = "Thư viện học tiếng Nhật",
                    Description = "Truy cập lesson, từ vựng, ngữ pháp và kanji theo các chứng chỉ tiếng Nhật được cấu hình trong hệ thống.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 2,
                    Name = "Luyện viết Kana và Kanji",
                    Description = "Luyện viết hiragana, katakana và kanji theo thứ tự nét, xem lại lỗi và tiến độ luyện tập.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 3,
                    Name = "Bài luyện tập theo kỹ năng",
                    Description = "Làm bài luyện tập theo kỹ năng và cấp độ chứng chỉ tiếng Nhật.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 4,
                    Name = "Đề thi thử chứng chỉ",
                    Description = "Luyện đề thi thử và theo dõi kết quả theo chứng chỉ tiếng Nhật có trong hệ thống.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 5,
                    Name = "Theo dõi tiến độ học",
                    Description = "Xem tiến độ học lesson, kết quả luyện tập và lịch sử làm đề.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 6,
                    Name = "Hỗ trợ học tập bằng AI",
                    Description = "Sử dụng các chức năng AI hiện có để nhận gợi ý lộ trình và phản hồi bài làm tiếng Nhật.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 7,
                    Name = "12 buổi meeting 1-1 với Mentor",
                    Description = "Gói bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Feature
                {
                    Id = 8,
                    Name = "Đặt lịch và phòng học trực tuyến",
                    Description = "Chọn lịch trống của Mentor; mỗi buổi đã đặt có phòng học trực tuyến realtime.",
                    IsDeleted = false,
                    CreatedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        }
    }
}
