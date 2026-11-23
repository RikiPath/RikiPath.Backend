using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class NotebookPageConfig : IEntityTypeConfiguration<NotebookPage>
    {
        public void Configure(EntityTypeBuilder<NotebookPage> builder)
        {
            builder.ToTable("NotebookPages");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
            builder.Property(x => x.ContentJson)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{\"type\":\"doc\",\"version\":1,\"blocks\":[]}'::jsonb");
            builder.Property(x => x.ContentPlainText)
                .IsRequired()
                .HasDefaultValue(string.Empty);
            builder.Property(x => x.ContentFormatVersion).HasDefaultValue(1);
            builder.Property(x => x.ChecklistTotalCount).HasDefaultValue(0);
            builder.Property(x => x.ChecklistCompletedCount).HasDefaultValue(0);
            builder.Property(x => x.IsFavorite).HasDefaultValue(false);
            builder.Property(x => x.IsDefault).HasDefaultValue(false);
            builder.Property(x => x.SourceType).HasMaxLength(40);
            builder.Property(x => x.SourcePath).HasMaxLength(300);
            builder.Property(x => x.SourceTitle).HasMaxLength(200);
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();

            // Avoid multiple cascade paths through UserAccount; deletion ownership cascades via LearnerNote -> NotebookPages.
            builder.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.LearnerNote)
                .WithMany(x => x.Pages)
                .HasForeignKey(x => x.LearnerNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.LearnerNoteId, x.IsDeleted, x.SortOrder });
            builder.HasIndex(x => new { x.UserId, x.IsDeleted, x.IsFavorite });
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_NotebookPages_UserId_IsDefault")
                .HasFilter("\"IsDeleted\" = false AND \"IsDefault\" = true");
        }
    }
}
