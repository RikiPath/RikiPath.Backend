using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RikiPath.Application;
using RikiPath.Application.IServices;

namespace RikiPath.Infrastructure.Services
{
    public class FcmOptions
    {
        public bool Enabled { get; set; } = false;
        public string ProjectId { get; set; } = "rikipath-9ee07";
        public string ServiceAccountPath { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
        public int InitialRetryDelayMs { get; set; } = 1000;
        public int QuotaExceededMinDelayMs { get; set; } = 1000;
        public int QuotaExceededDefaultDelaySeconds { get; set; } = 60;
        public int MaxImmediateRetryDelayMs { get; set; } = 5000;
    }

    /// <summary>
    /// Explicit classification of FCM HTTP v1 response errors.
    /// Distinguishes permanent invalid tokens from payload issues, project mismatches, and transient quota/server errors.
    /// Prevents catastrophic mass-deactivation of device tokens upon payload or credential errors.
    /// </summary>
    public enum FcmErrorClassification
    {
        None = 0,
        Success,
        UnregisteredToken,          // 404/410, UNREGISTERED -> Token revoked
        InvalidTokenArgument,       // 400 with fieldViolation on message.token -> Token revoked
        InvalidPayloadArgument,     // 400 with fieldViolation on non-token fields -> Tokens preserved!
        SenderIdMismatch,           // 403 SENDER_ID_MISMATCH -> Project config error -> Tokens preserved!
        AuthOrPermissionError,      // 401 UNAUTHENTICATED, 403 PERMISSION_DENIED -> Credential error -> Tokens preserved!
        QuotaExceeded,              // 429 RESOURCE_EXHAUSTED -> Transient, retry with backoff/jitter -> Tokens preserved!
        TransientServerUnavailable, // 500/503 INTERNAL, UNAVAILABLE -> Transient, retry with backoff/jitter -> Tokens preserved!
        ApnsConfigurationBlocker,   // APNS_AUTH_ERROR -> APNs certificate error on console -> Tokens preserved!
        Unknown
    }

    public class FcmTransportResponse
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int? RetryAfterSeconds { get; set; }
        public List<string> FieldViolations { get; set; } = new();
        public string? RawResponse { get; set; }

        public FcmErrorClassification Classify()
        {
            if (IsSuccess || (StatusCode >= 200 && StatusCode < 300))
                return FcmErrorClassification.Success;

            var normalizedCode = (ErrorCode ?? string.Empty).Trim().ToUpperInvariant();

            // 1. Unregistered token (404/410 or UNREGISTERED errorCode)
            if (StatusCode == 404 || StatusCode == 410 || normalizedCode == "UNREGISTERED" ||
                normalizedCode == "REGISTRATION_TOKEN_NOT_REGISTERED")
            {
                return FcmErrorClassification.UnregisteredToken;
            }

            // 2. Sender ID / Project mismatch (403 SENDER_ID_MISMATCH)
            if (normalizedCode == "SENDER_ID_MISMATCH")
            {
                return FcmErrorClassification.SenderIdMismatch;
            }

            // 3. APNs authorization / certificate errors
            if (normalizedCode.Contains("APNS_AUTH") || normalizedCode.Contains("APNS_CREDENTIAL") ||
                (ErrorMessage != null && ErrorMessage.Contains("apns", StringComparison.OrdinalIgnoreCase) && ErrorMessage.Contains("auth", StringComparison.OrdinalIgnoreCase)))
            {
                return FcmErrorClassification.ApnsConfigurationBlocker;
            }

            // 4. Authentication / Permission errors (401 or 403 PERMISSION_DENIED)
            if (StatusCode == 401 || normalizedCode == "UNAUTHENTICATED")
            {
                return FcmErrorClassification.AuthOrPermissionError;
            }

            if (StatusCode == 403 || normalizedCode == "PERMISSION_DENIED")
            {
                return FcmErrorClassification.AuthOrPermissionError;
            }

            // 5. Quota exceeded (429 or RESOURCE_EXHAUSTED)
            if (StatusCode == 429 || normalizedCode == "RESOURCE_EXHAUSTED" || normalizedCode == "QUOTA_EXCEEDED")
            {
                return FcmErrorClassification.QuotaExceeded;
            }

            // 6. Transient server unavailable (500, 503, INTERNAL, UNAVAILABLE)
            if (StatusCode == 500 || StatusCode == 503 || normalizedCode == "INTERNAL" || normalizedCode == "UNAVAILABLE")
            {
                return FcmErrorClassification.TransientServerUnavailable;
            }

            // 7. Invalid argument (400 or INVALID_ARGUMENT)
            if (StatusCode == 400 || normalizedCode == "INVALID_ARGUMENT")
            {
                var isTokenField = FieldViolations.Any(f => f.Contains("token", StringComparison.OrdinalIgnoreCase));
                var isTokenMessage = ErrorMessage != null &&
                    (ErrorMessage.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                     ErrorMessage.Contains("registration", StringComparison.OrdinalIgnoreCase));

                if (isTokenField || isTokenMessage)
                {
                    return FcmErrorClassification.InvalidTokenArgument;
                }

                // If not specifically a token field violation, it is a payload/formatting error
                return FcmErrorClassification.InvalidPayloadArgument;
            }

            return FcmErrorClassification.Unknown;
        }

        public bool IsTransient =>
            Classify() == FcmErrorClassification.QuotaExceeded ||
            Classify() == FcmErrorClassification.TransientServerUnavailable;

        public bool IsRevokableToken =>
            Classify() == FcmErrorClassification.UnregisteredToken ||
            Classify() == FcmErrorClassification.InvalidTokenArgument;
    }

    public interface IFcmTransport
    {
        Task<FcmTransportResponse> SendAsync(string token, FcmPushPayload payload, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Default FCM HTTP v1 transport.
    /// In production without credentials, safely reports unconfigured status.
    /// When credentials or token are provided, dispatches HTTP v1 request.
    /// </summary>
    public class DefaultFcmTransport : IFcmTransport
    {
        private readonly HttpClient _httpClient;
        private readonly FcmOptions _options;
        private readonly ILogger<DefaultFcmTransport> _logger;
        private string? _cachedAccessToken;
        private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public DefaultFcmTransport(
            HttpClient httpClient,
            IOptions<FcmOptions> options,
            ILogger<DefaultFcmTransport> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                return _cachedAccessToken;
            }

            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTimeOffset.UtcNow < _tokenExpiry)
                {
                    return _cachedAccessToken;
                }

                var saPath = _options.ServiceAccountPath;
                if (string.IsNullOrWhiteSpace(saPath))
                {
                    saPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                }

                if (string.IsNullOrWhiteSpace(saPath) || !System.IO.File.Exists(saPath))
                {
                    return null;
                }

                var json = await System.IO.File.ReadAllTextAsync(saPath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var clientEmail = doc.RootElement.GetProperty("client_email").GetString();
                var privateKeyPem = doc.RootElement.GetProperty("private_key").GetString();
                if (string.IsNullOrEmpty(clientEmail) || string.IsNullOrEmpty(privateKeyPem))
                {
                    return null;
                }

                var tokenUri = "https://oauth2.googleapis.com/token";
                using var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportFromPem(privateKeyPem);

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var header = new { alg = "RS256", typ = "JWT" };
                var payload = new
                {
                    iss = clientEmail,
                    scope = "https://www.googleapis.com/auth/firebase.messaging",
                    aud = tokenUri,
                    exp = now + 3600,
                    iat = now
                };

                var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
                var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
                var rawSignatureInput = $"{headerB64}.{payloadB64}";

                var signature = rsa.SignData(Encoding.UTF8.GetBytes(rawSignatureInput), System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                var signatureB64 = Base64UrlEncode(signature);
                var assertion = $"{rawSignatureInput}.{signatureB64}";

                using var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                    new KeyValuePair<string, string>("assertion", assertion)
                });

                var resp = await _httpClient.PostAsync(tokenUri, content, cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    var respBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                    using var respDoc = JsonDocument.Parse(respBody);
                    _cachedAccessToken = respDoc.RootElement.GetProperty("access_token").GetString();
                    _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(50);
                    return _cachedAccessToken;
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Failed to obtain Google OAuth token for FCM: {Error}", err);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while obtaining Google OAuth token for FCM");
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public async Task<FcmTransportResponse> SendAsync(string token, FcmPushPayload payload, CancellationToken cancellationToken)
        {
            if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ProjectId))
            {
                return new FcmTransportResponse
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorCode = "FCM_DISABLED_OR_UNCONFIGURED",
                    ErrorMessage = "FCM HTTP v1 transport is not enabled or project ID is missing.",
                    RawResponse = "FCM HTTP v1 transport is not enabled or project ID is missing."
                };
            }

            try
            {
                // Format FCM HTTP v1 JSON message structure
                var messageBody = new
                {
                    message = new
                    {
                        token = token,
                        notification = new
                        {
                            title = payload.Title,
                            body = payload.Body
                        },
                        data = payload.Data,
                        android = new
                        {
                            priority = "high",
                            notification = new
                            {
                                channel_id = "rikipath_mentor_notifications",
                                sound = "default",
                                click_action = "FLUTTER_NOTIFICATION_CLICK"
                            }
                        },
                        apns = new
                        {
                            payload = new
                            {
                                aps = new
                                {
                                    sound = "default",
                                    badge = 1,
                                    content_available = 1
                                }
                            }
                        }
                    }
                };

                var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";
                var json = JsonSerializer.Serialize(messageBody);
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var bearerToken = await GetAccessTokenAsync(cancellationToken);
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);

                int? retryAfterSeconds = null;
                if (response.Headers.TryGetValues("Retry-After", out var retryValues))
                {
                    if (int.TryParse(retryValues.FirstOrDefault(), out var sec))
                    {
                        retryAfterSeconds = sec;
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    return new FcmTransportResponse
                    {
                        IsSuccess = true,
                        StatusCode = (int)response.StatusCode,
                        RawResponse = raw
                    };
                }

                string? errorCode = null;
                string? errorMessage = null;
                var fieldViolations = new List<string>();

                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        if (errEl.TryGetProperty("status", out var stEl))
                        {
                            errorCode = stEl.GetString();
                        }
                        if (errEl.TryGetProperty("message", out var msgEl))
                        {
                            errorMessage = msgEl.GetString();
                        }
                        if (errEl.TryGetProperty("details", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var d in detailsEl.EnumerateArray())
                            {
                                if (d.TryGetProperty("errorCode", out var subCode))
                                {
                                    errorCode = subCode.GetString();
                                }
                                if (d.TryGetProperty("fieldViolations", out var fvEl) && fvEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var fv in fvEl.EnumerateArray())
                                    {
                                        if (fv.TryGetProperty("field", out var fieldEl))
                                        {
                                            var fStr = fieldEl.GetString();
                                            if (!string.IsNullOrEmpty(fStr))
                                            {
                                                fieldViolations.Add(fStr);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback if raw response is non-JSON
                }

                return new FcmTransportResponse
                {
                    IsSuccess = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorCode = errorCode ?? response.StatusCode.ToString(),
                    ErrorMessage = errorMessage,
                    RetryAfterSeconds = retryAfterSeconds,
                    FieldViolations = fieldViolations,
                    RawResponse = raw
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM transport network exception for token {Token}", token);
                return new FcmTransportResponse
                {
                    IsSuccess = false,
                    StatusCode = 503,
                    ErrorCode = "TRANSPORT_EXCEPTION",
                    ErrorMessage = ex.Message,
                    RawResponse = ex.Message
                };
            }
        }
    }

    public class FcmPushService : IFcmPushService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFcmTransport _transport;
        private readonly FcmOptions _options;
        private readonly ILogger<FcmPushService> _logger;

        public FcmPushService(
            IUnitOfWork unitOfWork,
            IFcmTransport transport,
            IOptions<FcmOptions> options,
            ILogger<FcmPushService> logger)
        {
            _unitOfWork = unitOfWork;
            _transport = transport;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<FcmSendResult> SendPushToUserAsync(int userId, FcmPushPayload payload, CancellationToken cancellationToken = default)
        {
            var activeTokens = await _unitOfWork.UserDeviceTokens.GetActiveTokensByUserIdAsync(userId, cancellationToken);
            if (activeTokens.Count == 0)
            {
                return new FcmSendResult
                {
                    Success = true,
                    TotalTokens = 0,
                    SuccessCount = 0,
                    FailureCount = 0,
                    Note = $"No active device tokens found for user {userId}."
                };
            }

            var tokenStrings = activeTokens.Select(t => t.FcmToken).ToList();
            return await SendPushToTokensAsync(tokenStrings, payload, cancellationToken);
        }

        public async Task<FcmSendResult> SendPushToTokenAsync(string token, FcmPushPayload payload, CancellationToken cancellationToken = default)
        {
            return await SendPushToTokensAsync(new[] { token }, payload, cancellationToken);
        }

        public async Task<FcmSendResult> SendPushToTokensAsync(IEnumerable<string> tokens, FcmPushPayload payload, CancellationToken cancellationToken = default)
        {
            var tokenList = tokens.Distinct().ToList();
            var result = new FcmSendResult
            {
                TotalTokens = tokenList.Count
            };

            if (tokenList.Count == 0)
            {
                result.Success = true;
                return result;
            }

            if (!_options.Enabled)
            {
                _logger.LogInformation("FCM Push is disabled or credentials not provided. Skipped push dispatch for {Count} tokens.", tokenList.Count);
                result.Success = true;
                result.Note = "FCM push disabled via configuration.";
                return result;
            }

            foreach (var token in tokenList)
            {
                var delivered = false;
                var baseDelayMs = Math.Max(10, _options.InitialRetryDelayMs);

                for (var attempt = 1; attempt <= Math.Max(1, _options.MaxRetries); attempt++)
                {
                    var response = await _transport.SendAsync(token, payload, cancellationToken);
                    var classification = response.Classify();

                    if (classification == FcmErrorClassification.Success)
                    {
                        delivered = true;
                        result.SuccessCount++;
                        result.SuccessfulTokens.Add(token);
                        break;
                    }

                    // 1. Only revoke token if error actually indicates an invalid/expired token!
                    if (response.IsRevokableToken)
                    {
                        _logger.LogWarning("Deactivating invalid FCM token {Token} (Classification: {Classification}, Status: {Status}, Code: {Code}, Message: {Message})",
                            token, classification, response.StatusCode, response.ErrorCode, response.ErrorMessage);
                        await _unitOfWork.UserDeviceTokens.DeactivateTokenAsync(token, cancellationToken);
                        result.DeactivatedTokens.Add(token);
                        result.FailureCount++;
                        break; // Permanent token failure - do not retry
                    }

                    // 2. Payload format errors: DO NOT revoke tokens!
                    if (classification == FcmErrorClassification.InvalidPayloadArgument)
                    {
                        _logger.LogError("FCM push failed due to INVALID_ARGUMENT on payload (NOT token): {Message}. Tokens preserved.",
                            response.ErrorMessage);
                        result.FailureCount++;
                        break; // Permanent payload error - retrying won't help
                    }

                    // 3. Sender ID mismatch: configuration issue, DO NOT revoke tokens!
                    if (classification == FcmErrorClassification.SenderIdMismatch)
                    {
                        _logger.LogError("FCM push failed due to SENDER_ID_MISMATCH for token {Token}. Verify Firebase Project ID and credentials. Tokens preserved.",
                            token);
                        result.FailureCount++;
                        break;
                    }

                    // 4. Authentication / Permission errors: credentials issue, DO NOT revoke tokens!
                    if (classification == FcmErrorClassification.AuthOrPermissionError)
                    {
                        _logger.LogError("FCM push failed due to authentication/permission error ({StatusCode}). Check service account configuration. Tokens preserved.",
                            response.StatusCode);
                        result.FailureCount++;
                        break;
                    }

                    // 5. APNs configuration blocker: iOS credentials issue, DO NOT revoke tokens!
                    if (classification == FcmErrorClassification.ApnsConfigurationBlocker)
                    {
                        _logger.LogError("FCM push failed due to APNs configuration blocker: {Message}. Check APNs Authentication Key in Firebase Console. Tokens preserved.",
                            response.ErrorMessage);
                        result.FailureCount++;
                        break;
                    }

                    // 6. Quota or Transient Server Errors: Retry with exponential backoff, proportional jitter, and Retry-After
                    if (response.IsTransient && attempt < _options.MaxRetries)
                    {
                        var isQuota = classification == FcmErrorClassification.QuotaExceeded;
                        int calculatedDelay;

                        if (isQuota)
                        {
                            // Official Google Firebase Scale guidance (https://firebase.google.com/docs/cloud-messaging/scale-fcm):
                            // If Retry-After header present, respect it; otherwise default to waiting 60 seconds
                            var baseSeconds = response.RetryAfterSeconds.HasValue && response.RetryAfterSeconds.Value >= 0
                                ? response.RetryAfterSeconds.Value
                                : _options.QuotaExceededDefaultDelaySeconds;

                            var expSeconds = (int)(baseSeconds * Math.Pow(2, attempt - 1));
                            var jitterSeconds = baseSeconds <= 1 ? 0 : Random.Shared.Next(1, Math.Min(15, Math.Max(2, expSeconds / 4)));
                            var totalSeconds = expSeconds + jitterSeconds;
                            calculatedDelay = totalSeconds * 1000;
                        }
                        else
                        {
                            var expDelayMs = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                            var maxJitter = baseDelayMs <= 50 ? 5 : Math.Min(500, Math.Max(100, expDelayMs / 4));
                            var minJitter = baseDelayMs <= 50 ? 1 : Math.Min(100, maxJitter / 2);
                            var jitterMs = Random.Shared.Next(minJitter, maxJitter + 1);
                            calculatedDelay = expDelayMs + jitterMs;

                            if (response.RetryAfterSeconds.HasValue && response.RetryAfterSeconds.Value > 0)
                            {
                                var retryAfterMs = response.RetryAfterSeconds.Value * 1000;
                                calculatedDelay = Math.Max(calculatedDelay, retryAfterMs);
                            }
                        }

                        // If required backoff exceeds immediate retry budget, defer to durable background worker
                        if (calculatedDelay > _options.MaxImmediateRetryDelayMs)
                        {
                            _logger.LogWarning(
                                "FCM {Classification} for token {Token} requires delay of {Delay}ms, exceeding immediate budget of {Budget}ms. Deferring to durable background worker retry.",
                                classification, token, calculatedDelay, _options.MaxImmediateRetryDelayMs);
                            result.HasDeferredRetry = true;
                            result.RecommendedBackoffSeconds = (int)Math.Ceiling(calculatedDelay / 1000.0);
                            result.FailureCount++;
                            break;
                        }

                        _logger.LogInformation("Transient FCM error ({Classification}) on token {Token} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                            classification, token, attempt, _options.MaxRetries, calculatedDelay);

                        try
                        {
                            await Task.Delay(calculatedDelay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        continue;
                    }

                    // Non-transient or retries exhausted
                    result.FailureCount++;
                    break;
                }

                if (!delivered)
                {
                    result.FailedTokens.Add(token);
                    if (!result.DeactivatedTokens.Contains(token))
                    {
                        _logger.LogWarning("Failed to deliver FCM push to token {Token} after retries.", token);
                    }
                }
            }

            result.Success = result.FailureCount == 0;
            return result;
        }
    }
}
