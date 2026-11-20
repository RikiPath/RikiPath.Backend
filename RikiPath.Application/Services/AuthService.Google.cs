using System.Net;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RikiPath.Application.Helpers;
using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Auth;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Application.Services;

public partial class AuthService
{
    public async Task<ApiResponse<LoginResponse>> ContinueWithGoogleAsync(
        GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        var token = string.IsNullOrWhiteSpace(request.Credential) ? request.IdToken : request.Credential;
        if (string.IsNullOrWhiteSpace(token))
            return ApiResponse<LoginResponse>.Fail("Token Google là bắt buộc.");

        var clientId = appSettings.GoogleAuth?.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = appSettings.Google?.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            return ApiResponse<LoginResponse>.Fail("Đăng nhập Google chưa được cấu hình.", HttpStatusCode.ServiceUnavailable);

        GoogleJsonWebSignature.Payload identity;
        try
        {
            identity = await GoogleJsonWebSignature.ValidateAsync(token,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] }).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidJwtException or FormatException or ArgumentException or Newtonsoft.Json.JsonException)
        {
            return ApiResponse<LoginResponse>.Unauthorized("Phiên xác thực Google không hợp lệ hoặc đã hết hạn. Hãy chọn Google lại.");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "Could not reach Google for ID token verification.");
            return ApiResponse<LoginResponse>.Fail("Không kết nối được dịch vụ xác thực Google. Hãy thử lại.", HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Could not verify the Google ID token.");
            return ApiResponse<LoginResponse>.Fail("Không thể xác thực với Google.", HttpStatusCode.InternalServerError);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
                return ApiResponse<LoginResponse>.Unauthorized("Không xác định được tài khoản Google.");

            var accounts = unitOfWork.UserAccounts;
            var linkedAccount = await accounts.FirstOrDefaultAsync(
                user => user.GoogleSubject == identity.Subject, cancellationToken);
            if (linkedAccount is not null)
                return GoogleLoginResponse(linkedAccount, identity);

            var email = identity.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !identity.EmailVerified)
                return ApiResponse<LoginResponse>.Fail("Hãy xác thực email trong tài khoản Google rồi thử lại.");

            var matchingAccounts = await accounts.Query()
                .Where(user => user.Email != null && user.Email.ToLower() == email)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (matchingAccounts.Count > 1)
                return ApiResponse<LoginResponse>.Fail(
                    "Email này trùng với nhiều tài khoản RikiPath. Hãy liên hệ hỗ trợ.", HttpStatusCode.Conflict);

            var account = matchingAccounts.SingleOrDefault();
            var created = account is null;
            if (account is not null)
            {
                if (account.IsDeleted || account.GoogleSubject is not null)
                    return ApiResponse<LoginResponse>.Fail(
                        "Không thể liên kết tài khoản Google với email này.", HttpStatusCode.Conflict);

                if (!account.IsEmailVerified || account.PasswordHash is not { Length: > 0 })
                    return ApiResponse<LoginResponse>.Fail(
                        "Hãy hoàn tất xác thực email và đăng nhập bằng phương thức hiện tại của tài khoản RikiPath.",
                        HttpStatusCode.Conflict);

                if (string.IsNullOrEmpty(request.CurrentPassword))
                    return ApiResponse<LoginResponse>.Fail(
                        "Nhập mật khẩu RikiPath hiện tại để liên kết tài khoản Google.",
                        HttpStatusCode.Conflict, "GOOGLE_LINK_PASSWORD_REQUIRED");

                if (!VerifyPasswordHash(request.CurrentPassword, account.PasswordHash, account.PasswordSalt))
                    return ApiResponse<LoginResponse>.Fail(
                        "Mật khẩu RikiPath không đúng.", HttpStatusCode.Unauthorized, "GOOGLE_LINK_PASSWORD_INVALID");

                account.GoogleSubject = identity.Subject;
            }
            else
            {
                if (!string.IsNullOrEmpty(request.CurrentPassword))
                    return ApiResponse<LoginResponse>.Fail(
                        "Không tìm thấy tài khoản RikiPath để liên kết. Hãy chọn Google lại.", HttpStatusCode.Conflict);

                // Google is authoritative for Gmail and managed Workspace email addresses.
                if (!email.EndsWith("@gmail.com", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(identity.HostedDomain))
                    return ApiResponse<LoginResponse>.Fail(
                        "Hãy đăng ký bằng email và xác thực email trước khi liên kết tài khoản Google này.");

                var googleFullName = !string.IsNullOrWhiteSpace(identity.Name)
                    ? identity.Name
                    : UserNameHelper.FormatFullName(identity.GivenName, identity.FamilyName);
                var (firstName, lastName) = UserNameHelper.SplitFullName(googleFullName);

                account = new UserAccount
                {
                    GoogleSubject = identity.Subject,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    IsEmailVerified = true,
                    Role = Role.Learner,
                    PasswordHash = [],
                    PasswordSalt = [],
                };
                await accounts.AddAsync(account, cancellationToken);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // A parallel request can create or link this identity first.
                var savedAccount = await accounts.Query().AsNoTracking().FirstOrDefaultAsync(
                    user => user.GoogleSubject == identity.Subject, cancellationToken);
                if (savedAccount is not null)
                    return GoogleLoginResponse(savedAccount, identity);

                var emailInUse = await accounts.Query().AsNoTracking().AnyAsync(
                    user => user.Email != null && user.Email.ToLower() == email &&
                        (created || user.GoogleSubject != null), cancellationToken);
                if (emailInUse)
                    return ApiResponse<LoginResponse>.Fail(
                        "Thông tin tài khoản vừa thay đổi. Hãy chọn Google lại.", HttpStatusCode.Conflict);

                logger?.LogError(ex, "Failed to save a Google account link.");
                return ApiResponse<LoginResponse>.Fail("Không thể lưu tài khoản Google.", HttpStatusCode.InternalServerError);
            }

            return GoogleLoginResponse(account, identity, created);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Google authentication failed.");
            return ApiResponse<LoginResponse>.Fail("Đăng nhập Google thất bại.", HttpStatusCode.InternalServerError);
        }
    }

    private ApiResponse<LoginResponse> GoogleLoginResponse(
        UserAccount account, GoogleJsonWebSignature.Payload identity, bool created = false)
    {
        if (account.IsDeleted)
            return ApiResponse<LoginResponse>.Fail("Tài khoản RikiPath không còn hoạt động.", HttpStatusCode.Forbidden);

        // A previously linked subject is not sufficient when the local email was changed
        // or is awaiting verification. Never issue a session for that intermediate state.
        if (!identity.EmailVerified || !account.IsEmailVerified ||
            !string.Equals(account.Email?.Trim(), identity.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            return ApiResponse<LoginResponse>.Fail(
                "Thông tin email của tài khoản đã thay đổi. Hãy xác thực email và liên kết lại Google.",
                HttpStatusCode.Forbidden);

        return ApiResponse<LoginResponse>.Success(
            BuildLoginResponse(account, CreateToken(account)),
            created ? HttpStatusCode.Created : HttpStatusCode.OK);
    }
}
