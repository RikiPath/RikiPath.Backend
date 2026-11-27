using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using RikiPath.Application;
using RikiPath.Application.Exceptions;
using RikiPath.Application.IServices;
using RikiPath.Domain;

namespace RikiPath.Infrastructure.Services
{
    // Đếm số lượt gọi + token AI theo user/ngày bằng IMemoryCache (per-instance, in-memory).
    //
    // LƯU Ý KHI SCALE-OUT: nếu deploy nhiều instance API (nhiều pod/container chạy song song),
    // IMemoryCache KHÔNG đếm chính xác across-instance (mỗi instance đếm riêng, user có thể vượt
    // quota tổng vì mỗi instance cho phép hết hạn ngạch riêng nó). Nếu bạn scale-out, thay
    // IMemoryCache bằng IDistributedCache (Redis) hoặc một bảng DB (AiUsageDailyCounter) - interface
    // IAiUsageQuotaService giữ nguyên, chỉ đổi implementation này.
    public class AiUsageQuotaService(
        IMemoryCache cache,
        AppSettings appSettings,
        IUnitOfWork unitOfWork) : IAiUsageQuotaService
    {
        private readonly AiRateLimitSettings _limits = appSettings.Ai.RateLimit;
        private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
        private static readonly object SyncRoot = new();

        public async Task EnsureWithinQuotaAsync(int userId, AiFeature feature, CancellationToken cancellationToken)
        {
            if (await IsUnlimitedUserAsync(userId, cancellationToken))
                return;

            lock (SyncRoot)
            {
                var usage = GetOrCreateUsage(userId, feature);

                if (usage.RequestCount >= _limits.MaxRequestsPerUserPerDay)
                {
                    throw new AiQuotaExceededException(
                        $"Bạn đã dùng hết {_limits.MaxRequestsPerUserPerDay} lượt {feature} AI hôm nay. " +
                        "Vui lòng thử lại vào ngày mai.");
                }

                if (usage.TokensUsed >= _limits.MaxTokensPerUserPerDay)
                {
                    throw new AiQuotaExceededException(
                        $"Bạn đã dùng hết {_limits.MaxTokensPerUserPerDay} token AI ({feature}) hôm nay. " +
                        "Vui lòng thử lại vào ngày mai.");
                }
            }

        }

        public async Task RecordUsageAsync(int userId, AiFeature feature, int tokensUsed, CancellationToken cancellationToken)
        {
            if (await IsUnlimitedUserAsync(userId, cancellationToken))
                return;

            lock (SyncRoot)
            {
                var key = BuildKey(userId, feature);
                var usage = GetOrCreateUsage(userId, feature);
                usage.RequestCount += 1;
                usage.TokensUsed += tokensUsed;
                cache.Set(key, usage, NextMidnightVietnamTimeUtc());
            }

        }

        private async Task<bool> IsUnlimitedUserAsync(int userId, CancellationToken cancellationToken)
        {
            var configuredEmails = _limits.UnlimitedUserEmails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim().ToUpperInvariant())
                .ToHashSet(StringComparer.Ordinal);

            if (configuredEmails.Count == 0)
                return false;

            var cacheKey = $"ai-unlimited-user:{userId}";
            if (cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            var email = await unitOfWork.UserAccounts.Query()
                .AsNoTracking()
                .Where(user => user.Id == userId && !user.IsDeleted)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

            var unlimited = email is not null && configuredEmails.Contains(email.Trim().ToUpperInvariant());
            cache.Set(cacheKey, unlimited, TimeSpan.FromMinutes(5));
            return unlimited;
        }

        private DailyUsage GetOrCreateUsage(int userId, AiFeature feature)
        {
            var key = BuildKey(userId, feature);
            return cache.GetOrCreate(key, entry =>
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
