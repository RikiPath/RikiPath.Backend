using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public class PracticeQuestionConfig : IEntityTypeConfiguration<PracticeQuestion>
{
    public void Configure(EntityTypeBuilder<PracticeQuestion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionText).IsRequired();
        builder.HasOne(x => x.PracticeExercise).WithMany(x => x.Questions).HasForeignKey(x => x.PracticeExerciseId).OnDelete(DeleteBehavior.Cascade);
    }
}
