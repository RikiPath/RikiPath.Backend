using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonKanjiConfig : IEntityTypeConfiguration<LessonKanji>
    {
        public void Configure(EntityTypeBuilder<LessonKanji> builder)
        {
            builder.HasKey(x => new { x.LessonId, x.KanjiEntryId });

            builder.HasOne(x => x.Lesson)
                .WithMany(x => x.LessonKanjis)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.KanjiEntry)
                .WithMany(x => x.LessonKanjis)
                .HasForeignKey(x => x.KanjiEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
