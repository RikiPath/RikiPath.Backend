using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RikiPath.Application;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Services;
using RikiPath.Domain;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.WebApi.Controllers;

namespace RikiPath.Tests;

public sealed class AuthAccountOwnershipTests
{
    [Theory]
    [InlineData(nameof(AuthController.UpdateEmail))]
    [InlineData(nameof(AuthController.ChangePassword))]
    public void Account_mutation_endpoint_requires_authentication(string actionName)
    {
        var action = typeof(AuthController).GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(action);
        Assert.NotNull(action!.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Owner_can_update_own_email_without_changing_existing_business_flow()
    {
        var fixture = CreateFixture(userId: 11);
        var user = CreateUser(11, "old@example.com", "current-secret");
        fixture.Users.Setup(x => x.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        fixture.Users.Setup(x => x.GetByEmailAsync("new@example.com")).ReturnsAsync((UserAccount?)null);
        fixture.EmailVerifications
            .Setup(x => x.AddAsync(It.IsAny<EmailVerification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerification entity, CancellationToken _) => entity);

        var result = await fixture.Service.UpdateEmailAsync(
            11,
            new UpdateEmailRequest { NewEmail = "new@example.com" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("new@example.com", user.Email);
        Assert.False(user.IsEmailVerified);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.EmailService.Verify(
            x => x.SendValidationEmailAsync("new@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task User_cannot_update_another_accounts_email()
    {
        var fixture = CreateFixture(userId: 11);

        var result = await fixture.Service.UpdateEmailAsync(
            22,
            new UpdateEmailRequest { NewEmail = "new@example.com" },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mentor_cannot_update_another_accounts_email()
    {
        var fixture = CreateFixture(userId: 33, role: Role.Mentor);

        var result = await fixture.Service.UpdateEmailAsync(
            22,
            new UpdateEmailRequest { NewEmail = "new@example.com" },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_can_update_own_email()
    {
        var fixture = CreateFixture(userId: 900001, role: Role.Admin);
        var user = ArrangeEmailUpdate(fixture, 900001, "admin@example.com", "admin-new@example.com");

        var result = await fixture.Service.UpdateEmailAsync(
            900001,
            new UpdateEmailRequest { NewEmail = "admin-new@example.com" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("admin-new@example.com", user.Email);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_can_update_another_accounts_email()
    {
        var fixture = CreateFixture(userId: 900001, role: Role.Admin);
        var targetUser = ArrangeEmailUpdate(fixture, 22, "target@example.com", "target-new@example.com");

        var result = await fixture.Service.UpdateEmailAsync(
            22,
            new UpdateEmailRequest { NewEmail = "target-new@example.com" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("target-new@example.com", targetUser.Email);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Request_body_fake_admin_role_cannot_bypass_jwt_role()
    {
        var fixture = CreateFixture(userId: 11, role: Role.Learner);
        var request = JsonSerializer.Deserialize<UpdateEmailRequest>(
            """{"NewEmail":"new@example.com","Role":"Admin"}""")!;

        var result = await fixture.Service.UpdateEmailAsync(22, request, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("new@example.com", request.NewEmail);
        Assert.DoesNotContain(
            typeof(UpdateEmailRequest).GetProperties(),
            property => string.Equals(property.Name, "Role", StringComparison.OrdinalIgnoreCase));
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalid_authenticated_identity_cannot_reach_email_mutation()
    {
        var fixture = CreateFixture(userId: 11);
        fixture.Claims.Setup(x => x.GetUserClaim()).Throws(new FormatException("invalid subject"));

        var result = await fixture.Service.UpdateEmailAsync(
            11,
            new UpdateEmailRequest { NewEmail = "new@example.com" },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Duplicate_email_validation_remains_unchanged()
    {
        var fixture = CreateFixture(userId: 11);
        fixture.Users
            .Setup(x => x.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(11, "old@example.com", "current-secret"));
        fixture.Users
            .Setup(x => x.GetByEmailAsync("used@example.com"))
            .ReturnsAsync(CreateUser(22, "used@example.com", "other-secret"));

        var result = await fixture.Service.UpdateEmailAsync(
            11,
            new UpdateEmailRequest { NewEmail = "used@example.com" },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Owner_can_change_own_password_with_existing_current_password_rule()
    {
        var fixture = CreateFixture(userId: 11);
        var user = CreateUser(11, "owner@example.com", "current-secret");
        var originalHash = user.PasswordHash.ToArray();
        fixture.Users.Setup(x => x.GetByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await fixture.Service.ChangePasswordAsync(
            11,
            new ChangePasswordRequest
            {
                CurrentPassword = "current-secret",
                NewPassword = "new-secret",
                ConfirmPassword = "new-secret",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(originalHash.SequenceEqual(user.PasswordHash));
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task User_cannot_change_another_accounts_password()
    {
        var fixture = CreateFixture(userId: 11);

        var result = await fixture.Service.ChangePasswordAsync(
            22,
            new ChangePasswordRequest
            {
                CurrentPassword = "current-secret",
                NewPassword = "new-secret",
                ConfirmPassword = "new-secret",
            },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mentor_cannot_change_another_accounts_password()
    {
        var fixture = CreateFixture(userId: 33, role: Role.Mentor);

        var result = await fixture.Service.ChangePasswordAsync(
            22,
            new ChangePasswordRequest
            {
                CurrentPassword = "current-secret",
                NewPassword = "new-secret",
                ConfirmPassword = "new-secret",
            },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_can_change_own_password()
    {
        var fixture = CreateFixture(userId: 900001, role: Role.Admin);
        var user = CreateUser(900001, "admin@example.com", "admin-secret");
        fixture.Users
            .Setup(x => x.GetByIdAsync(900001, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await fixture.Service.ChangePasswordAsync(
            900001,
            new ChangePasswordRequest
            {
                CurrentPassword = "admin-secret",
                NewPassword = "new-admin-secret",
                ConfirmPassword = "new-admin-secret",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Wrong_current_password_remains_controlled()
    {
        var fixture = CreateFixture(userId: 11);
        fixture.Users
            .Setup(x => x.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(11, "owner@example.com", "current-secret"));

        var result = await fixture.Service.ChangePasswordAsync(
            11,
            new ChangePasswordRequest
            {
                CurrentPassword = "wrong-secret",
                NewPassword = "new-secret",
                ConfirmPassword = "new-secret",
            },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        fixture.UnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_cannot_change_another_accounts_password()
    {
        var fixture = CreateFixture(userId: 900001, role: Role.Admin);

        var result = await fixture.Service.ChangePasswordAsync(
            11,
            new ChangePasswordRequest
            {
                CurrentPassword = "admin-secret",
                NewPassword = "new-secret",
                ConfirmPassword = "new-secret",
            },
            CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        fixture.Users.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_token_keeps_name_identifier_and_role_claims()
    {
        var fixture = CreateFixture(userId: 11);
        var user = CreateUser(11, "owner@example.com", "current-secret");
        user.Role = Role.Learner;
        user.IsEmailVerified = true;
        fixture.Users.Setup(x => x.GetByEmailAsync(user.Email!)).ReturnsAsync(user);

        var result = await fixture.Service.LoginAsync(
            new LoginRequest { Email = user.Email!, Password = "current-secret" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Result!.AccessToken);
        Assert.Equal("11", token.Claims.Single(x => x.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(Role.Learner.ToString(), token.Claims.Single(x => x.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Claim_service_keeps_current_user_lookup_compatible()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "11"),
            new Claim(ClaimTypes.Role, Role.Learner.ToString()),
        ], "test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        var claim = new ClaimService(accessor).GetUserClaim();

        Assert.Equal(11, claim.Id);
        Assert.Equal(Role.Learner, claim.Role);
    }

    private static AuthFixture CreateFixture(int userId, Role role = Role.Learner)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var users = new Mock<IUserAccountRepository>();
        var emailVerifications = new Mock<IEmailVerificationRepository>();
        var emailService = new Mock<IEmailService>();
        var firebaseAuth = new Mock<IFirebaseAuthService>();
        var claims = new Mock<IClaimService>();

        unitOfWork.SetupGet(x => x.UserAccounts).Returns(users.Object);
        unitOfWork.SetupGet(x => x.EmailVerifications).Returns(emailVerifications.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        emailService
            .Setup(x => x.SendValidationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Success());
        claims.Setup(x => x.GetUserClaim()).Returns(new ClaimDTO { Id = userId, Role = role });

        var appSettings = new AppSettings
        {
            SecretToken = new SecretToken
            {
                Value = "unit-test-only-jwt-secret-with-more-than-sixty-four-characters-1234567890",
            },
        };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(
            unitOfWork.Object,
            appSettings,
            emailService.Object,
            firebaseAuth.Object,
            memoryCache,
            claims.Object);

        return new AuthFixture(
            service,
            unitOfWork,
            users,
            emailVerifications,
            emailService,
            claims,
            memoryCache);
    }

    private static UserAccount CreateUser(int id, string email, string password)
    {
        using var hmac = new HMACSHA512();
        return new UserAccount
        {
            Id = id,
            Email = email,
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password)),
            Role = Role.Learner,
        };
    }

    private static UserAccount ArrangeEmailUpdate(
        AuthFixture fixture,
        int targetUserId,
        string currentEmail,
        string newEmail)
    {
        var user = CreateUser(targetUserId, currentEmail, "current-secret");
        fixture.Users
            .Setup(x => x.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        fixture.Users
            .Setup(x => x.GetByEmailAsync(newEmail))
            .ReturnsAsync((UserAccount?)null);
        fixture.EmailVerifications
            .Setup(x => x.AddAsync(It.IsAny<EmailVerification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerification entity, CancellationToken _) => entity);
        return user;
    }

    private sealed record AuthFixture(
        AuthService Service,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<IUserAccountRepository> Users,
        Mock<IEmailVerificationRepository> EmailVerifications,
        Mock<IEmailService> EmailService,
        Mock<IClaimService> Claims,
        MemoryCache MemoryCache) : IDisposable
    {
        public void Dispose() => MemoryCache.Dispose();
    }
}
