using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Auth;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Auth;
using RikiPath.Domain;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace RikiPath.Application.Services
{
    public class AuthService(
        IUnitOfWork unitOfWork,
        AppSettings appSettings,
        IEmailService emailService,
        IFirebaseAuthService firebaseAuthService,
        IMemoryCache memoryCache) : IAuthService
    {
        private const int VerificationCodeLifetimeMinutes = 30;
        private const int ResendCooldownSeconds = 60;
        private const int MaxResendsPerHour = 5;

        public async Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var existingUser = await unitOfWork.UserAccounts.GetByEmailAsync(request.Email);
                if (existingUser is not null)
                    return ApiResponse<RegisterResponse>.Fail("Email này đã được đăng ký.");

                var (hash, salt) = CreatePasswordHash(request.Password);

                var user = new UserAccount
                {
                    Email = request.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Role = Role.Learner, // ép cứng — tự đăng ký luôn là Learner
                    IsEmailVerified = false,
                };

                await unitOfWork.UserAccounts.AddAsync(user, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var verificationCode = GenerateVerificationCode();
                await unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = verificationCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    IsUsed = false,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var emailContent = $"Xin chào {user.FirstName},<br/>Mã xác thực email của bạn là: " +
                                    $"<strong>{verificationCode}</strong>.<br/>Mã có hiệu lực trong 30 phút.";

                var emailResult = await emailService.SendValidationEmailAsync(user.Email, emailContent, cancellationToken);
                if (!emailResult.IsSuccess)
                    return ApiResponse<RegisterResponse>.Fail(
                        "Tạo tài khoản thành công nhưng gửi email xác thực thất bại: " + emailResult.ErrorMessage);

                return ApiResponse<RegisterResponse>.Created(new RegisterResponse { UserId = user.Id, FullName = $"{user.FirstName} {user.LastName}".Trim(), Email = user.Email });
            }
            catch (Exception ex)
            {
                return ApiResponse<RegisterResponse>.Fail(
                    "Đăng ký thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<RegisterResponse>> RegisterWithFirebasePhoneAsync(FirebasePhoneAuthRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var verified = await firebaseAuthService.VerifyPhoneIdTokenAsync(request.IdToken, cancellationToken);

                var existingUser = await unitOfWork.UserAccounts.FirstOrDefaultAsync(
                    u => u.FirebaseUid == verified.Uid || u.PhoneNumber == verified.PhoneNumber,
                    cancellationToken);

                if (existingUser is not null)
                    return ApiResponse<RegisterResponse>.Fail("Số điện thoại này đã được đăng ký, vui lòng đăng nhập.");

                var user = new UserAccount
                {
                    PhoneNumber = verified.PhoneNumber,
                    FirebaseUid = verified.Uid,
                    IsPhoneVerified = true,
                    Role = Role.Learner,
                    IsEmailVerified = false,
                };

                await unitOfWork.UserAccounts.AddAsync(user, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse<RegisterResponse>.Created(new RegisterResponse
                {
                    UserId = user.Id,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    PhoneNumber = user.PhoneNumber,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<RegisterResponse>.Fail(
                    "Đăng ký bằng số điện thoại thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> ResendVerificationEmailAsync(
            ResendVerificationEmailRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var rateLimitKey = $"email-verification-resend:{normalizedEmail}";

                if (memoryCache.TryGetValue<ResendAttemptState>(rateLimitKey, out var state) && state is not null)
                {
                    if (DateTime.UtcNow < state.NextAllowedAt)
                        return ApiResponse.Fail(
                            $"Vui lòng chờ {Math.Ceiling((state.NextAllowedAt - DateTime.UtcNow).TotalSeconds)} giây trước khi gửi lại.");

                    if (state.Attempts.Count >= MaxResendsPerHour)
                        return ApiResponse.Fail("Bạn đã vượt quá số lần gửi lại email trong 1 giờ. Vui lòng thử lại sau.");
                }

                var user = await unitOfWork.UserAccounts.GetByEmailAsync(normalizedEmail);
                if (user is null)
                    return ApiResponse.Fail("Không thể gửi email xác thực. Vui lòng kiểm tra lại địa chỉ email.");

                if (user.IsEmailVerified)
                    return ApiResponse.Fail("Email này đã được xác thực.");

                var pendingVerifications = await unitOfWork.EmailVerifications.FindAsync(
                    x => x.UserId == user.Id && !x.IsUsed,
                    cancellationToken);
                foreach (var verification in pendingVerifications)
                    verification.IsUsed = true;

                if (pendingVerifications.Count > 0)
                    unitOfWork.EmailVerifications.UpdateRange(pendingVerifications);

                var verificationCode = GenerateVerificationCode();
                await unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = verificationCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var now = DateTime.UtcNow;
                var attempts = state?.Attempts
                    .Where(attempt => attempt > now.AddHours(-1))
                    .ToList() ?? [];
                attempts.Add(now);
                memoryCache.Set(
                    rateLimitKey,
                    new ResendAttemptState(now.AddSeconds(ResendCooldownSeconds), attempts),
                    TimeSpan.FromHours(1));

                var emailContent = $"Xin chào {user.FirstName},<br/>Mã xác thực email của bạn là: " +
                                   $"<strong>{verificationCode}</strong>.<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";
                var emailResult = await emailService.SendValidationEmailAsync(
                    user.Email!,
                    emailContent,
                    cancellationToken);

                if (!emailResult.IsSuccess)
                    return ApiResponse.Fail(
                        "Không thể gửi email xác thực: " + emailResult.ErrorMessage,
                        System.Net.HttpStatusCode.BadRequest);

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Gửi lại email xác thực thất bại.",
                    System.Net.HttpStatusCode.InternalServerError,
                    errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var record = await unitOfWork.EmailVerifications.GetValidCodeAsync(request.UserId, request.VerificationCode);
                if (record is null)
                    return ApiResponse.Fail("Mã xác thực không hợp lệ hoặc đã được sử dụng.");

                if (record.ExpiresAt < DateTime.UtcNow)
                    return ApiResponse.Fail("Mã xác thực đã hết hạn.");

                var user = await unitOfWork.UserAccounts.GetByIdAsync(request.UserId, cancellationToken);
                if (user is null)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                record.IsUsed = true;
                unitOfWork.EmailVerifications.Update(record);

                user.IsEmailVerified = true;
                unitOfWork.UserAccounts.Update(user);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Xác thực email thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private sealed record ResendAttemptState(DateTime NextAllowedAt, List<DateTime> Attempts);

        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var user = await unitOfWork.UserAccounts.GetByEmailAsync(request.Email);
                if (user is null || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
                    return ApiResponse<LoginResponse>.Fail("Email hoặc mật khẩu không đúng.");

                if (!user.IsEmailVerified)
                    return ApiResponse<LoginResponse>.Fail("Vui lòng xác thực email trước khi đăng nhập.");

                var response = new LoginResponse
                {
                    AccessToken = CreateToken(user),
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Role = user.Role.ToString(),
                };

                return ApiResponse<LoginResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponse>.Fail(
                    "Đăng nhập thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<LoginResponse>> LoginWithFirebasePhoneAsync(FirebasePhoneAuthRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var verified = await firebaseAuthService.VerifyPhoneIdTokenAsync(request.IdToken, cancellationToken);

                var user = await unitOfWork.UserAccounts.FirstOrDefaultAsync(
                    u => u.FirebaseUid == verified.Uid || u.PhoneNumber == verified.PhoneNumber,
                    cancellationToken);

                if (user is null)
                    return ApiResponse<LoginResponse>.Fail("Số điện thoại chưa được đăng ký, vui lòng đăng ký trước khi đăng nhập.");

                // Đồng bộ lại FirebaseUid phòng trường hợp user đổi thiết bị/app re-verify OTP lần nữa.
                user.FirebaseUid = verified.Uid;
                unitOfWork.UserAccounts.Update(user);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var response = new LoginResponse
                {
                    AccessToken = CreateToken(user),
                    UserId = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Role = user.Role.ToString(),
                };

                return ApiResponse<LoginResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponse>.Fail(
                    "Đăng nhập bằng số điện thoại thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> UpdateEmailAsync(int userId, UpdateEmailRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                if (await unitOfWork.UserAccounts.GetByEmailAsync(request.NewEmail) is not null)
                    return ApiResponse.Fail("Email mới đã được sử dụng bởi tài khoản khác.");

                user.Email = request.NewEmail;
                user.IsEmailVerified = false;
                unitOfWork.UserAccounts.Update(user);

                var verificationCode = GenerateVerificationCode();
                await unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = verificationCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    IsUsed = false,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var emailContent = $"Mã xác thực email mới của bạn là: <strong>{verificationCode}</strong>." +
                                    "<br/>Mã có hiệu lực trong 30 phút.";
                var emailResult = await emailService.SendValidationEmailAsync(user.Email, emailContent, cancellationToken);
                if (!emailResult.IsSuccess)
                    return ApiResponse.Fail("Đã cập nhật email nhưng gửi mã xác thực thất bại: " + emailResult.ErrorMessage);

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Cập nhật email thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                if (!VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                    return ApiResponse.Fail("Mật khẩu hiện tại không đúng.");

                var (hash, salt) = CreatePasswordHash(request.NewPassword);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
                unitOfWork.UserAccounts.Update(user);

                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Đổi mật khẩu thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        // ---------- Private helpers ----------

        private string CreateToken(UserAccount user)
        {
            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.ToString()),
            ];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.SecretToken.Value));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static (byte[] Hash, byte[] Salt) CreatePasswordHash(string password)
        {
            using var hmac = new HMACSHA512();
            var salt = hmac.Key;
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return (hash, salt);
        }

        private static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using var hmac = new HMACSHA512(passwordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return computedHash.SequenceEqual(passwordHash);
        }

        private static string GenerateVerificationCode() => Random.Shared.Next(100000, 999999).ToString();

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}