using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RikiPath.Application.EmailTemplates;
using RikiPath.Application.Helpers;
using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Responses;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public partial class AuthService
{
    private const string ResetIssuer = "RikiPath.PasswordReset";
    private const string ResetAudience = "RikiPath.Web";
    private const string ResetPurpose = "password-reset-v1";
    private const int ResetLifetimeMinutes = 30;

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var smtp = appSettings.SmtpSettings;
            if (smtp is null || string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
                string.IsNullOrWhiteSpace(smtp.FromEmail))
            {
                logger?.LogWarning("Password recovery is unavailable: SMTP configuration is incomplete.");
                return ApiResponse.Fail("Tính năng gửi email tạm thời chưa sẵn sàng. Vui lòng thử lại sau.",
                    HttpStatusCode.ServiceUnavailable);
            }
            if (!Uri.TryCreate(appSettings.FrontendBaseUrl?.TrimEnd('/'), UriKind.Absolute, out var frontend) ||
                frontend.Scheme is not ("http" or "https"))
            {
                logger?.LogWarning("Password recovery is unavailable: frontend URL is not configured.");
                return ApiResponse.Fail("Tính năng gửi email tạm thời chưa sẵn sàng. Vui lòng thử lại sau.",
                    HttpStatusCode.ServiceUnavailable);
            }
            if (string.IsNullOrWhiteSpace(appSettings.SecretToken?.Value))
            {
                logger?.LogWarning("Password recovery is unavailable: signing key is not configured.");
                return ApiResponse.Fail("Tính năng gửi email tạm thời chưa sẵn sàng. Vui lòng thử lại sau.",
                    HttpStatusCode.ServiceUnavailable);
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await unitOfWork.UserAccounts.GetByEmailAsync(normalizedEmail);
            if (user is null || user.IsDeleted || !user.IsEmailVerified ||
                user.PasswordHash is not { Length: > 0 })
                return ApiResponse.Success();

            var cooldownKey = $"password-reset-email:{user.Id}";
            var now = DateTime.UtcNow;
            memoryCache.TryGetValue<ResendAttemptState>(cooldownKey, out var state);
            var attempts = state?.Attempts.Where(attempt => attempt > now.AddHours(-1)).ToList() ?? [];
            if (state?.NextAllowedAt > now || attempts.Count >= 5)
                return ApiResponse.Success();

            var token = CreatePasswordResetToken(user);
            var url = $"{frontend.AbsoluteUri.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
            var content = Templates.BuildPasswordResetRequest(new PasswordResetRequestEmailData(
                UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                url,
                ResetLifetimeMinutes));
            var delivery = await emailService.SendEmailAsync(user.Email!, content, cancellationToken);
            if (delivery.IsSuccess)
            {
                attempts.Add(now);
                memoryCache.Set(cooldownKey,
                    new ResendAttemptState(now.AddSeconds(60), attempts), TimeSpan.FromHours(1));
            }
            else
                logger?.LogWarning("Password reset email could not be delivered.");

            return ApiResponse.Success();
        }
        catch (Exception ex)
        {
            logger?.LogError("Password reset request failed with {FailureType}.", ex.GetType().Name);
            return ApiResponse.Success();
        }
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword || request.NewPassword.Length is < 6 or > 128)
            return ApiResponse.Fail("Mật khẩu mới không hợp lệ.");

        ClaimsPrincipal principal;
        try
        {
            if (request.Token.Length > 4096) return ApiResponse.Fail("Liên kết không hợp lệ hoặc đã hết hạn.");
            principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
                request.Token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = ResetSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = ResetIssuer,
                    ValidateAudience = true,
                    ValidAudience = ResetAudience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                }, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or InvalidOperationException)
        {
            return ApiResponse.Fail("Liên kết không hợp lệ hoặc đã hết hạn.");
        }

        if (principal.FindFirst("purpose")?.Value != ResetPurpose ||
            !int.TryParse(principal.FindFirst("uid")?.Value, out var userId) || userId <= 0)
            return ApiResponse.Fail("Liên kết không hợp lệ hoặc đã hết hạn.");

        var policyError = PasswordPolicyHelper.Validate(
            request.NewPassword,
            principal.FindFirst("email")?.Value,
            fieldLabel: "Mật khẩu mới");
        if (policyError is not null)
            return ApiResponse.Fail(policyError);

        var result = ApiResponse.Fail("Liên kết không hợp lệ hoặc đã hết hạn.");
        UserAccount? resetUser = null;
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                // Khóa hàng người dùng: hai request đồng thời không thể dùng cùng liên kết hai lần.
                var user = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                if (user is null || user.IsDeleted || !user.IsEmailVerified ||
                    !string.Equals(user.Email, principal.FindFirst("email")?.Value, StringComparison.OrdinalIgnoreCase) ||
                    !MatchesPasswordFingerprint(user, principal.FindFirst("pwd")?.Value))
                    return;

                var (hash, salt) = CreatePasswordHash(request.NewPassword);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
                user.ModifiedDate = DateTime.UtcNow;
                user.TokenVersion++;
                unitOfWork.UserAccounts.Update(user);
                await DeactivateUserDeviceTokensAsync(user.Id, token);
                await unitOfWork.SaveChangesAsync(token);
                resetUser = user;
                result = ApiResponse.Success();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError("Password reset failed with {FailureType}.", ex.GetType().Name);
            return ApiResponse.Fail("Chưa đặt lại được mật khẩu lúc này.", HttpStatusCode.InternalServerError);
        }

        if (result.IsSuccess && resetUser is not null)
        {
            await SendPasswordSecurityNoticeBestEffortAsync(
                resetUser,
                "Đặt lại mật khẩu",
                "Mật khẩu tài khoản RikiPath của bạn vừa được đặt lại thành công.",
                cancellationToken);
        }

        return result;
    }

    private async Task SendPasswordSecurityNoticeBestEffortAsync(
        UserAccount user,
        string actionTitle,
        string actionDescription,
        CancellationToken cancellationToken)
    {
        if (!user.IsEmailVerified || string.IsNullOrWhiteSpace(user.Email))
            return;

        try
        {
            var content = Templates.BuildPasswordSecurityNotice(new PasswordSecurityNoticeEmailData(
                UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                user.Email,
                DateTime.UtcNow,
                actionTitle,
                actionDescription));
            var delivery = await emailService.SendEmailAsync(user.Email, content, cancellationToken);
            if (!delivery.IsSuccess)
                logger?.LogWarning("Password security notice email could not be delivered.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Password security notice email failed with {FailureType}.", ex.GetType().Name);
        }
    }

    private string CreatePasswordResetToken(UserAccount user)
    {
        var claims = new[]
        {
            new Claim("purpose", ResetPurpose),
            new Claim("uid", user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("email", user.Email!),
            new Claim("pwd", PasswordFingerprint(user)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var jwt = new JwtSecurityToken(
            issuer: ResetIssuer,
            audience: ResetAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(ResetLifetimeMinutes),
            signingCredentials: new SigningCredentials(ResetSigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private SymmetricSecurityKey ResetSigningKey()
    {
        var secret = appSettings.SecretToken?.Value;
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Password reset signing key is not configured.");
        return new SymmetricSecurityKey(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(ResetPurpose)));
    }

    private string PasswordFingerprint(UserAccount user)
    {
        var bytes = new byte[user.PasswordHash.Length + user.PasswordSalt.Length];
        user.PasswordHash.CopyTo(bytes, 0);
        user.PasswordSalt.CopyTo(bytes, user.PasswordHash.Length);
        return Base64UrlEncoder.Encode(HMACSHA256.HashData(ResetSigningKey().Key, bytes));
    }

    private bool MatchesPasswordFingerprint(UserAccount user, string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || user.PasswordHash is not { Length: > 0 } ||
            user.PasswordSalt is not { Length: > 0 }) return false;
        var expected = Encoding.ASCII.GetBytes(PasswordFingerprint(user));
        var actual = Encoding.ASCII.GetBytes(fingerprint);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
