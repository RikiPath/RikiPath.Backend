using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class BookingMentorDirectoryTests
{
    [Fact]
    public async Task Directory_returns_database_mentors_and_excludes_deleted_or_other_roles()
    {
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.AddRange(
            User(1, "Zulu", Role.Mentor),
            User(2, "An", Role.Mentor),
            User(3, "Deleted", Role.Mentor, true),
            User(4, "Learner", Role.Learner),
            User(5, "Admin", Role.Admin));
        await context.SaveChangesAsync();
        var service = new MentorMeetingService(new UnitOfWork(context),
            Mock.Of<IPaymentGatewayClient>(), Mock.Of<IClaimService>(), new AppSettings(),
            NullLogger<MentorMeetingService>.Instance);

        var result = await service.GetMentorsAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 2, 1 }, result.Result!.Select(x => x.Id));
        Assert.Equal("An Mentor", result.Result[0].Name);
        Assert.Equal("https://example.com/avatar.png", result.Result[0].AvatarUrl);

        context.Users.Add(User(6, "Binh", Role.Mentor));
        await context.SaveChangesAsync();
        Assert.Contains((await service.GetMentorsAsync(default)).Result!, x => x.Id == 6);
    }

    private static UserAccount User(int id, string name, Role role, bool deleted = false) => new()
    {
        Id = id, FirstName = name, LastName = "Mentor", Role = role, IsDeleted = deleted,
        AvatarUrl = "https://example.com/avatar.png", PasswordHash = [], PasswordSalt = []
    };
}
