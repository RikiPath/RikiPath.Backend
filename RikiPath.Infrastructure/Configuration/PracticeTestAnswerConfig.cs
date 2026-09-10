using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeTestAnswerConfig : IEntityTypeConfiguration<PracticeTestAnswer>
    {
        public void Configure(EntityTypeBuilder<PracticeTestAnswer> builder)
        {
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.PracticeTestAttempt)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.PracticeTestAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.PracticeQuestion)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.PracticeQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SelectedOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => new { x.PracticeTestAttemptId, x.PracticeQuestionId }).IsUnique();
        }
    }
}
