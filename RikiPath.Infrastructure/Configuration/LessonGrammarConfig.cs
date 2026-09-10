using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonGrammarConfig : IEntityTypeConfiguration<LessonGrammar>
    {
        public void Configure(EntityTypeBuilder<LessonGrammar> builder)
        {
            builder.HasKey(x => new { x.LessonId, x.GrammarPointId });

            builder.HasOne(x => x.Lesson)
                .WithMany(x => x.LessonGrammars)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.GrammarPoint)
                .WithMany(x => x.LessonGrammars)
                .HasForeignKey(x => x.GrammarPointId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
