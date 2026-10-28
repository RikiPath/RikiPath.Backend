using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockQuestionConfig : IEntityTypeConfiguration<MockQuestion>
    {
        public void Configure(EntityTypeBuilder<MockQuestion> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockTestSectionId).HasColumnName("PracticeTestSectionId");
            builder.Property(x => x.QuestionText).IsRequired();
            builder.Property(x => x.QuestionType).IsRequired().HasMaxLength(40).HasDefaultValue("SingleChoice");
            builder.Property(x => x.InteractionDataJson).HasColumnType("jsonb");
            builder.Property(x => x.AnswerKeyJson).HasColumnType("jsonb");

            builder.HasOne(x => x.MockTestSection)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.MockTestSectionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
