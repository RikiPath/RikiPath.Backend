using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.Application.Services
{
    // Đếm số lượt gọi + token AI theo user/ngày bằng IMemoryCache (per-instance, in-memory).
    //
    // LƯU Ý KHI SCALE-OUT: nếu deploy nhiều instance API (nhiều pod/container chạy song song),
    // IMemoryCache KHÔNG đếm chính xác across-instance (mỗi instance đếm riêng, user có thể vượt
    // quota tổng vì mỗi instance cho phép hết hạn ngạch riêng nó). Nếu bạn scale-out, thay
    // IMemoryCache bằng IDistributedCache (Redis) hoặc một bảng DB (AiUsageDailyCounter) - interface
    // IAiUsageQuotaService giữ nguyên, chỉ đổi implementation này.
    public class AiUsageQuotaService : IAiUsageQuotaService
    {
        private readonly IMemoryCache _cache;
        private readonly AiRateLimitSettings _limits;
        private readonly TranslationSettings _translationLimits;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
        private static readonly object SyncRoot = new();

        public AiUsageQuotaService(
            IMemoryCache cache,
            IOptions<AiSettings> aiSettings,
            IOptions<AppSettings>? appSettings = null,
            IOptions<TranslationSettings>? translationSettings = null,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            var primary = aiSettings?.Value?.RateLimit;
            var fallback = appSettings?.Value?.Ai?.RateLimit;
            // Program.cs binds section "Ai" to IOptions<AiSettings>, whereas legacy deployments may nest under "AppSettings:Ai".
            _limits = (primary is not null && (primary.MaxRequestsPerUserPerDay != 20 || primary.MaxTokensPerUserPerDay != 50000 || fallback is null))
                ? primary
                : (fallback ?? new AiRateLimitSettings());
            _translationLimits = translationSettings?.Value ?? new TranslationSettings();
        }

        public Task EnsureWithinQuotaAsync(int userId, AiFeature feature, CancellationToken cancellationToken)
        {
            // Demo account allowlist is intentionally evaluated from the signed request
            // identity, not from the numeric user id. The Huy account is registered
            // outside HasData, so its database id can differ between environments.
            if (IsUnlimitedUser())
                return Task.CompletedTask;

            var maxRequests = feature == AiFeature.Translation
                ? Math.Max(20, _translationLimits.DailyRequestsPerUser)
                : _limits.MaxRequestsPerUserPerDay;
            var maxTokens = feature == AiFeature.Translation
                ? Math.Max(50000, _translationLimits.DailyTokensPerUser)
                : _limits.MaxTokensPerUserPerDay;

            lock (SyncRoot)
            {
                var usage = GetOrCreateUsage(userId, feature);

                if (usage.RequestCount >= maxRequests)
                {
                    throw new AiQuotaExceededException(
                        feature == AiFeature.Translation
                            ? $"Bạn đã đạt giới hạn {maxRequests} lượt dịch hôm nay. Vui lòng thử lại vào ngày mai."
                            : $"Bạn đã dùng hết {maxRequests} lượt {feature} AI hôm nay. Vui lòng thử lại vào ngày mai.");
                }

                if (usage.TokensUsed >= maxTokens)
                {
                    throw new AiQuotaExceededException(
                        feature == AiFeature.Translation
                            ? "Bạn đã dùng hết hạn mức dịch hôm nay. Vui lòng thử lại vào ngày mai."
                            : $"Bạn đã dùng hết {maxTokens} token AI ({feature}) hôm nay. Vui lòng thử lại vào ngày mai.");
                }
            }

            return Task.CompletedTask;
        }

        public Task RecordUsageAsync(int userId, AiFeature feature, int tokensUsed, CancellationToken cancellationToken)
        {
            if (IsUnlimitedUser())
                return Task.CompletedTask;

            lock (SyncRoot)
            {
                var key = BuildKey(userId, feature);
                var usage = GetOrCreateUsage(userId, feature);
                usage.RequestCount += 1;
                usage.TokensUsed += tokensUsed;
                _cache.Set(key, usage, NextMidnightVietnamTimeUtc());
            }

            return Task.CompletedTask;
        }

        private bool IsUnlimitedUser()
        {
            var principal = _httpContextAccessor?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return false;

            var email = principal.FindFirst(ClaimTypes.Email)?.Value
                        ?? principal.FindFirst("email")?.Value;
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return (_limits.UnlimitedUserEmails ?? []).Any(configured =>
                string.Equals(configured?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private DailyUsage GetOrCreateUsage(int userId, AiFeature feature)
        {
            var key = BuildKey(userId, feature);
            return _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpiration = NextMidnightVietnamTimeUtc();
                return new DailyUsage();
            })!;
        }

        private static string BuildKey(int userId, AiFeature feature)
        {
            var todayVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone).Date;
            return $"ai-quota:{feature}:{userId}:{todayVn:yyyyMMdd}";
        }

        private static DateTimeOffset NextMidnightVietnamTimeUtc()
        {
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var nextMidnightVnUnspecified = DateTime.SpecifyKind(nowVn.Date.AddDays(1), DateTimeKind.Unspecified);
            var nextMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightVnUnspecified, VietnamTimeZone);
            return new DateTimeOffset(nextMidnightUtc, TimeSpan.Zero);
        }

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback: UTC+7 cố định, không có DST nên đủ chính xác cho Việt Nam.
                return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
            }
        }

        private class DailyUsage
        {
            public int RequestCount { get; set; }
            public int TokensUsed { get; set; }
        }
    }
}
