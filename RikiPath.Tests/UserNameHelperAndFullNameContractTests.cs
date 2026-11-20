using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RikiPath.Application;
using RikiPath.Application.Helpers;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.AdminMentor;
using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Requests.Profile;
using RikiPath.Application.Responses.AdminMentor;
using RikiPath.Application.Responses.Auth;
using RikiPath.Application.Responses.MentorProfile;
using RikiPath.Application.Responses.Profile;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class UserNameHelperAndFullNameContractTests
{
    [Fact]
    public void SplitFullName_and_FormatFullName_preserve_Vietnamese_order_and_column_limits()
    {
        var (first, last) = UserNameHelper.SplitFullName("  Nguyễn   Minh   Anh  ");
        Assert.Equal("Nguyễn Minh Anh", first);
        Assert.Null(last);
        Assert.Equal("Nguyễn Minh Anh", UserNameHelper.FormatFullName(first, last));

        var longName = $"{new string('A', 90)} {new string('B', 90)}";
        var (part1, part2) = UserNameHelper.SplitFullName(longName);
        Assert.NotNull(part1);
        Assert.NotNull(part2);
        Assert.True(part1!.Length <= UserNameHelper.MaxPartLength);
        Assert.True(part2!.Length <= UserNameHelper.MaxPartLength);
        Assert.Equal($"{new string('A', 90)} {new string('B', 90)}", UserNameHelper.FormatFullName(part1, part2));

        var (emptyFirst, emptyLast) = UserNameHelper.SplitFullName("   ");
        Assert.Null(emptyFirst);
        Assert.Null(emptyLast);
    }

    [Fact]
    public void Account_and_profile_API_contracts_expose_FullName_instead_of_FirstName_LastName()
    {
        Type[] contractTypes =
        [
            typeof(RegisterRequest),
            typeof(RegisterResponse),
            typeof(LoginResponse),
            typeof(UpdateProfileRequest),
            typeof(UserProfileResponse),
            typeof(CreateMentorRequest),
            typeof(MentorResponse),
            typeof(MentorProfileResponse),
            typeof(AdminMentorProfileResponse),
            typeof(AdminCreateUserRequest),
            typeof(AdminUpdateUserRequest),
        ];

        foreach (var contract in contractTypes)
        {
            Assert.NotNull(contract.GetProperty("FullName"));
            Assert.Null(contract.GetProperty("FirstName"));
            Assert.Null(contract.GetProperty("LastName"));
        }
    }

    [Fact]
    public async Task Register_and_UpdateProfile_persist_and_return_exact_FullName()
    {
        await using var db = CreateInMemoryDb();

        var emailService = new Mock<IEmailService>();
        string? verificationHtml = null;
        emailService
            .Setup(x => x.SendValidationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, body, _) => verificationHtml = body)
            .ReturnsAsync(EmailSendResult.Success());

        var uow = new UnitOfWork(db);
        var authService = new AuthService(
            uow,
            new AppSettings(),
            emailService.Object,
            Mock.Of<IFirebaseAuthService>(),
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<IClaimService>());

        var register = await authService.RegisterAsync(new RegisterRequest
        {
            Email = "anh.nguyen@example.test",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            FullName = "  Nguyễn  Minh   Anh ",
        }, CancellationToken.None);

        Assert.True(register.IsSuccess);
        Assert.Equal("Nguyễn Minh Anh", register.Result!.FullName);
        Assert.Contains("Xin chào Nguyễn Minh Anh,", verificationHtml);

        var createdUser = await db.Users.SingleAsync(u => u.Email == "anh.nguyen@example.test");
        var claims = new Mock<IClaimService>();
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = createdUser.Id, Role = Role.Learner });
        var profileService = new UserProfileService(uow, Mock.Of<IFileStorageService>(), claims.Object);

        var updated = await profileService.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "Trần   Hoàng Nam" },
            CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal("Trần Hoàng Nam", updated.Result!.FullName);

        var emptyReject = await profileService.UpdateProfileAsync(
            new UpdateProfileRequest { FullName = "   " },
            CancellationToken.None);
        Assert.False(emptyReject.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, (HttpStatusCode)emptyReject.StatusCode);
    }

    private static AppDbContext CreateInMemoryDb() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
