using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration
{
    public class FeatureConfig : IEntityTypeConfiguration<Feature>
    {
        public void Configure(EntityTypeBuilder<Feature> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(80);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Category).IsRequired().HasMaxLength(40).HasDefaultValue("general");
            builder.Property(x => x.AccessMode).IsRequired().HasMaxLength(32).HasDefaultValue(FeatureAccessModes.Free);
            builder.Property(x => x.IsCoreFree).HasDefaultValue(false);
            builder.Property(x => x.CreditFeatureKey).HasMaxLength(40);
            builder.Property(x => x.RoutePrefixesJson).HasMaxLength(500);
            builder.Property(x => x.SortOrder).HasDefaultValue(0);

            var seedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var v2SeedDate = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

            builder.HasData(
                new Feature
                {
                    Id = 1,
                    Code = FeatureCodes.LessonsContent,
                    Name = "Thư viện bài học JLPT",
                    Description = "Truy cập kho bài học JLPT đầy đủ theo cấp độ kèm nghe hiểu, đọc hiểu và lưu tiến độ.",
                    Category = "self_study",
                    AccessMode = FeatureAccessModes.PlanRequired,
                    IsCoreFree = false,
                    RoutePrefixesJson = "[\"/lessons\",\"/lesson-player\",\"/riki/listening\",\"/riki/reading\"]",
                    SortOrder = 10,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 2,
                    Code = FeatureCodes.JapaneseLibrary,
                    Name = "Thư viện Kana, Kanji & Ngữ pháp",
                    Description = "Tra cứu ngữ pháp, luyện viết Hiragana, Katakana và Kanji theo thứ tự nét miễn phí.",
                    Category = "core_tools",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = true,
                    RoutePrefixesJson = "[\"/grammar\",\"/kanji-writing\",\"/vocabulary-detail\"]",
                    SortOrder = 3,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 3,
                    Code = FeatureCodes.PracticeExercises,
                    Name = "Bài luyện tập theo kỹ năng",
                    Description = "Làm bài luyện tập củng cố kỹ năng và bộ đề theo cấp độ JLPT.",
                    Category = "practice_exam",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = false,
                    RoutePrefixesJson = "[\"/lesson-practice\",\"/practice-sets\"]",
                    SortOrder = 20,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 4,
                    Code = FeatureCodes.MockTests,
                    Name = "Đề thi thử JLPT",
                    Description = "Luyện đề thi thử JLPT mô phỏng đầy đủ 4 kỹ năng và theo dõi kết quả.",
                    Category = "practice_exam",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = false,
                    RoutePrefixesJson = "[\"/mock-test\",\"/mock-tests\",\"/exam-result\"]",
                    SortOrder = 21,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 5,
                    Code = FeatureCodes.StandardRoadmap,
                    Name = "Lộ trình tiêu chuẩn & Tiến độ học",
                    Description = "Xem lộ trình học JLPT tiêu chuẩn theo mục tiêu, kết quả đánh giá năng lực và tiến độ học.",
                    Category = "self_study",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = false,
                    RoutePrefixesJson = "[\"/roadmap\"]",
                    SortOrder = 11,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 6,
                    Code = FeatureCodes.AiMockExplanation,
                    Name = "Giải thích câu hỏi bằng AI",
                    Description = "Nhận phân tích chi tiết đáp án đề thi thử từ trợ lý AI.",
                    Category = "ai_services",
                    AccessMode = FeatureAccessModes.CreditRequired,
                    IsCoreFree = false,
                    CreditFeatureKey = "mock_test_explanation",
                    SortOrder = 33,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 7,
                    Code = FeatureCodes.MentorOneOnOneLegacy,
                    Name = "12 buổi meeting 1-1 với Mentor",
                    Description = "Gói cũ bao gồm 12 buổi ôn luyện trực tiếp 1-1 với Mentor (đã thay bằng lớp học nhóm).",
                    Category = "mentor_legacy",
                    AccessMode = FeatureAccessModes.Disabled,
                    IsCoreFree = false,
                    SortOrder = 90,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 8,
                    Code = FeatureCodes.MentorRoomLegacy,
                    Name = "Đặt lịch và phòng học trực tuyến",
                    Description = "Cấu hình lịch 1-1 cũ (lớp học nhóm dùng cơ chế ghi danh khóa học riêng).",
                    Category = "mentor_legacy",
                    AccessMode = FeatureAccessModes.Disabled,
                    IsCoreFree = false,
                    SortOrder = 91,
                    IsDeleted = false,
                    CreatedDate = seedDate
                },
                new Feature
                {
                    Id = 9,
                    Code = FeatureCodes.PersonalNotebook,
                    Name = "Sổ tay cá nhân & Mini Notebook",
                    Description = "Tạo thư mục, tệp ghi chú, trang bài học, highlight và mục đã lưu cá nhân miễn phí.",
                    Category = "core_tools",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = true,
                    RoutePrefixesJson = "[\"/kanji-notebook\"]",
                    SortOrder = 1,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                },
                new Feature
                {
                    Id = 10,
                    Code = FeatureCodes.SrsStudy,
                    Name = "Ôn tập SRS & Thư viện thẻ mẫu",
                    Description = "Ôn thẻ đến hạn, 4 chế độ ôn tự do, quản lý thẻ cá nhân và thư viện thẻ mẫu miễn phí.",
                    Category = "core_tools",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = true,
                    RoutePrefixesJson = "[\"/daily-srs\"]",
                    SortOrder = 2,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                },
                new Feature
                {
                    Id = 11,
                    Code = FeatureCodes.PlacementTest,
                    Name = "Đánh giá năng lực đầu vào",
                    Description = "Làm bài đánh giá trình độ JLPT đầu vào để xác định cấp độ học phù hợp.",
                    Category = "practice_exam",
                    AccessMode = FeatureAccessModes.Free,
                    IsCoreFree = false,
                    RoutePrefixesJson = "[\"/placement\"]",
                    SortOrder = 19,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                },
                new Feature
                {
                    Id = 12,
                    Code = FeatureCodes.AiLearningPath,
                    Name = "Lộ trình học cá nhân hóa bằng AI",
                    Description = "Tạo kế hoạch học tập chi tiết theo ngày bằng AI dựa trên điểm đánh giá và ngày thi.",
                    Category = "ai_services",
                    AccessMode = FeatureAccessModes.CreditRequired,
                    IsCoreFree = false,
                    CreditFeatureKey = "learning_path",
                    SortOrder = 30,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                },
                new Feature
                {
                    Id = 13,
                    Code = FeatureCodes.AiSpeaking,
                    Name = "Luyện nói & Chấm phát âm AI",
                    Description = "Thu âm tiếng Nhật và nhận điểm phát âm, ngữ điệu cùng nhận xét chi tiết từ AI.",
                    Category = "ai_services",
                    AccessMode = FeatureAccessModes.CreditRequired,
                    IsCoreFree = false,
                    CreditFeatureKey = "speaking",
                    RoutePrefixesJson = "[\"/riki/speaking\",\"/speech\"]",
                    SortOrder = 31,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                },
                new Feature
                {
                    Id = 14,
                    Code = FeatureCodes.AiWriting,
                    Name = "Luyện viết & Chấm bài luận AI",
                    Description = "Viết câu và bài luận tiếng Nhật, nhận sửa lỗi ngữ pháp và chấm điểm từ AI.",
                    Category = "ai_services",
                    AccessMode = FeatureAccessModes.CreditRequired,
                    IsCoreFree = false,
                    CreditFeatureKey = "writing",
                    RoutePrefixesJson = "[\"/riki/essay\",\"/sentence-studio\"]",
                    SortOrder = 32,
                    IsDeleted = false,
                    CreatedDate = v2SeedDate
                });
        }
    }
}
