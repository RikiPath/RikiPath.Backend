using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
namespace RikiPath.Infrastructure.Configuration;
public sealed class MentorWeeklyAvailabilityConfig : IEntityTypeConfiguration<MentorWeeklyAvailability>
{
    public void Configure(EntityTypeBuilder<MentorWeeklyAvailability> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Period).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.MentorId, x.Date, x.Period }).IsUnique().HasFilter("NOT \"IsDeleted\"");
        builder.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Slots).WithOne(x => x.WeeklyAvailability)
            .HasForeignKey(x => x.MentorWeeklyAvailabilityId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_MentorWeeklyAvailability_Period", "\"Period\" IN ('Morning', 'Afternoon', 'Evening')"));
    }
}
