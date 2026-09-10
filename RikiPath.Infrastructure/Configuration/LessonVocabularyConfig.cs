using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonVocabularyConfig : IEntityTypeConfiguration<LessonVocabulary>
    {
        public void Configure(EntityTypeBuilder<LessonVocabulary> builder)
        {
            builder.HasKey(x => new { x.LessonId, x.VocabularyEntryId });

            builder.HasOne(x => x.Lesson)
                .WithMany(x => x.LessonVocabularies)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.VocabularyEntry)
                .WithMany(x => x.LessonVocabularies)
                .HasForeignKey(x => x.VocabularyEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
