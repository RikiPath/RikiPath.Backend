using Microsoft.Extensions.Options;
using RikiPath.Application.Options;
using RikiPath.Application.Services;
using RikiPath.Domain.MentorScheduling;

namespace RikiPath.Tests;

public sealed class MentorSlotGeneratorTests
{
    private static readonly DateOnly LocalDate = new(2026, 6, 1);
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero); // 07:00 theo giờ Việt Nam.
    private const int MentorId = 42;

    [Fact]
    public void GeneratesMorningSlotsInsideConfiguredWindow()
    {
        var slots = Generate(MentorDaypart.Morning);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new TimeOnly(7, 0), LocalTime(slots[0].StartUtc));
        Assert.Equal(new TimeOnly(8, 45), LocalTime(slots[1].StartUtc));
        Assert.All(slots, slot => Assert.Equal(MentorDaypart.Morning, slot.Daypart));
    }

    [Fact]
    public void GeneratesNoonSlot()
    {
        var slots = Generate(MentorDaypart.Noon);

        Assert.Single(slots);
        Assert.Equal(new TimeOnly(11, 0), LocalTime(slots[0].StartUtc));
        Assert.Equal(new TimeOnly(12, 30), LocalTime(slots[0].EndUtc));
    }

    [Fact]
    public void GeneratesAfternoonSlotsIncludingExactShiftEnd()
    {
        var slots = Generate(MentorDaypart.Afternoon);

        Assert.Equal(3, slots.Count);
        Assert.Equal(new TimeOnly(16, 30), LocalTime(slots[^1].StartUtc));
        Assert.Equal(new TimeOnly(18, 0), LocalTime(slots[^1].EndUtc));
    }

    [Fact]
    public void GeneratesEveningOnlyFromItsOwnStart()
    {
        var slots = Generate(MentorDaypart.Evening);

        Assert.Equal(2, slots.Count);
        Assert.Equal(new TimeOnly(18, 0), LocalTime(slots[0].StartUtc));
    }

    [Fact]
    public void AppliesRestAcrossAdjacentAfternoonAndEveningDayparts()
    {
        var slots = Generate(MentorDaypart.Afternoon, MentorDaypart.Evening);
        var eveningSlots = slots.Where(x => x.Daypart == MentorDaypart.Evening).OrderBy(x => x.StartUtc).ToArray();
        Assert.Equal(2, eveningSlots.Length);
        var firstEveningSlot = eveningSlots[0];
        var lastAfternoonSlot = slots.Last(x => x.Daypart == MentorDaypart.Afternoon);

        Assert.Equal(new TimeOnly(18, 15), LocalTime(firstEveningSlot.StartUtc));
        Assert.Equal(new TimeOnly(16, 30), LocalTime(lastAfternoonSlot.StartUtc));
        Assert.Equal(TimeSpan.FromMinutes(15), firstEveningSlot.StartUtc - lastAfternoonSlot.EndUtc);
    }

    [Fact]
    public void EveryGeneratedSlotIsExactlyNinetyMinutes()
    {
        var slots = Generate(MentorDaypart.Morning, MentorDaypart.Noon, MentorDaypart.Afternoon, MentorDaypart.Evening);

        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromMinutes(90), slot.EndUtc - slot.StartUtc));
    }

    [Fact]
    public void RejectsFourteenMinuteNinetyMinutePolicyRestAndAcceptsFifteenMinutes()
    {
        var existing = Appointment(LocalTimeUtc(new TimeOnly(13, 0)), usesNinetyMinutePolicy: true);
        var tooSoon = Candidate(new TimeOnly(14, 44));
        var exactlyRested = Candidate(new TimeOnly(14, 45));

        var result = Service().ValidateBooking(tooSoon, [existing], NowUtc);
        Assert.Equal("REST_BUFFER", result.ErrorCode);
        Assert.Equal("Hai buổi hỗ trợ theo chính sách 90 phút phải cách nhau ít nhất 15 phút.", result.Message);
        Assert.True(Service().ValidateBooking(exactlyRested, [existing], NowUtc).IsValid);
    }

    [Fact]
    public void RejectsAppointmentOutsideSelectedDaypart()
    {
        var candidate = new MentorBookingCandidate(MentorId, MentorDaypart.Morning,
            LocalTimeUtc(new TimeOnly(10, 30)), LocalTimeUtc(new TimeOnly(12, 0)));

        Assert.Equal("OUTSIDE_DAYPART", Service().ValidateBooking(candidate, [], NowUtc).ErrorCode);
    }

    [Fact]
    public void AcceptsThirtiethLocalCalendarDayAndRejectsThirtyFirst()
    {
        var onBoundary = Candidate(new DateOnly(2026, 7, 1), MentorDaypart.Afternoon, new TimeOnly(13, 0));
        var beyondBoundary = Candidate(new DateOnly(2026, 7, 2), MentorDaypart.Afternoon, new TimeOnly(13, 0));

        Assert.True(Service().ValidateBooking(onBoundary, [], NowUtc).IsValid);
        Assert.Equal("HORIZON", Service().ValidateBooking(beyondBoundary, [], NowUtc).ErrorCode);
    }

    [Fact]
    public void AcceptsExactTwoHourLeadAndRejectsOneTickLess()
    {
        var exact = Candidate(LocalDate, MentorDaypart.Morning, new TimeOnly(9, 0));
        var shortLead = exact with { StartUtc = exact.StartUtc.AddTicks(-1), EndUtc = exact.EndUtc.AddTicks(-1) };

        Assert.True(Service().ValidateBooking(exact, [], NowUtc).IsValid);
        Assert.Equal("LEAD_TIME", Service().ValidateBooking(shortLead, [], NowUtc).ErrorCode);
    }

    [Fact]
    public void GenerationIdentityIsDeterministicForIdenticalInput()
    {
        var first = Generate(MentorDaypart.Afternoon);
        var second = Generate(MentorDaypart.Afternoon);

        Assert.Equal(first.Select(x => x.GenerationKey), second.Select(x => x.GenerationKey));
        Assert.Equal(first.Select(x => x.StartUtc), second.Select(x => x.StartUtc));
    }

    [Fact]
    public void UsesVietnamLocalDateAcrossUtcMidnightBoundary()
    {
        var afterUtcMidnight = new DateTimeOffset(2026, 5, 12, 17, 30, 0, TimeSpan.Zero); // 00:30 ngày 13/5 theo giờ Việt Nam.
        var slots = Service().GenerateDailySlots(MentorId, new DateOnly(2026, 5, 13), [MentorDaypart.Morning], afterUtcMidnight);

        Assert.Equal(new DateOnly(2026, 5, 13), slots[0].LocalDate);
        Assert.Equal(new TimeOnly(7, 0), LocalTime(slots[0].StartUtc));
    }

    [Fact]
    public void AllowsFourthNinetyMinutePolicyBookingWhenThreeNonCancelledBookingsExist()
    {
        var existing = new[]
        {
            Appointment(LocalTimeUtc(new TimeOnly(7, 0)), true),
            Appointment(LocalTimeUtc(new TimeOnly(8, 45)), true),
            Appointment(LocalTimeUtc(new TimeOnly(10, 30)), true)
        };

        Assert.True(Service().ValidateBooking(Candidate(new TimeOnly(13, 0)), existing, NowUtc).IsValid);
    }

    [Fact]
    public void RejectsFifthNinetyMinutePolicyBooking()
    {
        var existing = FourDailyAppointments();

        Assert.Equal("DAILY_CAP", Service().ValidateBooking(Candidate(new TimeOnly(13, 0)), existing, NowUtc).ErrorCode);
    }

    [Fact]
    public void ExcludesCancelledNinetyMinutePolicyBookingFromDailyCap()
    {
        var existing = FourDailyAppointments().ToArray();
        existing[3] = existing[3] with { IsCancelled = true };

        Assert.True(Service().ValidateBooking(Candidate(new TimeOnly(13, 0)), existing, NowUtc).IsValid);
    }

    [Fact]
    public void CompletedNinetyMinutePolicyBookingStillCountsTowardDailyCap()
    {
        // Buổi đã hoàn thành vẫn được tính vào giới hạn buổi học trong ngày.
        var existing = FourDailyAppointments();

        Assert.Equal("DAILY_CAP", Service().ValidateBooking(Candidate(new TimeOnly(13, 0)), existing, NowUtc).ErrorCode);
    }

    [Fact]
    public void ConfiguredDaypartHoursAndDailyCapAreApplied()
    {
        var options = new MentorSlotGenerationOptions
        {
            DailyBookingCap = 2,
            Dayparts = [new MentorDaypartWindowOptions
            {
                Daypart = MentorDaypart.Evening,
                StartLocal = "19:00",
                EndLocal = "22:00"
            }]
        };
        var service = new MentorSlotGenerator(Options.Create(options));
        var slots = service.GenerateDailySlots(MentorId, LocalDate, [MentorDaypart.Evening], NowUtc);
        var existing = new[]
        {
            Appointment(LocalTimeUtc(new TimeOnly(7, 0)), usesNinetyMinutePolicy: true),
            Appointment(LocalTimeUtc(new TimeOnly(8, 45)), usesNinetyMinutePolicy: true)
        };

        Assert.Equal(new TimeOnly(19, 0), LocalTime(slots[0].StartUtc));
        Assert.Equal("DAILY_CAP", service.ValidateBooking(Candidate(LocalDate, MentorDaypart.Evening, new TimeOnly(19, 0)), existing, NowUtc).ErrorCode);
    }

    [Fact]
    public void AutoPublicationDefaultsOff()
    {
        Assert.False(new MentorSlotGenerationOptions().ToPolicy().AutoPublicationEnabled);
    }

    [Fact]
    public void RejectsActualOverlapWithLegacyAppointmentButDoesNotApplyUnapprovedCrossVersionRest()
    {
        var legacyFiftyMinute = Appointment(LocalTimeUtc(new TimeOnly(14, 0)), usesNinetyMinutePolicy: false) with
        {
            EndUtc = LocalTimeUtc(new TimeOnly(14, 50))
        };
        var candidateAfterTenMinuteLegacyGap = Candidate(new TimeOnly(15, 0));
        var candidateOverlappingLegacy = Candidate(new TimeOnly(14, 30));

        Assert.True(Service().ValidateBooking(candidateAfterTenMinuteLegacyGap, [legacyFiftyMinute], NowUtc).IsValid);
        Assert.Equal("APPOINTMENT_OVERLAP", Service().ValidateBooking(candidateOverlappingLegacy, [legacyFiftyMinute], NowUtc).ErrorCode);
        Assert.Equal(TimeSpan.FromMinutes(50), legacyFiftyMinute.EndUtc - legacyFiftyMinute.StartUtc);
    }

    [Fact]
    public void RejectsDuplicateOrInvalidConfiguredDayparts()
    {
        var duplicate = new[]
        {
            new MentorDaypartWindow(MentorDaypart.Morning, new TimeOnly(7, 0), new TimeOnly(10, 0)),
            new MentorDaypartWindow(MentorDaypart.Morning, new TimeOnly(10, 0), new TimeOnly(11, 0))
        };

        Assert.Throws<ArgumentException>(() => new MentorSlotPolicy(duplicate));
        Assert.Throws<ArgumentException>(() => new MentorSlotPolicy(
            [new MentorDaypartWindow(MentorDaypart.Noon, new TimeOnly(12, 0), new TimeOnly(11, 0))]));
    }

    [Fact]
    public void RejectsDuplicateSelectionAndOutOfHorizonGeneration()
    {
        var duplicate = Assert.Throws<ArgumentException>(() => Service().GenerateDailySlots(MentorId, LocalDate,
            [MentorDaypart.Morning, MentorDaypart.Morning], NowUtc));
        Assert.StartsWith("Không được chọn trùng ca làm việc.", duplicate.Message);

        var outOfHorizon = Assert.Throws<ArgumentOutOfRangeException>(() => Service().GenerateDailySlots(MentorId,
            LocalDate.AddDays(31), [MentorDaypart.Morning], NowUtc));
        Assert.StartsWith("Chỉ có thể tạo lịch cho 30 ngày tới theo giờ Việt Nam.", outOfHorizon.Message);
    }

    [Fact]
    public void SchedulingValidationMessagesAreVietnamese()
    {
        var service = Service();
        var noDaypart = Assert.Throws<ArgumentException>(() => service.GenerateDailySlots(MentorId, LocalDate, [], NowUtc));
        Assert.StartsWith("Vui lòng chọn ít nhất một ca làm việc.", noDaypart.Message);

        var configuredOnlyForEvening = new MentorSlotGenerator(Options.Create(new MentorSlotGenerationOptions
        {
            Dayparts = [new MentorDaypartWindowOptions
            {
                Daypart = MentorDaypart.Evening,
                StartLocal = "18:00",
                EndLocal = "22:00"
            }]
        }));
        var missingDaypart = Assert.Throws<ArgumentException>(() => configuredOnlyForEvening.GenerateDailySlots(
            MentorId, LocalDate, [MentorDaypart.Morning], NowUtc));
        Assert.StartsWith("Ca làm việc Morning chưa được cấu hình.", missingDaypart.Message);

        var invalidDuration = Candidate(new TimeOnly(13, 0)) with
        {
            EndUtc = LocalTimeUtc(new TimeOnly(14, 0))
        };
        var result = service.ValidateBooking(invalidDuration, [], NowUtc);
        Assert.Equal("INVALID_DURATION", result.ErrorCode);
        Assert.Equal("Buổi hỗ trợ theo chính sách này phải kéo dài đúng 90 phút.", result.Message);

        var invalidTime = new MentorSlotGenerationOptions
        {
            Dayparts = [new MentorDaypartWindowOptions
            {
                Daypart = MentorDaypart.Morning,
                StartLocal = "7 giờ",
                EndLocal = "11:00"
            }]
        };
        var invalidFormat = Assert.Throws<ArgumentException>(() => invalidTime.ToPolicy());
        Assert.StartsWith("Trường StartLocal phải dùng định dạng giờ 24 giờ HH:mm.", invalidFormat.Message);
    }

    private static IReadOnlyList<MentorGeneratedSlot> Generate(params MentorDaypart[] dayparts)
        => Service().GenerateDailySlots(MentorId, LocalDate, dayparts, NowUtc);

    private static MentorSlotGenerator Service()
        => new(Options.Create(new MentorSlotGenerationOptions()));

    private static MentorBookingCandidate Candidate(TimeOnly start)
        => Candidate(LocalDate, MentorDaypart.Afternoon, start);

    private static MentorBookingCandidate Candidate(DateOnly date, MentorDaypart daypart, TimeOnly start)
    {
        var startUtc = LocalTimeUtc(date, start);
        return new MentorBookingCandidate(MentorId, daypart, startUtc, startUtc.AddMinutes(90));
    }

    private static ExistingMentorAppointment[] FourDailyAppointments()
    =>
    [
        Appointment(LocalTimeUtc(new TimeOnly(7, 0)), usesNinetyMinutePolicy: true),
        Appointment(LocalTimeUtc(new TimeOnly(8, 45)), usesNinetyMinutePolicy: true),
        Appointment(LocalTimeUtc(new TimeOnly(10, 30)), usesNinetyMinutePolicy: true),
        Appointment(LocalTimeUtc(new TimeOnly(14, 45)), usesNinetyMinutePolicy: true)
    ];

    private static ExistingMentorAppointment Appointment(DateTimeOffset startUtc, bool usesNinetyMinutePolicy)
        => new(MentorId, startUtc, startUtc.AddMinutes(90), usesNinetyMinutePolicy, IsCancelled: false);

    private static DateTimeOffset LocalTimeUtc(TimeOnly time)
        => LocalTimeUtc(LocalDate, time);

    private static DateTimeOffset LocalTimeUtc(DateOnly date, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(MentorSlotPolicy.BusinessTimeZoneId);
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static TimeOnly LocalTime(DateTimeOffset utc)
        => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(
            MentorSlotPolicy.BusinessTimeZoneId)).DateTime);
}
