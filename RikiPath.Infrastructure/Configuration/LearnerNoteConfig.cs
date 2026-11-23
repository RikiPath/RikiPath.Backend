using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class LearnerNoteConfig : IEntityTypeConfiguration<LearnerNote>
    {
        public void Configure(EntityTypeBuilder<LearnerNote> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.Property(x => x.IsDefault).HasDefaultValue(false);
            builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();

            builder.HasOne(x => x.Folder)
                .WithMany(x => x.Files)
                .HasForeignKey(x => x.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserId FK/relationship (Cascade) configured from UserConfig.
            builder.HasIndex(x => new { x.UserId, x.Name });
            builder.HasIndex(x => new { x.UserId, x.FolderId, x.IsDeleted, x.SortOrder });
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_LearnerNotes_UserId_IsDefault")
                .HasFilter("\"IsDeleted\" = false AND \"IsDefault\" = true");
        }
    }
}
