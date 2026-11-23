using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration
{
    public class NotebookFolderConfig : IEntityTypeConfiguration<NotebookFolder>
    {
        public void Configure(EntityTypeBuilder<NotebookFolder> builder)
        {
            builder.ToTable("NotebookFolders");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
            builder.Property(x => x.NormalizedName).IsRequired().HasMaxLength(120);
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.Property(x => x.IsDefault).HasDefaultValue(false);
            builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();

            builder.HasOne(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.UserId, x.IsDeleted, x.SortOrder });
            builder.HasIndex(x => new { x.UserId, x.NormalizedName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_NotebookFolders_UserId_IsDefault")
                .HasFilter("\"IsDeleted\" = false AND \"IsDefault\" = true");
        }
    }
}
