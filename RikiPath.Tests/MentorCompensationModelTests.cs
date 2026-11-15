using Microsoft.EntityFrameworkCore;
using RikiPath.Domain.Entities;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class MentorCompensationModelTests
{
    private static AppDbContext ModelContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=invalid;Database=metadata_only").Options);

    [Fact]
    public void LegacyBookingsKeepNullableCompensationSnapshot()
    {
        using var db = ModelContext();
        var booking = db.Model.FindEntityType(typeof(MentorBooking))!;
        Assert.True(booking.FindProperty(nameof(MentorBooking.CompensationLevelId))!.IsNullable);
        Assert.True(booking.FindProperty(nameof(MentorBooking.FrozenHourlyRate))!.IsNullable);
        Assert.True(booking.FindProperty(nameof(MentorBooking.FrozenLevelCoefficient))!.IsNullable);
        Assert.True(booking.FindProperty(nameof(MentorBooking.ScheduledCompensationAmount))!.IsNullable);
        Assert.True(booking.FindProperty(nameof(MentorBooking.CompensationFormulaVersion))!.IsNullable);
    }

    [Fact]
    public void AdminConfigurationAndQualificationHaveDistinctKeys()
    {
        using var db = ModelContext();
        var rate = db.Model.FindEntityType(typeof(MentorHourlyRate))!;
        var coefficient = db.Model.FindEntityType(typeof(JlptLevelPayCoefficient))!;
        var qualification = db.Model.FindEntityType(typeof(MentorTeachingQualification))!;
        Assert.Contains(rate.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MentorUserId", "EffectiveFromUtc" }));
        Assert.Contains(coefficient.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "CertificateLevelId", "EffectiveFromUtc" }));
        Assert.Contains(qualification.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MentorUserId", "CertificateLevelId" }));
        Assert.Equal(18, rate.FindProperty(nameof(MentorHourlyRate.AmountPerHour))!.GetPrecision());
        Assert.Equal(2, rate.FindProperty(nameof(MentorHourlyRate.AmountPerHour))!.GetScale());
    }
}
