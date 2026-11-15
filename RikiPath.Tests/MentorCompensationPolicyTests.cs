using RikiPath.Domain.MentorCompensation;

namespace RikiPath.Tests;

public sealed class MentorCompensationPolicyTests
{
    [Theory]
    [InlineData("N5")]
    [InlineData("N4")]
    [InlineData("N3")]
    [InlineData("N2")]
    public void ApprovedLevelsProduceNinetyMinuteSnapshot(string level)
    {
        var snapshot = MentorCompensationPolicy.CreateSnapshot(level, 100_000m, 1.2m);
        Assert.Equal(90, snapshot.SessionMinutes);
        Assert.Equal(180_000m, snapshot.ScheduledAmount);
        Assert.Equal(1, snapshot.FormulaVersion);
    }

    [Theory]
    [InlineData("N1")]
    [InlineData("N6")]
    [InlineData("")]
    public void UnsupportedLevelIsRejected(string level)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            MentorCompensationPolicy.CreateSnapshot(level, 100_000m, 1m));

    [Fact]
    public void SnapshotDoesNotChangeAfterRateVariablesChange()
    {
        var hourlyRate = 100_000m;
        var coefficient = 1.5m;
        var snapshot = MentorCompensationPolicy.CreateSnapshot("N3", hourlyRate, coefficient);
        hourlyRate = 200_000m;
        coefficient = 2m;
        Assert.Equal(225_000m, snapshot.ScheduledAmount);
        Assert.Equal(100_000m, snapshot.HourlyRate);
        Assert.Equal(1.5m, snapshot.LevelCoefficient);
    }

    [Fact]
    public void InvalidRatesAndCurrencyAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MentorCompensationPolicy.CreateSnapshot("N5", 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MentorCompensationPolicy.CreateSnapshot("N5", 100, 0));
        Assert.Throws<ArgumentException>(() => MentorCompensationPolicy.CreateSnapshot("N5", 100, 1, "USD"));
    }
}
