using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using RikiPath.Application.IRepositories;
using RikiPath.Application.Helpers;
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
using Microsoft.Extensions.Logging;

namespace RikiPath.Application.Services
{
    public partial class AuthService(
        IUnitOfWork unitOfWork,
        AppSettings appSettings,
        IEmailService emailService,
        IFirebaseAuthService firebaseAuthService,
        IMemoryCache memoryCache,
        IClaimService claimService,
        ILogger<AuthService>? logger = null) : IAuthService
    {
        private int VerificationCodeLifetimeMinutes => Math.Clamp(appSettings.EmailVerification?.CodeLifetimeMinutes ?? 30, 5, 60);
        private int ResendCooldownSeconds => Math.Clamp(appSettings.EmailVerification?.ResendCooldownSeconds ?? 60, 30, 300);
        private int MaxResendsPerHour => Math.Clamp(appSettings.EmailVerification?.MaxResendsPerHour ?? 5, 1, 20);
        private const string RegistrationVerificationPurpose = "registration-email-v1";
        private const string ChangeEmailVerificationPurpose = "change-email-v1";
        // Fixed-size lock stripes avoid an unbounded per-address lock dictionary. The
        // A distributed limiter is still required for multi-instance deployments.
        private static readonly SemaphoreSlim[] ResendLocks = Enumerable.Range(0, 256)
            .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

        public async Task<ApiResponse<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var email = request.Email.Trim().ToLowerInvariant();
                var existingUser = await unitOfWork.UserAccounts.GetByEmailAsync(email);
                if (existingUser is not null)
                    return ApiResponse<RegisterResponse>.Fail("Email này đã được đăng ký.");

                var policyError = PasswordPolicyHelper.Validate(request.Password, email, request.FullName, "Mật khẩu");
                if (policyError is not null)
                    return ApiResponse<RegisterResponse>.Fail(policyError);

                var (hash, salt) = CreatePasswordHash(request.Password);
                var (firstName, lastName) = UserNameHelper.SplitFullName(request.FullName);
                var fullName = UserNameHelper.FormatFullName(firstName, lastName);

                var user = new UserAccount
                {
                    Email = email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FirstName = firstName,
                    LastName = lastName,
                    Role = Role.Learner, // ép cứng — tự đăng ký luôn là Learner
                    IsEmailVerified = false,
                };

                await unitOfWork.UserAccounts.AddAsync(user, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var verificationCode = GenerateVerificationCode();
                await unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = ProtectVerificationCode(user.Id, verificationCode, RegistrationVerificationPurpose),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var greetingName = System.Net.WebUtility.HtmlEncode(string.IsNullOrEmpty(fullName) ? "bạn" : fullName);
                var emailContent = $"Xin chào {greetingName},<br/>Mã xác thực email của bạn là: " +
                                    $"<strong>{verificationCode}</strong>.<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";

                var emailResult = await emailService.SendValidationEmailAsync(user.Email, emailContent, cancellationToken);
                if (!emailResult.IsSuccess)
                    return ApiResponse<RegisterResponse>.Created(new RegisterResponse
                    {
                        UserId = user.Id,
                        FullName = fullName,
                        Email = user.Email,
                        VerificationEmailSent = false,
                    });

                return ApiResponse<RegisterResponse>.Created(new RegisterResponse
                {
                    UserId = user.Id,
                    FullName = fullName,
                    Email = user.Email,
                    VerificationEmailSent = true,
                });
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
                    FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
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
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var resendLock = ResendLocks[(StringComparer.Ordinal.GetHashCode(normalizedEmail) & int.MaxValue) % ResendLocks.Length];
            await resendLock.WaitAsync(cancellationToken);
            try
            {
                var rateLimitKey = $"email-verification-resend:{normalizedEmail}";
                var now = DateTime.UtcNow;
                memoryCache.TryGetValue<ResendAttemptState>(rateLimitKey, out var state);
                var attempts = state?.Attempts.Where(attempt => attempt > now.AddHours(-1)).ToList() ?? [];

                if (state is not null)
                {
                    if (now < state.NextAllowedAt)
                        return ApiResponse.Fail(
                            $"Vui lòng chờ {Math.Ceiling((state.NextAllowedAt - now).TotalSeconds)} giây trước khi gửi lại.",
                            System.Net.HttpStatusCode.TooManyRequests);

                    if (attempts.Count >= MaxResendsPerHour)
                        return ApiResponse.Fail("Bạn đã vượt quá số lần gửi lại email trong 1 giờ. Vui lòng thử lại sau.",
                            System.Net.HttpStatusCode.TooManyRequests);
                }

                // Reserve the attempt before checking account state or contacting SMTP.
                // Unknown, verified and unverified addresses have the same public quota.
                attempts.Add(now);
                memoryCache.Set(rateLimitKey,
                    new ResendAttemptState(now.AddSeconds(ResendCooldownSeconds), attempts),
                    TimeSpan.FromHours(1));

                var user = await unitOfWork.UserAccounts.GetByEmailAsync(normalizedEmail);
                // Public callers must not be able to probe account existence or verification state.
                // In either case no code is created and no email is sent.
                if (user is null || user.IsDeleted || user.IsEmailVerified)
                    return ApiResponse.SuccessWithRetryAfter(ResendCooldownSeconds);

                var verificationCode = GenerateVerificationCode();
                var newVerification = new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = ProtectVerificationCode(user.Id, verificationCode, RegistrationVerificationPurpose),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                };
                await unitOfWork.EmailVerifications.AddAsync(newVerification, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var greetingName = System.Net.WebUtility.HtmlEncode(UserNameHelper.FormatFullName(user.FirstName, user.LastName));
                var emailContent = $"Xin chào {(string.IsNullOrEmpty(greetingName) ? "bạn" : greetingName)},<br/>Mã xác thực email của bạn là: " +
                                   $"<strong>{verificationCode}</strong>.<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";
                var emailResult = await emailService.SendValidationEmailAsync(
                    user.Email!,
                    emailContent,
                    cancellationToken);

                if (!emailResult.IsSuccess)
                {
                    // Không làm mất mã cũ khi nhà cung cấp chưa nhận email.
                    newVerification.IsUsed = true;
                    unitOfWork.EmailVerifications.Update(newVerification);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    logger?.LogWarning("Registration verification email could not be delivered.");
                    return ApiResponse.SuccessWithRetryAfter(ResendCooldownSeconds);
                }

                var pendingVerifications = await unitOfWork.EmailVerifications.FindAsync(
                    x => x.UserId == user.Id && !x.IsUsed && x.Id != newVerification.Id,
                    cancellationToken);
                foreach (var verification in pendingVerifications)
                    verification.IsUsed = true;
                if (pendingVerifications.Count > 0)
                    unitOfWork.EmailVerifications.UpdateRange(pendingVerifications);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse.SuccessWithRetryAfter(ResendCooldownSeconds);
            }
            catch (Exception ex)
            {
                logger?.LogError("Verification resend failed with {FailureType}.", ex.GetType().Name);
                return ApiResponse.Fail(
                    "Gửi lại email xác thực thất bại.",
                    System.Net.HttpStatusCode.InternalServerError);
            }
            finally
            {
                resendLock.Release();
            }
        }

        public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var normalizedEmail = request.Email?.Trim().ToLowerInvariant();
                var user = request.UserId is > 0
                    ? await unitOfWork.UserAccounts.GetByIdAsync(request.UserId.Value, cancellationToken)
                    : !string.IsNullOrWhiteSpace(normalizedEmail)
                        ? await unitOfWork.UserAccounts.FirstOrDefaultAsync(
                            u => !u.IsDeleted &&
                                 ((u.Email != null && u.Email.ToLower() == normalizedEmail) ||
                                  (u.PendingEmail != null && u.PendingEmail.ToLower() == normalizedEmail)),
                            cancellationToken)
                        : null;
                if (user is null || user.IsDeleted ||
                    (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                     !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(user.PendingEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                    return ApiResponse.Fail("Mã xác thực không hợp lệ hoặc đã được sử dụng.");

                var verifyAttemptKey = $"verify-email-fail:{user.Id}";
                if (memoryCache.TryGetValue<int>(verifyAttemptKey, out var failCount) && failCount >= 5)
                {
                    logger?.LogWarning("Email verification temporarily locked for userId={UserId} after repeated invalid codes.", user.Id);
                    return ApiResponse.Fail(
                        "Bạn đã nhập sai mã xác thực quá nhiều lần. Vui lòng thử lại sau 15 phút.",
                        System.Net.HttpStatusCode.TooManyRequests);
                }

                ApiResponse? outcome = null;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    // Serialize all verification attempts for this account across API instances.
                    var lockedUser = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(user.Id, token);
                    if (lockedUser is null || lockedUser.IsDeleted ||
                        (!string.IsNullOrWhiteSpace(normalizedEmail) &&
                         !string.Equals(lockedUser.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(lockedUser.PendingEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                    {
                        outcome = ApiResponse.Fail("Mã xác thực không hợp lệ hoặc đã được sử dụng.");
                        return;
                    }

                    var pendingEmailMatch = !string.IsNullOrWhiteSpace(lockedUser.PendingEmail) &&
                        (string.IsNullOrWhiteSpace(normalizedEmail) ||
                         string.Equals(lockedUser.PendingEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));
                    if (lockedUser.IsEmailVerified && !pendingEmailMatch)
                    {
                        outcome = ApiResponse.Fail("Mã xác thực không hợp lệ hoặc đã được sử dụng.");
                        return;
                    }

                    // The purpose is part of the stored HMAC. Legacy plaintext codes
                    // remain valid only until their original expiration time.
                    var primaryPurpose = pendingEmailMatch ? ChangeEmailVerificationPurpose : RegistrationVerificationPurpose;
                    var record = await unitOfWork.EmailVerifications.GetValidCodeAsync(
                        lockedUser.Id, ProtectVerificationCode(lockedUser.Id, request.VerificationCode, primaryPurpose));
                    if (record is null && !pendingEmailMatch)
                        record = await unitOfWork.EmailVerifications.GetValidCodeAsync(
                            lockedUser.Id, ProtectVerificationCode(lockedUser.Id, request.VerificationCode, ChangeEmailVerificationPurpose));
                    if (record is null)
                        record = await unitOfWork.EmailVerifications.GetValidCodeAsync(lockedUser.Id, request.VerificationCode);
                    if (record is null)
                    {
                        memoryCache.Set(verifyAttemptKey, failCount + 1, TimeSpan.FromMinutes(15));
                        outcome = ApiResponse.Fail("Mã xác thực không hợp lệ hoặc đã được sử dụng.");
                        return;
                    }
                    if (record.ExpiresAt < DateTime.UtcNow)
                    {
                        outcome = ApiResponse.Fail("Mã xác thực đã hết hạn.");
                        return;
                    }

                    var pendingNormalized = lockedUser.PendingEmail?.Trim().ToLowerInvariant();
                    if (pendingEmailMatch && pendingNormalized is not null)
                    {
                        var emailTaken = await unitOfWork.UserAccounts.AnyAsync(
                            u => u.Id != lockedUser.Id && !u.IsDeleted && u.Email != null && u.Email.ToLower() == pendingNormalized,
                            token);
                        if (emailTaken)
                        {
                            outcome = ApiResponse.Fail("Email mới đã được sử dụng bởi tài khoản khác.");
                            return;
                        }

                        var previousEmail = lockedUser.Email;
                        lockedUser.Email = pendingNormalized;
                        lockedUser.PendingEmail = null;
                        lockedUser.PendingEmailRequestedAtUtc = null;
                        lockedUser.IsEmailVerified = true;
                        lockedUser.ModifiedDate = DateTime.UtcNow;
                        lockedUser.TokenVersion++;
                        await DeactivateUserDeviceTokensAsync(lockedUser.Id, token);

                        await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                        {
                            ActorUserId = lockedUser.Id,
                            EntityType = "UserAccount",
                            EntityId = lockedUser.Id,
                            Action = "EmailChangedVerified",
                            BeforeJson = System.Text.Json.JsonSerializer.Serialize(new { Email = previousEmail }),
                            AfterJson = System.Text.Json.JsonSerializer.Serialize(new { Email = pendingNormalized }),
                            CreatedAtUtc = DateTime.UtcNow,
                        }, token);
                    }
                    else
                    {
                        lockedUser.IsEmailVerified = true;
                        lockedUser.ModifiedDate = DateTime.UtcNow;
                    }

                    record.IsUsed = true;
                    unitOfWork.EmailVerifications.Update(record);
                    unitOfWork.UserAccounts.Update(lockedUser);
                    await unitOfWork.SaveChangesAsync(token);
                    memoryCache.Remove(verifyAttemptKey);
                    outcome = ApiResponse.Success();
                }, cancellationToken);

                return outcome ?? ApiResponse.Fail("Xác thực email thất bại.", System.Net.HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                logger?.LogError("Email verification failed with {FailureType}.", ex.GetType().Name);
                return ApiResponse.Fail(
                    "Xác thực email thất bại.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        private sealed record ResendAttemptState(DateTime NextAllowedAt, List<DateTime> Attempts);
        private sealed record LoginFailureState(int Count, DateTime? LockedUntilUtc);

        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
                var lockKey = $"login-lock:{normalizedEmail}";
                if (memoryCache.TryGetValue<LoginFailureState>(lockKey, out var lockState)
                    && lockState?.LockedUntilUtc is DateTime lockedUntil
                    && lockedUntil > DateTime.UtcNow)
                {
                    logger?.LogWarning("Locked login attempt rejected cleanly for account.");
                    return ApiResponse<LoginResponse>.Fail(
                        "Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 5 phút.",
                        System.Net.HttpStatusCode.TooManyRequests);
                }

                var user = await unitOfWork.UserAccounts.GetByEmailAsync(request.Email ?? string.Empty);
                if (user is null || user.IsDeleted || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
                {
                    var nextCount = (lockState?.Count ?? 0) + 1;
                    DateTime? lockExpiry = nextCount >= 5 ? DateTime.UtcNow.AddMinutes(5) : null;
                    memoryCache.Set(lockKey, new LoginFailureState(nextCount, lockExpiry), TimeSpan.FromMinutes(10));
                    logger?.LogWarning("Failed login attempt count={AttemptCount}.", nextCount);
                    return ApiResponse<LoginResponse>.Fail("Email hoặc mật khẩu không đúng.");
                }

                memoryCache.Remove(lockKey);

                if (!user.IsEmailVerified)
                    return ApiResponse<LoginResponse>.Fail("Vui lòng xác thực email trước khi đăng nhập.");

                var response = BuildLoginResponse(user, CreateToken(user));

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

                var response = BuildLoginResponse(user, CreateToken(user));

                return ApiResponse<LoginResponse>.Success(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponse>.Fail(
                    "Đăng nhập bằng số điện thoại thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<RegisterResponse>> ChangeUnverifiedEmailAsync(
            ChangeUnverifiedEmailRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var currentEmail = request.CurrentEmail.Trim().ToLowerInvariant();
                var newEmail = request.NewEmail.Trim().ToLowerInvariant();
                var failureKey = $"change-unverified-email-fail:{currentEmail}";
                var failures = memoryCache.Get<int>(failureKey);
                if (failures >= 5)
                    return ApiResponse<RegisterResponse>.Fail(
                        "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau 15 phút.",
                        System.Net.HttpStatusCode.TooManyRequests);

                var user = await unitOfWork.UserAccounts.GetByEmailAsync(currentEmail);
                if (user is null || user.IsDeleted || user.IsEmailVerified ||
                    !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
                {
                    memoryCache.Set(failureKey, failures + 1, TimeSpan.FromMinutes(15));
                    return ApiResponse<RegisterResponse>.Fail("Không thể đổi email. Vui lòng kiểm tra lại thông tin.");
                }

                if (newEmail == currentEmail)
                    return ApiResponse<RegisterResponse>.Fail("Email mới cần khác email hiện tại.");
                if (await unitOfWork.UserAccounts.GetByEmailAsync(newEmail) is not null)
                    return ApiResponse<RegisterResponse>.Fail("Email mới đã được sử dụng bởi tài khoản khác.");

                var oldCodes = await unitOfWork.EmailVerifications.FindAsync(
                    code => code.UserId == user.Id && !code.IsUsed, cancellationToken);
                foreach (var oldCode in oldCodes)
                    oldCode.IsUsed = true;
                if (oldCodes.Count > 0)
                    unitOfWork.EmailVerifications.UpdateRange(oldCodes);

                user.Email = newEmail;
                unitOfWork.UserAccounts.Update(user);
                var verificationCode = GenerateVerificationCode();
                await unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = ProtectVerificationCode(user.Id, verificationCode, RegistrationVerificationPurpose),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                }, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                memoryCache.Remove(failureKey);

                var emailContent = $"Mã xác thực email của bạn là: <strong>{verificationCode}</strong>." +
                    $"<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";
                var sendResult = await emailService.SendValidationEmailAsync(newEmail, emailContent, cancellationToken);
                return ApiResponse<RegisterResponse>.Success(new RegisterResponse
                {
                    UserId = user.Id,
                    Email = newEmail,
                    FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                    VerificationEmailSent = sendResult.IsSuccess,
                });
            }
            catch (Exception ex)
            {
                logger?.LogError("Changing an unverified email failed with {FailureType}.", ex.GetType().Name);
                return ApiResponse<RegisterResponse>.Fail(
                    "Không thể đổi email lúc này.", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<LoginResponse>> GetMeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                var user = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse<LoginResponse>.NotFound("Không tìm thấy tài khoản.");

                return ApiResponse<LoginResponse>.Success(BuildLoginResponse(user, string.Empty));
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return ApiResponse<LoginResponse>.Unauthorized("Không xác định được tài khoản hiện tại từ JWT.");
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponse>.Fail(
                    "Không thể tải thông tin tài khoản.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> UpdateEmailAsync(int userId, UpdateEmailRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // User thường chỉ được cập nhật email của chính mình.
                // Admin được phép cập nhật email tài khoản khác theo nghiệp vụ quản trị.
                var ownershipError = ValidateAccountOwnership(userId, allowAdminEmailOverride: true);
                if (ownershipError is not null)
                    return ownershipError;

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                var actor = claimService.GetUserClaim();
                var isAdminOverride = actor.Role == Role.Admin && actor.Id != user.Id;
                var previousEmail = user.Email ?? string.Empty;
                var newEmail = request.NewEmail.Trim().ToLowerInvariant();

                if (string.Equals(newEmail, previousEmail, StringComparison.OrdinalIgnoreCase))
                    return ApiResponse.Fail("Email mới cần khác với email hiện tại.");

                if (!isAdminOverride)
                {
                    if (user.PasswordHash is not { Length: 64 })
                        return ApiResponse.Fail("Tài khoản đăng nhập Google chưa có mật khẩu nội bộ. Vui lòng thiết lập mật khẩu trước khi đổi email.");

                    var attemptKey = $"email-pwd-fail:{user.Id}";
                    var failures = memoryCache.Get<int>(attemptKey);
                    if (failures >= 5)
                    {
                        return ApiResponse.Fail(
                            "Bạn đã nhập sai mật khẩu quá nhiều lần. Vui lòng thử lại sau 15 phút.",
                            System.Net.HttpStatusCode.TooManyRequests);
                    }

                    if (string.IsNullOrEmpty(request.CurrentPassword) ||
                        !VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                    {
                        memoryCache.Set(attemptKey, failures + 1, TimeSpan.FromMinutes(15));
                        return ApiResponse.Fail("Mật khẩu hiện tại không đúng.");
                    }

                    memoryCache.Remove(attemptKey);
                }

                var emailInUse = await unitOfWork.UserAccounts.AnyAsync(
                    u => u.Id != user.Id && !u.IsDeleted &&
                         ((u.Email != null && u.Email.ToLower() == newEmail) ||
                          (u.PendingEmail != null && u.PendingEmail.ToLower() == newEmail)),
                    cancellationToken);
                if (emailInUse)
                    return ApiResponse.Fail("Email mới đã được sử dụng bởi tài khoản khác.");

                var oldCodes = await unitOfWork.EmailVerifications.FindAsync(
                    code => code.UserId == user.Id && !code.IsUsed, cancellationToken);
                foreach (var oldCode in oldCodes)
                    oldCode.IsUsed = true;
                if (oldCodes.Count > 0)
                    unitOfWork.EmailVerifications.UpdateRange(oldCodes);

                var previousPendingEmail = user.PendingEmail;
                var previousPendingRequestedAt = user.PendingEmailRequestedAtUtc;

                var verificationCode = GenerateVerificationCode();
                var verificationRecord = new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = ProtectVerificationCode(user.Id, verificationCode, ChangeEmailVerificationPurpose),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                };
                await unitOfWork.EmailVerifications.AddAsync(verificationRecord, cancellationToken);

                if (isAdminOverride)
                {
                    user.Email = newEmail;
                    user.PendingEmail = null;
                    user.PendingEmailRequestedAtUtc = null;
                    user.IsEmailVerified = false;
                    user.ModifiedDate = DateTime.UtcNow;
                    user.TokenVersion++;
                    await DeactivateUserDeviceTokensAsync(user.Id, cancellationToken);
                    unitOfWork.UserAccounts.Update(user);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = actor.Id,
                        EntityType = "UserAccount",
                        EntityId = user.Id,
                        Action = "EmailUpdatedByAdmin",
                        BeforeJson = System.Text.Json.JsonSerializer.Serialize(new { Email = previousEmail }),
                        AfterJson = System.Text.Json.JsonSerializer.Serialize(new { Email = newEmail }),
                        CreatedAtUtc = DateTime.UtcNow,
                    }, cancellationToken);

                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    // Safe email change: keep current user.Email active until new email OTP is confirmed.
                    user.PendingEmail = newEmail;
                    user.PendingEmailRequestedAtUtc = DateTime.UtcNow;
                    user.ModifiedDate = DateTime.UtcNow;
                    unitOfWork.UserAccounts.Update(user);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = actor.Id,
                        EntityType = "UserAccount",
                        EntityId = user.Id,
                        Action = "EmailChangeRequested",
                        BeforeJson = System.Text.Json.JsonSerializer.Serialize(new { Email = previousEmail }),
                        AfterJson = System.Text.Json.JsonSerializer.Serialize(new { PendingEmail = newEmail }),
                        CreatedAtUtc = DateTime.UtcNow,
                    }, cancellationToken);

                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                var emailContent = $"Mã xác thực email mới của bạn là: <strong>{verificationCode}</strong>." +
                                    $"<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";
                var emailResult = await emailService.SendValidationEmailAsync(newEmail, emailContent, cancellationToken);
                if (!emailResult.IsSuccess)
                {
                    if (!isAdminOverride)
                    {
                        user.PendingEmail = previousPendingEmail;
                        user.PendingEmailRequestedAtUtc = previousPendingRequestedAt;
                        verificationRecord.IsUsed = true;
                        unitOfWork.UserAccounts.Update(user);
                        unitOfWork.EmailVerifications.Update(verificationRecord);
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    return ApiResponse.Fail("Không thể gửi mã xác thực tới email mới. Vui lòng thử lại.");
                }

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Cập nhật email thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> ResendPendingEmailAsync(CancellationToken cancellationToken)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                var user = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                var pendingEmail = user.PendingEmail?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(pendingEmail))
                    return ApiResponse.Fail("Không có yêu cầu đổi email đang chờ xác thực.");

                var cooldownKey = $"resend-pending-email:{user.Id}";
                if (memoryCache.TryGetValue<DateTime>(cooldownKey, out var allowedAt) && allowedAt > DateTime.UtcNow)
                    return ApiResponse.Fail(
                        $"Vui lòng chờ {Math.Ceiling((allowedAt - DateTime.UtcNow).TotalSeconds)} giây trước khi gửi lại.",
                        System.Net.HttpStatusCode.TooManyRequests);

                memoryCache.Set(cooldownKey, DateTime.UtcNow.AddSeconds(ResendCooldownSeconds), TimeSpan.FromHours(1));

                var oldCodes = await unitOfWork.EmailVerifications.FindAsync(
                    code => code.UserId == user.Id && !code.IsUsed, cancellationToken);

                var verificationCode = GenerateVerificationCode();
                var newCode = new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = ProtectVerificationCode(user.Id, verificationCode, ChangeEmailVerificationPurpose),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationCodeLifetimeMinutes),
                    IsUsed = false,
                };
                await unitOfWork.EmailVerifications.AddAsync(newCode, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var emailContent = $"Mã xác thực email mới của bạn là: <strong>{verificationCode}</strong>." +
                                    $"<br/>Mã có hiệu lực trong {VerificationCodeLifetimeMinutes} phút.";
                var emailResult = await emailService.SendValidationEmailAsync(pendingEmail, emailContent, cancellationToken);
                if (!emailResult.IsSuccess)
                {
                    newCode.IsUsed = true;
                    unitOfWork.EmailVerifications.Update(newCode);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return ApiResponse.Fail("Gửi lại mã xác thực thất bại.");
                }

                foreach (var oldCode in oldCodes)
                    oldCode.IsUsed = true;
                if (oldCodes.Count > 0)
                    unitOfWork.EmailVerifications.UpdateRange(oldCodes);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponse.SuccessWithRetryAfter(ResendCooldownSeconds);
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Gửi lại mã xác thực thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Mật khẩu chỉ được thay đổi bởi chính chủ tài khoản.
                // Admin không được phép đổi mật khẩu của người dùng khác.
                var ownershipError = ValidateAccountOwnership(userId, allowAdminEmailOverride: false);
                if (ownershipError is not null)
                    return ownershipError;

                var user = await unitOfWork.UserAccounts.GetByIdAsync(userId, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                if (user.PasswordHash is not { Length: 64 })
                    return ApiResponse.Fail("Tài khoản này chưa có mật khẩu nội bộ. Hãy dùng chức năng Tạo mật khẩu.");

                var attemptKey = $"change-pwd-fail:{user.Id}";
                var failures = memoryCache.Get<int>(attemptKey);
                if (failures >= 5)
                    return ApiResponse.Fail(
                        "Bạn đã nhập sai mật khẩu hiện tại quá nhiều lần. Vui lòng thử lại sau 15 phút.",
                        System.Net.HttpStatusCode.TooManyRequests);

                if (!VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                {
                    memoryCache.Set(attemptKey, failures + 1, TimeSpan.FromMinutes(15));
                    return ApiResponse.Fail("Mật khẩu hiện tại không đúng.");
                }

                if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
                    return ApiResponse.Fail("Mật khẩu mới phải khác mật khẩu hiện tại.");

                var policyError = PasswordPolicyHelper.Validate(
                    request.NewPassword,
                    user.Email,
                    UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                    "Mật khẩu mới");
                if (policyError is not null)
                    return ApiResponse.Fail(policyError);

                memoryCache.Remove(attemptKey);

                var staleOrMissing = false;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var lockedUser = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(userId, token);
                    if (lockedUser is null || lockedUser.IsDeleted ||
                        !VerifyPasswordHash(request.CurrentPassword, lockedUser.PasswordHash, lockedUser.PasswordSalt))
                    {
                        staleOrMissing = true;
                        return;
                    }

                    var (hash, salt) = CreatePasswordHash(request.NewPassword);
                    lockedUser.PasswordHash = hash;
                    lockedUser.PasswordSalt = salt;
                    lockedUser.ModifiedDate = DateTime.UtcNow;
                    lockedUser.TokenVersion++;
                    unitOfWork.UserAccounts.Update(lockedUser);
                    await DeactivateUserDeviceTokensAsync(lockedUser.Id, token);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = lockedUser.Id,
                        EntityType = "UserAccount",
                        EntityId = lockedUser.Id,
                        Action = "PasswordChangedByOwner",
                        CreatedAtUtc = DateTime.UtcNow,
                    }, token);

                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (staleOrMissing)
                    return ApiResponse.Fail("Mật khẩu hiện tại không đúng hoặc vừa được thay đổi.", System.Net.HttpStatusCode.Conflict);

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Đổi mật khẩu thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var claim = claimService.GetUserClaim();
                var user = await unitOfWork.UserAccounts.GetByIdAsync(claim.Id, cancellationToken);
                if (user is null || user.IsDeleted)
                    return ApiResponse.NotFound("Không tìm thấy người dùng.");

                if (user.PasswordHash is { Length: 64 })
                    return ApiResponse.Fail("Tài khoản đã có mật khẩu nội bộ. Vui lòng dùng chức năng Đổi mật khẩu.");

                var policyError = PasswordPolicyHelper.Validate(
                    request.NewPassword,
                    user.Email,
                    UserNameHelper.FormatFullName(user.FirstName, user.LastName),
                    "Mật khẩu mới");
                if (policyError is not null)
                    return ApiResponse.Fail(policyError);

                var concurrentConflict = false;
                await unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    var lockedUser = await unitOfWork.UserAccounts.GetByIdForUpdateAsync(claim.Id, token);
                    if (lockedUser is null || lockedUser.IsDeleted || lockedUser.PasswordHash is { Length: 64 })
                    {
                        concurrentConflict = true;
                        return;
                    }

                    var (hash, salt) = CreatePasswordHash(request.NewPassword);
                    lockedUser.PasswordHash = hash;
                    lockedUser.PasswordSalt = salt;
                    lockedUser.ModifiedDate = DateTime.UtcNow;
                    lockedUser.TokenVersion++;
                    unitOfWork.UserAccounts.Update(lockedUser);
                    await DeactivateUserDeviceTokensAsync(lockedUser.Id, token);

                    await unitOfWork.AdminAuditLogs.AddAsync(new AdminAuditLog
                    {
                        ActorUserId = lockedUser.Id,
                        EntityType = "UserAccount",
                        EntityId = lockedUser.Id,
                        Action = "PasswordSetByOwner",
                        CreatedAtUtc = DateTime.UtcNow,
                    }, token);

                    await unitOfWork.SaveChangesAsync(token);
                }, cancellationToken);

                if (concurrentConflict)
                    return ApiResponse.Fail("Tài khoản đã thiết lập mật khẩu ở phiên khác.", System.Net.HttpStatusCode.Conflict);

                return ApiResponse.Success();
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return ApiResponse.Unauthorized("Không xác định được tài khoản hiện tại từ JWT.");
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Thiết lập mật khẩu thất bại.", System.Net.HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        // ---------- Private helpers ----------

        private ApiResponse? ValidateAccountOwnership(int targetUserId, bool allowAdminEmailOverride)
        {
            try
            {
                // userId trên route chỉ xác định tài khoản đích; quyền sở hữu phải được đối chiếu với JWT trước khi đọc hoặc cập nhật dữ liệu.
                var currentUser = claimService.GetUserClaim();

                var isOwner = currentUser.Id == targetUserId;
                var isAdminEmailOverride = allowAdminEmailOverride && currentUser.Role == Role.Admin;

                return isOwner || isAdminEmailOverride
                    ? null
                    : ApiResponse.Forbidden("Bạn không có quyền thay đổi tài khoản này.");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                return ApiResponse.Unauthorized("Không xác định được tài khoản hiện tại từ JWT.");
            }
        }


        private string CreateToken(UserAccount user)
        {
            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("tv", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.SecretToken.Value));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static LoginResponse BuildLoginResponse(UserAccount user, string accessToken) => new()
        {
            AccessToken = accessToken,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            PendingEmail = user.PendingEmail,
            FullName = UserNameHelper.FormatFullName(user.FirstName, user.LastName),
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            HasPassword = user.PasswordHash is { Length: 64 },
            GoogleLinked = !string.IsNullOrWhiteSpace(user.GoogleSubject),
        };

        private async Task DeactivateUserDeviceTokensAsync(int userId, CancellationToken cancellationToken)
        {
            var activeTokens = await unitOfWork.UserDeviceTokens.GetActiveTokensByUserIdAsync(userId, cancellationToken);
            if (activeTokens.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var deviceToken in activeTokens)
            {
                deviceToken.IsActive = false;
                deviceToken.ModifiedDate = now;
            }
            unitOfWork.UserDeviceTokens.UpdateRange(activeTokens);
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
            if (passwordHash is not { Length: 64 } || passwordSalt is not { Length: > 0 }) return false;
            using var hmac = new HMACSHA512(passwordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(computedHash, passwordHash);
        }

        private static string GenerateVerificationCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        private string ProtectVerificationCode(int userId, string code, string purpose)
        {
            var secret = appSettings.SecretToken?.Value;
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("OTP signing secret is not configured.");

            var key = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("rikipath-email-otp-storage-v1"));
            var digest = HMACSHA256.HashData(
                key, Encoding.UTF8.GetBytes($"{userId}:{purpose}:{code}"));
            return $"{purpose}:{Convert.ToHexString(digest)}";
        }

        private List<string>? BuildDebugErrors(Exception ex)
        {
            // Never serialize exception messages into public Auth responses, including
            // Development: provider errors may contain tokens or private addresses.
            logger?.LogError("Authentication operation failed with {FailureType}.", ex.GetType().Name);
            return null;
        }
    }
}
