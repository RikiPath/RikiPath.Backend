using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;

namespace RikiPath.Infrastructure.Configuration;

public sealed class MentorCalendarConnectionConfig : IEntityTypeConfiguration<MentorCalendarConnection>
{
    public void Configure(EntityTypeBuilder<MentorCalendarConnection> b)
    {
        b.ToTable("MentorCalendarConnections");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.MentorId).IsUnique();
        b.HasIndex(x => x.StateHash).IsUnique();
        b.HasOne(x => x.Mentor).WithMany().HasForeignKey(x => x.MentorId).OnDelete(DeleteBehavior.Restrict);
    }
}
