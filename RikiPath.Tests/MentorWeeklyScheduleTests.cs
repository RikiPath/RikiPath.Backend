using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Application.Services;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.WebApi.Controllers;
namespace RikiPath.Tests;

public sealed class MentorWeeklyScheduleTests
{
    [Theory]
    [InlineData("2026-10-30T10:59:59Z", true)]
    [InlineData("2026-10-30T11:00:00Z", false)]
    [InlineData("2026-10-31T01:00:00Z", false)]
    public void Deadline_is_Friday_18_Vietnam_time(string utc, bool expected)
        => Assert.Equal(expected, MentorScheduleRules.CanRegister(new DateOnly(2026, 11, 2), DateTime.Parse(utc).ToUniversalTime()));
    [Theory]
    [InlineData("Morning", 1)] [InlineData("Afternoon", 6)] [InlineData("Evening", 11)]
    public void Fixed_slots_use_Vietnam_time_and_leave_ten_minutes(string period, int utcHour)
    {
        var start = MentorScheduleRules.SlotStart(new DateOnly(2026, 11, 2), period, 0);
        Assert.Equal(utcHour, start.Hour);
        Assert.Equal(TimeSpan.FromMinutes(10), MentorScheduleRules.SlotStart(new DateOnly(2026, 11, 2), period, 1) - start.AddMinutes(50));
    }
    private static IClaimService User(Role role, int id = 1)
    {
        var mock = new Mock<IClaimService>(); mock.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = id, Role = role }); return mock.Object;
    }
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static MentorScheduleService Service(AppDbContext db, Role role, int id = 1) => new(new UnitOfWork(db), User(role, id),
        Mock.Of<IEmailService>(), NullLogger<MentorScheduleService>.Instance);

    [Fact]
    public async Task Retired_weekly_write_routes_are_removed_while_historical_reads_remain()
    {
        Assert.Null(typeof(IMentorScheduleService).GetMethod("SaveWeekAsync"));
        Assert.Null(typeof(IMentorScheduleService).GetMethod("AssignAsync"));
        Assert.Null(typeof(IMentorScheduleService).GetMethod("PublishAsync"));
        var controllerActions = typeof(MentorWeeklyScheduleController).GetMethods().Select(m => m.Name).ToList();
        Assert.DoesNotContain("Save", controllerActions);
        Assert.DoesNotContain("Assign", controllerActions);
        Assert.DoesNotContain("Publish", controllerActions);

        await using var db = Context();
        var week = MentorScheduleRules.Monday(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))).AddDays(7);
        var mentor = new UserAccount { Id = 2, Role = Role.Mentor, FirstName = "Mentor", PasswordHash = [], PasswordSalt = [] };
        db.MentorWeeklyAvailabilities.Add(new MentorWeeklyAvailability { Mentor = mentor, Date = week, Period = "Morning" });
        await db.SaveChangesAsync();

        var history = await Service(db, Role.Mentor, mentor.Id).GetWeekAsync(week, default);
        Assert.True(history.IsSuccess);
        Assert.Single(history.Result!);
    }

    [Fact]
    public async Task Legacy_slot_resolution_cannot_refund_bookings_outside_credit_ledger()
    {
        await using var db = Context();
        var result = await Service(db, Role.Admin).ResolveChangeAsync(42,
            new ResolveMentorSlotRequest(true, "Hủy buổi tư vấn"), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("/api/mentor-bookings/", result.ErrorMessage);
    }
}
