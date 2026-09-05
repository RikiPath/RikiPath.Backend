using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeQuestionConfig : IEntityTypeConfiguration<PracticeQuestion>
    {
        public void Configure(EntityTypeBuilder<PracticeQuestion> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.QuestionText).IsRequired();

            builder.HasOne(x => x.PracticeTestSection)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.PracticeTestSectionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
