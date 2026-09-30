using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockTestAnswerConfig : IEntityTypeConfiguration<MockTestAnswer>
    {
        public void Configure(EntityTypeBuilder<MockTestAnswer> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockTestAttemptId).HasColumnName("PracticeTestAttemptId");
            builder.Property(x => x.MockQuestionId).HasColumnName("PracticeQuestionId");

            builder.HasOne(x => x.MockTestAttempt)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.MockTestAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.MockQuestion)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.MockQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SelectedOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => new { x.MockTestAttemptId, x.MockQuestionId }).IsUnique();
        }
    }
}
