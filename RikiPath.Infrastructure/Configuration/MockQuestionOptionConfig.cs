using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class MockQuestionOptionConfig : IEntityTypeConfiguration<MockQuestionOption>
    {
        public void Configure(EntityTypeBuilder<MockQuestionOption> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MockQuestionId).HasColumnName("PracticeQuestionId");
            builder.Property(x => x.OptionText).IsRequired().HasMaxLength(500);

            builder.HasOne(x => x.MockQuestion)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.MockQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
