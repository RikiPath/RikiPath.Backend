using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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

public sealed class EmailRelaySecurityTests
{
    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Learner")]
    [InlineData("Mentor")]
    [InlineData("Admin")]
    public void Generic_email_relay_route_is_not_exposed(string callerKind)
    {
        var routes = typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(GetPostRoutes)
            .ToList();

        Assert.DoesNotContain(
            routes,
            route => string.Equals(route, "api/email/send-validation", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(callerKind));
    }

    [Fact]
    public void Resend_contract_accepts_email_only()
    {
        var properties = typeof(ResendVerificationEmailRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal([nameof(ResendVerificationEmailRequest.Email)], properties);
    }

    [Fact]
    public async Task Resend_uses_account_recipient_and_server_generated_content()
    {
        using var fixture = CreateFixture();
        var user = CreateUser(11, "owner@example.com", isVerified: false);
        fixture.Users.Setup(x => x.GetByEmailAsync("owner@example.com")).ReturnsAsync(user);

        string? recipient = null;
        string? html = null;
        fixture.EmailService
            .Setup(x => x.SendValidationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((string to, string content, CancellationToken _) =>
            {
                recipient = to;
                html = content;
            })
            .ReturnsAsync(EmailSendResult.Success());

        var result = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = " OWNER@EXAMPLE.COM " },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(user.Email, recipient);
        Assert.Contains("Xin chào Owner", html);
        Assert.Matches("<strong>[0-9]{6}</strong>", html!);
        fixture.EmailVerifications.Verify(
            x => x.AddAsync(It.Is<EmailVerification>(verification =>
                verification.UserId == user.Id &&
                verification.VerificationCode.Length == 6 &&
                !verification.IsUsed), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Already_verified_account_keeps_existing_controlled_rule()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("verified@example.com"))
            .ReturnsAsync(CreateUser(12, "verified@example.com", isVerified: true));

        var result = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = "verified@example.com" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        fixture.EmailService.Verify(
            x => x.SendValidationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Unknown_account_keeps_existing_controlled_rule()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("missing@example.com"))
            .ReturnsAsync((UserAccount?)null);

        var result = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = "missing@example.com" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("Không thể gửi email xác thực. Vui lòng kiểm tra lại địa chỉ email.", result.ErrorMessage);
        fixture.EmailService.Verify(
            x => x.SendValidationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Resend_rate_limit_blocks_immediate_second_send()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("owner@example.com"))
            .ReturnsAsync(CreateUser(11, "owner@example.com", isVerified: false));

        var first = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = "owner@example.com" },
            CancellationToken.None);
        var second = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = "owner@example.com" },
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Contains("Vui lòng chờ", second.ErrorMessage);
        fixture.EmailService.Verify(
            x => x.SendValidationEmailAsync("owner@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Resend_does_not_expose_provider_error_details()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("owner@example.com"))
            .ReturnsAsync(CreateUser(11, "owner@example.com", isVerified: false));
        fixture.EmailService
            .Setup(x => x.SendValidationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Fail("SmtpException: provider-internal-secret"));

        var result = await fixture.Service.ResendVerificationEmailAsync(
            new ResendVerificationEmailRequest { Email = "owner@example.com" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.DoesNotContain("SmtpException", result.ErrorMessage);
        Assert.DoesNotContain("provider-internal-secret", result.ErrorMessage);
    }

    [Fact]
    public async Task Register_still_uses_server_generated_verification_email()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("new@example.com"))
            .ReturnsAsync((UserAccount?)null);
        fixture.Users
            .Setup(x => x.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()))
            .Callback((UserAccount user, CancellationToken _) => user.Id = 21)
            .ReturnsAsync((UserAccount user, CancellationToken _) => user);

        string? recipient = null;
        string? html = null;
        fixture.EmailService
            .Setup(x => x.SendValidationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((string to, string content, CancellationToken _) =>
            {
                recipient = to;
                html = content;
            })
            .ReturnsAsync(EmailSendResult.Success());

        var result = await fixture.Service.RegisterAsync(
            new RegisterRequest
            {
                Email = "new@example.com",
                Password = "safe-secret",
                ConfirmPassword = "safe-secret",
                FirstName = "New",
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal("new@example.com", recipient);
        Assert.Contains("Xin chào New", html);
        Assert.Matches("<strong>[0-9]{6}</strong>", html!);
    }

    [Fact]
    public async Task Register_does_not_expose_provider_error_details()
    {
        using var fixture = CreateFixture();
        fixture.Users
            .Setup(x => x.GetByEmailAsync("new@example.com"))
            .ReturnsAsync((UserAccount?)null);
        fixture.Users
            .Setup(x => x.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount user, CancellationToken _) => user);
        fixture.EmailService
            .Setup(x => x.SendValidationEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Fail("SmtpException: provider-internal-secret"));

        var result = await fixture.Service.RegisterAsync(
            new RegisterRequest
            {
                Email = "new@example.com",
                Password = "safe-secret",
                ConfirmPassword = "safe-secret",
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("SmtpException", result.ErrorMessage);
        Assert.DoesNotContain("provider-internal-secret", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_remains_unaffected()
    {
        using var fixture = CreateFixture();
        var user = CreateUser(11, "owner@example.com", isVerified: true, password: "current-secret");
        fixture.Users.Setup(x => x.GetByEmailAsync(user.Email!)).ReturnsAsync(user);

        var result = await fixture.Service.LoginAsync(
            new LoginRequest { Email = user.Email!, Password = "current-secret" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Result!.AccessToken);
        Assert.Equal("11", token.Claims.Single(x => x.Type == ClaimTypes.NameIdentifier).Value);
    }

    [Fact]
    public async Task Email_service_returns_generic_failure_for_smtp_exception()
    {
        var settings = new AppSettings
        {
            SmtpSettings = new SmtpSettings
            {
                Host = "127.0.0.1",
                Port = 1,
                EnableSsl = false,
                Username = "unused",
                Password = "unused",
                FromEmail = "noreply@example.invalid",
                FromName = "RikiPath",
            },
        };
        var service = new EmailService(settings);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await service.SendValidationEmailAsync(
            "nobody@example.invalid",
            "server-generated-content",
            timeout.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("Không thể gửi email qua nhà cung cấp.", result.ErrorMessage);
        Assert.DoesNotContain("SmtpException", result.ErrorMessage);
    }

    private static IEnumerable<string> GetPostRoutes(Type controllerType)
    {
        var controllerTemplate = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
        var controllerName = controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;
        controllerTemplate = controllerTemplate.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);

        foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var attribute in method.GetCustomAttributes<HttpPostAttribute>())
            {
                var route = string.Join(
                    '/',
                    new[] { controllerTemplate, attribute.Template ?? string.Empty }
                        .Where(part => !string.IsNullOrWhiteSpace(part))
                        .Select(part => part.Trim('/')));
                yield return route;
            }
        }
    }

    private static EmailFixture CreateFixture()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var users = new Mock<IUserAccountRepository>();
        var emailVerifications = new Mock<IEmailVerificationRepository>();
        var emailService = new Mock<IEmailService>();
        var firebaseAuth = new Mock<IFirebaseAuthService>();
        var claims = new Mock<IClaimService>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        unitOfWork.SetupGet(x => x.UserAccounts).Returns(users.Object);
        unitOfWork.SetupGet(x => x.EmailVerifications).Returns(emailVerifications.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        emailVerifications
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<EmailVerification, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmailVerification>());
        emailVerifications
            .Setup(x => x.AddAsync(It.IsAny<EmailVerification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerification entity, CancellationToken _) => entity);
        emailService
            .Setup(x => x.SendValidationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailSendResult.Success());

        var settings = new AppSettings
        {
            SecretToken = new SecretToken
            {
                Value = "unit-test-only-jwt-secret-with-more-than-sixty-four-characters-1234567890",
            },
        };
        var service = new AuthService(
            unitOfWork.Object,
            settings,
            emailService.Object,
            firebaseAuth.Object,
            memoryCache,
            claims.Object);

        return new EmailFixture(
            service,
            unitOfWork,
            users,
            emailVerifications,
            emailService,
            memoryCache);
    }

    private static UserAccount CreateUser(
        int id,
        string email,
        bool isVerified,
        string password = "current-secret")
    {
        using var hmac = new HMACSHA512();
        return new UserAccount
        {
            Id = id,
            Email = email,
            FirstName = "Owner",
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password)),
            Role = Role.Learner,
            IsEmailVerified = isVerified,
        };
    }

    private sealed record EmailFixture(
        AuthService Service,
        Mock<IUnitOfWork> UnitOfWork,
        Mock<IUserAccountRepository> Users,
        Mock<IEmailVerificationRepository> EmailVerifications,
        Mock<IEmailService> EmailService,
        MemoryCache MemoryCache) : IDisposable
    {
        public void Dispose() => MemoryCache.Dispose();
    }
}
