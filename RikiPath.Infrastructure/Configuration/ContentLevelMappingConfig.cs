using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class ContentLevelMappingConfig : IEntityTypeConfiguration<ContentLevelMapping>
    {
        public void Configure(EntityTypeBuilder<ContentLevelMapping> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CertificateLevelId).HasColumnName("CertificationLevelId");

            // Level: Restrict giống các config khác (không cho xoá level khi còn mapping)
            builder.HasOne(x => x.CertificateLevel)
                .WithMany(x => x.ContentLevelMappings)
                .HasForeignKey(x => x.CertificateLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Nội dung: Cascade vì mapping chỉ là bảng nối, vô nghĩa khi nội dung đã bị xoá cứng
            builder.HasOne(x => x.Kanji)
                .WithMany(x => x.ContentLevelMappings)
                .HasForeignKey(x => x.KanjiId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Vocabulary)
                .WithMany(x => x.ContentLevelMappings)
                .HasForeignKey(x => x.VocabularyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.GrammarPattern)
                .WithMany(x => x.ContentLevelMappings)
                .HasForeignKey(x => x.GrammarPatternId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mỗi dòng mapping phải trỏ tới ĐÚNG MỘT loại nội dung (Kanji hoặc Vocabulary hoặc GrammarPattern)
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_ContentLevelMappings_ExactlyOneContent",
                "(CASE WHEN \"KanjiId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"VocabularyId\" IS NOT NULL THEN 1 ELSE 0 END) + " +
                "(CASE WHEN \"GrammarPatternId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));

            // Không cho gán trùng cùng một nội dung vào cùng một level (bỏ qua dòng đã xoá mềm)
            builder.HasIndex(x => new { x.CertificateLevelId, x.KanjiId })
                .IsUnique()
                .HasFilter("\"KanjiId\" IS NOT NULL AND \"IsDeleted\" = false");

            builder.HasIndex(x => new { x.CertificateLevelId, x.VocabularyId })
                .IsUnique()
                .HasFilter("\"VocabularyId\" IS NOT NULL AND \"IsDeleted\" = false");

            builder.HasIndex(x => new { x.CertificateLevelId, x.GrammarPatternId })
                .IsUnique()
                .HasFilter("\"GrammarPatternId\" IS NOT NULL AND \"IsDeleted\" = false");

            // Seed: gán 10 kanji mẫu (910001..910010) vào N5 (CertificateLevelId = 1)
            builder.HasData(Enumerable.Range(1, 10).Select(i => new ContentLevelMapping
            {
                Id = 920000 + i,
                CertificateLevelId = 1,
                KanjiId = 910000 + i,
                CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                IsDeleted = false
            }));
        }
    }
}