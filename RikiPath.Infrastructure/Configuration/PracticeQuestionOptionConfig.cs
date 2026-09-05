using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class PracticeQuestionOptionConfig : IEntityTypeConfiguration<PracticeQuestionOption>
    {
        public void Configure(EntityTypeBuilder<PracticeQuestionOption> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.OptionText).IsRequired().HasMaxLength(500);

            builder.HasOne(x => x.PracticeQuestion)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.PracticeQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
