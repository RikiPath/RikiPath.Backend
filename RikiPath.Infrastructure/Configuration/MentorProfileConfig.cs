using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration
{
    public class MentorProfileConfig : IEntityTypeConfiguration<MentorProfile>
    {
        public void Configure(EntityTypeBuilder<MentorProfile> builder)
        {
            builder.ToTable("MentorProfiles");
            builder.HasKey(x => x.MentorId);
            builder.Property(x => x.MentorId).ValueGeneratedNever();

            builder.Property(x => x.Bio).HasMaxLength(2000);
            builder.Property(x => x.Expertise).HasMaxLength(500);
            builder.Property(x => x.JlptLevel).HasMaxLength(20);
            builder.Property(x => x.Workplace).HasMaxLength(200);
            builder.Property(x => x.VerificationDocumentUrl).HasMaxLength(1000);
            builder.Property(x => x.IntroductionVideoUrl).HasMaxLength(1000);
            builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(30).IsConcurrencyToken();
            builder.Property(x => x.ReviewedByAdminName).HasMaxLength(100);
            builder.Property(x => x.RejectionReason).HasMaxLength(1000);
            builder.Property(x => x.ReviewNotes).HasMaxLength(1000);

            builder.HasOne(x => x.Mentor)
                .WithOne(x => x.MentorProfile)
                .HasForeignKey<MentorProfile>(x => x.MentorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deterministic dev accounts seed
            builder.HasData(
                new MentorProfile
                {
                    MentorId = 900002,
                    Bio = "Chuyên gia tiếng Nhật với 8 năm kinh nghiệm giảng dạy JLPT N1, N2 và tiếng Nhật doanh nghiệp.",
                    Expertise = "Luyện thi JLPT N1-N2, Tiếng Nhật thương mại, Phỏng vấn xin việc",
                    JlptLevel = "N1",
                    YearsOfExperience = 8,
                    Workplace = "Riki Nihongo Academy",
                    VerificationStatus = MentorVerificationStatus.Approved,
                    SubmittedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
                    ReviewedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
                    ReviewedByAdminName = "Admin System",
                    CreatedDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
                },
                new MentorProfile
                {
                    MentorId = 900003,
                    Bio = "Giảng viên tiếng Nhật bản xứ, chuyên sâu về phát âm và giao tiếp Kaiwa thực tế.",
                    Expertise = "Giao tiếp Kaiwa tự nhiên, Luyện phát âm chuẩn Tokyo, JLPT N3-N2",
                    JlptLevel = "N1",
                    YearsOfExperience = 5,
                    Workplace = "Riki Nihongo Academy",
                    VerificationStatus = MentorVerificationStatus.Approved,
                    SubmittedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
                    ReviewedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
                    ReviewedByAdminName = "Admin System",
                    CreatedDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
