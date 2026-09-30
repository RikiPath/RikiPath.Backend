using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LessonGrammarConfig : IEntityTypeConfiguration<LessonGrammar>
    {
        public void Configure(EntityTypeBuilder<LessonGrammar> builder)
        {
            builder.HasKey(x => new { x.LessonId, x.GrammarPatternId });
            builder.Property(x => x.GrammarPatternId).HasColumnName("GrammarPointId");

            builder.HasOne(x => x.Lesson)
                .WithMany(x => x.LessonGrammars)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.GrammarPattern)
                .WithMany(x => x.LessonGrammars)
                .HasForeignKey(x => x.GrammarPatternId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
